# Macro Settings UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Macro settings page to PowerToys Settings app so users can create, edit, delete, and enable/disable macros with a full step-tree editor and hotkey capture.

**Architecture:** All new code lives in the existing `Settings.UI` project following the existing MVVM pattern. Macros are stored as individual JSON files in `%AppData%\Microsoft\PowerToys\Macros\` via `MacroSerializer`. The settings page communicates with the running engine via `StreamJsonRpc` only to suspend/resume hotkeys during shortcut capture.

**Tech Stack:** WinUI 3, CommunityToolkit.WinUI, MSTest, `PowerToys.MacroCommon` (project reference), `StreamJsonRpc` (already in Settings.UI).

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `src/modules/Macro/MacroCommon/Models/MacroDefinition.cs` | Modify | Add `IsEnabled` property |
| `src/modules/Macro/MacroEngine/MacroEngineHost.cs` | Modify | Skip disabled macros in `RegisterAllHotkeys` |
| `src/modules/Macro/MacroCommon.Tests/Serialization/MacroSerializerTests.cs` | Modify | Add `IsEnabled` round-trip test |
| `src/settings-ui/Settings.UI/PowerToys.Settings.csproj` | Modify | Add `MacroCommon` project reference |
| `src/settings-ui/Settings.UI/Strings/en-us/Resources.resw` | Modify | Add nav item and page strings |
| `src/settings-ui/Settings.UI/ViewModels/MacroHotkeyConverter.cs` | Create | Convert `HotkeySettings` ↔ string |
| `src/settings-ui/Settings.UI/ViewModels/MacroStepViewModel.cs` | Create | Observable wrapper around `MacroStep`, recursive sub-steps |
| `src/settings-ui/Settings.UI/ViewModels/MacroListItem.cs` | Create | Observable wrapper around `MacroDefinition` for the list |
| `src/settings-ui/Settings.UI/ViewModels/MacroEditViewModel.cs` | Create | Edit dialog VM: step tree, hotkey, validation |
| `src/settings-ui/Settings.UI/ViewModels/MacroViewModel.cs` | Create | Page VM: macro collection, file I/O, IPC suspend/resume |
| `src/settings-ui/Settings.UI/SettingsXAML/Views/MacroEditDialog.xaml` + `.cs` | Create | ContentDialog: name, hotkey, app scope, step tree editor |
| `src/settings-ui/Settings.UI/SettingsXAML/Views/MacroPage.xaml` + `.cs` | Create | Settings page: macro list with enable toggles |
| `src/settings-ui/Settings.UI/SettingsXAML/Views/ShellPage.xaml` | Modify | Add Macro nav item under Input / Output group |
| `src/settings-ui/Settings.UI.UnitTests/ViewModelTests/MacroHotkeyConverterTests.cs` | Create | Unit tests for hotkey conversion |
| `src/settings-ui/Settings.UI.UnitTests/ViewModelTests/MacroStepViewModelTests.cs` | Create | Unit tests for step VM round-trip |
| `src/settings-ui/Settings.UI.UnitTests/ViewModelTests/MacroViewModelTests.cs` | Create | Unit tests for macro list load/save/delete |
| `src/settings-ui/Settings.UI.UnitTests/ViewModelTests/MacroEditViewModelTests.cs` | Create | Unit tests for edit VM validation and save |

---

## Build commands (run from worktree root)

```powershell
# Set up VS dev environment first (once per session):
$vsPath = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -property installationPath
Import-Module "$vsPath\Common7\Tools\Microsoft.VisualStudio.DevShell.dll"
Enter-VsDevShell -VsInstallPath $vsPath -SkipAutomaticLocation -DevCmdArguments "-arch=x64"
$env:DOTNET_ROOT = "C:\Program Files\dotnet"

# Build MacroCommon tests:
dotnet test src\modules\Macro\MacroCommon.Tests\MacroCommon.Tests.csproj -p:Platform=x64 --filter "FullyQualifiedName~MacroSerializer"

# Build Settings.UI.UnitTests:
MSBuild src\settings-ui\Settings.UI.UnitTests\Settings.UI.UnitTests.csproj /p:Platform=x64 /p:Configuration=Debug /t:Build

# Run Settings.UI.UnitTests (filter to Macro tests):
vstest.console x64\Debug\tests\SettingsTests\Settings.UI.UnitTests.exe /TestCaseFilter:"FullyQualifiedName~Macro"

# Build full Settings.UI (for XAML tasks):
MSBuild src\settings-ui\Settings.UI\PowerToys.Settings.csproj /p:Platform=x64 /p:Configuration=Debug /t:Build
```

---

## Task 1: Add `IsEnabled` to `MacroDefinition` and engine

**Files:**
- Modify: `src/modules/Macro/MacroCommon/Models/MacroDefinition.cs`
- Modify: `src/modules/Macro/MacroEngine/MacroEngineHost.cs`
- Modify: `src/modules/Macro/MacroCommon.Tests/Serialization/MacroSerializerTests.cs`

- [ ] **Step 1: Write the failing test**

Add to `MacroSerializerTests.cs` (inside the `MacroSerializerTests` class):

```csharp
[TestMethod]
public void IsEnabled_DefaultsToTrue()
{
    var macro = new MacroDefinition { Name = "T" };
    Assert.IsTrue(macro.IsEnabled);
}

[TestMethod]
public void IsEnabled_False_RoundTrips()
{
    var macro = new MacroDefinition { Name = "T", IsEnabled = false };
    var json = MacroSerializer.Serialize(macro);
    var restored = MacroSerializer.Deserialize(json);
    Assert.IsFalse(restored.IsEnabled);
}

[TestMethod]
public void IsEnabled_True_RoundTrips()
{
    var macro = new MacroDefinition { Name = "T", IsEnabled = true };
    var json = MacroSerializer.Serialize(macro);
    var restored = MacroSerializer.Deserialize(json);
    Assert.IsTrue(restored.IsEnabled);
}
```

- [ ] **Step 2: Run tests to verify they fail**

```powershell
dotnet test src\modules\Macro\MacroCommon.Tests\MacroCommon.Tests.csproj -p:Platform=x64 --filter "FullyQualifiedName~IsEnabled"
```

Expected: FAIL — `MacroDefinition` has no `IsEnabled` property.

- [ ] **Step 3: Add `IsEnabled` to `MacroDefinition`**

Current file at `src/modules/Macro/MacroCommon/Models/MacroDefinition.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerToys.MacroCommon.Models;

public sealed record MacroDefinition
{
    public string Id { get; init; } = Guid.NewGuid().ToString();

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string? Hotkey { get; init; }

    public string? AppScope { get; init; }

    public bool IsEnabled { get; init; } = true;

    public List<MacroStep> Steps { get; init; } = [];
}
```

- [ ] **Step 4: Run tests to verify they pass**

```powershell
dotnet test src\modules\Macro\MacroCommon.Tests\MacroCommon.Tests.csproj -p:Platform=x64 --filter "FullyQualifiedName~IsEnabled"
```

Expected: PASS (3 tests).

- [ ] **Step 5: Update engine to skip disabled macros**

In `src/modules/Macro/MacroEngine/MacroEngineHost.cs`, change `RegisterAllHotkeys`:

```csharp
private void RegisterAllHotkeys()
{
    foreach (var macro in _loader.Macros.Values)
    {
        if (macro.Hotkey is not null && macro.IsEnabled)
        {
            try
            {
                _hotkeyManager.RegisterHotkey(macro.Hotkey, macro.Id);
            }
            catch (InvalidOperationException ex)
            {
                Logger.LogWarning($"MacroEngine: Hotkey conflict for '{macro.Name}': {ex.Message}");
            }
        }
    }
}
```

- [ ] **Step 6: Run all MacroCommon + MacroEngine tests**

```powershell
dotnet test src\modules\Macro\MacroCommon.Tests\MacroCommon.Tests.csproj -p:Platform=x64
dotnet test src\modules\Macro\MacroEngine.Tests\MacroEngine.Tests.csproj -p:Platform=x64
```

Expected: All pass (no regressions).

- [ ] **Step 7: Commit**

```bash
git add src/modules/Macro/MacroCommon/Models/MacroDefinition.cs
git add src/modules/Macro/MacroEngine/MacroEngineHost.cs
git add src/modules/Macro/MacroCommon.Tests/Serialization/MacroSerializerTests.cs
git commit -m "feat(macro): add IsEnabled to MacroDefinition, engine skips disabled macros"
```

---

## Task 2: Add project reference and resource strings

**Files:**
- Modify: `src/settings-ui/Settings.UI/PowerToys.Settings.csproj`
- Modify: `src/settings-ui/Settings.UI/Strings/en-us/Resources.resw`

- [ ] **Step 1: Add MacroCommon project reference**

In `src/settings-ui/Settings.UI/PowerToys.Settings.csproj`, add inside the existing `<ItemGroup>` that contains `<ProjectReference>` elements:

```xml
<ProjectReference Include="..\..\modules\Macro\MacroCommon\MacroCommon.csproj" />
```

The full ItemGroup already ends with:
```xml
    <ProjectReference Include="..\Settings.UI.Controls\Settings.UI.Controls.csproj" />
</ItemGroup>
```

Add the MacroCommon reference before the closing `</ItemGroup>`:
```xml
    <ProjectReference Include="..\Settings.UI.Controls\Settings.UI.Controls.csproj" />
    <ProjectReference Include="..\..\modules\Macro\MacroCommon\MacroCommon.csproj" />
</ItemGroup>
```

- [ ] **Step 2: Add resource strings**

In `src/settings-ui/Settings.UI/Strings/en-us/Resources.resw`, add after the last `Shell_*` entry (find `Shell_ZoomIt.Content`, add after it):

```xml
  <data name="Shell_Macro.Content" xml:space="preserve">
    <value>Macro</value>
    <comment>Product name: Navigation view item name for Macro</comment>
  </data>
  <data name="MacroPage_Header.Title" xml:space="preserve">
    <value>Macro</value>
    <comment>Settings page header title for Macro module</comment>
  </data>
  <data name="MacroPage_Header.Description" xml:space="preserve">
    <value>Create and manage keyboard macros with multiple steps</value>
    <comment>Settings page header description for Macro module</comment>
  </data>
  <data name="Macro_NewMacroButton.Content" xml:space="preserve">
    <value>New macro</value>
    <comment>Button label to create a new macro</comment>
  </data>
  <data name="Macro_MacrosGroup.Header" xml:space="preserve">
    <value>Macros</value>
    <comment>Settings group header listing all macros</comment>
  </data>
  <data name="MacroEdit_NameLabel.Header" xml:space="preserve">
    <value>Name</value>
    <comment>Label for macro name field in edit dialog</comment>
  </data>
  <data name="MacroEdit_HotkeyLabel.Header" xml:space="preserve">
    <value>Hotkey</value>
    <comment>Label for macro hotkey field in edit dialog</comment>
  </data>
  <data name="MacroEdit_AppScopeLabel.Header" xml:space="preserve">
    <value>App scope (process name)</value>
    <comment>Label for app scope field in edit dialog. Leave blank for global.</comment>
  </data>
  <data name="MacroEdit_StepsGroup.Header" xml:space="preserve">
    <value>Steps</value>
    <comment>Header for the steps section in macro edit dialog</comment>
  </data>
  <data name="MacroEdit_AddStep.Content" xml:space="preserve">
    <value>+ Add step</value>
    <comment>Button label to add a step to a macro</comment>
  </data>
  <data name="MacroEdit_AddSubStep.Content" xml:space="preserve">
    <value>+ Add sub-step</value>
    <comment>Button label to add a sub-step inside a Repeat step</comment>
  </data>
```

- [ ] **Step 3: Verify project builds**

```powershell
MSBuild src\settings-ui\Settings.UI\PowerToys.Settings.csproj /p:Platform=x64 /p:Configuration=Debug /t:Restore
MSBuild src\settings-ui\Settings.UI\PowerToys.Settings.csproj /p:Platform=x64 /p:Configuration=Debug /t:Build
```

Expected: Build succeeds (MacroCommon types now available in Settings.UI).

- [ ] **Step 4: Commit**

```bash
git add src/settings-ui/Settings.UI/PowerToys.Settings.csproj
git add "src/settings-ui/Settings.UI/Strings/en-us/Resources.resw"
git commit -m "feat(macro-ui): add MacroCommon project reference and resource strings"
```

---

## Task 3: `MacroHotkeyConverter` with tests

**Files:**
- Create: `src/settings-ui/Settings.UI.UnitTests/ViewModelTests/MacroHotkeyConverterTests.cs`
- Create: `src/settings-ui/Settings.UI/ViewModels/MacroHotkeyConverter.cs`

- [ ] **Step 1: Write the failing tests**

Create `src/settings-ui/Settings.UI.UnitTests/ViewModelTests/MacroHotkeyConverterTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests;

[TestClass]
public sealed class MacroHotkeyConverterTests
{
    [TestMethod]
    public void ToHotkeySettings_Null_ReturnsEmpty()
    {
        var hs = MacroHotkeyConverter.ToHotkeySettings(null);
        Assert.AreEqual(0, hs.Code);
        Assert.IsFalse(hs.Ctrl);
    }

    [TestMethod]
    public void ToHotkeySettings_Empty_ReturnsEmpty()
    {
        var hs = MacroHotkeyConverter.ToHotkeySettings(string.Empty);
        Assert.AreEqual(0, hs.Code);
    }

    [TestMethod]
    public void ToHotkeySettings_CtrlF12_Correct()
    {
        var hs = MacroHotkeyConverter.ToHotkeySettings("Ctrl+F12");
        Assert.IsTrue(hs.Ctrl);
        Assert.IsFalse(hs.Shift);
        Assert.IsFalse(hs.Alt);
        Assert.IsFalse(hs.Win);
        Assert.AreEqual(0x7B, hs.Code); // F12
    }

    [TestMethod]
    public void ToHotkeySettings_CtrlShiftV_Correct()
    {
        var hs = MacroHotkeyConverter.ToHotkeySettings("Ctrl+Shift+V");
        Assert.IsTrue(hs.Ctrl);
        Assert.IsTrue(hs.Shift);
        Assert.IsFalse(hs.Alt);
        Assert.AreEqual((int)'V', hs.Code);
    }

    [TestMethod]
    public void ToHotkeySettings_WinAltG_Correct()
    {
        var hs = MacroHotkeyConverter.ToHotkeySettings("Win+Alt+G");
        Assert.IsTrue(hs.Win);
        Assert.IsTrue(hs.Alt);
        Assert.IsFalse(hs.Ctrl);
        Assert.AreEqual((int)'G', hs.Code);
    }

    [TestMethod]
    public void FromHotkeySettings_Null_ReturnsNull()
    {
        Assert.IsNull(MacroHotkeyConverter.FromHotkeySettings(null));
    }

    [TestMethod]
    public void FromHotkeySettings_NoCode_ReturnsNull()
    {
        var hs = new HotkeySettings(false, true, false, false, 0);
        Assert.IsNull(MacroHotkeyConverter.FromHotkeySettings(hs));
    }

    [TestMethod]
    public void FromHotkeySettings_CtrlF12_Correct()
    {
        var hs = new HotkeySettings(false, true, false, false, 0x7B);
        Assert.AreEqual("Ctrl+F12", MacroHotkeyConverter.FromHotkeySettings(hs));
    }

    [TestMethod]
    public void RoundTrip_CtrlShiftF5()
    {
        const string original = "Ctrl+Shift+F5";
        var hs = MacroHotkeyConverter.ToHotkeySettings(original);
        var result = MacroHotkeyConverter.FromHotkeySettings(hs);
        Assert.AreEqual(original, result);
    }

    [TestMethod]
    public void RoundTrip_WinAltG()
    {
        const string original = "Win+Alt+G";
        var hs = MacroHotkeyConverter.ToHotkeySettings(original);
        var result = MacroHotkeyConverter.FromHotkeySettings(hs);
        Assert.AreEqual(original, result);
    }

    [TestMethod]
    public void ToHotkeySettings_CaseInsensitive_Modifiers()
    {
        var hs = MacroHotkeyConverter.ToHotkeySettings("ctrl+shift+F1");
        Assert.IsTrue(hs.Ctrl);
        Assert.IsTrue(hs.Shift);
        Assert.AreEqual(0x70, hs.Code); // F1
    }
}
```

- [ ] **Step 2: Build tests to verify they fail**

```powershell
MSBuild src\settings-ui\Settings.UI.UnitTests\Settings.UI.UnitTests.csproj /p:Platform=x64 /p:Configuration=Debug /t:Build
```

Expected: Build error — `MacroHotkeyConverter` does not exist.

- [ ] **Step 3: Implement `MacroHotkeyConverter`**

Create `src/settings-ui/Settings.UI/ViewModels/MacroHotkeyConverter.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

internal static class MacroHotkeyConverter
{
    private static readonly Dictionary<string, int> s_nameToVk;
    private static readonly Dictionary<int, string> s_vkToName;

    static MacroHotkeyConverter()
    {
        s_nameToVk = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Enter"] = 0x0D,
            ["Tab"] = 0x09,
            ["Space"] = 0x20,
            ["Backspace"] = 0x08,
            ["Escape"] = 0x1B,
            ["Esc"] = 0x1B,
            ["Delete"] = 0x2E,
            ["Del"] = 0x2E,
            ["Insert"] = 0x2D,
            ["Ins"] = 0x2D,
            ["Home"] = 0x24,
            ["End"] = 0x23,
            ["PageUp"] = 0x21,
            ["PageDown"] = 0x22,
            ["Left"] = 0x25,
            ["Right"] = 0x27,
            ["Up"] = 0x26,
            ["Down"] = 0x28,
            ["F1"] = 0x70,
            ["F2"] = 0x71,
            ["F3"] = 0x72,
            ["F4"] = 0x73,
            ["F5"] = 0x74,
            ["F6"] = 0x75,
            ["F7"] = 0x76,
            ["F8"] = 0x77,
            ["F9"] = 0x78,
            ["F10"] = 0x79,
            ["F11"] = 0x7A,
            ["F12"] = 0x7B,
        };

        for (int i = 0; i < 26; i++)
        {
            s_nameToVk[((char)('A' + i)).ToString()] = 0x41 + i;
            s_nameToVk[((char)('a' + i)).ToString()] = 0x41 + i;
        }

        for (int i = 0; i < 10; i++)
        {
            s_nameToVk[((char)('0' + i)).ToString()] = 0x30 + i;
        }

        s_vkToName = new Dictionary<int, string>();
        foreach (var (name, vk) in s_nameToVk)
        {
            s_vkToName.TryAdd(vk, name);
        }
    }

    public static HotkeySettings ToHotkeySettings(string? hotkey)
    {
        if (string.IsNullOrWhiteSpace(hotkey))
        {
            return new HotkeySettings();
        }

        var parts = hotkey.Split('+');
        if (parts.Length == 0)
        {
            return new HotkeySettings();
        }

        var mods = parts[..^1];
        var key = parts[^1].Trim();

        bool win = mods.Any(m => m.Trim().Equals("Win", StringComparison.OrdinalIgnoreCase));
        bool ctrl = mods.Any(m => m.Trim().Equals("Ctrl", StringComparison.OrdinalIgnoreCase));
        bool alt = mods.Any(m => m.Trim().Equals("Alt", StringComparison.OrdinalIgnoreCase));
        bool shift = mods.Any(m => m.Trim().Equals("Shift", StringComparison.OrdinalIgnoreCase));
        int code = s_nameToVk.TryGetValue(key, out int vk) ? vk : 0;

        return new HotkeySettings(win, ctrl, alt, shift, code);
    }

    public static string? FromHotkeySettings(HotkeySettings? hs)
    {
        if (hs is null || hs.Code == 0)
        {
            return null;
        }

        if (!s_vkToName.TryGetValue(hs.Code, out string? keyName))
        {
            return null;
        }

        var parts = new List<string>();
        if (hs.Win)
        {
            parts.Add("Win");
        }

        if (hs.Ctrl)
        {
            parts.Add("Ctrl");
        }

        if (hs.Alt)
        {
            parts.Add("Alt");
        }

        if (hs.Shift)
        {
            parts.Add("Shift");
        }

        parts.Add(keyName);
        return string.Join("+", parts);
    }
}
```

- [ ] **Step 4: Build and run tests**

```powershell
MSBuild src\settings-ui\Settings.UI.UnitTests\Settings.UI.UnitTests.csproj /p:Platform=x64 /p:Configuration=Debug /t:Build
vstest.console x64\Debug\tests\SettingsTests\Settings.UI.UnitTests.exe /TestCaseFilter:"FullyQualifiedName~MacroHotkeyConverter"
```

Expected: 11 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/settings-ui/Settings.UI/ViewModels/MacroHotkeyConverter.cs
git add src/settings-ui/Settings.UI.UnitTests/ViewModelTests/MacroHotkeyConverterTests.cs
git commit -m "feat(macro-ui): add MacroHotkeyConverter with tests"
```

---

## Task 4: `MacroStepViewModel` with tests

**Files:**
- Create: `src/settings-ui/Settings.UI.UnitTests/ViewModelTests/MacroStepViewModelTests.cs`
- Create: `src/settings-ui/Settings.UI/ViewModels/MacroStepViewModel.cs`

- [ ] **Step 1: Write the failing tests**

Create `src/settings-ui/Settings.UI.UnitTests/ViewModelTests/MacroStepViewModelTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.MacroCommon.Models;

namespace ViewModelTests;

[TestClass]
public sealed class MacroStepViewModelTests
{
    [TestMethod]
    public void FromModel_PressKey_RoundTrip()
    {
        var step = new MacroStep { Type = StepType.PressKey, Key = "Ctrl+C" };
        var vm = MacroStepViewModel.FromModel(step);
        var result = vm.ToModel();
        Assert.AreEqual(StepType.PressKey, result.Type);
        Assert.AreEqual("Ctrl+C", result.Key);
    }

    [TestMethod]
    public void FromModel_TypeText_RoundTrip()
    {
        var step = new MacroStep { Type = StepType.TypeText, Text = "Hello" };
        var vm = MacroStepViewModel.FromModel(step);
        var result = vm.ToModel();
        Assert.AreEqual(StepType.TypeText, result.Type);
        Assert.AreEqual("Hello", result.Text);
    }

    [TestMethod]
    public void FromModel_Wait_RoundTrip()
    {
        var step = new MacroStep { Type = StepType.Wait, Ms = 500 };
        var vm = MacroStepViewModel.FromModel(step);
        var result = vm.ToModel();
        Assert.AreEqual(StepType.Wait, result.Type);
        Assert.AreEqual(500, result.Ms);
    }

    [TestMethod]
    public void FromModel_Repeat_RoundTrip()
    {
        var step = new MacroStep
        {
            Type = StepType.Repeat,
            Count = 3,
            Steps = [new MacroStep { Type = StepType.PressKey, Key = "Tab" }],
        };
        var vm = MacroStepViewModel.FromModel(step);
        Assert.AreEqual(1, vm.SubSteps.Count);
        Assert.AreEqual(StepType.PressKey, vm.SubSteps[0].Type);
        var result = vm.ToModel();
        Assert.AreEqual(StepType.Repeat, result.Type);
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(1, result.Steps?.Count);
        Assert.AreEqual("Tab", result.Steps![0].Key);
    }

    [TestMethod]
    public void FromModel_NestedRepeat_RoundTrip()
    {
        var step = new MacroStep
        {
            Type = StepType.Repeat,
            Count = 2,
            Steps =
            [
                new MacroStep
                {
                    Type = StepType.Repeat,
                    Count = 3,
                    Steps = [new MacroStep { Type = StepType.TypeText, Text = "x" }],
                },
            ],
        };
        var vm = MacroStepViewModel.FromModel(step);
        Assert.AreEqual(1, vm.SubSteps.Count);
        Assert.AreEqual(StepType.Repeat, vm.SubSteps[0].Type);
        Assert.AreEqual(1, vm.SubSteps[0].SubSteps.Count);

        var result = vm.ToModel();
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(1, result.Steps!.Count);
        Assert.AreEqual(3, result.Steps[0].Count);
        Assert.AreEqual("x", result.Steps[0].Steps![0].Text);
    }

    [TestMethod]
    public void MsDouble_GetSet_Syncs()
    {
        var vm = new MacroStepViewModel { Type = StepType.Wait, Ms = 100 };
        Assert.AreEqual(100.0, vm.MsDouble);
        vm.MsDouble = 200.0;
        Assert.AreEqual(200, vm.Ms);
    }

    [TestMethod]
    public void CountDouble_GetSet_Syncs()
    {
        var vm = new MacroStepViewModel { Type = StepType.Repeat, Count = 5 };
        Assert.AreEqual(5.0, vm.CountDouble);
        vm.CountDouble = 3.0;
        Assert.AreEqual(3, vm.Count);
    }

    [TestMethod]
    public void IsPressKey_OnlyTrueForPressKey()
    {
        Assert.IsTrue(new MacroStepViewModel { Type = StepType.PressKey }.IsPressKey);
        Assert.IsFalse(new MacroStepViewModel { Type = StepType.TypeText }.IsPressKey);
    }

    [TestMethod]
    public void IsRepeat_OnlyTrueForRepeat()
    {
        Assert.IsTrue(new MacroStepViewModel { Type = StepType.Repeat }.IsRepeat);
        Assert.IsFalse(new MacroStepViewModel { Type = StepType.Wait }.IsRepeat);
    }

    [TestMethod]
    public void ToModel_NonRepeat_NullSubSteps()
    {
        var vm = new MacroStepViewModel { Type = StepType.PressKey, Key = "A" };
        var model = vm.ToModel();
        Assert.IsNull(model.Steps);
    }

    [TestMethod]
    public void ToModel_RepeatWithEmptySubSteps_NullSteps()
    {
        var vm = new MacroStepViewModel { Type = StepType.Repeat, Count = 2 };
        var model = vm.ToModel();
        Assert.IsNull(model.Steps);
    }
}
```

- [ ] **Step 2: Build tests to verify they fail**

```powershell
MSBuild src\settings-ui\Settings.UI.UnitTests\Settings.UI.UnitTests.csproj /p:Platform=x64 /p:Configuration=Debug /t:Build
```

Expected: Build error — `MacroStepViewModel` does not exist.

- [ ] **Step 3: Implement `MacroStepViewModel`**

Create `src/settings-ui/Settings.UI/ViewModels/MacroStepViewModel.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using PowerToys.MacroCommon.Models;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

public sealed class MacroStepViewModel : Observable
{
    private StepType _type;
    private string? _key;
    private string? _text;
    private int? _ms;
    private int? _count;

    public StepType Type
    {
        get => _type;
        set
        {
            if (Set(ref _type, value))
            {
                OnPropertyChanged(nameof(TypeLabel));
                OnPropertyChanged(nameof(IsPressKey));
                OnPropertyChanged(nameof(IsTypeText));
                OnPropertyChanged(nameof(IsWait));
                OnPropertyChanged(nameof(IsRepeat));
            }
        }
    }

    public string? Key
    {
        get => _key;
        set => Set(ref _key, value);
    }

    public string? Text
    {
        get => _text;
        set => Set(ref _text, value);
    }

    public int? Ms
    {
        get => _ms;
        set => Set(ref _ms, value);
    }

    public int? Count
    {
        get => _count;
        set => Set(ref _count, value);
    }

    public ObservableCollection<MacroStepViewModel> SubSteps { get; } = [];

    public double MsDouble
    {
        get => _ms ?? 0;
        set
        {
            Ms = (int)value;
            OnPropertyChanged();
        }
    }

    public double CountDouble
    {
        get => _count ?? 1;
        set
        {
            Count = Math.Max(1, (int)value);
            OnPropertyChanged();
        }
    }

    public string TypeLabel => Type switch
    {
        StepType.PressKey => "Key",
        StepType.TypeText => "Text",
        StepType.Wait => "Wait (ms)",
        StepType.Repeat => "Repeat",
        _ => Type.ToString(),
    };

    public bool IsPressKey => Type == StepType.PressKey;

    public bool IsTypeText => Type == StepType.TypeText;

    public bool IsWait => Type == StepType.Wait;

    public bool IsRepeat => Type == StepType.Repeat;

    public static MacroStepViewModel FromModel(MacroStep step)
    {
        var vm = new MacroStepViewModel
        {
            Type = step.Type,
            Key = step.Key,
            Text = step.Text,
            Ms = step.Ms,
            Count = step.Count,
        };

        if (step.Steps != null)
        {
            foreach (var sub in step.Steps)
            {
                vm.SubSteps.Add(FromModel(sub));
            }
        }

        return vm;
    }

    public MacroStep ToModel() => new()
    {
        Type = Type,
        Key = Key,
        Text = Text,
        Ms = Ms,
        Count = Count,
        Steps = SubSteps.Count > 0 ? [.. SubSteps.Select(s => s.ToModel())] : null,
    };
}
```

- [ ] **Step 4: Build and run tests**

```powershell
MSBuild src\settings-ui\Settings.UI.UnitTests\Settings.UI.UnitTests.csproj /p:Platform=x64 /p:Configuration=Debug /t:Build
vstest.console x64\Debug\tests\SettingsTests\Settings.UI.UnitTests.exe /TestCaseFilter:"FullyQualifiedName~MacroStepViewModel"
```

Expected: 11 tests pass.

- [ ] **Step 5: Commit**

```bash
git add src/settings-ui/Settings.UI/ViewModels/MacroStepViewModel.cs
git add src/settings-ui/Settings.UI.UnitTests/ViewModelTests/MacroStepViewModelTests.cs
git commit -m "feat(macro-ui): add MacroStepViewModel with recursive step tree"
```

---

## Task 5: `MacroListItem` and `MacroViewModel` with tests

**Files:**
- Create: `src/settings-ui/Settings.UI/ViewModels/MacroListItem.cs`
- Create: `src/settings-ui/Settings.UI/ViewModels/MacroViewModel.cs`
- Create: `src/settings-ui/Settings.UI.UnitTests/ViewModelTests/MacroViewModelTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `src/settings-ui/Settings.UI.UnitTests/ViewModelTests/MacroViewModelTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.MacroCommon.Models;
using PowerToys.MacroCommon.Serialization;

namespace ViewModelTests;

[TestClass]
public sealed class MacroViewModelTests
{
    private string _tempDir = null!;

    [TestInitialize]
    public void Init()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    private void WriteMacro(MacroDefinition def)
    {
        File.WriteAllText(
            Path.Combine(_tempDir, $"{def.Id}.json"),
            MacroSerializer.Serialize(def));
    }

    [TestMethod]
    public void Constructor_LoadsMacrosFromDirectory()
    {
        WriteMacro(new MacroDefinition { Id = "a", Name = "Alpha" });
        WriteMacro(new MacroDefinition { Id = "b", Name = "Beta" });

        var vm = new MacroViewModel(_tempDir);

        Assert.AreEqual(2, vm.Macros.Count);
        Assert.IsTrue(vm.Macros.Any(m => m.Name == "Alpha"));
        Assert.IsTrue(vm.Macros.Any(m => m.Name == "Beta"));
    }

    [TestMethod]
    public void Constructor_EmptyDirectory_EmptyCollection()
    {
        var vm = new MacroViewModel(_tempDir);
        Assert.AreEqual(0, vm.Macros.Count);
    }

    [TestMethod]
    public void Constructor_MalformedFile_Skipped()
    {
        File.WriteAllText(Path.Combine(_tempDir, "bad.json"), "{ not valid json");
        WriteMacro(new MacroDefinition { Id = "ok", Name = "Good" });

        var vm = new MacroViewModel(_tempDir);

        Assert.AreEqual(1, vm.Macros.Count);
        Assert.AreEqual("Good", vm.Macros[0].Name);
    }

    [TestMethod]
    public async Task SaveMacroAsync_WritesJsonFile()
    {
        var vm = new MacroViewModel(_tempDir);
        var editVm = new MacroEditViewModel(new MacroDefinition { Id = "new-1", Name = "My Macro" });

        await vm.SaveMacroAsync(editVm);

        var expectedPath = Path.Combine(_tempDir, "new-1.json");
        Assert.IsTrue(File.Exists(expectedPath));
        var json = File.ReadAllText(expectedPath);
        var restored = MacroSerializer.Deserialize(json);
        Assert.AreEqual("My Macro", restored.Name);
    }

    [TestMethod]
    public void DeleteMacro_RemovesFileAndItem()
    {
        var def = new MacroDefinition { Id = "del-1", Name = "ToDelete" };
        WriteMacro(def);

        var vm = new MacroViewModel(_tempDir);
        Assert.AreEqual(1, vm.Macros.Count);

        vm.DeleteMacro(vm.Macros[0]);

        Assert.AreEqual(0, vm.Macros.Count);
        Assert.IsFalse(File.Exists(Path.Combine(_tempDir, "del-1.json")));
    }

    [TestMethod]
    public void MacroListItem_IsEnabled_Toggle_WritesJson()
    {
        var def = new MacroDefinition { Id = "en-1", Name = "Toggleable", IsEnabled = true };
        WriteMacro(def);

        var vm = new MacroViewModel(_tempDir);
        var item = vm.Macros[0];
        Assert.IsTrue(item.IsEnabled);

        item.IsEnabled = false;

        var json = File.ReadAllText(Path.Combine(_tempDir, "en-1.json"));
        var restored = MacroSerializer.Deserialize(json);
        Assert.IsFalse(restored.IsEnabled);
    }
}
```

- [ ] **Step 2: Build tests to verify they fail**

```powershell
MSBuild src\settings-ui\Settings.UI.UnitTests\Settings.UI.UnitTests.csproj /p:Platform=x64 /p:Configuration=Debug /t:Build
```

Expected: Build error — `MacroListItem`, `MacroViewModel`, `MacroEditViewModel` do not exist yet.

- [ ] **Step 3: Implement `MacroListItem`**

Create `src/settings-ui/Settings.UI/ViewModels/MacroListItem.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using PowerToys.MacroCommon.Models;
using PowerToys.MacroCommon.Serialization;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

public sealed class MacroListItem : Observable
{
    private MacroDefinition _definition;
    private bool _isEnabled;

    public MacroListItem(MacroDefinition definition, string filePath)
    {
        _definition = definition;
        _isEnabled = definition.IsEnabled;
        FilePath = filePath;
    }

    public MacroDefinition Definition => _definition;

    public string FilePath { get; }

    public string Name => _definition.Name;

    public string? Hotkey => _definition.Hotkey;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (Set(ref _isEnabled, value))
            {
                _definition = _definition with { IsEnabled = value };
                File.WriteAllText(FilePath, MacroSerializer.Serialize(_definition));
            }
        }
    }

    public bool HasHotkey => !string.IsNullOrEmpty(_definition.Hotkey);
}
```

- [ ] **Step 4: Implement `MacroViewModel`**

Create `src/settings-ui/Settings.UI/ViewModels/MacroViewModel.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.IO.Pipes;
using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using PowerToys.MacroCommon.Ipc;
using PowerToys.MacroCommon.Serialization;
using StreamJsonRpc;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

public sealed class MacroViewModel : Observable, IDisposable
{
    private readonly string _macrosDirectory;
    private readonly FileSystemWatcher _watcher;
    private readonly System.Timers.Timer _debounce;
    private readonly DispatcherQueue? _dispatcherQueue;
    private JsonRpc? _rpc;
    private NamedPipeClientStream? _pipe;
    private bool _disposed;

    public ObservableCollection<MacroListItem> Macros { get; } = [];

    public MacroViewModel()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft",
            "PowerToys",
            "Macros"))
    {
    }

    internal MacroViewModel(string macrosDirectory)
    {
        _macrosDirectory = macrosDirectory;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        Directory.CreateDirectory(_macrosDirectory);
        LoadMacros();

        _debounce = new System.Timers.Timer(300) { AutoReset = false };
        _debounce.Elapsed += (_, _) =>
        {
            _dispatcherQueue?.TryEnqueue(LoadMacros);
        };

        _watcher = new FileSystemWatcher(_macrosDirectory, "*.json")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
            EnableRaisingEvents = true,
        };
        _watcher.Changed += OnFileChanged;
        _watcher.Created += OnFileChanged;
        _watcher.Deleted += OnFileChanged;
        _watcher.Renamed += (s, e) => OnFileChanged(s, e);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        _debounce.Stop();
        _debounce.Start();
    }

    private void LoadMacros()
    {
        var files = Directory.Exists(_macrosDirectory)
            ? Directory.GetFiles(_macrosDirectory, "*.json")
            : [];

        Macros.Clear();
        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var def = MacroSerializer.Deserialize(json);
                Macros.Add(new MacroListItem(def, file));
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Macro settings: skipping malformed {Path.GetFileName(file)}: {ex.Message}");
            }
        }
    }

    public async Task SuspendHotkeysAsync()
    {
        await EnsureRpcAsync();
        if (_rpc is null)
        {
            return;
        }

        try
        {
            await _rpc.InvokeWithCancellationAsync<object>(
                nameof(IMacroEngineRpc.SuspendHotkeysAsync),
                [],
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Macro settings: SuspendHotkeys IPC failed: {ex.Message}");
            _rpc = null;
            _pipe?.Dispose();
            _pipe = null;
        }
    }

    public async Task ResumeHotkeysAsync()
    {
        if (_rpc is null)
        {
            return;
        }

        try
        {
            await _rpc.InvokeWithCancellationAsync<object>(
                nameof(IMacroEngineRpc.ResumeHotkeysAsync),
                [],
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Macro settings: ResumeHotkeys IPC failed: {ex.Message}");
            _rpc = null;
            _pipe?.Dispose();
            _pipe = null;
        }
    }

    public async Task SaveMacroAsync(MacroEditViewModel editVm)
    {
        var def = editVm.ToDefinition();
        var path = Path.Combine(_macrosDirectory, $"{def.Id}.json");
        await MacroSerializer.SerializeFileAsync(def, path);
    }

    public void DeleteMacro(MacroListItem item)
    {
        try
        {
            File.Delete(item.FilePath);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Macro settings: delete failed for {item.FilePath}: {ex.Message}");
        }

        Macros.Remove(item);
    }

    private async Task EnsureRpcAsync()
    {
        if (_rpc != null)
        {
            return;
        }

        try
        {
            var pipe = new NamedPipeClientStream(
                ".",
                MacroIpcConstants.PipeName,
                PipeDirection.InOut,
                PipeOptions.Asynchronous);
            await pipe.ConnectAsync(500, CancellationToken.None);
            _pipe = pipe;
            _rpc = JsonRpc.Attach(pipe);
        }
        catch
        {
            _pipe?.Dispose();
            _pipe = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _watcher.Dispose();
        _debounce.Dispose();
        _rpc?.Dispose();
        _pipe?.Dispose();
    }
}
```

- [ ] **Step 5: Build and run tests**

```powershell
MSBuild src\settings-ui\Settings.UI.UnitTests\Settings.UI.UnitTests.csproj /p:Platform=x64 /p:Configuration=Debug /t:Build
vstest.console x64\Debug\tests\SettingsTests\Settings.UI.UnitTests.exe /TestCaseFilter:"FullyQualifiedName~MacroViewModel"
```

Expected: Tests referencing `MacroEditViewModel` still fail. That is expected — `MacroEditViewModel` is created in the next task. Only `MacroListItem` and `MacroViewModel` constructor/delete tests will pass. Proceed to Task 6 before marking tests complete.

- [ ] **Step 6: Commit**

```bash
git add src/settings-ui/Settings.UI/ViewModels/MacroListItem.cs
git add src/settings-ui/Settings.UI/ViewModels/MacroViewModel.cs
git add src/settings-ui/Settings.UI.UnitTests/ViewModelTests/MacroViewModelTests.cs
git commit -m "feat(macro-ui): add MacroListItem and MacroViewModel"
```

---

## Task 6: `MacroEditViewModel` with tests

**Files:**
- Create: `src/settings-ui/Settings.UI.UnitTests/ViewModelTests/MacroEditViewModelTests.cs`
- Create: `src/settings-ui/Settings.UI/ViewModels/MacroEditViewModel.cs`

- [ ] **Step 1: Write the failing tests**

Create `src/settings-ui/Settings.UI.UnitTests/ViewModelTests/MacroEditViewModelTests.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerToys.MacroCommon.Models;

namespace ViewModelTests;

[TestClass]
public sealed class MacroEditViewModelTests
{
    [TestMethod]
    public void Constructor_Default_HasEmptyName()
    {
        var vm = new MacroEditViewModel();
        Assert.AreEqual(string.Empty, vm.Name);
        Assert.IsTrue(vm.HasValidationError);
    }

    [TestMethod]
    public void Constructor_WithDefinition_PopulatesFields()
    {
        var def = new MacroDefinition
        {
            Id = "abc",
            Name = "My Macro",
            Hotkey = "Ctrl+F9",
            AppScope = "notepad.exe",
        };
        var vm = new MacroEditViewModel(def);

        Assert.AreEqual("My Macro", vm.Name);
        Assert.AreEqual("notepad.exe", vm.AppScope);
        Assert.IsTrue(vm.Hotkey.Ctrl);
        Assert.AreEqual(0x78, vm.Hotkey.Code); // F9
    }

    [TestMethod]
    public void HasValidationError_FalseWhenNameSet()
    {
        var vm = new MacroEditViewModel { Name = "My Macro" };
        Assert.IsFalse(vm.HasValidationError);
    }

    [TestMethod]
    public void HasValidationError_TrueWhenNameWhitespace()
    {
        var vm = new MacroEditViewModel { Name = "   " };
        Assert.IsTrue(vm.HasValidationError);
    }

    [TestMethod]
    public void ToDefinition_PreservesOriginalId()
    {
        var def = new MacroDefinition { Id = "keep-me", Name = "X" };
        var vm = new MacroEditViewModel(def) { Name = "Updated" };
        var result = vm.ToDefinition();
        Assert.AreEqual("keep-me", result.Id);
        Assert.AreEqual("Updated", result.Name);
    }

    [TestMethod]
    public void ToDefinition_HotkeyRoundTrip()
    {
        var def = new MacroDefinition { Name = "X", Hotkey = "Ctrl+Shift+F5" };
        var vm = new MacroEditViewModel(def);
        var result = vm.ToDefinition();
        Assert.AreEqual("Ctrl+Shift+F5", result.Hotkey);
    }

    [TestMethod]
    public void ToDefinition_AppScopeBlank_SerializesNull()
    {
        var vm = new MacroEditViewModel { Name = "X", AppScope = "   " };
        var result = vm.ToDefinition();
        Assert.IsNull(result.AppScope);
    }

    [TestMethod]
    public void AddStep_AddsToCollection()
    {
        var vm = new MacroEditViewModel { Name = "X" };
        vm.AddStep(StepType.PressKey);
        Assert.AreEqual(1, vm.Steps.Count);
        Assert.AreEqual(StepType.PressKey, vm.Steps[0].Type);
    }

    [TestMethod]
    public void DeleteStep_RemovesFromCollection()
    {
        var vm = new MacroEditViewModel { Name = "X" };
        vm.AddStep(StepType.TypeText);
        var step = vm.Steps[0];
        vm.DeleteStep(step);
        Assert.AreEqual(0, vm.Steps.Count);
    }

    [TestMethod]
    public void AddSubStep_AddsToRepeatSubSteps()
    {
        var vm = new MacroEditViewModel { Name = "X" };
        vm.AddStep(StepType.Repeat);
        var repeat = vm.Steps[0];
        vm.AddSubStep(repeat, StepType.Wait);
        Assert.AreEqual(1, repeat.SubSteps.Count);
        Assert.AreEqual(StepType.Wait, repeat.SubSteps[0].Type);
    }

    [TestMethod]
    public void DeleteSubStep_RemovesFromRepeatSubSteps()
    {
        var vm = new MacroEditViewModel { Name = "X" };
        vm.AddStep(StepType.Repeat);
        var repeat = vm.Steps[0];
        vm.AddSubStep(repeat, StepType.PressKey);
        var sub = repeat.SubSteps[0];
        vm.DeleteSubStep(repeat, sub);
        Assert.AreEqual(0, repeat.SubSteps.Count);
    }

    [TestMethod]
    public void ToDefinition_Steps_RoundTrip()
    {
        var def = new MacroDefinition
        {
            Name = "X",
            Steps =
            [
                new MacroStep { Type = StepType.TypeText, Text = "Hello" },
                new MacroStep
                {
                    Type = StepType.Repeat,
                    Count = 2,
                    Steps = [new MacroStep { Type = StepType.PressKey, Key = "Tab" }],
                },
            ],
        };
        var vm = new MacroEditViewModel(def);
        var result = vm.ToDefinition();

        Assert.AreEqual(2, result.Steps.Count);
        Assert.AreEqual("Hello", result.Steps[0].Text);
        Assert.AreEqual(2, result.Steps[1].Count);
        Assert.AreEqual("Tab", result.Steps[1].Steps![0].Key);
    }
}
```

- [ ] **Step 2: Build tests to verify they fail**

```powershell
MSBuild src\settings-ui\Settings.UI.UnitTests\Settings.UI.UnitTests.csproj /p:Platform=x64 /p:Configuration=Debug /t:Build
```

Expected: Build error — `MacroEditViewModel` does not exist.

- [ ] **Step 3: Implement `MacroEditViewModel`**

Create `src/settings-ui/Settings.UI/ViewModels/MacroEditViewModel.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using PowerToys.MacroCommon.Models;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

public sealed class MacroEditViewModel : Observable
{
    private string _name;
    private HotkeySettings _hotkey;
    private string? _appScope;

    public MacroEditViewModel()
        : this(new MacroDefinition())
    {
    }

    public MacroEditViewModel(MacroDefinition definition)
    {
        OriginalId = definition.Id;
        _name = definition.Name;
        _hotkey = MacroHotkeyConverter.ToHotkeySettings(definition.Hotkey);
        _appScope = definition.AppScope;
        Steps = new ObservableCollection<MacroStepViewModel>(
            definition.Steps.Select(MacroStepViewModel.FromModel));
    }

    public string OriginalId { get; }

    public string Name
    {
        get => _name;
        set
        {
            if (Set(ref _name, value))
            {
                OnPropertyChanged(nameof(HasValidationError));
            }
        }
    }

    public HotkeySettings Hotkey
    {
        get => _hotkey;
        set => Set(ref _hotkey, value);
    }

    public string? AppScope
    {
        get => _appScope;
        set => Set(ref _appScope, value);
    }

    public ObservableCollection<MacroStepViewModel> Steps { get; }

    public bool HasValidationError => string.IsNullOrWhiteSpace(_name);

    public MacroDefinition ToDefinition() => new()
    {
        Id = OriginalId,
        Name = Name.Trim(),
        Hotkey = MacroHotkeyConverter.FromHotkeySettings(Hotkey),
        AppScope = string.IsNullOrWhiteSpace(AppScope) ? null : AppScope!.Trim(),
        Steps = [.. Steps.Select(s => s.ToModel())],
    };

    public void AddStep(StepType type)
    {
        Steps.Add(new MacroStepViewModel { Type = type });
    }

    public void DeleteStep(MacroStepViewModel step)
    {
        Steps.Remove(step);
    }

    public void AddSubStep(MacroStepViewModel parent, StepType type)
    {
        parent.SubSteps.Add(new MacroStepViewModel { Type = type });
    }

    public void DeleteSubStep(MacroStepViewModel parent, MacroStepViewModel child)
    {
        parent.SubSteps.Remove(child);
    }
}
```

- [ ] **Step 4: Build and run all Macro ViewModel tests**

```powershell
MSBuild src\settings-ui\Settings.UI.UnitTests\Settings.UI.UnitTests.csproj /p:Platform=x64 /p:Configuration=Debug /t:Build
vstest.console x64\Debug\tests\SettingsTests\Settings.UI.UnitTests.exe /TestCaseFilter:"FullyQualifiedName~Macro"
```

Expected: All Macro tests pass (MacroHotkeyConverter 11 + MacroStepViewModel 11 + MacroViewModel 5 + MacroEditViewModel 11 = 38 tests).

- [ ] **Step 5: Commit**

```bash
git add src/settings-ui/Settings.UI/ViewModels/MacroEditViewModel.cs
git add src/settings-ui/Settings.UI.UnitTests/ViewModelTests/MacroEditViewModelTests.cs
git commit -m "feat(macro-ui): add MacroEditViewModel with step tree management"
```

---

## Task 7: `MacroEditDialog` XAML and code-behind

**Files:**
- Create: `src/settings-ui/Settings.UI/SettingsXAML/Views/MacroEditDialog.xaml`
- Create: `src/settings-ui/Settings.UI/SettingsXAML/Views/MacroEditDialog.xaml.cs`

- [ ] **Step 1: Create `MacroEditDialog.xaml`**

Create `src/settings-ui/Settings.UI/SettingsXAML/Views/MacroEditDialog.xaml`:

```xml
<!-- Copyright (c) Microsoft Corporation -->
<!-- The Microsoft Corporation licenses this file to you under the MIT license. -->
<!-- See the LICENSE file in the project root for more information. -->

<ContentDialog
    x:Class="Microsoft.PowerToys.Settings.UI.Views.MacroEditDialog"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:controls="using:Microsoft.PowerToys.Settings.UI.Controls"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:local="using:Microsoft.PowerToys.Settings.UI.Views"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:tkcontrols="using:CommunityToolkit.WinUI.Controls"
    xmlns:tkconverters="using:CommunityToolkit.WinUI.Converters"
    xmlns:viewmodels="using:Microsoft.PowerToys.Settings.UI.ViewModels"
    MinWidth="480"
    CloseButtonText="Cancel"
    DefaultButton="Primary"
    PrimaryButtonText="Save"
    mc:Ignorable="d">

    <ContentDialog.Resources>
        <tkconverters:BoolToVisibilityConverter x:Key="BoolToVisibilityConverter" />
    </ContentDialog.Resources>

    <ScrollViewer VerticalScrollBarVisibility="Auto">
        <StackPanel Spacing="16" Padding="0,8,0,8">

            <!-- Name -->
            <tkcontrols:SettingsCard x:Uid="MacroEdit_NameLabel">
                <TextBox
                    x:Name="NameBox"
                    MinWidth="200"
                    Text="{x:Bind ViewModel.Name, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
            </tkcontrols:SettingsCard>

            <!-- Hotkey -->
            <tkcontrols:SettingsCard x:Uid="MacroEdit_HotkeyLabel">
                <controls:ShortcutControl
                    x:Name="HotkeyControl"
                    HotkeySettings="{x:Bind ViewModel.Hotkey, Mode=TwoWay}" />
            </tkcontrols:SettingsCard>

            <!-- App Scope -->
            <tkcontrols:SettingsCard x:Uid="MacroEdit_AppScopeLabel">
                <TextBox
                    MinWidth="200"
                    PlaceholderText="e.g. notepad.exe (leave blank for global)"
                    Text="{x:Bind ViewModel.AppScope, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}" />
            </tkcontrols:SettingsCard>

            <!-- Steps -->
            <StackPanel Spacing="4">
                <TextBlock
                    x:Uid="MacroEdit_StepsGroup"
                    Style="{StaticResource BodyStrongTextBlockStyle}" />

                <!-- Step list -->
                <ListView
                    x:Name="StepsList"
                    CanDragItems="True"
                    CanReorderItems="True"
                    AllowDrop="True"
                    ItemsSource="{x:Bind ViewModel.Steps}"
                    SelectionMode="None">
                    <ListView.ItemContainerStyle>
                        <Style TargetType="ListViewItem">
                            <Setter Property="HorizontalContentAlignment" Value="Stretch" />
                            <Setter Property="Padding" Value="0" />
                        </Style>
                    </ListView.ItemContainerStyle>
                    <ListView.ItemTemplate>
                        <DataTemplate x:DataType="viewmodels:MacroStepViewModel">
                            <Border
                                Padding="8,6"
                                BorderBrush="{ThemeResource DividerStrokeColorDefaultBrush}"
                                BorderThickness="0,0,0,1">
                                <StackPanel Spacing="4">
                                    <!-- Step row -->
                                    <Grid ColumnSpacing="8">
                                        <Grid.ColumnDefinitions>
                                            <ColumnDefinition Width="80" />
                                            <ColumnDefinition Width="*" />
                                            <ColumnDefinition Width="32" />
                                        </Grid.ColumnDefinitions>
                                        <TextBlock
                                            Grid.Column="0"
                                            VerticalAlignment="Center"
                                            Text="{x:Bind TypeLabel, Mode=OneWay}" />
                                        <!-- PressKey input -->
                                        <TextBox
                                            Grid.Column="1"
                                            PlaceholderText="e.g. Ctrl+C"
                                            Text="{x:Bind Key, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                                            Visibility="{x:Bind IsPressKey, Converter={StaticResource BoolToVisibilityConverter}, Mode=OneWay}" />
                                        <!-- TypeText input -->
                                        <TextBox
                                            Grid.Column="1"
                                            PlaceholderText="Text to type"
                                            Text="{x:Bind Text, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                                            Visibility="{x:Bind IsTypeText, Converter={StaticResource BoolToVisibilityConverter}, Mode=OneWay}" />
                                        <!-- Wait input -->
                                        <NumberBox
                                            Grid.Column="1"
                                            Minimum="0"
                                            Maximum="60000"
                                            PlaceholderText="milliseconds"
                                            SpinButtonPlacementMode="Inline"
                                            Value="{x:Bind MsDouble, Mode=TwoWay}"
                                            Visibility="{x:Bind IsWait, Converter={StaticResource BoolToVisibilityConverter}, Mode=OneWay}" />
                                        <!-- Repeat count -->
                                        <NumberBox
                                            Grid.Column="1"
                                            Minimum="1"
                                            Maximum="999"
                                            PlaceholderText="repeat count"
                                            SpinButtonPlacementMode="Inline"
                                            Value="{x:Bind CountDouble, Mode=TwoWay}"
                                            Visibility="{x:Bind IsRepeat, Converter={StaticResource BoolToVisibilityConverter}, Mode=OneWay}" />
                                        <!-- Delete button -->
                                        <Button
                                            Grid.Column="2"
                                            Width="32"
                                            Height="32"
                                            Padding="0"
                                            Content="✕"
                                            Tag="{x:Bind}"
                                            Click="DeleteStep_Click"
                                            ToolTipService.ToolTip="Delete step" />
                                    </Grid>

                                    <!-- Repeat sub-steps (indented, only visible for Repeat type) -->
                                    <Border
                                        Margin="20,0,0,0"
                                        Visibility="{x:Bind IsRepeat, Converter={StaticResource BoolToVisibilityConverter}, Mode=OneWay}">
                                        <StackPanel Spacing="2">
                                            <ListView
                                                x:Name="SubStepsList"
                                                CanDragItems="True"
                                                CanReorderItems="True"
                                                AllowDrop="True"
                                                ItemsSource="{x:Bind SubSteps}"
                                                SelectionMode="None"
                                                Loaded="SubStepsList_Loaded">
                                                <ListView.ItemContainerStyle>
                                                    <Style TargetType="ListViewItem">
                                                        <Setter Property="HorizontalContentAlignment" Value="Stretch" />
                                                        <Setter Property="Padding" Value="0" />
                                                    </Style>
                                                </ListView.ItemContainerStyle>
                                                <!-- ItemTemplate set from code-behind via SubStepsList_Loaded -->
                                            </ListView>
                                            <Button
                                                x:Uid="MacroEdit_AddSubStep"
                                                Tag="{x:Bind}"
                                                Click="AddSubStep_Click" />
                                        </StackPanel>
                                    </Border>
                                </StackPanel>
                            </Border>
                        </DataTemplate>
                    </ListView.ItemTemplate>
                </ListView>

                <!-- Add Step button -->
                <Button x:Uid="MacroEdit_AddStep">
                    <Button.Flyout>
                        <MenuFlyout>
                            <MenuFlyoutItem
                                Text="Press Key"
                                Tag="PressKey"
                                Click="AddStep_Click" />
                            <MenuFlyoutItem
                                Text="Type Text"
                                Tag="TypeText"
                                Click="AddStep_Click" />
                            <MenuFlyoutItem
                                Text="Wait"
                                Tag="Wait"
                                Click="AddStep_Click" />
                            <MenuFlyoutItem
                                Text="Repeat"
                                Tag="Repeat"
                                Click="AddStep_Click" />
                        </MenuFlyout>
                    </Button.Flyout>
                </Button>
            </StackPanel>

            <!-- Validation error -->
            <InfoBar
                IsOpen="{x:Bind ViewModel.HasValidationError, Mode=OneWay}"
                IsClosable="False"
                Severity="Error"
                Title="Name is required." />

        </StackPanel>
    </ScrollViewer>
</ContentDialog>
```

- [ ] **Step 2: Create `MacroEditDialog.xaml.cs`**

Create `src/settings-ui/Settings.UI/SettingsXAML/Views/MacroEditDialog.xaml.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerToys.MacroCommon.Models;

namespace Microsoft.PowerToys.Settings.UI.Views;

public sealed partial class MacroEditDialog : ContentDialog
{
    public MacroEditViewModel ViewModel { get; }

    public MacroEditDialog(MacroEditViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
    }

    private DataTemplate? _stepTemplate;

    private DataTemplate GetStepTemplate()
    {
        _stepTemplate ??= StepsList.ItemTemplate;
        return _stepTemplate!;
    }

    private void SubStepsList_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is ListView list)
        {
            list.ItemTemplate = GetStepTemplate();
        }
    }

    private void AddStep_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuFlyoutItem item && item.Tag is string tag)
        {
            var type = Enum.Parse<StepType>(tag);
            ViewModel.AddStep(type);
        }
    }

    private void DeleteStep_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is MacroStepViewModel step)
        {
            ViewModel.DeleteStep(step);
        }
    }

    private void AddSubStep_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is MacroStepViewModel parent)
        {
            // Default sub-step type is PressKey
            ViewModel.AddSubStep(parent, StepType.PressKey);
        }
    }
}
```

- [ ] **Step 3: Build Settings.UI to verify XAML compiles**

```powershell
MSBuild src\settings-ui\Settings.UI\PowerToys.Settings.csproj /p:Platform=x64 /p:Configuration=Debug /t:Build
```

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add src/settings-ui/Settings.UI/SettingsXAML/Views/MacroEditDialog.xaml
git add src/settings-ui/Settings.UI/SettingsXAML/Views/MacroEditDialog.xaml.cs
git commit -m "feat(macro-ui): add MacroEditDialog with step tree editor"
```

---

## Task 8: `MacroPage` XAML, code-behind, and navigation wiring

**Files:**
- Create: `src/settings-ui/Settings.UI/SettingsXAML/Views/MacroPage.xaml`
- Create: `src/settings-ui/Settings.UI/SettingsXAML/Views/MacroPage.xaml.cs`
- Modify: `src/settings-ui/Settings.UI/SettingsXAML/Views/ShellPage.xaml`
- Add: `src/settings-ui/Settings.UI/Assets/Settings/Icons/Macro.png`

- [ ] **Step 1: Add a placeholder icon**

Copy an existing PNG icon to use as a placeholder for the Macro module. The icon must be a 32×32 or 64×64 PNG. Copy the `KeyboardManager.png` icon:

```powershell
Copy-Item `
  "src\settings-ui\Settings.UI\Assets\Settings\Icons\KeyboardManager.png" `
  "src\settings-ui\Settings.UI\Assets\Settings\Icons\Macro.png"
```

(Replace with a proper Macro icon before shipping.)

- [ ] **Step 2: Create `MacroPage.xaml`**

Create `src/settings-ui/Settings.UI/SettingsXAML/Views/MacroPage.xaml`:

```xml
<!-- Copyright (c) Microsoft Corporation -->
<!-- The Microsoft Corporation licenses this file to you under the MIT license. -->
<!-- See the LICENSE file in the project root for more information. -->

<local:NavigablePage
    x:Class="Microsoft.PowerToys.Settings.UI.Views.MacroPage"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:controls="using:Microsoft.PowerToys.Settings.UI.Controls"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:local="using:Microsoft.PowerToys.Settings.UI.Helpers"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:tkcontrols="using:CommunityToolkit.WinUI.Controls"
    xmlns:tkconverters="using:CommunityToolkit.WinUI.Converters"
    xmlns:viewmodels="using:Microsoft.PowerToys.Settings.UI.ViewModels"
    AutomationProperties.LandmarkType="Main"
    mc:Ignorable="d">

    <local:NavigablePage.Resources>
        <tkconverters:BoolToVisibilityConverter x:Key="BoolToVisibilityConverter" />
    </local:NavigablePage.Resources>

    <controls:SettingsPageControl
        x:Uid="MacroPage_Header"
        IsTabStop="False"
        ModuleImageSource="ms-appx:///Assets/Settings/Modules/Macro.png">
        <controls:SettingsPageControl.ModuleContent>
            <StackPanel ChildrenTransitions="{StaticResource SettingsCardsAnimations}" Orientation="Vertical" Spacing="4">

                <controls:SettingsGroup x:Uid="Macro_MacrosGroup">
                    <!-- New Macro button -->
                    <Button
                        x:Uid="Macro_NewMacroButton"
                        Margin="0,0,0,8"
                        Click="NewMacro_Click" />

                    <!-- Macro list -->
                    <ItemsControl ItemsSource="{x:Bind ViewModel.Macros}">
                        <ItemsControl.ItemTemplate>
                            <DataTemplate x:DataType="viewmodels:MacroListItem">
                                <tkcontrols:SettingsCard Margin="0,0,0,4">
                                    <tkcontrols:SettingsCard.Header>
                                        <StackPanel Orientation="Horizontal" Spacing="8">
                                            <TextBlock
                                                VerticalAlignment="Center"
                                                Style="{StaticResource BodyTextBlockStyle}"
                                                Text="{x:Bind Name, Mode=OneWay}" />
                                            <Border
                                                Padding="6,2"
                                                Background="{ThemeResource SystemFillColorAttentionBackgroundBrush}"
                                                CornerRadius="4"
                                                Visibility="{x:Bind HasHotkey, Converter={StaticResource BoolToVisibilityConverter}, Mode=OneWay}">
                                                <TextBlock
                                                    FontSize="11"
                                                    Text="{x:Bind Hotkey, Mode=OneWay}" />
                                            </Border>
                                        </StackPanel>
                                    </tkcontrols:SettingsCard.Header>
                                    <StackPanel Orientation="Horizontal" Spacing="8">
                                        <ToggleSwitch
                                            IsOn="{x:Bind IsEnabled, Mode=TwoWay}"
                                            OffContent=""
                                            OnContent="" />
                                        <Button
                                            Content="Edit"
                                            Tag="{x:Bind}"
                                            Click="EditMacro_Click" />
                                        <Button
                                            Content="Delete"
                                            Tag="{x:Bind}"
                                            Click="DeleteMacro_Click" />
                                    </StackPanel>
                                </tkcontrols:SettingsCard>
                            </DataTemplate>
                        </ItemsControl.ItemTemplate>
                    </ItemsControl>
                </controls:SettingsGroup>

            </StackPanel>
        </controls:SettingsPageControl.ModuleContent>
    </controls:SettingsPageControl>
</local:NavigablePage>
```

> **Note:** The `SettingsPageControl` requires a module image at `Assets/Settings/Modules/Macro.png`. Copy the icon there too (Step 1 copied to `Icons/`; a separate copy goes to `Modules/`).

- [ ] **Step 3: Copy icon to Modules folder**

```powershell
Copy-Item `
  "src\settings-ui\Settings.UI\Assets\Settings\Icons\KeyboardManager.png" `
  "src\settings-ui\Settings.UI\Assets\Settings\Modules\Macro.png"
```

- [ ] **Step 4: Add `HasHotkey` to `MacroListItem`**

In `src/settings-ui/Settings.UI/ViewModels/MacroListItem.cs`, add this computed property after the `Hotkey` property:

```csharp
public bool HasHotkey => !string.IsNullOrEmpty(_definition.Hotkey);
```

- [ ] **Step 5: Create `MacroPage.xaml.cs`**

Create `src/settings-ui/Settings.UI/SettingsXAML/Views/MacroPage.xaml.cs`:

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Helpers;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Views;

public sealed partial class MacroPage : NavigablePage
{
    public MacroViewModel ViewModel { get; }

    public MacroPage()
    {
        ViewModel = new MacroViewModel();
        DataContext = ViewModel;
        InitializeComponent();
    }

    private async void NewMacro_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.SuspendHotkeysAsync();
        try
        {
            var editVm = new MacroEditViewModel();
            var dialog = new MacroEditDialog(editVm) { XamlRoot = XamlRoot };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !editVm.HasValidationError)
            {
                await ViewModel.SaveMacroAsync(editVm);
            }
        }
        finally
        {
            await ViewModel.ResumeHotkeysAsync();
        }
    }

    private async void EditMacro_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: MacroListItem item })
        {
            return;
        }

        await ViewModel.SuspendHotkeysAsync();
        try
        {
            var editVm = new MacroEditViewModel(item.Definition);
            var dialog = new MacroEditDialog(editVm) { XamlRoot = XamlRoot };
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && !editVm.HasValidationError)
            {
                await ViewModel.SaveMacroAsync(editVm);
            }
        }
        finally
        {
            await ViewModel.ResumeHotkeysAsync();
        }
    }

    private async void DeleteMacro_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: MacroListItem item })
        {
            return;
        }

        var confirm = new ContentDialog
        {
            Title = "Delete macro?",
            Content = $"'{item.Name}' will be permanently deleted.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot,
        };

        var result = await confirm.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            ViewModel.DeleteMacro(item);
        }
    }
}
```

- [ ] **Step 6: Add nav item to `ShellPage.xaml`**

In `src/settings-ui/Settings.UI/SettingsXAML/Views/ShellPage.xaml`, find the `InputOutputNavigationItem` group. After the `KeyboardManagerNavigationItem` block (around line 316), add:

```xml
                        <NavigationViewItem
                            x:Name="MacroNavigationItem"
                            x:Uid="Shell_Macro"
                            helpers:NavHelper.NavigateTo="views:MacroPage"
                            AutomationProperties.AutomationId="MacroNavItem"
                            Icon="{ui:BitmapIcon Source=/Assets/Settings/Icons/Macro.png}" />
```

Full context (find this existing block and insert the new item after it):

```xml
                        <NavigationViewItem
                            x:Name="KeyboardManagerNavigationItem"
                            x:Uid="Shell_KeyboardManager"
                            helpers:NavHelper.NavigateTo="views:KeyboardManagerPage"
                            AutomationProperties.AutomationId="KeyboardManagerNavItem"
                            Icon="{ui:BitmapIcon Source=/Assets/Settings/Icons/KeyboardManager.png}" />
                        <!-- INSERT BELOW: -->
                        <NavigationViewItem
                            x:Name="MacroNavigationItem"
                            x:Uid="Shell_Macro"
                            helpers:NavHelper.NavigateTo="views:MacroPage"
                            AutomationProperties.AutomationId="MacroNavItem"
                            Icon="{ui:BitmapIcon Source=/Assets/Settings/Icons/Macro.png}" />
                        <NavigationViewItem
                            x:Name="MouseUtilitiesNavigationItem"
```

- [ ] **Step 7: Build Settings.UI**

```powershell
MSBuild src\settings-ui\Settings.UI\PowerToys.Settings.csproj /p:Platform=x64 /p:Configuration=Debug /t:Build
```

Expected: Build succeeds with no errors.

- [ ] **Step 8: Manual smoke test**

1. Launch `x64\Debug\WinUI3Apps\PowerToys.Settings.exe`
2. Confirm "Macro" appears in the left nav under "Input / Output"
3. Navigate to Macro page — page loads with empty macro list
4. Click "New macro" — edit dialog opens (name, hotkey, app scope, empty steps list)
5. Enter name "Test", set hotkey `Ctrl+F12` via ShortcutControl, click Save
6. Macro card appears in list. File `%AppData%\Microsoft\PowerToys\Macros\{id}.json` exists
7. Click edit — dialog opens pre-populated with name and hotkey
8. Add a PressKey step (key: `Tab`) — step row appears in list
9. Add a Repeat step (count: 3) — repeat row appears with "Add sub-step"
10. Click "Add sub-step" — sub-step row appears indented
11. Click Save — JSON file updated, engine FSW reloads
12. Toggle the enable switch — JSON file updated with `is_enabled: false`
13. Click Delete — confirm dialog, macro removed from list, JSON file deleted

- [ ] **Step 9: Commit**

```bash
git add "src/settings-ui/Settings.UI/Assets/Settings/Icons/Macro.png"
git add "src/settings-ui/Settings.UI/Assets/Settings/Modules/Macro.png"
git add src/settings-ui/Settings.UI/SettingsXAML/Views/MacroPage.xaml
git add src/settings-ui/Settings.UI/SettingsXAML/Views/MacroPage.xaml.cs
git add src/settings-ui/Settings.UI/SettingsXAML/Views/ShellPage.xaml
git add src/settings-ui/Settings.UI/ViewModels/MacroListItem.cs
git commit -m "feat(macro-ui): add MacroPage, navigation entry, and icon"
```
