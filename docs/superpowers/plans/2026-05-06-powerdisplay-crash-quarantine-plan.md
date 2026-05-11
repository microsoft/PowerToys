# PowerDisplay 崩溃隔离机制 —— 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 PowerDisplay.exe 启动时检测上次运行是否在 DDC/CI capability 获取阶段崩溃，若是则自动禁用模块、写崩溃标志、Settings UI 顶部红色 InfoBar 警示并锁死页面，直到用户点 Ignore 显式确认。

**Architecture:** 两个磁盘 artifact（`discovery.lock` 标记危险代码区、`crash_detected.flag` UI 信号）+ 一个 IDisposable scope（`CrashDetectionScope`）+ 一次性检测类（`CrashRecovery`）+ 一条新 Windows 命名 event（`POWER_DISPLAY_AUTO_DISABLE_EVENT`）+ module DLL 内一条新 listener 线程把内存状态校准到磁盘事实 + Settings UI 顶部 InfoBar + 整页 IsEnabled 锁。设计文档：[2026-05-06-powerdisplay-crash-quarantine-design.md](../specs/2026-05-06-powerdisplay-crash-quarantine-design.md)。

**Tech Stack:** C# (.NET, WinUI3), C++ (PowerDisplay module DLL), MSTest + Moq（测试），WinRT 投影（cross-language event 名）。

---

## 文件结构概览

**新增文件：**
- `src/modules/powerdisplay/PowerDisplay.Models/PowerDisplayPaths.cs` — Settings UI 也能引用的路径常量
- `src/modules/powerdisplay/PowerDisplay.Lib/Services/CrashDetectionScope.cs` — Phase 2 期间持有 lock 的 IDisposable
- `src/modules/powerdisplay/PowerDisplay.Lib/Services/CrashRecovery.cs` — Phase 0 检测 + 严格 fail-fast 序列
- `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/CrashDetectionScopeTests.cs`
- `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/CrashRecoveryTests.cs`

**修改文件：**
- `src/common/interop/shared_constants.h` — 加 `POWER_DISPLAY_AUTO_DISABLE_EVENT`
- `src/common/interop/Constants.cpp/.h/.idl` — 加 WinRT 投影 `AutoDisablePowerDisplayEvent()`
- `src/modules/powerdisplay/PowerDisplay.Lib/PathConstants.cs` — 加 `DiscoveryLockPath` / `CrashDetectedFlagPath`
- `src/modules/powerdisplay/PowerDisplayModuleInterface/dllmain.cpp` — 加 listener 线程
- `src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs` — 在 Phase 2 用 scope；加 debug-only crash 注入
- `src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs` — Phase 0 hook
- `src/settings-ui/Settings.UI/ViewModels/PowerDisplayViewModel.cs` — 加 `IsCrashLockActive` + `DismissCrashWarningCommand`
- `src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml` — 顶部 InfoBar + 锁定 wrapper
- `src/settings-ui/Settings.UI/Strings/en-us/Resources.resw` — 新增本地化字符串
- `src/settings-ui/Settings.UI.Library/SettingsBackupAndRestoreUtils.cs` — 验证排除（最后一步审计）

---

## Task 1: 在 PowerDisplay.Models 中加路径常量类

**Files:**
- Create: `src/modules/powerdisplay/PowerDisplay.Models/PowerDisplayPaths.cs`

- [ ] **Step 1: 创建 PowerDisplayPaths.cs**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace PowerDisplay.Models
{
    /// <summary>
    /// Path constants for PowerDisplay artifacts that are consumed by Settings UI.
    /// Lives in PowerDisplay.Models because Settings UI references this project but
    /// not PowerDisplay.Lib. PowerDisplay.Lib's PathConstants delegates to this for
    /// the same constants to keep a single source of truth.
    /// </summary>
    public static class PowerDisplayPaths
    {
        /// <summary>
        /// %LOCALAPPDATA%\Microsoft\PowerToys\PowerDisplay
        /// </summary>
        public static string PowerDisplayFolder
            => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "PowerToys", "PowerDisplay");

        /// <summary>
        /// %LOCALAPPDATA%\Microsoft\PowerToys\PowerDisplay\discovery.lock
        /// Existence at PowerDisplay.exe startup indicates the previous run crashed
        /// inside DDC/CI capability fetch.
        /// </summary>
        public static string DiscoveryLockPath
            => Path.Combine(PowerDisplayFolder, "discovery.lock");

        /// <summary>
        /// %LOCALAPPDATA%\Microsoft\PowerToys\PowerDisplay\crash_detected.flag
        /// UI signal — Settings UI shows the auto-disable InfoBar when this exists.
        /// </summary>
        public static string CrashDetectedFlagPath
            => Path.Combine(PowerDisplayFolder, "crash_detected.flag");

        /// <summary>
        /// %LOCALAPPDATA%\Microsoft\PowerToys\settings.json — global PowerToys settings
        /// (NOT the per-module file). PowerDisplay.exe Phase 0 mutates enabled.PowerDisplay here.
        /// </summary>
        public static string GlobalPowerToysSettingsPath
            => Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "PowerToys", "settings.json");
    }
}
```

- [ ] **Step 2: 编译 Models 项目验证无误**

Run: `dotnet build src/modules/powerdisplay/PowerDisplay.Models/PowerDisplay.Models.csproj -c Debug --no-restore`
Expected: Build succeeded, 0 Error(s).

- [ ] **Step 3: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplay.Models/PowerDisplayPaths.cs
git commit -m "feat(powerdisplay): add PowerDisplayPaths shared between Lib and Settings UI"
```

---

## Task 2: 在 PowerDisplay.Lib 的 PathConstants 委托给 PowerDisplayPaths

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay.Lib/PathConstants.cs`

- [ ] **Step 1: 编辑 PathConstants.cs，在文件常量段加新 file name 常量并暴露 lock/flag 路径**

在 `PathConstants` 类中（`MonitorStateFileName` 常量附近）追加：

```csharp
        /// <summary>
        /// File name of the discovery lock used by the crash quarantine mechanism.
        /// </summary>
        public const string DiscoveryLockFileName = "discovery.lock";

        /// <summary>
        /// File name of the crash-detected flag used by the crash quarantine mechanism.
        /// </summary>
        public const string CrashDetectedFlagFileName = "crash_detected.flag";

        /// <summary>
        /// Full path of discovery.lock. Delegates to PowerDisplay.Models.PowerDisplayPaths
        /// so the same constant is reachable from Settings UI without taking a
        /// PowerDisplay.Lib reference.
        /// </summary>
        public static string DiscoveryLockPath => global::PowerDisplay.Models.PowerDisplayPaths.DiscoveryLockPath;

        /// <summary>
        /// Full path of crash_detected.flag. Delegates to PowerDisplay.Models.PowerDisplayPaths.
        /// </summary>
        public static string CrashDetectedFlagPath => global::PowerDisplay.Models.PowerDisplayPaths.CrashDetectedFlagPath;

        /// <summary>
        /// Full path of the global PowerToys settings.json. Delegates to PowerDisplay.Models.PowerDisplayPaths.
        /// </summary>
        public static string GlobalPowerToysSettingsPath => global::PowerDisplay.Models.PowerDisplayPaths.GlobalPowerToysSettingsPath;
```

- [ ] **Step 2: 编译 Lib 项目验证**

Run: `dotnet build src/modules/powerdisplay/PowerDisplay.Lib/PowerDisplay.Lib.csproj -c Debug --no-restore`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplay.Lib/PathConstants.cs
git commit -m "feat(powerdisplay): expose crash-quarantine paths via PathConstants"
```

---

## Task 3: 在 shared_constants.h 中加 C++ 事件名常量

**Files:**
- Modify: `src/common/interop/shared_constants.h`

- [ ] **Step 1: 生成新 GUID 用作 event 名后缀**

任何方法都行；推荐 PowerShell：

Run: `[guid]::NewGuid().ToString()`
Expected: 形如 `b3a835c0-eaa2-49b0-b8eb-f793e3df3368` 的 GUID。**记下来**（下一步要用，且 Task 4 也要用同一个）。

- [ ] **Step 2: 编辑 shared_constants.h**

在 `HOTKEY_UPDATED_POWER_DISPLAY_EVENT` 那一行（约 line 167）下面追加（用 Step 1 生成的 GUID 替换 `<UUID>`）：

```cpp
    const wchar_t POWER_DISPLAY_AUTO_DISABLE_EVENT[] = L"Local\\PowerToysPowerDisplay-AutoDisableEvent-<UUID>";
```

- [ ] **Step 3: Commit**

```bash
git add src/common/interop/shared_constants.h
git commit -m "feat(powerdisplay): add POWER_DISPLAY_AUTO_DISABLE_EVENT shared constant"
```

---

## Task 4: 在 PowerToys.Interop 中加 WinRT 投影方法

**Files:**
- Modify: `src/common/interop/Constants.idl`
- Modify: `src/common/interop/Constants.h`
- Modify: `src/common/interop/Constants.cpp`

- [ ] **Step 1: 编辑 Constants.idl，加 IDL 声明**

找到 `static String TerminatePowerDisplayEvent();` 那一行（约 line 67），下方追加：

```idl
            static String AutoDisablePowerDisplayEvent();
```

- [ ] **Step 2: 编辑 Constants.h，加 C++ 声明**

找到 `static hstring TerminatePowerDisplayEvent();` 那一行（约 line 70），下方追加：

```cpp
        static hstring AutoDisablePowerDisplayEvent();
```

- [ ] **Step 3: 编辑 Constants.cpp，加实现**

找到 `hstring Constants::TerminatePowerDisplayEvent()` 那个方法（约 line 258），在它之后追加：

```cpp
    hstring Constants::AutoDisablePowerDisplayEvent()
    {
        return CommonSharedConstants::POWER_DISPLAY_AUTO_DISABLE_EVENT;
    }
```

- [ ] **Step 4: 编译 PowerToys.Interop 验证**

Run: `msbuild src/common/interop/PowerToys.Interop.vcxproj -p:Configuration=Debug -p:Platform=x64 -t:Build -nologo` (或 IDE 中编译)
Expected: Build succeeded. WinRT 投影 metadata 自动重新生成。

- [ ] **Step 5: Commit**

```bash
git add src/common/interop/Constants.idl src/common/interop/Constants.h src/common/interop/Constants.cpp
git commit -m "feat(powerdisplay): expose AutoDisablePowerDisplayEvent via WinRT"
```

---

## Task 5: TDD —— `CrashDetectionScope` 测试用例

**Files:**
- Create: `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/CrashDetectionScopeTests.cs`

- [ ] **Step 1: 创建测试文件，先把所有测试写好（此时实现还不存在，编译就会失败）**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

[TestClass]
public class CrashDetectionScopeTests
{
    private string _tempDir = string.Empty;
    private string _lockPath = string.Empty;

    [TestInitialize]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"pd-scope-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _lockPath = Path.Combine(_tempDir, "discovery.lock");
    }

    [TestCleanup]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    [TestMethod]
    public void Begin_WritesLockFile()
    {
        using var _ = CrashDetectionScope.Begin(_lockPath);

        Assert.IsTrue(File.Exists(_lockPath), "lock file should exist after Begin()");
        var contents = File.ReadAllText(_lockPath);
        StringAssert.Contains(contents, "\"version\":1");
        StringAssert.Contains(contents, "\"pid\":");
        StringAssert.Contains(contents, "\"startedAt\":");
    }

    [TestMethod]
    [ExpectedException(typeof(IOException))]
    public void Begin_ThrowsIfLockAlreadyExists()
    {
        File.WriteAllText(_lockPath, "stale");

        // Should throw because of FileMode.CreateNew
        _ = CrashDetectionScope.Begin(_lockPath);
    }

    [TestMethod]
    public void Dispose_DeletesLockFile()
    {
        var scope = CrashDetectionScope.Begin(_lockPath);
        Assert.IsTrue(File.Exists(_lockPath));

        scope.Dispose();

        Assert.IsFalse(File.Exists(_lockPath), "lock file should be gone after Dispose()");
    }

    [TestMethod]
    public void Dispose_IsIdempotent()
    {
        var scope = CrashDetectionScope.Begin(_lockPath);

        scope.Dispose();
        scope.Dispose();  // must not throw
    }

    [TestMethod]
    public void Dispose_DoesNotThrowWhenLockMissing()
    {
        var scope = CrashDetectionScope.Begin(_lockPath);
        File.Delete(_lockPath);  // simulate lock removed externally

        scope.Dispose();  // must not throw

        Assert.IsFalse(File.Exists(_lockPath));
    }
}
```

- [ ] **Step 2: 跑测试确认失败（因为 CrashDetectionScope 还不存在）**

Run: `dotnet test src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplay.Lib.UnitTests.csproj --filter FullyQualifiedName~CrashDetectionScopeTests`
Expected: 编译错误，类型 `CrashDetectionScope` 找不到。

---

## Task 6: 实现 `CrashDetectionScope`

**Files:**
- Create: `src/modules/powerdisplay/PowerDisplay.Lib/Services/CrashDetectionScope.cs`

- [ ] **Step 1: 创建 CrashDetectionScope.cs**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using ManagedCommon;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// IDisposable scope that writes <c>discovery.lock</c> on Begin() and deletes it on Dispose().
    /// Wrap DDC/CI capability fetch (Phase 2 of monitor discovery) in a using-block to mark
    /// "we are inside the dangerous code path." If the process is killed externally (BSOD,
    /// TerminateProcess, FailFast) Dispose() never runs and the lock survives — at next
    /// PowerDisplay.exe startup CrashRecovery detects the orphan and disables the module.
    ///
    /// On normal completion or .NET exception, Dispose() removes the lock.
    /// </summary>
    public sealed class CrashDetectionScope : IDisposable
    {
        private readonly string _lockPath;
        private bool _disposed;

        /// <summary>
        /// Begin a new scope. Writes the lock file using FileMode.CreateNew (fails if it
        /// already exists — defensive against duplicate invocation), WriteThrough, and
        /// FlushFileBuffers (L3 durability). Throws on any IO failure; the caller should
        /// not enter Phase 2 if Begin() throws.
        /// </summary>
        /// <param name="lockPath">Override the default lock path. Defaults to
        /// <see cref="PathConstants.DiscoveryLockPath"/>. Test code passes a temp path.</param>
        public static CrashDetectionScope Begin(string? lockPath = null)
        {
            var path = lockPath ?? PathConstants.DiscoveryLockPath;

            // Ensure parent directory exists.
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var payload = JsonSerializer.SerializeToUtf8Bytes(new LockPayload(
                Version: 1,
                Pid: Environment.ProcessId,
                StartedAt: DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)));

            // CreateNew: fail if file exists (means duplicate scope or Phase 0 didn't clean up).
            // WriteThrough + Flush(true): force the bytes to physical storage before returning,
            // so that even an immediate BSOD preserves the lock.
            using (var fs = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                options: FileOptions.WriteThrough))
            {
                fs.Write(payload);
                fs.Flush(flushToDisk: true);
            }

            Logger.LogInfo($"CrashDetectionScope: lock written at {path}");
            return new CrashDetectionScope(path);
        }

        private CrashDetectionScope(string lockPath)
        {
            _lockPath = lockPath;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            try
            {
                if (File.Exists(_lockPath))
                {
                    File.Delete(_lockPath);
                    Logger.LogInfo($"CrashDetectionScope: lock deleted at {_lockPath}");
                }
            }
            catch (Exception ex)
            {
                // Worst case: a single false-positive quarantine on next start, recoverable
                // by the user clicking Ignore. Don't propagate — DiscoverMonitorsAsync should
                // not crash because of cleanup failure.
                Logger.LogWarning($"CrashDetectionScope: failed to delete lock at {_lockPath}: {ex.Message}");
            }
        }

        // System.Text.Json record for the lock payload.
        private sealed record LockPayload(int Version, int Pid, string StartedAt);
    }
}
```

- [ ] **Step 2: 跑测试确认通过**

Run: `dotnet test src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplay.Lib.UnitTests.csproj --filter FullyQualifiedName~CrashDetectionScopeTests`
Expected: 5 个测试全部通过。

- [ ] **Step 3: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplay.Lib/Services/CrashDetectionScope.cs \
        src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/CrashDetectionScopeTests.cs
git commit -m "feat(powerdisplay): add CrashDetectionScope for Phase 2 lock lifecycle"
```

---

## Task 7: TDD —— `CrashRecovery` 测试用例

**Files:**
- Create: `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/CrashRecoveryTests.cs`

`CrashRecovery` 依赖三类外部资源：lock 文件路径、flag 文件路径、global settings.json 路径、SignalEvent 函数。我们用构造器注入的方式让它们可被替换。

- [ ] **Step 1: 创建测试文件**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;

namespace PowerDisplay.UnitTests;

[TestClass]
public class CrashRecoveryTests
{
    private string _tempDir = string.Empty;
    private string _lockPath = string.Empty;
    private string _flagPath = string.Empty;
    private string _settingsPath = string.Empty;

    [TestInitialize]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"pd-rec-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _lockPath = Path.Combine(_tempDir, "discovery.lock");
        _flagPath = Path.Combine(_tempDir, "crash_detected.flag");
        _settingsPath = Path.Combine(_tempDir, "settings.json");
        File.WriteAllText(_settingsPath, "{\"enabled\":{\"PowerDisplay\":true}}");
    }

    [TestCleanup]
    public void TearDown()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // best-effort
        }
    }

    private CrashRecovery NewRecovery(Func<string, bool>? signal = null)
        => new CrashRecovery(_lockPath, _flagPath, _settingsPath, signal ?? (_ => true));

    [TestMethod]
    public void DetectOrphanAndDisable_ReturnsFalseWhenNoLock()
    {
        var rec = NewRecovery();

        var result = rec.DetectOrphanAndDisable();

        Assert.IsFalse(result);
        Assert.IsFalse(File.Exists(_flagPath), "flag must not be written when no orphan");
    }

    [TestMethod]
    public void DetectOrphanAndDisable_RunsFullSequenceWhenOrphanPresent()
    {
        File.WriteAllText(_lockPath, "{\"version\":1,\"pid\":1234,\"startedAt\":\"2026-05-06T10:00:00Z\"}");
        var signaled = false;
        var rec = NewRecovery(_ => { signaled = true; return true; });

        var result = rec.DetectOrphanAndDisable();

        Assert.IsTrue(result);
        Assert.IsTrue(File.Exists(_flagPath), "flag should be written");
        Assert.IsFalse(File.Exists(_lockPath), "lock should be deleted (commit)");
        Assert.IsTrue(signaled, "auto-disable event should be signaled");

        var settingsJson = File.ReadAllText(_settingsPath);
        StringAssert.Contains(settingsJson, "\"PowerDisplay\":false");
    }

    [TestMethod]
    public void DetectOrphanAndDisable_HandlesUnknownVersionAsOrphan()
    {
        File.WriteAllText(_lockPath, "{\"version\":99,\"pid\":1234}");
        var rec = NewRecovery();

        var result = rec.DetectOrphanAndDisable();

        Assert.IsTrue(result, "unknown version should still be treated as orphan");
        Assert.IsTrue(File.Exists(_flagPath));
    }

    [TestMethod]
    [ExpectedException(typeof(IOException))]
    public void DetectOrphanAndDisable_LeavesLockIntactOnFlagWriteFailure()
    {
        File.WriteAllText(_lockPath, "{\"version\":1}");

        // Make the flag path point to a directory that doesn't exist *and* isn't creatable
        // (use a path containing an invalid character on the local filesystem).
        var unwritableFlag = Path.Combine(_tempDir, "no\0way", "crash_detected.flag");
        var rec = new CrashRecovery(_lockPath, unwritableFlag, _settingsPath, _ => true);

        try
        {
            rec.DetectOrphanAndDisable();
        }
        finally
        {
            Assert.IsTrue(File.Exists(_lockPath), "lock must remain on failure");
        }
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void DetectOrphanAndDisable_LeavesLockIntactOnSignalFailure()
    {
        File.WriteAllText(_lockPath, "{\"version\":1}");
        var rec = new CrashRecovery(
            _lockPath, _flagPath, _settingsPath,
            signalEvent: _ => throw new InvalidOperationException("simulated"));

        try
        {
            rec.DetectOrphanAndDisable();
        }
        finally
        {
            Assert.IsTrue(File.Exists(_lockPath), "lock must remain on failure");
            // Steps 1 and 2 already ran; flag and settings are written. That's expected —
            // they'll be no-ops on retry.
            Assert.IsTrue(File.Exists(_flagPath));
        }
    }
}
```

- [ ] **Step 2: 跑测试确认失败（CrashRecovery 还不存在）**

Run: `dotnet test src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplay.Lib.UnitTests.csproj --filter FullyQualifiedName~CrashRecoveryTests`
Expected: 编译错误，类型 `CrashRecovery` 找不到。

---

## Task 8: 实现 `CrashRecovery`

**Files:**
- Create: `src/modules/powerdisplay/PowerDisplay.Lib/Services/CrashRecovery.cs`

- [ ] **Step 1: 创建 CrashRecovery.cs**

```csharp
// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ManagedCommon;
using PowerDisplay.Common.Utils;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// Detects crash evidence at PowerDisplay.exe startup and runs the strict auto-disable
    /// sequence when an orphan <c>discovery.lock</c> is found.
    ///
    /// The sequence is: write crash_detected.flag → write settings.json → signal event →
    /// delete lock. Any step failure throws and leaves the lock in place; next startup
    /// retries the entire sequence. This "lock-as-commit-point" pattern is what makes
    /// the mechanism self-healing.
    /// </summary>
    public sealed class CrashRecovery
    {
        private readonly string _lockPath;
        private readonly string _flagPath;
        private readonly string _settingsPath;
        private readonly Func<string, bool> _signalEvent;

        public CrashRecovery(string lockPath, string flagPath, string settingsPath, Func<string, bool> signalEvent)
        {
            _lockPath = lockPath;
            _flagPath = flagPath;
            _settingsPath = settingsPath;
            _signalEvent = signalEvent;
        }

        /// <summary>
        /// Production constructor. Uses <see cref="PathConstants"/> defaults and the real
        /// <see cref="EventHelper.SignalEvent"/>. The auto-disable event name comes from
        /// the WinRT <c>Constants</c> projection.
        /// </summary>
        public static CrashRecovery CreateDefault()
        {
            return new CrashRecovery(
                lockPath: PathConstants.DiscoveryLockPath,
                flagPath: PathConstants.CrashDetectedFlagPath,
                settingsPath: PathConstants.GlobalPowerToysSettingsPath,
                signalEvent: name => EventHelper.SignalEvent(name));
        }

        /// <summary>
        /// Returns true if an orphan lock was found and the auto-disable sequence executed.
        /// Caller should exit the process when this returns true.
        /// Throws on any sequence step failure (strict fail-fast). Lock is preserved on
        /// failure so the next startup retries.
        /// </summary>
        public bool DetectOrphanAndDisable()
        {
            if (!File.Exists(_lockPath))
            {
                Logger.LogTrace("Phase 0: no orphan lock; normal startup");
                return false;
            }

            string lockContent = SafeReadAllText(_lockPath) ?? "<unreadable>";
            Logger.LogWarning($"Phase 0: found orphan lock at {_lockPath} with content {lockContent}; entering auto-disable sequence");

            // Step 1: write crash_detected.flag (UI signal)
            WriteCrashFlag();
            Logger.LogInfo("Phase 0: step 1 (write crash_detected.flag) ok");

            // Step 2: persist disabled state
            WriteSettingsDisabled();
            Logger.LogInfo("Phase 0: step 2 (write settings.json) ok");

            // Step 3: signal event so the running runner's module DLL listener calls disable()
            SignalAutoDisable();
            Logger.LogInfo("Phase 0: step 3 (signal AutoDisable event) ok");

            // Step 4: commit by deleting the lock. Last on purpose — retry self-heal.
            File.Delete(_lockPath);
            Logger.LogInfo("Phase 0: step 4 (delete discovery.lock) ok — sequence committed");

            return true;
        }

        private void WriteCrashFlag()
        {
            var dir = Path.GetDirectoryName(_flagPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var payload = JsonSerializer.Serialize(new
            {
                version = 1,
                detectedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
            });
            File.WriteAllText(_flagPath, payload);
        }

        private void WriteSettingsDisabled()
        {
            // Read-modify-write the global PowerToys settings.json.
            // We mutate enabled.PowerDisplay = false. Other fields untouched.
            JsonNode root;
            if (File.Exists(_settingsPath))
            {
                var text = File.ReadAllText(_settingsPath);
                root = JsonNode.Parse(text) ?? new JsonObject();
            }
            else
            {
                root = new JsonObject();
            }

            var enabled = root["enabled"] as JsonObject ?? new JsonObject();
            enabled["PowerDisplay"] = false;
            root["enabled"] = enabled;

            File.WriteAllText(_settingsPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        private void SignalAutoDisable()
        {
            // The event name comes from PowerToys.Interop.Constants.AutoDisablePowerDisplayEvent().
            // We accept it via the injected delegate so unit tests can substitute.
            var eventName = global::PowerToys.Interop.Constants.AutoDisablePowerDisplayEvent();
            if (!_signalEvent(eventName))
            {
                throw new InvalidOperationException($"Failed to signal {eventName}");
            }
        }

        private static string? SafeReadAllText(string path)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch
            {
                return null;
            }
        }
    }
}
```

- [ ] **Step 2: 跑测试确认通过**

Run: `dotnet test src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplay.Lib.UnitTests.csproj --filter FullyQualifiedName~CrashRecoveryTests`
Expected: 5 个测试全部通过。

- [ ] **Step 3: 跑全部 PowerDisplay.Lib 单元测试确认未破坏现有测试**

Run: `dotnet test src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplay.Lib.UnitTests.csproj`
Expected: 全部测试通过（已有的 MccsCapabilitiesParserTests + 新增 10 个）。

- [ ] **Step 4: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplay.Lib/Services/CrashRecovery.cs \
        src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/CrashRecoveryTests.cs
git commit -m "feat(powerdisplay): add CrashRecovery with strict fail-fast auto-disable sequence"
```

---

## Task 9: PowerDisplayModuleInterface DLL 加 listener 线程

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplayModuleInterface/dllmain.cpp`

参考已有 `m_hToggleEvent` / `StartToggleEventListener` / `StopToggleEventListener` / `m_toggleEventThread` 的模式做同样的事。Listener 是**一次性的**——收到信号后调用 `disable()` 即退出，再被启用时由 `enable()` 重新启动。

- [ ] **Step 1: 在 PowerDisplayModule 类的 private 成员变量段加 handle 与线程**

找到（约 line 50-55）：
```cpp
    HANDLE m_hToggleEvent = nullptr;
    HANDLE m_hStopEvent = nullptr;  // Manual-reset event to signal thread termination
    std::thread m_toggleEventThread;
```

下方追加：
```cpp
    HANDLE m_hAutoDisableEvent = nullptr;
    std::thread m_autoDisableEventThread;
```

- [ ] **Step 2: 在构造函数里 CreateDefaultEvent**

找到（约 line 75）`m_hToggleEvent = CreateDefaultEvent(...);` 那一行下方，追加：

```cpp
        m_hAutoDisableEvent = CreateDefaultEvent(CommonSharedConstants::POWER_DISPLAY_AUTO_DISABLE_EVENT);
        Logger::trace(L"Created AUTO_DISABLE_EVENT: handle={}", reinterpret_cast<void*>(m_hAutoDisableEvent));
```

并把现有的 handle 校验日志（约 line 82-89）也把 `m_hAutoDisableEvent` 加进去：把
```cpp
        if (!m_hRefreshEvent || !m_hSendSettingsTelemetryEvent || !m_hToggleEvent || !m_hStopEvent)
```
改成
```cpp
        if (!m_hRefreshEvent || !m_hSendSettingsTelemetryEvent || !m_hToggleEvent || !m_hStopEvent || !m_hAutoDisableEvent)
```
对应 error log 中也加上 `AutoDisable={...}` 字段。

- [ ] **Step 3: 在析构函数里 CloseHandle**

找到（约 line 115-119）现有的 `if (m_hToggleEvent) { CloseHandle(...); ... }` 块下方追加：

```cpp
        if (m_hAutoDisableEvent)
        {
            CloseHandle(m_hAutoDisableEvent);
            m_hAutoDisableEvent = nullptr;
        }
```

- [ ] **Step 4: 添加 Start/Stop 方法**

在 `StopToggleEventListener` 方法下方（约 line 212）追加：

```cpp
    void StartAutoDisableEventListener()
    {
        if (!m_hAutoDisableEvent || !m_hStopEvent || m_autoDisableEventThread.joinable())
        {
            return;
        }

        m_autoDisableEventThread = std::thread([this]() {
            Logger::info(L"AutoDisable listener thread started");

            HANDLE handles[] = { m_hAutoDisableEvent, m_hStopEvent };
            constexpr DWORD AUTO_DISABLE_INDEX = 0;
            constexpr DWORD STOP_INDEX = 1;

            DWORD result = WaitForMultipleObjects(2, handles, FALSE, INFINITE);
            if (result == WAIT_OBJECT_0 + AUTO_DISABLE_INDEX)
            {
                Logger::warn(L"PowerDisplay AutoDisable event received - disabling module");
                // Calling disable() updates m_enabled (which runner queries via is_enabled())
                // and stops the process manager. PowerDisplay.exe Phase 0 already wrote
                // settings.json; we don't touch it here.
                this->disable();
            }
            else
            {
                Logger::trace(L"AutoDisable listener: stop event signaled");
            }

            Logger::info(L"AutoDisable listener thread stopped");
        });
    }

    void StopAutoDisableEventListener()
    {
        // m_hStopEvent shared with toggle listener; SetEvent there already wakes us up.
        if (m_autoDisableEventThread.joinable())
        {
            m_autoDisableEventThread.join();
        }
    }
```

- [ ] **Step 5: 在 enable() 里启动 listener**

找到（约 line 331-343）：
```cpp
    virtual void enable() override
    {
        Logger::info(L"enable: PowerDisplay module is being enabled");
        m_enabled = true;
        Trace::EnablePowerDisplay(true);

        StartToggleEventListener();
        ...
    }
```

在 `StartToggleEventListener();` 下方追加：
```cpp
        StartAutoDisableEventListener();
```

- [ ] **Step 6: 在 disable() 里停 listener**

找到（约 line 345-356）：
```cpp
    virtual void disable() override
    {
        ...
        m_enabled = false;
        StopToggleEventListener();
        ...
    }
```

在 `StopToggleEventListener();` 下方追加：
```cpp
        StopAutoDisableEventListener();
```

注意：当 listener 自己调 `disable()`（被自己唤醒后）时，会再次调到 `StopAutoDisableEventListener()`——`std::thread::join()` 在自己线程上会死锁。需要在 disable() 里改成"如果当前线程就是 listener 线程，跳过 join"。最简单：`StopAutoDisableEventListener()` 改成：

```cpp
    void StopAutoDisableEventListener()
    {
        if (m_autoDisableEventThread.joinable() &&
            m_autoDisableEventThread.get_id() != std::this_thread::get_id())
        {
            m_autoDisableEventThread.join();
        }
        else if (m_autoDisableEventThread.joinable())
        {
            // 自己 join 自己会死锁；detach 让它正常结束。
            m_autoDisableEventThread.detach();
        }
    }
```

- [ ] **Step 7: 编译 module DLL**

Run: `msbuild src/modules/powerdisplay/PowerDisplayModuleInterface/PowerDisplayModuleInterface.vcxproj -p:Configuration=Debug -p:Platform=x64 -t:Build -nologo`
Expected: Build succeeded.

- [ ] **Step 8: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplayModuleInterface/dllmain.cpp
git commit -m "feat(powerdisplay): add AutoDisable event listener in module DLL"
```

---

## Task 10: 在 DdcCiController 集成 CrashDetectionScope

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs`

- [ ] **Step 1: 在文件顶部 using 段加引用**

找到（约 line 4-21）现有的 using 段，确保有：
```csharp
using PowerDisplay.Common.Services;
```
没有就加上。

- [ ] **Step 2: 修改 DiscoverMonitorsAsync**

找到（[DdcCiController.cs:264-298](src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs#L264-L298)）现有的方法：

```csharp
        public async Task<IEnumerable<Monitor>> DiscoverMonitorsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Get monitor display info from QueryDisplayConfig, keyed by device path (unique per target)
                var allMonitorDisplayInfo = DdcCiNative.GetAllMonitorDisplayInfo();

                // Phase 1: Collect candidate monitors
                var monitorHandles = EnumerateMonitorHandles();
                if (monitorHandles.Count == 0)
                {
                    return Enumerable.Empty<Monitor>();
                }

                var candidateMonitors = await CollectCandidateMonitorsAsync(
                    monitorHandles, allMonitorDisplayInfo, cancellationToken);

                if (candidateMonitors.Count == 0)
                {
                    return Enumerable.Empty<Monitor>();
                }

                // Phase 2: Fetch capabilities in parallel
                var fetchResults = await FetchCapabilitiesInParallelAsync(
                    candidateMonitors, cancellationToken);

                // Phase 3: Create monitor objects
                return CreateValidMonitors(fetchResults);
            }
            catch (Exception ex)
            {
                Logger.LogError($"DDC: DiscoverMonitorsAsync exception: {ex.Message}\nStack: {ex.StackTrace}");
                return Enumerable.Empty<Monitor>();
            }
        }
```

把 Phase 2 那个 `await FetchCapabilitiesInParallelAsync(...)` 调用替换成 using 块包裹：

```csharp
                // Phase 2: Fetch capabilities in parallel — protected by crash-detection scope.
                // The scope writes discovery.lock before entering and deletes it after.
                // If the process is killed during fetch (BSOD), the lock survives and the
                // next PowerDisplay.exe startup will detect it via CrashRecovery.
                (CandidateMonitor Candidate, DdcCiValidationResult Result)[] fetchResults;
                using (CrashDetectionScope.Begin())
                {
                    fetchResults = await FetchCapabilitiesInParallelAsync(
                        candidateMonitors, cancellationToken);
                }

                // Phase 3: Create monitor objects (outside scope — VCP get/set on those
                // objects are not protected; they are not the documented BSOD path).
                return CreateValidMonitors(fetchResults);
```

- [ ] **Step 3: 编译 PowerDisplay.Lib**

Run: `dotnet build src/modules/powerdisplay/PowerDisplay.Lib/PowerDisplay.Lib.csproj -c Debug --no-restore`
Expected: Build succeeded.

- [ ] **Step 4: 跑全部 PowerDisplay.Lib 单元测试**

Run: `dotnet test src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplay.Lib.UnitTests.csproj`
Expected: 全部测试通过。

- [ ] **Step 5: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs
git commit -m "feat(powerdisplay): wrap Phase 2 capability fetch in CrashDetectionScope"
```

---

## Task 11: 在 PowerDisplay.exe App.xaml.cs 加 Phase 0 hook

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs`

- [ ] **Step 1: 在文件顶部 using 段加引用**

找到现有 using 段，加：
```csharp
using PowerDisplay.Common.Services;
```

- [ ] **Step 2: 在 OnLaunched 方法的最开头插入 Phase 0**

找到 [App.xaml.cs:83-86](src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs#L83-L86)：

```csharp
        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            Logger.LogInfo("OnLaunched: Application launching");
            try
            {
```

在 `Logger.LogInfo("OnLaunched: Application launching");` 下方、`try` 之前插入：

```csharp
            // Phase 0: crash recovery. Must run before any DDC/CI initialization.
            // If the previous run left an orphan discovery.lock, write the user-visible
            // crash_detected.flag, set enabled.PowerDisplay=false in global settings.json,
            // signal the AutoDisable event, delete the lock, and exit. The runner's module
            // DLL listener will pick up the event and call disable() to sync m_enabled.
            try
            {
                if (CrashRecovery.CreateDefault().DetectOrphanAndDisable())
                {
                    Logger.LogWarning("Phase 0: orphan discovery.lock detected; auto-disable sequence executed; exiting");
                    Environment.Exit(0);
                }
            }
            catch (Exception phaseZeroEx)
            {
                Logger.LogError($"Phase 0: auto-disable sequence failed: {phaseZeroEx}");
                // discovery.lock not deleted; next PowerDisplay.exe startup will retry.
                Environment.Exit(1);
            }

```

- [ ] **Step 3: 编译 PowerDisplay 项目**

Run: `dotnet build src/modules/powerdisplay/PowerDisplay/PowerDisplay.csproj -c Debug --no-restore`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs
git commit -m "feat(powerdisplay): add Phase 0 crash detection at app startup"
```

---

## Task 12: 在 DdcCiController 加 debug-only crash 注入开关（QA 用）

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs`

- [ ] **Step 1: 在 FetchCapabilitiesInParallelAsync 入口加注入**

找到（[DdcCiController.cs:400-412](src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs#L400-L412)）：

```csharp
        private async Task<(CandidateMonitor Candidate, DdcCiValidationResult Result)[]> FetchCapabilitiesInParallelAsync(
            List<CandidateMonitor> candidates,
            CancellationToken cancellationToken)
        {
            var tasks = candidates.Select(candidate =>
                Task.Run(
                    () => (Candidate: candidate, Result: DdcCiNative.FetchCapabilities(candidate.Handle)),
                    cancellationToken));

            var results = await Task.WhenAll(tasks);

            return results;
        }
```

在方法体最开头插入：

```csharp
#if DEBUG
            if (Environment.GetEnvironmentVariable("POWERDISPLAY_SIMULATE_CRASH") == "1")
            {
                // Debug-only: simulate a hard process kill to test the crash recovery
                // pipeline without actually invoking the kernel BSOD path. FailFast does
                // not run finally blocks, so the discovery.lock written by
                // CrashDetectionScope will survive — just like a real BSOD.
                Logger.LogWarning("DEBUG: POWERDISPLAY_SIMULATE_CRASH=1 — invoking FailFast");
                Environment.FailFast("Simulated crash for quarantine testing");
            }
#endif

```

- [ ] **Step 2: 编译验证**

Run: `dotnet build src/modules/powerdisplay/PowerDisplay.Lib/PowerDisplay.Lib.csproj -c Debug --no-restore`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs
git commit -m "feat(powerdisplay): add debug-only crash injection for QA"
```

---

## Task 13: 在 PowerDisplayViewModel 加 IsCrashLockActive 与 DismissCrashWarningCommand

**Files:**
- Modify: `src/settings-ui/Settings.UI/ViewModels/PowerDisplayViewModel.cs`

- [ ] **Step 1: 加 using 引用**

文件顶部 using 段加：
```csharp
using System.IO;
using PowerDisplay.Models;
```
（如果没有的话）

- [ ] **Step 2: 在类的字段段加 backing field 与 property**

找到 `private bool _isEnabled;`（构造器附近）那个字段，下方追加：

```csharp
        private bool _isCrashLockActive;

        public bool IsCrashLockActive
        {
            get => _isCrashLockActive;
            private set
            {
                if (_isCrashLockActive != value)
                {
                    _isCrashLockActive = value;
                    OnPropertyChanged(nameof(IsCrashLockActive));
                }
            }
        }

        public ButtonClickCommand DismissCrashWarningCommand => new ButtonClickCommand(DismissCrashWarning);

        private void DismissCrashWarning()
        {
            try
            {
                var path = PowerDisplayPaths.CrashDetectedFlagPath;
                if (File.Exists(path))
                {
                    File.Delete(path);
                    Logger.LogInfo("PowerDisplayViewModel: user dismissed crash warning, flag deleted");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"PowerDisplayViewModel: failed to delete crash flag: {ex.Message}");
            }

            IsCrashLockActive = false;
        }
```

- [ ] **Step 3: 在构造器末尾读 flag 初始化属性**

找到构造器中（[PowerDisplayViewModel.cs:75](src/settings-ui/Settings.UI/ViewModels/PowerDisplayViewModel.cs#L75) 附近）现有逻辑结束的地方，追加：

```csharp
            // Crash quarantine state — show error InfoBar + lock the page if PowerDisplay.exe
            // detected a previous-session crash and wrote the flag.
            if (File.Exists(PowerDisplayPaths.CrashDetectedFlagPath))
            {
                Logger.LogInfo("PowerDisplayViewModel: crash flag present, locking page");
                IsCrashLockActive = true;
            }
```

- [ ] **Step 4: 确保 Logger 已被 using 引用**

文件顶部如果没有 `using ManagedCommon;`，加上。

- [ ] **Step 5: 编译 Settings.UI**

Run: `dotnet build src/settings-ui/Settings.UI/PowerToys.Settings.csproj -c Debug --no-restore`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add src/settings-ui/Settings.UI/ViewModels/PowerDisplayViewModel.cs
git commit -m "feat(powerdisplay): IsCrashLockActive + DismissCrashWarningCommand in ViewModel"
```

---

## Task 14: 在 Resources.resw 加本地化字符串

**Files:**
- Modify: `src/settings-ui/Settings.UI/Strings/en-us/Resources.resw`

- [ ] **Step 1: 在 Resources.resw 末尾（最后一个 `</data>` 之后、`</root>` 之前）追加 3 条**

```xml
  <data name="PowerDisplay_CrashDetectedInfoBar.Title" xml:space="preserve">
    <value>PowerDisplay was automatically disabled</value>
    <comment>Title of the crash recovery InfoBar shown on PowerDisplay settings page after a detected crash.</comment>
  </data>
  <data name="PowerDisplay_CrashDetectedInfoBar.Message" xml:space="preserve">
    <value>A system crash was detected during the previous PowerDisplay session. PowerDisplay has been disabled to prevent another crash. If you understand the risk, click Ignore to dismiss this warning, then re-enable PowerDisplay manually.</value>
    <comment>Body text of the crash recovery InfoBar.</comment>
  </data>
  <data name="PowerDisplay_CrashDetected_IgnoreButton.Content" xml:space="preserve">
    <value>Ignore</value>
    <comment>Action button on the crash recovery InfoBar that dismisses the warning and unlocks the PowerDisplay settings page.</comment>
  </data>
```

- [ ] **Step 2: 编译 Settings.UI（resw 改动需要重新生成 .pri）**

Run: `dotnet build src/settings-ui/Settings.UI/PowerToys.Settings.csproj -c Debug --no-restore`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/settings-ui/Settings.UI/Strings/en-us/Resources.resw
git commit -m "feat(powerdisplay): add localization strings for crash recovery InfoBar"
```

---

## Task 15: 在 PowerDisplayPage.xaml 加 InfoBar + 锁定 wrapper

**Files:**
- Modify: `src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml`

- [ ] **Step 1: 重构根 StackPanel**

找到（[PowerDisplayPage.xaml:33-36](src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml#L33-L36)）：

```xml
    <controls:SettingsPageControl x:Uid="PowerDisplay" ModuleImageSource="ms-appx:///Assets/Settings/Modules/PowerDisplay.png">
        <controls:SettingsPageControl.ModuleContent>
            <StackPanel ChildrenTransitions="{StaticResource SettingsCardsAnimations}">
                <controls:GPOInfoControl ShowWarning="{x:Bind ViewModel.IsEnabledGpoConfigured, Mode=OneWay}">
```

把外层 StackPanel 那一行改成嵌套两层。把：
```xml
            <StackPanel ChildrenTransitions="{StaticResource SettingsCardsAnimations}">
                <controls:GPOInfoControl ShowWarning="{x:Bind ViewModel.IsEnabledGpoConfigured, Mode=OneWay}">
```
改成：
```xml
            <StackPanel ChildrenTransitions="{StaticResource SettingsCardsAnimations}">
                <!-- Crash recovery banner. Outside the inner StackPanel so the Ignore button stays clickable when the rest of the page is locked. -->
                <InfoBar
                    x:Uid="PowerDisplay_CrashDetectedInfoBar"
                    IsClosable="False"
                    IsOpen="{x:Bind ViewModel.IsCrashLockActive, Mode=OneWay}"
                    Severity="Error">
                    <InfoBar.ActionButton>
                        <Button
                            x:Uid="PowerDisplay_CrashDetected_IgnoreButton"
                            Command="{x:Bind ViewModel.DismissCrashWarningCommand}" />
                    </InfoBar.ActionButton>
                </InfoBar>

                <!-- Inner wrapper: everything below is locked when crash flag is present. -->
                <StackPanel IsEnabled="{x:Bind ViewModel.IsCrashLockActive, Mode=OneWay, Converter={StaticResource BoolNegationConverter}}">
                    <controls:GPOInfoControl ShowWarning="{x:Bind ViewModel.IsEnabledGpoConfigured, Mode=OneWay}">
```

- [ ] **Step 2: 在最末尾加上对应的关闭标签**

找到（[PowerDisplayPage.xaml:316-318](src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml#L316-L318)）：

```xml
                </controls:SettingsGroup>
            </StackPanel>
        </controls:SettingsPageControl.ModuleContent>
```

把它改成：

```xml
                </controls:SettingsGroup>
                </StackPanel>  <!-- close inner lockable wrapper -->
            </StackPanel>
        </controls:SettingsPageControl.ModuleContent>
```

- [ ] **Step 3: 编译 Settings.UI 验证 XAML**

Run: `dotnet build src/settings-ui/Settings.UI/PowerToys.Settings.csproj -c Debug --no-restore`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml
git commit -m "feat(powerdisplay): InfoBar + page lockdown in Settings UI"
```

---

## Task 16: 验证 settings 备份排除新 artifact

**Files:**
- Read-only audit: `src/settings-ui/Settings.UI.Library/SettingsBackupAndRestoreUtils.cs`

崩溃恢复用的两个文件不应被 PowerToys 设置备份功能纳入备份；如果被纳入，恢复一个老备份会把 `crash_detected.flag` 还原回来，体验异常。需要检查 `SettingsBackupAndRestoreUtils.GetSettingsFiles` 的 glob 模式。

- [ ] **Step 1: 阅读 SettingsBackupAndRestoreUtils.GetSettingsFiles 的 glob 规则**

Run: `grep -n "GetSettingsFiles\|\\*\\.json\\|MonitorState\\|profiles" src/settings-ui/Settings.UI.Library/SettingsBackupAndRestoreUtils.cs | head -20`
仔细看 glob 是否包括 `*.json` / `*.lock` / `*.flag`。

- [ ] **Step 2: 决定动作**

判断准则：
- 如果 glob 只包含 `settings.json` / `*.json`，那 `crash_detected.flag` 和 `discovery.lock` 都不会被匹配（不同后缀），**无需改动**。
- 如果 glob 包含 `*.*` 或 `*.flag` / `*.lock`，需要在排除列表里加这两个文件名。

如果第一种：跳到 Step 3 直接 commit 一个空说明（或不 commit）。
如果第二种：编辑文件加 exclusion，然后 commit。

- [ ] **Step 3: 记录结论（如无需改动）**

如果无需改动，给设计文档第 9 节"备份/恢复"那一节加一行确认即可；或就此跳过、在 PR 描述里说明 "verified: backup utility uses settings.json glob, .lock/.flag files not included"。

- [ ] **Step 4: 全量构建 + 全部测试做最终回归**

Run:
```bash
dotnet build src/modules/powerdisplay/PowerDisplay.Lib/PowerDisplay.Lib.csproj -c Debug
dotnet test src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplay.Lib.UnitTests.csproj
dotnet build src/settings-ui/Settings.UI/PowerToys.Settings.csproj -c Debug
```

Expected: 全部 Build succeeded、全部测试通过。

- [ ] **Step 5: 端到端手工 QA（可选但强烈建议）**

按设计文档 § 7.2 的脚本：
1. 整 solution 编译 Debug 版（`msbuild PowerToys.sln -p:Configuration=Debug -p:Platform=x64`）
2. `set POWERDISPLAY_SIMULATE_CRASH=1`，启动 PowerToys runner
3. 等 PowerDisplay 启动 → 进 Phase 2 → FailFast 进程死
4. 验证 `%LOCALAPPDATA%\Microsoft\PowerToys\PowerDisplay\discovery.lock` 存在
5. `set POWERDISPLAY_SIMULATE_CRASH=`（清掉），重启 PowerToys（或等 runner 重新拉起 PowerDisplay.exe）
6. 验证 `crash_detected.flag` 被写、`%LOCALAPPDATA%\Microsoft\PowerToys\settings.json` 中 `enabled.PowerDisplay=false`、`discovery.lock` 已删
7. 打开 PowerToys Settings → PowerDisplay 页 → 看到红色 InfoBar、整页 disabled、Ignore 按钮可点
8. 点 Ignore → InfoBar 消失，页面变可交互、toggle 仍 OFF
9. 翻 toggle 到 ON → PowerDisplay.exe 正常启动（这次因为 env var 已清，不会 FailFast）

- [ ] **Step 6: 最终 commit（如有 audit 修复）**

```bash
git add -A
git commit -m "chore(powerdisplay): verify backup utility excludes crash artifacts"
```

如果 Step 3 决定无需改动，这步跳过。

---

## 总收尾

到这里所有功能已实现。请检查：

* `git log --oneline yuleng/pd/f/crash/1` 应有约 14-15 个原子 commit
* `dotnet test src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/` 全绿
* 端到端 QA 走过一遍

下一步可走 [superpowers:finishing-a-development-branch](https://) 或 [superpowers:requesting-code-review](https://)。
