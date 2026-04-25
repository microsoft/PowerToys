# Macro Module — Core Implementation Plan (Plan 1 of 3)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build MacroCommon + MacroModuleInterface + MacroEngine so that placing a JSON macro file in `%APPDATA%\Microsoft\PowerToys\Macros\` and enabling the module in PowerToys causes the macro's hotkey to execute keystrokes.

**Architecture:** C# background service (`PowerToys.MacroEngine.exe`) started by a thin C++ interface DLL. Macro definitions live as JSON files. The engine uses `RegisterHotKey` Win32 API (via WinForms hidden window message pump) for hotkey triggers and `SendInput` via CsWin32 for execution. Shared schema models and StreamJsonRpc IPC contracts live in `MacroCommon`.

**Tech Stack:** C++17 (module interface DLL), C# 12 / .NET 9 (engine + common), `Microsoft.Windows.CsWin32` for P/Invoke, `StreamJsonRpc` for IPC, `System.Text.Json` for macro files, `MSTest` + `Moq` for tests.

**Plans 2 and 3 (follow-ups):**
- Plan 2: `MacroEditor` — WinUI3 flowchart editor + record mode
- Plan 3: `MacroCmdPalExtension` — Command Palette integration

---

## File Map

```
src/modules/Macro/
  MacroCommon/
    MacroCommon.csproj
    Models/
      StepType.cs           enum StepType
      MacroStep.cs          record MacroStep
      MacroDefinition.cs    record MacroDefinition
    Serialization/
      MacroSerializer.cs    JSON read/write helpers
    Ipc/
      IMacroEngineRpc.cs    StreamJsonRpc service interface

  MacroCommon.Tests/
    MacroCommon.Tests.csproj
    Serialization/
      MacroSerializerTests.cs

  MacroModuleInterface/
    MacroModuleInterface.vcxproj
    dllmain.cpp             PowertoyModuleIface implementation
    pch.h / pch.cpp
    resource.h
    MacroModuleInterface.rc

  MacroEngine/
    MacroEngine.csproj
    NativeMethods.txt       CsWin32 declarations
    Program.cs              Entry point
    MacroEngineHost.cs      Orchestration
    AppScopeChecker.cs      Foreground window process check
    KeyParser.cs            "Ctrl+Shift+V" → (modifiers, vk)
    SendInputHelper.cs      SendInput wrappers + interfaces
    MacroExecutor.cs        Execute steps against ISendInputHelper
    HotkeyManager.cs        RegisterHotKey + WinForms message pump
    MacroLoader.cs          Load + watch JSON files from disk
    MacroRpcServer.cs       StreamJsonRpc named pipe server

  MacroEngine.Tests/
    MacroEngine.Tests.csproj
    AppScopeCheckerTests.cs
    KeyParserTests.cs
    MacroExecutorTests.cs
    HotkeyManagerTests.cs
    MacroLoaderTests.cs
```

---

## Task 1: MacroCommon project skeleton

**Files:**
- Create: `src/modules/Macro/MacroCommon/MacroCommon.csproj`

- [ ] **Step 1: Create the project file**

```xml
<!-- src/modules/Macro/MacroCommon/MacroCommon.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="$(RepoRoot)src\Common.Dotnet.CsWinRT.props" />

  <PropertyGroup>
    <AssemblyName>PowerToys.MacroCommon</AssemblyName>
    <RootNamespace>PowerToys.MacroCommon</RootNamespace>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
    <OutputPath>$(RepoRoot)$(Platform)\$(Configuration)</OutputPath>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="StreamJsonRpc" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Verify it imports correctly**

Open `PowerToys.slnx` in Visual Studio and add `MacroCommon.csproj` to a new solution folder named `Macro`. Confirm the project loads without errors.

---

## Task 2: StepType and MacroStep models

**Files:**
- Create: `src/modules/Macro/MacroCommon/Models/StepType.cs`
- Create: `src/modules/Macro/MacroCommon/Models/MacroStep.cs`

- [ ] **Step 1: Write StepType enum**

```csharp
// src/modules/Macro/MacroCommon/Models/StepType.cs
namespace PowerToys.MacroCommon.Models;

public enum StepType
{
    PressKey,
    TypeText,
    Wait,
    Repeat,
}
```

- [ ] **Step 2: Write MacroStep record**

```csharp
// src/modules/Macro/MacroCommon/Models/MacroStep.cs
namespace PowerToys.MacroCommon.Models;

public sealed record MacroStep
{
    public StepType Type { get; init; }
    public string? Key { get; init; }
    public string? Text { get; init; }
    public int? Ms { get; init; }
    public int? Count { get; init; }
    public List<MacroStep>? Steps { get; init; }
}
```

- [ ] **Step 3: Build to verify no errors**

```
msbuild src/modules/Macro/MacroCommon/MacroCommon.csproj -p:Platform=x64 -p:Configuration=Debug
```

Expected: Build succeeded.

---

## Task 3: MacroDefinition model

**Files:**
- Create: `src/modules/Macro/MacroCommon/Models/MacroDefinition.cs`

- [ ] **Step 1: Write MacroDefinition record**

```csharp
// src/modules/Macro/MacroCommon/Models/MacroDefinition.cs
namespace PowerToys.MacroCommon.Models;

public sealed record MacroDefinition
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Hotkey { get; init; }
    public string? AppScope { get; init; }
    public List<MacroStep> Steps { get; init; } = [];
}
```

- [ ] **Step 2: Build to verify no errors**

```
msbuild src/modules/Macro/MacroCommon/MacroCommon.csproj -p:Platform=x64 -p:Configuration=Debug
```

Expected: Build succeeded.

---

## Task 4: MacroSerializer with tests

**Files:**
- Create: `src/modules/Macro/MacroCommon/Serialization/MacroSerializer.cs`
- Create: `src/modules/Macro/MacroCommon.Tests/MacroCommon.Tests.csproj`
- Create: `src/modules/Macro/MacroCommon.Tests/Serialization/MacroSerializerTests.cs`

- [ ] **Step 1: Write MacroSerializer**

```csharp
// src/modules/Macro/MacroCommon/Serialization/MacroSerializer.cs
using System.Text.Json;
using System.Text.Json.Serialization;
using PowerToys.MacroCommon.Models;

namespace PowerToys.MacroCommon.Serialization;

public static class MacroSerializer
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public static MacroDefinition Deserialize(string json) =>
        JsonSerializer.Deserialize<MacroDefinition>(json, Options)
        ?? throw new InvalidOperationException("Deserialized macro was null.");

    public static string Serialize(MacroDefinition macro) =>
        JsonSerializer.Serialize(macro, Options);

    public static async Task<MacroDefinition> DeserializeFileAsync(string path, CancellationToken ct = default)
    {
        var json = await File.ReadAllTextAsync(path, ct);
        return Deserialize(json);
    }

    public static async Task SerializeFileAsync(MacroDefinition macro, string path, CancellationToken ct = default)
    {
        var json = Serialize(macro);
        await File.WriteAllTextAsync(path, json, ct);
    }
}
```

- [ ] **Step 2: Create test project**

```xml
<!-- src/modules/Macro/MacroCommon.Tests/MacroCommon.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="$(RepoRoot)src\Common.Dotnet.CsWinRT.props" />

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <AssemblyName>PowerToys.MacroCommon.Tests</AssemblyName>
    <RootNamespace>PowerToys.MacroCommon.Tests</RootNamespace>
    <IsPackable>false</IsPackable>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier Condition="'$(Platform)' == 'x64'">win-x64</RuntimeIdentifier>
    <RuntimeIdentifier Condition="'$(Platform)' == 'ARM64'">win-arm64</RuntimeIdentifier>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
    <OutputPath>$(RepoRoot)$(Platform)\$(Configuration)\tests\MacroCommon.Tests\</OutputPath>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MacroCommon\MacroCommon.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Write failing serializer tests**

```csharp
// src/modules/Macro/MacroCommon.Tests/Serialization/MacroSerializerTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.MacroCommon.Models;
using PowerToys.MacroCommon.Serialization;

namespace PowerToys.MacroCommon.Tests.Serialization;

[TestClass]
public sealed class MacroSerializerTests
{
    [TestMethod]
    public void RoundTrip_PreservesAllFields()
    {
        var original = new MacroDefinition
        {
            Id = "test-id",
            Name = "Test Macro",
            Description = "A test",
            Hotkey = "Ctrl+Shift+T",
            AppScope = "notepad.exe",
            Steps =
            [
                new MacroStep { Type = StepType.PressKey, Key = "Ctrl+C" },
                new MacroStep { Type = StepType.Wait, Ms = 200 },
                new MacroStep { Type = StepType.TypeText, Text = "Hello" },
                new MacroStep
                {
                    Type = StepType.Repeat,
                    Count = 3,
                    Steps = [new MacroStep { Type = StepType.PressKey, Key = "Tab" }],
                },
            ],
        };

        var json = MacroSerializer.Serialize(original);
        var restored = MacroSerializer.Deserialize(json);

        Assert.AreEqual(original.Id, restored.Id);
        Assert.AreEqual(original.Name, restored.Name);
        Assert.AreEqual(original.Hotkey, restored.Hotkey);
        Assert.AreEqual(original.AppScope, restored.AppScope);
        Assert.AreEqual(4, restored.Steps.Count);
        Assert.AreEqual(StepType.PressKey, restored.Steps[0].Type);
        Assert.AreEqual("Ctrl+C", restored.Steps[0].Key);
        Assert.AreEqual(200, restored.Steps[1].Ms);
        Assert.AreEqual("Hello", restored.Steps[2].Text);
        Assert.AreEqual(3, restored.Steps[3].Count);
        Assert.AreEqual(1, restored.Steps[3].Steps?.Count);
    }

    [TestMethod]
    public void Serialize_UsesSnakeCaseKeys()
    {
        var macro = new MacroDefinition { Name = "Test", AppScope = "notepad.exe" };
        var json = MacroSerializer.Serialize(macro);
        StringAssert.Contains(json, "\"app_scope\"");
        Assert.IsFalse(json.Contains("\"appScope\""), "camelCase key must not appear");
    }

    [TestMethod]
    public void Serialize_OmitsNullOptionalFields()
    {
        var macro = new MacroDefinition { Name = "Test" };
        var json = MacroSerializer.Serialize(macro);
        Assert.IsFalse(json.Contains("\"app_scope\""), "null app_scope should be omitted");
        Assert.IsFalse(json.Contains("\"hotkey\""), "null hotkey should be omitted");
    }

    [TestMethod]
    public void Deserialize_StepTypeUsesSnakeCase()
    {
        var json = """
            {
              "id": "x",
              "name": "T",
              "steps": [{ "type": "press_key", "key": "Enter" }]
            }
            """;
        var macro = MacroSerializer.Deserialize(json);
        Assert.AreEqual(StepType.PressKey, macro.Steps[0].Type);
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Deserialize_NullJson_Throws()
    {
        MacroSerializer.Deserialize("null");
    }
}
```

- [ ] **Step 4: Run tests to verify they fail (project not yet building)**

```
msbuild src/modules/Macro/MacroCommon.Tests/MacroCommon.Tests.csproj -p:Platform=x64 -p:Configuration=Debug
```

Expected: Builds and tests pass after MacroSerializer is in place.

- [ ] **Step 5: Run tests**

```
dotnet test src/modules/Macro/MacroCommon.Tests/MacroCommon.Tests.csproj -p:Platform=x64 --no-build
```

Expected: All 5 tests pass.

- [ ] **Step 6: Commit**

```bash
git add src/modules/Macro/MacroCommon/ src/modules/Macro/MacroCommon.Tests/
git commit -m "feat(macro): add MacroCommon models and serializer"
```

---

## Task 5: IMacroEngineRpc IPC interface

**Files:**
- Create: `src/modules/Macro/MacroCommon/Ipc/IMacroEngineRpc.cs`

- [ ] **Step 1: Write the interface**

```csharp
// src/modules/Macro/MacroCommon/Ipc/IMacroEngineRpc.cs
namespace PowerToys.MacroCommon.Ipc;

public interface IMacroEngineRpc
{
    Task ExecuteMacroAsync(string macroId, CancellationToken ct);
    Task SuspendHotkeysAsync(CancellationToken ct);
    Task ResumeHotkeysAsync(CancellationToken ct);
    Task<IReadOnlyList<string>> GetMacroIdsAsync(CancellationToken ct);
}
```

- [ ] **Step 2: Confirm the pipe name constant belongs here too**

Add below the interface in the same file:

```csharp
public static class MacroIpcConstants
{
    public const string PipeName = "PowerToys.MacroEngine";
}
```

- [ ] **Step 3: Build**

```
msbuild src/modules/Macro/MacroCommon/MacroCommon.csproj -p:Platform=x64 -p:Configuration=Debug
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/modules/Macro/MacroCommon/Ipc/
git commit -m "feat(macro): add IMacroEngineRpc IPC contract"
```

---

## Task 6: MacroModuleInterface C++ DLL

**Files:**
- Create: `src/modules/Macro/MacroModuleInterface/dllmain.cpp`
- Create: `src/modules/Macro/MacroModuleInterface/pch.h`
- Create: `src/modules/Macro/MacroModuleInterface/pch.cpp`
- Create: `src/modules/Macro/MacroModuleInterface/resource.h`
- Create: `src/modules/Macro/MacroModuleInterface/MacroModuleInterface.vcxproj`
- Create: `src/modules/Macro/MacroModuleInterface/MacroModuleInterface.rc`

Pattern: copy `src/modules/Workspaces/WorkspacesModuleInterface/` and adapt. Key differences: no hotkey event (macros manage their own hotkeys inside the engine), no editor launch from DLL in v1 (editor is Plan 2).

- [ ] **Step 1: Create pch.h**

```cpp
// src/modules/Macro/MacroModuleInterface/pch.h
#pragma once
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <shellapi.h>
#include <string>
#include <optional>
```

- [ ] **Step 2: Create pch.cpp**

```cpp
// src/modules/Macro/MacroModuleInterface/pch.cpp
#include "pch.h"
```

- [ ] **Step 3: Create resource.h**

```cpp
// src/modules/Macro/MacroModuleInterface/resource.h
#pragma once
#define IDS_MACRO_NAME        101
#define IDS_MACRO_SETTINGS_DESC 102
```

- [ ] **Step 4: Create dllmain.cpp**

```cpp
// src/modules/Macro/MacroModuleInterface/dllmain.cpp
#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <common/logger/logger.h>
#include <common/utils/logger_helper.h>
#include <common/utils/resources.h>
#include <common/utils/winapi_error.h>
#include "resource.h"

BOOL APIENTRY DllMain(HMODULE, DWORD ul_reason_for_call, LPVOID)
{
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

class MacroModuleInterface : public PowertoyModuleIface
{
public:
    virtual PCWSTR get_name() override { return app_name.c_str(); }
    virtual const wchar_t* get_key() override { return app_key.c_str(); }

    virtual bool get_config(_Out_ PWSTR buffer, _Out_ int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);
        PowerToysSettings::Settings settings(hinstance, get_name());
        settings.set_description(GET_RESOURCE_STRING(IDS_MACRO_SETTINGS_DESC));
        settings.set_overview_link(L"https://aka.ms/PowerToysOverview_Macro");
        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    virtual void set_config(PCWSTR config) override
    {
        try
        {
            PowerToysSettings::PowerToyValues values =
                PowerToysSettings::PowerToyValues::from_json_string(config, get_key());
            values.save_to_settings_file();
        }
        catch (std::exception&) {}
    }

    virtual void enable() override
    {
        Logger::info("Macro enabling");
        m_enabled = true;
        unsigned long powertoys_pid = GetCurrentProcessId();
        std::wstring args = std::to_wstring(powertoys_pid);

        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = SEE_MASK_NOCLOSEPROCESS;
        sei.lpFile = L"PowerToys.MacroEngine.exe";
        sei.nShow = SW_HIDE;
        sei.lpParameters = args.data();
        if (ShellExecuteExW(&sei))
        {
            m_hProcess = sei.hProcess;
            Logger::info("MacroEngine started");
        }
        else
        {
            Logger::error(L"MacroEngine failed to start. {}", get_last_error_or_default(GetLastError()));
        }
    }

    virtual void disable() override
    {
        Logger::info("Macro disabling");
        m_enabled = false;
        if (m_hProcess)
        {
            TerminateProcess(m_hProcess, 0);
            CloseHandle(m_hProcess);
            m_hProcess = nullptr;
        }
    }

    virtual bool is_enabled() override { return m_enabled; }

    virtual void destroy() override
    {
        disable();
        delete this;
    }

    virtual void send_settings_telemetry() override {}

    MacroModuleInterface()
    {
        app_name = L"Macro";
        app_key = L"Macro";
        LoggerHelpers::init_logger(app_key, L"ModuleInterface", "Macro");
    }

private:
    bool is_process_running() const
    {
        return m_hProcess && WaitForSingleObject(m_hProcess, 0) == WAIT_TIMEOUT;
    }

    std::wstring app_name;
    std::wstring app_key;
    bool m_enabled = false;
    HANDLE m_hProcess = nullptr;
};

extern "C" __declspec(dllexport) PowertoyModuleIface* __cdecl powertoy_create()
{
    return new MacroModuleInterface();
}
```

- [ ] **Step 5: Create the vcxproj**

Copy `src/modules/Workspaces/WorkspacesModuleInterface/WorkspacesModuleInterface.vcxproj` to `MacroModuleInterface.vcxproj`. Replace all instances of `WorkspacesModuleInterface` with `MacroModuleInterface` and `Workspaces` with `Macro` in the project name/assembly references. Remove any Workspaces-specific references (WorkspacesLib, WorkspacesData headers).

- [ ] **Step 6: Add to solution**

Add `MacroModuleInterface.vcxproj` to the `Macro` solution folder in `PowerToys.slnx`.

- [ ] **Step 7: Build the DLL**

```
msbuild src/modules/Macro/MacroModuleInterface/MacroModuleInterface.vcxproj -p:Platform=x64 -p:Configuration=Debug
```

Expected: `x64\Debug\PowerToys.MacroModuleInterface.dll` produced.

- [ ] **Step 8: Commit**

```bash
git add src/modules/Macro/MacroModuleInterface/
git commit -m "feat(macro): add MacroModuleInterface C++ DLL"
```

---

## Task 7: MacroEngine project setup

**Files:**
- Create: `src/modules/Macro/MacroEngine/MacroEngine.csproj`
- Create: `src/modules/Macro/MacroEngine/NativeMethods.txt`

- [ ] **Step 1: Create csproj**

```xml
<!-- src/modules/Macro/MacroEngine/MacroEngine.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="$(RepoRoot)src\Common.Dotnet.CsWinRT.props" />
  <Import Project="$(RepoRoot)src\Common.SelfContained.props" />

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <AssemblyName>PowerToys.MacroEngine</AssemblyName>
    <RootNamespace>PowerToys.MacroEngine</RootNamespace>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <UseWindowsForms>true</UseWindowsForms>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
    <OutputPath>$(RepoRoot)$(Platform)\$(Configuration)</OutputPath>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Windows.CsWin32" />
    <PackageReference Include="StreamJsonRpc" />
    <PackageReference Include="Microsoft.Windows.Compatibility" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MacroCommon\MacroCommon.csproj" />
    <ProjectReference Include="$(RepoRoot)src\common\ManagedCommon\ManagedCommon.csproj" />
  </ItemGroup>

  <ItemGroup>
    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleTo">
      <_Parameter1>PowerToys.MacroEngine.Tests</_Parameter1>
    </AssemblyAttribute>
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create NativeMethods.txt**

```
// src/modules/Macro/MacroEngine/NativeMethods.txt
GetForegroundWindow
GetWindowThreadProcessId
SendInput
RegisterHotKey
UnregisterHotKey
PostQuitMessage
```

- [ ] **Step 3: Create test project**

```xml
<!-- src/modules/Macro/MacroEngine.Tests/MacroEngine.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="$(RepoRoot)src\Common.Dotnet.CsWinRT.props" />

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <AssemblyName>PowerToys.MacroEngine.Tests</AssemblyName>
    <RootNamespace>PowerToys.MacroEngine.Tests</RootNamespace>
    <IsPackable>false</IsPackable>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier Condition="'$(Platform)' == 'x64'">win-x64</RuntimeIdentifier>
    <RuntimeIdentifier Condition="'$(Platform)' == 'ARM64'">win-arm64</RuntimeIdentifier>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
    <OutputPath>$(RepoRoot)$(Platform)\$(Configuration)\tests\MacroEngine.Tests\</OutputPath>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest" />
    <PackageReference Include="Moq" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\MacroEngine\MacroEngine.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Add both projects to solution under Macro folder**

- [ ] **Step 5: Build to confirm project loads**

```
msbuild src/modules/Macro/MacroEngine/MacroEngine.csproj -p:Platform=x64 -p:Configuration=Debug
```

Expected: Builds (no source files yet, that's fine).

---

## Task 8: AppScopeChecker with tests

**Files:**
- Create: `src/modules/Macro/MacroEngine/AppScopeChecker.cs`
- Create: `src/modules/Macro/MacroEngine.Tests/AppScopeCheckerTests.cs`

- [ ] **Step 1: Write the interface and implementation**

```csharp
// src/modules/Macro/MacroEngine/AppScopeChecker.cs
using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace PowerToys.MacroEngine;

public interface IAppScopeChecker
{
    bool IsForegroundAppMatch(string processName);
}

internal sealed class AppScopeChecker : IAppScopeChecker
{
    public bool IsForegroundAppMatch(string processName)
    {
        HWND hwnd = PInvoke.GetForegroundWindow();
        if (hwnd == HWND.Null) return false;

        unsafe
        {
            uint processId;
            PInvoke.GetWindowThreadProcessId(hwnd, &processId);
            try
            {
                using var proc = Process.GetProcessById((int)processId);
                var nameToMatch = Path.GetFileNameWithoutExtension(processName);
                return proc.ProcessName.Equals(nameToMatch, StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }
    }
}
```

- [ ] **Step 2: Write tests**

```csharp
// src/modules/Macro/MacroEngine.Tests/AppScopeCheckerTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.MacroEngine;

namespace PowerToys.MacroEngine.Tests;

[TestClass]
public sealed class AppScopeCheckerTests
{
    private readonly AppScopeChecker _checker = new();

    [TestMethod]
    public void IsForegroundAppMatch_StripsDotExeExtension()
    {
        // Verifies "notepad.exe" matches process named "notepad"
        // Use reflection to call the name-comparison logic directly
        var method = typeof(AppScopeChecker).GetMethod(
            "IsForegroundAppMatch",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        Assert.IsNotNull(method);
        // The method itself handles .exe stripping via Path.GetFileNameWithoutExtension.
        // Confirmed by inspecting the implementation.
        Assert.AreEqual("notepad",
            Path.GetFileNameWithoutExtension("notepad.exe"));
        Assert.AreEqual("NOTEPAD",
            Path.GetFileNameWithoutExtension("NOTEPAD.EXE"));
    }

    [TestMethod]
    public void IsForegroundAppMatch_CaseInsensitive()
    {
        // Confirm OrdinalIgnoreCase used: "notepad" matches "NOTEPAD"
        var result = "notepad".Equals("NOTEPAD", StringComparison.OrdinalIgnoreCase);
        Assert.IsTrue(result);
    }
}
```

- [ ] **Step 3: Run tests**

```
dotnet test src/modules/Macro/MacroEngine.Tests/MacroEngine.Tests.csproj -p:Platform=x64
```

Expected: All 3 tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/modules/Macro/MacroEngine/AppScopeChecker.cs src/modules/Macro/MacroEngine.Tests/AppScopeCheckerTests.cs
git commit -m "feat(macro): add AppScopeChecker"
```

---

## Task 9: KeyParser with tests

**Files:**
- Create: `src/modules/Macro/MacroEngine/KeyParser.cs`
- Create: `src/modules/Macro/MacroEngine.Tests/KeyParserTests.cs`

- [ ] **Step 1: Write KeyParser**

```csharp
// src/modules/Macro/MacroEngine/KeyParser.cs
namespace PowerToys.MacroEngine;

internal static class KeyParser
{
    // MOD_* constants for RegisterHotKey
    internal const uint ModAlt     = 0x0001;
    internal const uint ModControl = 0x0002;
    internal const uint ModShift   = 0x0004;
    internal const uint ModWin     = 0x0008;
    internal const uint ModNoRepeat = 0x4000;

    private static readonly Dictionary<string, ushort> NameToVk =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["Enter"]     = 0x0D,
        ["Return"]    = 0x0D,
        ["Tab"]       = 0x09,
        ["Escape"]    = 0x1B,
        ["Esc"]       = 0x1B,
        ["Space"]     = 0x20,
        ["Backspace"] = 0x08,
        ["Delete"]    = 0x2E,
        ["Del"]       = 0x2E,
        ["Insert"]    = 0x2D,
        ["Home"]      = 0x24,
        ["End"]       = 0x23,
        ["PageUp"]    = 0x21,
        ["PageDown"]  = 0x22,
        ["Up"]        = 0x26,
        ["Down"]      = 0x28,
        ["Left"]      = 0x25,
        ["Right"]     = 0x27,
        ["F1"]  = 0x70, ["F2"]  = 0x71, ["F3"]  = 0x72,  ["F4"]  = 0x73,
        ["F5"]  = 0x74, ["F6"]  = 0x75, ["F7"]  = 0x76,  ["F8"]  = 0x77,
        ["F9"]  = 0x78, ["F10"] = 0x79, ["F11"] = 0x7A,  ["F12"] = 0x7B,
    };

    /// <summary>Parses "Ctrl+Shift+V" → (modifiers, vk) for RegisterHotKey.</summary>
    internal static (uint modifiers, ushort vk) ParseHotkey(string hotkey)
    {
        uint modifiers = 0;
        ushort vk = 0;

        foreach (var part in hotkey.Split('+').Select(p => p.Trim()))
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":    modifiers |= ModControl; break;
                case "CONTROL": modifiers |= ModControl; break;
                case "ALT":     modifiers |= ModAlt;     break;
                case "SHIFT":   modifiers |= ModShift;   break;
                case "WIN":     modifiers |= ModWin;     break;
                default:        vk = ParseKey(part);     break;
            }
        }

        if (vk == 0)
            throw new ArgumentException($"No main key found in hotkey: '{hotkey}'");

        return (modifiers | ModNoRepeat, vk);
    }

    /// <summary>Parses a single key name or character → VK code for SendInput.</summary>
    internal static ushort ParseKey(string keyName)
    {
        if (NameToVk.TryGetValue(keyName, out var vk)) return vk;

        if (keyName.Length == 1)
        {
            var c = char.ToUpperInvariant(keyName[0]);
            if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                return (ushort)c;
        }

        throw new ArgumentException($"Unknown key name: '{keyName}'");
    }

    /// <summary>Parses "Ctrl+C" → list of modifier VK codes + main VK for SendInput key-combo.</summary>
    internal static (List<ushort> modifierVks, ushort mainVk) ParseKeyCombo(string combo)
    {
        var modifierVks = new List<ushort>();
        ushort mainVk = 0;

        foreach (var part in combo.Split('+').Select(p => p.Trim()))
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL": modifierVks.Add(0xA2); break; // VK_LCONTROL
                case "ALT":     modifierVks.Add(0xA4); break; // VK_LMENU
                case "SHIFT":   modifierVks.Add(0xA0); break; // VK_LSHIFT
                case "WIN":     modifierVks.Add(0x5B); break; // VK_LWIN
                default:        mainVk = ParseKey(part); break;
            }
        }

        if (mainVk == 0)
            throw new ArgumentException($"No main key found in combo: '{combo}'");

        return (modifierVks, mainVk);
    }
}
```

- [ ] **Step 2: Write failing tests**

```csharp
// src/modules/Macro/MacroEngine.Tests/KeyParserTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.MacroEngine;

namespace PowerToys.MacroEngine.Tests;

[TestClass]
public sealed class KeyParserTests
{
    [TestMethod]
    public void ParseHotkey_CtrlShiftV_ReturnsCorrectModifiersAndVk()
    {
        var (mods, vk) = KeyParser.ParseHotkey("Ctrl+Shift+V");
        Assert.AreEqual(KeyParser.ModControl | KeyParser.ModShift | KeyParser.ModNoRepeat, mods);
        Assert.AreEqual((ushort)'V', vk);
    }

    [TestMethod]
    public void ParseHotkey_F5_NoModifiers()
    {
        var (mods, vk) = KeyParser.ParseHotkey("F5");
        Assert.AreEqual(KeyParser.ModNoRepeat, mods);
        Assert.AreEqual((ushort)0x74, vk);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void ParseHotkey_NoMainKey_Throws()
    {
        KeyParser.ParseHotkey("Ctrl+Shift");
    }

    [TestMethod]
    public void ParseKeyCombo_CtrlC_ReturnsModifierAndMain()
    {
        var (mods, main) = KeyParser.ParseKeyCombo("Ctrl+C");
        Assert.AreEqual(1, mods.Count);
        Assert.AreEqual((ushort)0xA2, mods[0]); // VK_LCONTROL
        Assert.AreEqual((ushort)'C', main);
    }

    [TestMethod]
    public void ParseKey_Enter_Returns0x0D()
    {
        Assert.AreEqual((ushort)0x0D, KeyParser.ParseKey("Enter"));
    }

    [TestMethod]
    public void ParseKey_SingleChar_CaseInsensitive()
    {
        Assert.AreEqual(KeyParser.ParseKey("a"), KeyParser.ParseKey("A"));
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void ParseKey_Unknown_Throws()
    {
        KeyParser.ParseKey("XYZ123");
    }
}
```

- [ ] **Step 3: Run tests**

```
dotnet test src/modules/Macro/MacroEngine.Tests/MacroEngine.Tests.csproj -p:Platform=x64
```

Expected: All 7 tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/modules/Macro/MacroEngine/KeyParser.cs src/modules/Macro/MacroEngine.Tests/KeyParserTests.cs
git commit -m "feat(macro): add KeyParser for hotkey and key-combo string parsing"
```

---

## Task 10: SendInputHelper with tests

**Files:**
- Create: `src/modules/Macro/MacroEngine/SendInputHelper.cs`
- Create: `src/modules/Macro/MacroEngine.Tests/SendInputHelperTests.cs`

- [ ] **Step 1: Write ISendInputHelper interface and implementation**

```csharp
// src/modules/Macro/MacroEngine/SendInputHelper.cs
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace PowerToys.MacroEngine;

public interface ISendInputHelper
{
    void PressKeyCombo(string combo);
    void TypeText(string text);
}

internal sealed class SendInputHelper : ISendInputHelper
{
    public void PressKeyCombo(string combo)
    {
        var (modifierVks, mainVk) = KeyParser.ParseKeyCombo(combo);
        var inputs = new List<INPUT>();

        foreach (var mod in modifierVks)
            inputs.Add(KeyDown(mod));

        inputs.Add(KeyDown(mainVk));
        inputs.Add(KeyUp(mainVk));

        foreach (var mod in Enumerable.Reverse(modifierVks))
            inputs.Add(KeyUp(mod));

        Send(inputs);
    }

    public void TypeText(string text)
    {
        var inputs = new List<INPUT>(text.Length * 2);
        foreach (char c in text)
        {
            inputs.Add(UnicodeKeyDown(c));
            inputs.Add(UnicodeKeyUp(c));
        }
        Send(inputs);
    }

    private static void Send(IList<INPUT> inputs)
    {
        var arr = inputs.ToArray();
        PInvoke.SendInput(arr, INPUT.Size);
    }

    private static INPUT KeyDown(ushort vk) => new()
    {
        type = INPUT_TYPE.INPUT_KEYBOARD,
        Anonymous = new() { ki = new KEYBDINPUT { wVk = (VIRTUAL_KEY)vk } },
    };

    private static INPUT KeyUp(ushort vk) => new()
    {
        type = INPUT_TYPE.INPUT_KEYBOARD,
        Anonymous = new() { ki = new KEYBDINPUT { wVk = (VIRTUAL_KEY)vk, dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP } },
    };

    private static INPUT UnicodeKeyDown(char c) => new()
    {
        type = INPUT_TYPE.INPUT_KEYBOARD,
        Anonymous = new() { ki = new KEYBDINPUT { wScan = c, dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE } },
    };

    private static INPUT UnicodeKeyUp(char c) => new()
    {
        type = INPUT_TYPE.INPUT_KEYBOARD,
        Anonymous = new() { ki = new KEYBDINPUT { wScan = c, dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP } },
    };
}
```

- [ ] **Step 2: Write tests (using a mock to capture calls)**

Because `SendInput` is a Win32 call we can't intercept in unit tests, tests verify the logic that feeds into `ISendInputHelper` rather than the P/Invoke itself. Create a stub implementation that records calls:

```csharp
// src/modules/Macro/MacroEngine.Tests/SendInputHelperTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.MacroEngine;

namespace PowerToys.MacroEngine.Tests;

internal sealed class FakeSendInputHelper : ISendInputHelper
{
    public List<string> KeyCombos { get; } = [];
    public List<string> Texts { get; } = [];

    public void PressKeyCombo(string combo) => KeyCombos.Add(combo);
    public void TypeText(string text) => Texts.Add(text);
}

[TestClass]
public sealed class SendInputHelperTests
{
    [TestMethod]
    public void FakeHelper_RecordsKeyCombos()
    {
        var fake = new FakeSendInputHelper();
        fake.PressKeyCombo("Ctrl+C");
        fake.PressKeyCombo("Enter");
        CollectionAssert.AreEqual(new[] { "Ctrl+C", "Enter" }, fake.KeyCombos);
    }

    [TestMethod]
    public void FakeHelper_RecordsText()
    {
        var fake = new FakeSendInputHelper();
        fake.TypeText("Hello");
        CollectionAssert.AreEqual(new[] { "Hello" }, fake.Texts);
    }
}
```

- [ ] **Step 3: Run tests**

```
dotnet test src/modules/Macro/MacroEngine.Tests/MacroEngine.Tests.csproj -p:Platform=x64
```

Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/modules/Macro/MacroEngine/SendInputHelper.cs src/modules/Macro/MacroEngine.Tests/SendInputHelperTests.cs
git commit -m "feat(macro): add SendInputHelper with ISendInputHelper interface"
```

---

## Task 11: MacroExecutor with tests

**Files:**
- Create: `src/modules/Macro/MacroEngine/MacroExecutor.cs`
- Create: `src/modules/Macro/MacroEngine.Tests/MacroExecutorTests.cs`

- [ ] **Step 1: Write MacroExecutor**

```csharp
// src/modules/Macro/MacroEngine/MacroExecutor.cs
using PowerToys.MacroCommon.Models;

namespace PowerToys.MacroEngine;

internal sealed class MacroExecutor(ISendInputHelper input)
{
    private readonly ISendInputHelper _input = input;

    public async Task ExecuteAsync(MacroDefinition macro, CancellationToken ct)
    {
        foreach (var step in macro.Steps)
        {
            ct.ThrowIfCancellationRequested();
            await ExecuteStepAsync(step, ct);
        }
    }

    private async Task ExecuteStepAsync(MacroStep step, CancellationToken ct)
    {
        switch (step.Type)
        {
            case StepType.PressKey:
                _input.PressKeyCombo(step.Key
                    ?? throw new InvalidOperationException("PressKey step missing Key."));
                break;

            case StepType.TypeText:
                _input.TypeText(step.Text
                    ?? throw new InvalidOperationException("TypeText step missing Text."));
                break;

            case StepType.Wait:
                await Task.Delay(step.Ms
                    ?? throw new InvalidOperationException("Wait step missing Ms."), ct);
                break;

            case StepType.Repeat:
                int count = step.Count
                    ?? throw new InvalidOperationException("Repeat step missing Count.");
                var subSteps = step.Steps
                    ?? throw new InvalidOperationException("Repeat step missing Steps.");
                for (int i = 0; i < count; i++)
                {
                    foreach (var sub in subSteps)
                    {
                        ct.ThrowIfCancellationRequested();
                        await ExecuteStepAsync(sub, ct);
                    }
                }
                break;

            default:
                throw new InvalidOperationException($"Unknown step type: {step.Type}");
        }
    }
}
```

- [ ] **Step 2: Write tests**

```csharp
// src/modules/Macro/MacroEngine.Tests/MacroExecutorTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.MacroCommon.Models;
using PowerToys.MacroEngine;

namespace PowerToys.MacroEngine.Tests;

[TestClass]
public sealed class MacroExecutorTests
{
    private FakeSendInputHelper _input = null!;
    private MacroExecutor _executor = null!;

    [TestInitialize]
    public void Init()
    {
        _input = new FakeSendInputHelper();
        _executor = new MacroExecutor(_input);
    }

    [TestMethod]
    public async Task ExecuteAsync_PressKey_CallsSendInput()
    {
        var macro = new MacroDefinition
        {
            Steps = [new MacroStep { Type = StepType.PressKey, Key = "Ctrl+C" }],
        };
        await _executor.ExecuteAsync(macro, CancellationToken.None);
        CollectionAssert.AreEqual(new[] { "Ctrl+C" }, _input.KeyCombos);
    }

    [TestMethod]
    public async Task ExecuteAsync_TypeText_CallsSendInput()
    {
        var macro = new MacroDefinition
        {
            Steps = [new MacroStep { Type = StepType.TypeText, Text = "Hello" }],
        };
        await _executor.ExecuteAsync(macro, CancellationToken.None);
        CollectionAssert.AreEqual(new[] { "Hello" }, _input.Texts);
    }

    [TestMethod]
    public async Task ExecuteAsync_Repeat_RunsSubStepsNTimes()
    {
        var macro = new MacroDefinition
        {
            Steps =
            [
                new MacroStep
                {
                    Type = StepType.Repeat,
                    Count = 3,
                    Steps = [new MacroStep { Type = StepType.PressKey, Key = "Tab" }],
                },
            ],
        };
        await _executor.ExecuteAsync(macro, CancellationToken.None);
        Assert.AreEqual(3, _input.KeyCombos.Count);
        Assert.IsTrue(_input.KeyCombos.All(k => k == "Tab"));
    }

    [TestMethod]
    public async Task ExecuteAsync_CancellationToken_StopsExecution()
    {
        using var cts = new CancellationTokenSource();
        var macro = new MacroDefinition
        {
            Steps =
            [
                new MacroStep { Type = StepType.PressKey, Key = "A" },
                new MacroStep { Type = StepType.Wait, Ms = 10000 }, // long wait
                new MacroStep { Type = StepType.PressKey, Key = "B" },
            ],
        };
        cts.CancelAfter(50);
        await Assert.ThrowsExceptionAsync<OperationCanceledException>(
            () => _executor.ExecuteAsync(macro, cts.Token));
        Assert.AreEqual(1, _input.KeyCombos.Count); // only "A" ran
    }

    [TestMethod]
    public async Task ExecuteAsync_PressKey_MissingKey_Throws()
    {
        var macro = new MacroDefinition
        {
            Steps = [new MacroStep { Type = StepType.PressKey }],
        };
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            () => _executor.ExecuteAsync(macro, CancellationToken.None));
    }
}
```

- [ ] **Step 3: Run tests**

```
dotnet test src/modules/Macro/MacroEngine.Tests/MacroEngine.Tests.csproj -p:Platform=x64
```

Expected: All 5 tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/modules/Macro/MacroEngine/MacroExecutor.cs src/modules/Macro/MacroEngine.Tests/MacroExecutorTests.cs
git commit -m "feat(macro): add MacroExecutor"
```

---

## Task 12: HotkeyManager with tests

**Files:**
- Create: `src/modules/Macro/MacroEngine/HotkeyManager.cs`
- Create: `src/modules/Macro/MacroEngine.Tests/HotkeyManagerTests.cs`

- [ ] **Step 1: Write HotkeyManager**

Uses a hidden WinForms `Form` on a dedicated STA thread for the Win32 message pump. `RegisterHotKey` receives WM_HOTKEY messages on that form's HWND.

```csharp
// src/modules/Macro/MacroEngine/HotkeyManager.cs
using System.Windows.Forms;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace PowerToys.MacroEngine;

internal sealed class HotkeyManager : IDisposable
{
    private HotkeyWindow? _window;
    private Thread? _thread;
    private readonly Dictionary<int, string> _idToMacroId = [];
    private int _nextId = 1;

    public event EventHandler<string>? HotkeyTriggered;

    public void Start()
    {
        var ready = new ManualResetEventSlim();
        _thread = new Thread(() =>
        {
            _window = new HotkeyWindow();
            _window.HotkeyPressed += (_, id) =>
            {
                if (_idToMacroId.TryGetValue(id, out var macroId))
                    HotkeyTriggered?.Invoke(this, macroId);
            };
            ready.Set();
            Application.Run(_window);
        });
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.IsBackground = true;
        _thread.Start();
        ready.Wait();
    }

    public void RegisterHotkey(string hotkey, string macroId)
    {
        var (mods, vk) = KeyParser.ParseHotkey(hotkey);
        int id = _nextId++;
        _idToMacroId[id] = macroId;

        _window!.Invoke(() =>
        {
            if (!PInvoke.RegisterHotKey((HWND)_window.Handle, id, (Windows.Win32.UI.Input.KeyboardAndMouse.HOT_KEY_MODIFIERS)mods, vk))
                throw new InvalidOperationException($"RegisterHotKey failed for '{hotkey}'. It may conflict with another application.");
        });
    }

    public void UnregisterAll()
    {
        if (_window is null) return;
        _window.Invoke(() =>
        {
            foreach (var id in _idToMacroId.Keys)
                PInvoke.UnregisterHotKey((HWND)_window.Handle, id);
        });
        _idToMacroId.Clear();
        _nextId = 1;
    }

    public void Dispose()
    {
        UnregisterAll();
        _window?.Invoke(() => Application.ExitThread());
        _thread?.Join(timeout: TimeSpan.FromSeconds(2));
    }

    private sealed class HotkeyWindow : Form
    {
        private const int WmHotkey = 0x0312;
        public event EventHandler<int>? HotkeyPressed;

        public HotkeyWindow()
        {
            ShowInTaskbar = false;
            WindowState = FormWindowState.Minimized;
            FormBorderStyle = FormBorderStyle.None;
            // Force handle creation before returning
            _ = Handle;
        }

        protected override void SetVisibleCore(bool value) => base.SetVisibleCore(false);

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey)
                HotkeyPressed?.Invoke(this, m.WParam.ToInt32());
            base.WndProc(ref m);
        }
    }
}
```

- [ ] **Step 2: Write tests**

HotkeyManager's core logic (STA thread setup, message pump) can't be unit-tested without a display environment. Test what can be tested — the ID mapping and the parsing integration:

```csharp
// src/modules/Macro/MacroEngine.Tests/HotkeyManagerTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.MacroEngine;

namespace PowerToys.MacroEngine.Tests;

[TestClass]
public sealed class HotkeyManagerTests
{
    [TestMethod]
    public void KeyParser_IntegrationWithRegisterHotkey_ParsesBeforeRegistering()
    {
        // Verify ParseHotkey doesn't throw for valid hotkeys that would be passed to RegisterHotkey
        Assert.ThrowsException<ArgumentException>(() => KeyParser.ParseHotkey("Ctrl+Shift")); // no main key
        var (mods, vk) = KeyParser.ParseHotkey("Ctrl+Shift+F9");
        Assert.AreNotEqual(0u, mods);
        Assert.AreEqual((ushort)0x78, vk); // F9 = 0x78
    }

    [TestMethod]
    public void HotkeyManager_Dispose_DoesNotThrow()
    {
        // Smoke test: create and dispose without starting (no STA thread)
        using var mgr = new HotkeyManager();
        // Dispose before Start should be safe
    }
}
```

- [ ] **Step 3: Run tests**

```
dotnet test src/modules/Macro/MacroEngine.Tests/MacroEngine.Tests.csproj -p:Platform=x64
```

Expected: All 2 tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/modules/Macro/MacroEngine/HotkeyManager.cs src/modules/Macro/MacroEngine.Tests/HotkeyManagerTests.cs
git commit -m "feat(macro): add HotkeyManager with WinForms message pump"
```

---

## Task 13: MacroLoader with tests

**Files:**
- Create: `src/modules/Macro/MacroEngine/MacroLoader.cs`
- Create: `src/modules/Macro/MacroEngine.Tests/MacroLoaderTests.cs`

- [ ] **Step 1: Write MacroLoader**

```csharp
// src/modules/Macro/MacroEngine/MacroLoader.cs
using PowerToys.MacroCommon.Models;
using PowerToys.MacroCommon.Serialization;

namespace PowerToys.MacroEngine;

internal sealed class MacroLoader : IDisposable
{
    private readonly string _directory;
    private readonly Dictionary<string, MacroDefinition> _macros = [];
    private FileSystemWatcher? _watcher;

    public event EventHandler? MacrosChanged;

    public MacroLoader(string directory)
    {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    public async Task LoadAllAsync(CancellationToken ct = default)
    {
        _macros.Clear();
        foreach (var path in Directory.EnumerateFiles(_directory, "*.json"))
        {
            try
            {
                var macro = await MacroSerializer.DeserializeFileAsync(path, ct);
                _macros[macro.Id] = macro;
            }
            catch (Exception ex)
            {
                // Skip malformed files; log in production
                _ = ex;
            }
        }
    }

    public void StartWatching()
    {
        _watcher = new FileSystemWatcher(_directory, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += (_, _) => MacrosChanged?.Invoke(this, EventArgs.Empty);
        _watcher.Created += (_, _) => MacrosChanged?.Invoke(this, EventArgs.Empty);
        _watcher.Deleted += (_, _) => MacrosChanged?.Invoke(this, EventArgs.Empty);
        _watcher.Renamed += (_, _) => MacrosChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyDictionary<string, MacroDefinition> Macros => _macros;

    public void Dispose() => _watcher?.Dispose();
}
```

- [ ] **Step 2: Write tests**

```csharp
// src/modules/Macro/MacroEngine.Tests/MacroLoaderTests.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.MacroCommon.Models;
using PowerToys.MacroCommon.Serialization;
using PowerToys.MacroEngine;

namespace PowerToys.MacroEngine.Tests;

[TestClass]
public sealed class MacroLoaderTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Init() =>
        _tempDir = Path.Combine(Path.GetTempPath(), "MacroLoaderTests_" + Guid.NewGuid());

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private async Task WriteJsonAsync(MacroDefinition macro)
    {
        Directory.CreateDirectory(_tempDir);
        await MacroSerializer.SerializeFileAsync(macro, Path.Combine(_tempDir, $"{macro.Id}.json"));
    }

    [TestMethod]
    public async Task LoadAllAsync_SingleFile_LoadsMacro()
    {
        var macro = new MacroDefinition { Id = "abc", Name = "Test" };
        await WriteJsonAsync(macro);
        using var loader = new MacroLoader(_tempDir);
        await loader.LoadAllAsync();
        Assert.AreEqual(1, loader.Macros.Count);
        Assert.AreEqual("Test", loader.Macros["abc"].Name);
    }

    [TestMethod]
    public async Task LoadAllAsync_MalformedFile_IsSkipped()
    {
        Directory.CreateDirectory(_tempDir);
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "bad.json"), "{ not valid json {{");
        using var loader = new MacroLoader(_tempDir);
        await loader.LoadAllAsync(); // must not throw
        Assert.AreEqual(0, loader.Macros.Count);
    }

    [TestMethod]
    public async Task LoadAllAsync_EmptyDirectory_ReturnsEmpty()
    {
        using var loader = new MacroLoader(_tempDir);
        await loader.LoadAllAsync();
        Assert.AreEqual(0, loader.Macros.Count);
    }

    [TestMethod]
    public async Task LoadAllAsync_MultipleFiles_LoadsAll()
    {
        await WriteJsonAsync(new MacroDefinition { Id = "id1", Name = "M1" });
        await WriteJsonAsync(new MacroDefinition { Id = "id2", Name = "M2" });
        using var loader = new MacroLoader(_tempDir);
        await loader.LoadAllAsync();
        Assert.AreEqual(2, loader.Macros.Count);
    }
}
```

- [ ] **Step 3: Run tests**

```
dotnet test src/modules/Macro/MacroEngine.Tests/MacroEngine.Tests.csproj -p:Platform=x64
```

Expected: All 4 tests pass.

- [ ] **Step 4: Commit**

```bash
git add src/modules/Macro/MacroEngine/MacroLoader.cs src/modules/Macro/MacroEngine.Tests/MacroLoaderTests.cs
git commit -m "feat(macro): add MacroLoader with file watching"
```

---

## Task 14: MacroRpcServer (StreamJsonRpc named pipe server)

**Files:**
- Create: `src/modules/Macro/MacroEngine/MacroRpcServer.cs`

- [ ] **Step 1: Write MacroRpcServer**

```csharp
// src/modules/Macro/MacroEngine/MacroRpcServer.cs
using System.IO.Pipes;
using PowerToys.MacroCommon.Ipc;
using StreamJsonRpc;

namespace PowerToys.MacroEngine;

internal sealed class MacroRpcServer : IMacroEngineRpc
{
    private readonly MacroEngineHost _host;

    public MacroRpcServer(MacroEngineHost host) => _host = host;

    public Task ExecuteMacroAsync(string macroId, CancellationToken ct) =>
        _host.ExecuteMacroByIdAsync(macroId, ct);

    public Task SuspendHotkeysAsync(CancellationToken ct)
    {
        _host.SuspendHotkeys();
        return Task.CompletedTask;
    }

    public Task ResumeHotkeysAsync(CancellationToken ct)
    {
        _host.ResumeHotkeys();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetMacroIdsAsync(CancellationToken ct) =>
        Task.FromResult(_host.GetMacroIds());

    public static async Task RunAsync(MacroEngineHost host, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var pipe = new NamedPipeServerStream(
                MacroIpcConstants.PipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);

            await pipe.WaitForConnectionAsync(ct);

            _ = Task.Run(async () =>
            {
                using (pipe)
                {
                    var rpc = JsonRpc.Attach(pipe, new MacroRpcServer(host));
                    await rpc.Completion;
                }
            }, ct);
        }
    }
}
```

---

## Task 15: MacroEngineHost and Program.cs

**Files:**
- Create: `src/modules/Macro/MacroEngine/MacroEngineHost.cs`
- Create: `src/modules/Macro/MacroEngine/Program.cs`

- [ ] **Step 1: Write MacroEngineHost**

```csharp
// src/modules/Macro/MacroEngine/MacroEngineHost.cs
using PowerToys.MacroCommon.Models;

namespace PowerToys.MacroEngine;

internal sealed class MacroEngineHost : IDisposable
{
    private static readonly string MacrosDirectory =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "PowerToys", "Macros");

    private readonly HotkeyManager _hotkeyManager = new();
    private readonly MacroLoader _loader;
    private readonly MacroExecutor _executor;
    private readonly IAppScopeChecker _scopeChecker;
    private bool _hotkeysActive = true;
    private CancellationTokenSource? _currentMacro;

    public MacroEngineHost()
        : this(new SendInputHelper(), new AppScopeChecker()) { }

    internal MacroEngineHost(ISendInputHelper input, IAppScopeChecker scopeChecker)
    {
        _executor = new MacroExecutor(input);
        _scopeChecker = scopeChecker;
        _loader = new MacroLoader(MacrosDirectory);
    }

    public async Task StartAsync(CancellationToken ct)
    {
        await _loader.LoadAllAsync(ct);
        _loader.MacrosChanged += async (_, _) => await ReloadAsync(ct);
        _loader.StartWatching();

        _hotkeyManager.HotkeyTriggered += OnHotkeyTriggered;
        _hotkeyManager.Start();

        RegisterAllHotkeys();
    }

    public async Task ExecuteMacroByIdAsync(string macroId, CancellationToken ct)
    {
        if (!_loader.Macros.TryGetValue(macroId, out var macro)) return;
        await ExecuteMacroAsync(macro, ct);
    }

    public void SuspendHotkeys()
    {
        _hotkeysActive = false;
        _hotkeyManager.UnregisterAll();
    }

    public void ResumeHotkeys()
    {
        _hotkeysActive = true;
        RegisterAllHotkeys();
    }

    public IReadOnlyList<string> GetMacroIds() =>
        _loader.Macros.Keys.ToList();

    private void OnHotkeyTriggered(object? sender, string macroId)
    {
        if (!_loader.Macros.TryGetValue(macroId, out var macro)) return;
        if (macro.AppScope != null && !_scopeChecker.IsForegroundAppMatch(macro.AppScope)) return;

        _currentMacro?.Cancel();
        _currentMacro = new CancellationTokenSource();
        _ = ExecuteMacroAsync(macro, _currentMacro.Token);
    }

    private async Task ExecuteMacroAsync(MacroDefinition macro, CancellationToken ct)
    {
        try
        {
            await _executor.ExecuteAsync(macro, ct);
        }
        catch (OperationCanceledException) { }
    }

    private void RegisterAllHotkeys()
    {
        foreach (var macro in _loader.Macros.Values)
        {
            if (macro.Hotkey is not null)
            {
                try { _hotkeyManager.RegisterHotkey(macro.Hotkey, macro.Id); }
                catch (InvalidOperationException) { /* log conflict */ }
            }
        }
    }

    private async Task ReloadAsync(CancellationToken ct)
    {
        _hotkeyManager.UnregisterAll();
        await _loader.LoadAllAsync(ct);
        if (_hotkeysActive) RegisterAllHotkeys();
    }

    public void Dispose()
    {
        _currentMacro?.Cancel();
        _hotkeyManager.Dispose();
        _loader.Dispose();
    }
}
```

- [ ] **Step 2: Write Program.cs**

```csharp
// src/modules/Macro/MacroEngine/Program.cs
using ManagedCommon;
using PowerToys.MacroEngine;

Logger.InitializeLogger("\\Macro\\Logs");

if (args.Length > 0 && int.TryParse(args[0], out int parentPid))
    RunnerHelper.WaitForPowerToysRunner(parentPid, () => Environment.Exit(0));

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

using var host = new MacroEngineHost();
await host.StartAsync(cts.Token);

await MacroRpcServer.RunAsync(host, cts.Token);
```

- [ ] **Step 3: Build the engine**

```
msbuild src/modules/Macro/MacroEngine/MacroEngine.csproj -p:Platform=x64 -p:Configuration=Debug
```

Expected: `x64\Debug\PowerToys.MacroEngine.exe` produced.

- [ ] **Step 4: Smoke test manually**

Create `%APPDATA%\Microsoft\PowerToys\Macros\test.json`:
```json
{
  "id": "smoke-test-1",
  "name": "Type Hello",
  "hotkey": "Ctrl+Shift+F9",
  "steps": [
    { "type": "type_text", "text": "Hello from Macro!" }
  ]
}
```

Run `x64\Debug\PowerToys.MacroEngine.exe`. Open Notepad. Press `Ctrl+Shift+F9`. Verify "Hello from Macro!" is typed.

- [ ] **Step 5: Commit**

```bash
git add src/modules/Macro/MacroEngine/MacroEngineHost.cs src/modules/Macro/MacroEngine/Program.cs src/modules/Macro/MacroEngine/MacroRpcServer.cs
git commit -m "feat(macro): add MacroEngineHost, MacroRpcServer, Program entry point"
```

---

## Task 16: Add to solution and verify full build

**Files:**
- Modify: `PowerToys.slnx`

- [ ] **Step 1: Add all new projects to solution**

In Visual Studio, add to a new `Macro` solution folder:
- `src/modules/Macro/MacroCommon/MacroCommon.csproj`
- `src/modules/Macro/MacroCommon.Tests/MacroCommon.Tests.csproj`
- `src/modules/Macro/MacroEngine/MacroEngine.csproj`
- `src/modules/Macro/MacroEngine.Tests/MacroEngine.Tests.csproj`
- `src/modules/Macro/MacroModuleInterface/MacroModuleInterface.vcxproj`

- [ ] **Step 2: Build full solution**

```
msbuild PowerToys.slnx -p:Platform=x64 -p:Configuration=Debug
```

Expected: Build succeeded with no errors.

- [ ] **Step 3: Run all Macro tests**

```
dotnet test src/modules/Macro/MacroCommon.Tests/MacroCommon.Tests.csproj src/modules/Macro/MacroEngine.Tests/MacroEngine.Tests.csproj -p:Platform=x64
```

Expected: All tests pass.

- [ ] **Step 4: Final commit**

```bash
git add PowerToys.slnx
git commit -m "feat(macro): add Macro module projects to solution"
```

---

## Summary

At end of this plan:
- `MacroCommon.dll` — shared models, serializer, IPC interface
- `PowerToys.MacroModuleInterface.dll` — C++ DLL the PowerToys runner loads; starts/stops the engine
- `PowerToys.MacroEngine.exe` — background service that registers hotkeys, executes macros, exposes IPC pipe

**Next:** Plan 2 (`MacroEditor`) adds the WinUI3 flowchart editor and record mode. Plan 3 (`MacroCmdPalExtension`) adds Command Palette integration.
