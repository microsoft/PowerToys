# PowerDisplay Crash Quarantine — Design

**Status:** Draft for review
**Branch:** `yuleng/pd/f/crash/1`
**Author:** yuleng@microsoft.com
**Date:** 2026-05-06
**Related issue:** [microsoft/PowerToys#47556](https://github.com/microsoft/PowerToys/issues/47556)

## 1. Background

GitHub issue #47556 reports a `KERNEL_SECURITY_CHECK_FAILURE (0x139, subcode 0x2)` BSOD originating in `win32kfull!CPhysicalMonitorHandle::DdcciGetCapabilitiesStringFromMonitor`. The crash is reproducible on Windows 11 build 26200.8328 with PowerToys 0.99.1.0 when a particular LG monitor (LG 27MR400 with malformed EDID) is attached. A second user reports a related symptom (no BSOD but the LG firmware's display rendering pipeline gets wedged after sleep/resume) on a different LG model.

Both reports point to the same kernel-side path: PowerDisplay invokes `GetCapabilitiesStringLength` / `CapabilitiesRequestAndCapabilitiesReply` (Dxva2.dll) on a non-conformant monitor, and the kernel function corrupts its own stack while parsing the malformed DDC/CI capability string.

**The root cause is in the Windows kernel** (`win32kfull`) and must be fixed by the Windows team. PowerToys cannot patch the kernel. This design covers a *mitigation* on the PowerToys side: **detect that PowerDisplay was responsible for a crash and refuse to repeat the dangerous operation until the user explicitly acknowledges the risk**.

Mitigation goal: any user who has experienced this BSOD once will not experience it again automatically. After the first crash, PowerDisplay disables itself, displays an error InfoBar in Settings UI, and locks the entire PowerDisplay settings page. The user must explicitly click "Ignore" to dismiss the warning, then manually re-enable PowerDisplay. If the offending monitor is still attached, re-enabling will trigger the crash again — but at that point it's the user's informed choice.

## 2. Scope

### In scope (Scope A, agreed during brainstorming)

* **Capability fetch** is the only DDC/CI path protected by the crash-detection mechanism. Specifically:
  * `Dxva2.dll!GetCapabilitiesStringLength` ([PInvoke.cs:107-109](src/modules/powerdisplay/PowerDisplay.Lib/Drivers/PInvoke.cs#L107-L109))
  * `Dxva2.dll!CapabilitiesRequestAndCapabilitiesReply` ([PInvoke.cs:111-116](src/modules/powerdisplay/PowerDisplay.Lib/Drivers/PInvoke.cs#L111-L116))
* These are invoked exclusively from `DdcCiNative.FetchCapabilities` ([DdcCiNative.cs:27-82](src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiNative.cs#L27-L82)) and `DdcCiController.GetCapabilitiesStringAsync` ([DdcCiController.cs:179-235](src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs#L179-L235)). The protected window is the parallel fetch in `DdcCiController.FetchCapabilitiesInParallelAsync` ([DdcCiController.cs:400-412](src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs#L400-L412)).

### Out of scope

* **VCP get/set** (`SetVCPFeature`, `GetVCPFeatureAndVCPFeatureReply`). These are hot-path operations (the brightness slider triggers many per second). Adding crash detection here would require synchronous disk flushes per VCP call, with unacceptable latency. The reported BSOD is not in this path. Decision rationale: brainstorming round 1 — chose A over B (which covered all VCP ops) after analyzing the L3-flush cost on the slider hot path.
* **Per-monitor quarantine.** We do not attempt to identify which specific monitor caused the crash. Phase 2 fetches capabilities for all candidate monitors in parallel, so multiple BEGIN markers would be in flight simultaneously when a BSOD occurs — there is no reliable way to attribute the crash to a single monitor. Instead we disable the entire PowerDisplay module. Decision rationale: brainstorming round 2.
* **Telemetry of crash events.** No telemetry hook in v1.
* **Listing offending monitors in the InfoBar.** The InfoBar text is generic ("crash detected, auto-disabled"). Decision rationale: explicit user request during brainstorming.
* **Kernel-side fix.** That requires a Windows update from the win32k team.

### Non-goals

* Preventing every possible PowerDisplay-related crash. We only protect the documented BSOD path. PowerDisplay.exe could still crash for unrelated reasons (e.g., bugs in our C# code) and those are outside this design.
* Auto-recovery. After Ignore + re-enable with the offending monitor still attached, the system will BSOD again. We do not try to be clever about this — the user has been warned.

## 3. Architecture

### 3.1 Disk artifacts

Both files live under `%LOCALAPPDATA%\Microsoft\PowerToys\PowerDisplay\` (PathConstants.PowerDisplayFolderPath).

| File | Lifetime | Purpose |
|---|---|---|
| `discovery.lock` | Written before Phase 2 begins; deleted after Phase 2 completes (or on any non-crash exit). Survives only if the process was killed externally (BSOD, TerminateProcess, FailFast). | "We were inside the dangerous code path." Existence at next startup ⇒ previous run crashed. Internal mechanism only — not user-visible. |
| `crash_detected.flag` | Written by PowerDisplay.exe Phase 0 when an orphan `discovery.lock` is found; deleted when the user clicks Ignore in Settings UI. | UI signal. Settings UI reads this to decide whether to render the error InfoBar and lock the page. |

Both files are JSON with a `version` field for forward compatibility. The version is currently `1`. Phase 0 treats any unexpected version as "format unknown ⇒ act conservatively, treat as orphan".

```jsonc
// discovery.lock
{
  "version": 1,
  "pid": 12345,
  "startedAt": "2026-05-06T10:00:00Z"
}

// crash_detected.flag
{
  "version": 1,
  "detectedAt": "2026-05-06T10:01:23Z"
}
```

The `pid` and timestamp fields are diagnostic only — they appear in logs to help triage user reports but are not consulted in decision logic. The detection rule is "lock file exists ⇒ orphan".

### 3.2 Component overview

```
┌──────────────────────────────────────────────────────────────┐
│  runner.exe process                                          │
│                                                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  PowerDisplayModuleInterface.dll (loaded into runner)│   │
│  │                                                       │   │
│  │  • [NEW] AutoDisable listener thread waits on         │   │
│  │    POWER_DISPLAY_AUTO_DISABLE_EVENT                   │   │
│  │  • Existing toggle/refresh listeners unchanged        │   │
│  └──────┬───────────────────────────────────────────────┘   │
│         │ spawns (existing)                                  │
└─────────┼───────────────────────────────────────────────────┘
          │
          ▼
┌──────────────────────────────────────────────────────────────┐
│  PowerDisplay.exe (C# WinUI app)                             │
│                                                              │
│  Startup:                                                    │
│    [NEW] CrashRecovery.DetectOrphanAndDisable()             │
│      → if orphan lock: write flag, write settings.json,     │
│        signal event, delete lock, Exit(0)                   │
│                                                              │
│  Discovery:                                                  │
│    DdcCiController.DiscoverMonitorsAsync()                  │
│      Phase 1 (GDI enumerate)        — safe                  │
│      [NEW] using (CrashDetectionScope.Begin())               │
│        Phase 2 (FetchCapabilitiesInParallelAsync) — DANGER   │
│      Phase 3 (CreateValidMonitors) — safe                   │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│  PowerToys Settings UI process (separate, on-demand)         │
│                                                              │
│  PowerDisplayViewModel:                                      │
│    [NEW] On construction: read crash_detected.flag           │
│    [NEW] IsCrashLockActive property drives:                  │
│      - Top error InfoBar visibility                          │
│      - Whole-page IsEnabled binding (page locked)            │
│    [NEW] DismissCrashWarning command deletes flag            │
└──────────────────────────────────────────────────────────────┘
```

### 3.3 Five components

1. **DdcCiController (C#)** — wraps Phase 2 in a `using (CrashDetectionScope.Begin())` block. Phase 3 is outside the scope.
2. **PowerDisplay.exe Phase 0 (C#)** — at app startup, before any DDC/CI activity, calls `CrashRecovery.DetectOrphanAndDisable()`. If true, exit immediately.
3. **CommonSharedConstants (C++)** — adds `POWER_DISPLAY_AUTO_DISABLE_EVENT` constant.
4. **PowerDisplayModuleInterface DLL (C++)** — adds a listener thread waiting on the new event; on signal, calls `this->disable()` to align runner-internal state with the (already-on-disk) disabled state.
5. **PowerDisplayViewModel + PowerDisplayPage XAML (C#/XAML)** — adds `IsCrashLockActive` property, top error InfoBar, page-wide IsEnabled lock-out, Ignore button.

## 4. Detailed design

### 4.1 `CrashDetectionScope` (new class)

**Location:** `src/modules/powerdisplay/PowerDisplay.Lib/Services/CrashDetectionScope.cs`

**Responsibility:** Manage the `discovery.lock` file's lifetime around Phase 2. Single responsibility — does NOT do detection, does NOT write any other file.

**API:**

```csharp
public sealed class CrashDetectionScope : IDisposable
{
    public static CrashDetectionScope Begin();
    public void Dispose();
}
```

**Implementation contract:**

* `Begin()` writes `discovery.lock` using `FileMode.CreateNew` (fails if file already exists — defensive), `FileOptions.WriteThrough`, and an explicit `Flush(flushToDisk: true)` after the write. Throws `IOException` on file system failure or if the lock already exists.
* `Begin()` writes the JSON payload with `version`, `pid`, `startedAt`. Content is for diagnostics only; existence is what matters.
* `Dispose()` deletes `discovery.lock`. If deletion fails, log a warning but do not rethrow — the worst-case consequence is a single false-positive quarantine on next start, recoverable by the user.
* `Dispose()` is idempotent (a `_disposed` guard).

**Caller pattern:**

```csharp
// DdcCiController.DiscoverMonitorsAsync
(CandidateMonitor, DdcCiValidationResult)[] fetchResults;
using (CrashDetectionScope.Begin())
{
    fetchResults = await FetchCapabilitiesInParallelAsync(candidates, cancellationToken);
}
return CreateValidMonitors(fetchResults);
```

The `using` block (not `using var`) ensures the lock is released *immediately after* Phase 2 completes, not at end-of-method. Phase 3 (`CreateValidMonitors`) executes outside the scope so its own DDC/CI calls (VCP gets to initialize input source, color temperature) are not covered — consistent with Scope A.

**Why `FileMode.CreateNew` instead of `Create`:** if a stray `discovery.lock` exists at Phase 2 entry, that means either (a) Phase 0 detection failed to clean it up, or (b) two PowerDisplay.exe instances are running concurrently. Both are bugs. Failing fast is better than overwriting silently.

### 4.2 `CrashRecovery` (new static class)

**Location:** `src/modules/powerdisplay/PowerDisplay.Lib/Services/CrashRecovery.cs`

**Responsibility:** One-shot Phase 0 detection. Independent from `CrashDetectionScope`.

**API:**

```csharp
public static class CrashRecovery
{
    /// <summary>
    /// Run at PowerDisplay.exe startup before any DDC/CI activity.
    /// If an orphan discovery.lock is found, executes the strict auto-disable
    /// sequence (write flag, write settings.json, signal event, delete lock).
    /// </summary>
    /// <returns>true if orphan was detected and handled; caller should exit immediately.</returns>
    /// <exception>Throws on any sequence-step failure (strict fail-fast).</exception>
    public static bool DetectOrphanAndDisable();
}
```

**Strict fail-fast sequence (must execute in this exact order):**

1. **Write `crash_detected.flag`** (UI signal)
2. **Write global `settings.json`** with `enabled.PowerDisplay = false` (persistent disable)
3. **Signal `POWER_DISPLAY_AUTO_DISABLE_EVENT`** (live runner-internal state sync)
4. **Delete `discovery.lock`** (commit point)

If any of steps 1–3 throws, the lock is **not** deleted. The exception propagates up; `Program.Main` should `Environment.Exit(1)`. Next startup re-runs the detection and retries the entire sequence — self-healing via the lock-as-commit-point pattern.

**Step ordering rationale:**

| Step | Failure consequence (if subsequent steps not executed) | Self-healing on retry |
|---|---|---|
| 1 fails | No UI banner this start; lock remains | Retry; full recovery |
| 2 fails | Settings.json still says enabled; runner re-spawns next boot; lock remains | Retry; eventually succeeds |
| 3 fails | Current runner session not synced; user sees toggle OFF + InfoBar; flipping toggle requires OFF→ON cycle (because runner m_enabled still true). But settings.json was written, so next runner restart is consistent. | Retry on next start; cleaner state |
| 4 must be last | If 4 ran before 1–3, we'd lose the "this was a crash" evidence and Phase 1/2 would proceed normally on the next restart, potentially BSOD-ing again | N/A — this is the final commit |

**Deletion of `discovery.lock` is the commit point.** Until the lock is deleted, the sequence is "pending"; any failure leaves the system in a recoverable state.

**No partial fault tolerance.** Per design decision, individual step failures are *not* swallowed. If we cannot reliably write the flag or settings.json, we cannot reliably claim "PowerDisplay is disabled" — better to leave evidence for the next attempt than to fake success.

### 4.3 `CommonSharedConstants` addition (C++)

**Location:** `src/common/interop/shared_constants.h`

Add a single `constexpr` string constant:

```cpp
// Naming follows existing convention (see TOGGLE_POWER_DISPLAY_EVENT, REFRESH_POWER_DISPLAY_MONITORS_EVENT).
inline constexpr wchar_t POWER_DISPLAY_AUTO_DISABLE_EVENT[] =
    L"Local\\PowerToysPowerDisplay-AutoDisable-Event-{insert-uuid-when-implementing}";
```

The UUID suffix is a fresh GUID generated at implementation time (mirroring the pattern used by `LIGHT_SWITCH_LIGHT_THEME_EVENT` etc. in [PathConstants.cs:84](src/modules/powerdisplay/PowerDisplay.Lib/PathConstants.cs#L84)).

The same constant must be exposed to C# in `PathConstants.cs` (or a new `EventConstants.cs`) so `CrashRecovery` can signal it from PowerDisplay.exe.

### 4.4 PowerDisplayModuleInterface DLL changes (C++)

**Location:** `src/modules/powerdisplay/PowerDisplayModuleInterface/dllmain.cpp`

Add an event handle and listener thread mirroring the existing `m_hToggleEvent` / `m_toggleEventThread` pattern ([dllmain.cpp:75-80, 127-170](src/modules/powerdisplay/PowerDisplayModuleInterface/dllmain.cpp#L75-L80)):

```cpp
// In PowerDisplayModule class:
HANDLE m_hAutoDisableEvent = nullptr;
std::thread m_autoDisableEventThread;

// In constructor (alongside existing event creates):
m_hAutoDisableEvent = CreateDefaultEvent(CommonSharedConstants::POWER_DISPLAY_AUTO_DISABLE_EVENT);

// New listener thread, started in enable() and stopped in disable():
void StartAutoDisableEventListener();
void StopAutoDisableEventListener();
```

The listener body:

```cpp
m_autoDisableEventThread = std::thread([this]() {
    HANDLE handles[] = { m_hAutoDisableEvent, m_hStopEvent };
    while (true) {
        DWORD result = WaitForMultipleObjects(2, handles, FALSE, INFINITE);
        if (result == WAIT_OBJECT_0) {
            Logger::warn(L"PowerDisplay AutoDisable event received — disabling module");
            // Call own disable() — this sets m_enabled=false (which runner queries via is_enabled())
            // and stops the process manager (PowerDisplay.exe has already self-exited).
            this->disable();
            // Note: we don't write settings.json here. PowerDisplay.exe Phase 0 already did that.
            break;  // one-shot — listener exits; will be recreated if module is re-enabled
        }
        else {
            break;  // stop event signaled
        }
    }
});
```

**Why one-shot:** after disable() runs, the listener has done its job. If the user later re-enables PowerDisplay (which calls `enable()`), a fresh listener can be started. This avoids ambiguity about "what does it mean to receive an AutoDisable event when the module is already disabled".

**Lifecycle:**

* `enable()` — creates the event handle (already created in constructor), starts the listener thread.
* `disable()` — already does `StopToggleEventListener`; we add `StopAutoDisableEventListener` alongside.
* Constructor / destructor manage the event handle lifetime.

### 4.5 PowerDisplay.exe Phase 0 wiring (C#)

**Location:** `PowerDisplay.exe` startup. Likely in `App.xaml.cs::OnLaunched` or a new bootstrap method called before any window is constructed. Implementation detail to be confirmed during planning.

```csharp
protected override void OnLaunched(LaunchActivatedEventArgs args)
{
    // Phase 0: Crash recovery. Must run before ANY DDC/CI initialization.
    try
    {
        if (CrashRecovery.DetectOrphanAndDisable())
        {
            Logger.LogWarning("Phase 0: orphan discovery.lock detected; auto-disable sequence executed; exiting.");
            Environment.Exit(0);
        }
    }
    catch (Exception ex)
    {
        Logger.LogError($"Phase 0: auto-disable sequence failed: {ex}");
        // Lock was not deleted; next startup will retry.
        Environment.Exit(1);
    }

    // ... existing OnLaunched logic ...
}
```

### 4.6 PowerDisplay.Lib changes — DdcCiController integration

**Location:** [DdcCiController.cs:264-298](src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs#L264-L298)

Modify `DiscoverMonitorsAsync` to wrap Phase 2:

```csharp
public async Task<IEnumerable<Monitor>> DiscoverMonitorsAsync(CancellationToken cancellationToken = default)
{
    try
    {
        var allMonitorDisplayInfo = DdcCiNative.GetAllMonitorDisplayInfo();

        var monitorHandles = EnumerateMonitorHandles();
        if (monitorHandles.Count == 0) return Enumerable.Empty<Monitor>();

        var candidates = await CollectCandidateMonitorsAsync(
            monitorHandles, allMonitorDisplayInfo, cancellationToken);
        if (candidates.Count == 0) return Enumerable.Empty<Monitor>();

        // Phase 2: protected by crash-detection scope.
        (CandidateMonitor Candidate, DdcCiValidationResult Result)[] fetchResults;
        using (CrashDetectionScope.Begin())
        {
            fetchResults = await FetchCapabilitiesInParallelAsync(candidates, cancellationToken);
        }

        // Phase 3: outside scope.
        return CreateValidMonitors(fetchResults);
    }
    catch (Exception ex)
    {
        Logger.LogError($"DDC: DiscoverMonitorsAsync exception: {ex.Message}\nStack: {ex.StackTrace}");
        return Enumerable.Empty<Monitor>();
    }
}
```

The `using` block boundary is exact — Phase 1 happens before Begin(), Phase 3 happens after Dispose(). This matches Scope A.

### 4.7 PathConstants additions

**Location:** [PathConstants.cs](src/modules/powerdisplay/PowerDisplay.Lib/PathConstants.cs)

Add three new path/event accessors:

```csharp
public const string DiscoveryLockFileName = "discovery.lock";
public const string CrashDetectedFlagFileName = "crash_detected.flag";

public static string DiscoveryLockPath
    => Path.Combine(PowerDisplayFolderPath, DiscoveryLockFileName);

public static string CrashDetectedFlagPath
    => Path.Combine(PowerDisplayFolderPath, CrashDetectedFlagFileName);

public const string AutoDisableEventName =
    "Local\\PowerToysPowerDisplay-AutoDisable-Event-{matches-shared_constants.h-uuid}";
```

The `AutoDisableEventName` value must exactly match `POWER_DISPLAY_AUTO_DISABLE_EVENT` in `shared_constants.h`. We rely on the existing pattern (string constant duplication between C++ and C#) used by `LightSwitchLightThemeEventName` etc.

### 4.8 Settings UI — PowerDisplayViewModel (C#)

**Location:** [PowerDisplayViewModel.cs](src/settings-ui/Settings.UI/ViewModels/PowerDisplayViewModel.cs)

Add:

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
        var path = /* CrashDetectedFlagPath — see Section 4.10 */;
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
    catch (Exception ex)
    {
        Logger.LogError($"DismissCrashWarning: failed to delete flag: {ex.Message}");
    }
    IsCrashLockActive = false;
}
```

In the constructor, after existing initialization:

```csharp
IsCrashLockActive = File.Exists(/* CrashDetectedFlagPath */);
```

The IsEnabled toggle setter is **not** modified — when `IsCrashLockActive` is true, the entire page (including the toggle) is disabled by the XAML binding. The user must click Ignore first.

### 4.9 Settings UI — PowerDisplayPage XAML

**Location:** [PowerDisplayPage.xaml](src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml)

Two changes:

**Change 1:** Add the InfoBar at the top of `ModuleContent`'s root StackPanel (above the existing GPOInfoControl):

```xml
<StackPanel ChildrenTransitions="{StaticResource SettingsCardsAnimations}">
    <!-- NEW: Crash recovery banner -->
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

    <!-- Existing content, but wrapped to be disabled when locked -->
    <StackPanel
        IsEnabled="{x:Bind ViewModel.IsCrashLockActive, Mode=OneWay, Converter={StaticResource BoolNegationConverter}}">
        <controls:GPOInfoControl ...>
            ...
        </controls:GPOInfoControl>
        <!-- All existing SettingsGroup elements remain inside this inner StackPanel -->
    </StackPanel>
</StackPanel>
```

**Change 2:** Add localized resources to `Settings.UI/Strings/en-us/Resources.resw`:

| Resource key | Value (en-US) |
|---|---|
| `PowerDisplay_CrashDetectedInfoBar.Title` | `PowerDisplay was automatically disabled` |
| `PowerDisplay_CrashDetectedInfoBar.Message` | `A system crash was detected during the previous PowerDisplay session. PowerDisplay has been disabled to prevent another crash. If you understand the risk, click Ignore to dismiss this warning, then re-enable PowerDisplay manually.` |
| `PowerDisplay_CrashDetected_IgnoreButton.Content` | `Ignore` |

**Why InfoBar is outside the inner disabled StackPanel:** the InfoBar itself (and especially the Ignore button) must remain interactive even when the rest of the page is locked. Putting it outside the IsEnabled-bound container achieves this naturally.

### 4.10 Sharing the file path between PowerDisplay.Lib and Settings.UI

**Problem:** Settings UI's `PowerDisplayViewModel` lives in `src/settings-ui/Settings.UI/`, which by default does not reference `PowerDisplay.Lib`. The `crash_detected.flag` path is defined in `PowerDisplay.Lib/PathConstants.cs`.

**Resolution:** Add a small `PowerDisplayPaths` static class to `PowerDisplay.Models` (which Settings UI **does** reference — see [PowerDisplayPage.xaml:10](src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml#L10)). This class exposes only the path-derivation logic needed by the UI:

```csharp
// PowerDisplay.Models/PowerDisplayPaths.cs
public static class PowerDisplayPaths
{
    public static string CrashDetectedFlagPath
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "PowerToys", "PowerDisplay", "crash_detected.flag");
}
```

`PathConstants` in `PowerDisplay.Lib` continues to be the source of truth for runtime; it can either reference `PowerDisplayPaths` or duplicate the literal. Implementation note for the plan: pick one and stick with it; do not let the two diverge.

## 5. End-to-end flow

### 5.1 Happy path (no crash)

1. PowerDisplay.exe launches.
2. Phase 0: `CrashRecovery.DetectOrphanAndDisable()` checks for `discovery.lock` → not found → returns false.
3. Normal startup continues.
4. `DdcCiController.DiscoverMonitorsAsync` runs:
   * Phase 1: GDI enumerate.
   * `CrashDetectionScope.Begin()` writes `discovery.lock`.
   * Phase 2: `FetchCapabilitiesInParallelAsync` runs.
   * On normal completion or exception, `Dispose()` deletes `discovery.lock`.
   * Phase 3: `CreateValidMonitors` (lock already gone).
5. PowerDisplay normal operation.

### 5.2 BSOD path

1. PowerDisplay.exe launches; Phase 0 finds no lock; normal startup.
2. `DdcCiController.DiscoverMonitorsAsync`: Phase 1 → `CrashDetectionScope.Begin()` writes lock with `WriteThrough+Flush(true)` → Phase 2 starts → BSOD inside `win32kfull!DdcciGetCapabilitiesStringFromMonitor`.
3. System hard-reboots.
4. Windows boots; runner reads global `settings.json` (still has `enabled.PowerDisplay=true`) → enables PowerDisplay module → spawns PowerDisplay.exe.
5. PowerDisplay.exe launches; Phase 0 finds **orphan** `discovery.lock` from step 2.
6. `CrashRecovery.DetectOrphanAndDisable()` runs strict sequence:
   * Writes `crash_detected.flag`.
   * Writes global `settings.json` with `enabled.PowerDisplay=false`.
   * Signals `POWER_DISPLAY_AUTO_DISABLE_EVENT` → runner-internal listener thread (in module DLL) wakes up → calls `this->disable()` → `m_enabled=false`, `m_processManager.stop()` (PowerDisplay.exe will exit shortly anyway).
   * Deletes `discovery.lock` (commit point).
   * Returns true.
7. PowerDisplay.exe `Environment.Exit(0)`.
8. From this point: `enabled.PowerDisplay=false` on disk, `m_enabled=false` in runner, `crash_detected.flag` exists. Consistent state.

### 5.3 User opens Settings UI

9. User opens Settings UI (separate process; reads `settings.json` fresh).
10. PowerDisplayPage navigated to → PowerDisplayViewModel constructed.
11. ViewModel reads `crash_detected.flag` → `IsCrashLockActive = true`.
12. ViewModel reads `_isEnabled = GeneralSettingsConfig.Enabled.PowerDisplay` → false.
13. UI renders:
    * Top InfoBar (Severity=Error) visible with "Ignore" button.
    * **Entire page below the InfoBar is disabled** (binding to `!IsCrashLockActive`). Toggle, monitor list, profiles, custom mappings — all greyed out and unclickable.
    * Only the Ignore button is interactive.

### 5.4 User dismisses warning and re-enables

14. User clicks Ignore → `DismissCrashWarningCommand` → flag deleted, `IsCrashLockActive=false`.
15. Page becomes interactive. Toggle is OFF (settings.json says false; runner agrees because step 6 synced it).
16. User flips toggle to ON → existing setter logic → IPC to runner with `enabled.PowerDisplay=true` → runner sees target=true, `module->is_enabled()=false` (because step 6 set it) → not equal → `enable()` called → PowerDisplay.exe spawns.
17. PowerDisplay.exe Phase 0 (no lock, no flag now) → normal startup → discovery → if offending monitor still attached, BSOD again → loop back to step 2.

### 5.5 Edge case: PowerDisplay.exe crashes (not BSOD)

If PowerDisplay.exe itself crashes (e.g., unhandled C# exception) inside Phase 2:

* Lock is left on disk (no Dispose ran).
* The module DLL's `m_processManager` does not auto-restart; runner thinks PowerDisplay is enabled but the process is gone.
* Next time `m_processManager.send_message` is called (e.g., Quick Access toggle), it detects `!is_process_running()` and calls `refresh()` → respawn PowerDisplay.exe → Phase 0 detects orphan → auto-disable sequence runs.
* From the user's perspective: the same as the BSOD path, just triggered later (when something tries to interact with PowerDisplay).

## 6. Failure modes and recovery matrix

| Scenario | What's left on disk | What happens on next start | User action needed |
|---|---|---|---|
| Phase 2 BSOD | `discovery.lock` only | Phase 0 detects → full sequence → `crash_detected.flag` + `settings.json=false` | Open Settings, click Ignore, re-enable manually |
| Phase 2 process crash (non-BSOD) | `discovery.lock` only | Same as BSOD scenario, just later (next process spawn) | Same |
| Phase 0 step 1 fails (can't write flag) | `discovery.lock` only | Retry full sequence | None initially; if persistent disk issue, user must fix that |
| Phase 0 step 2 fails (can't write settings.json) | `discovery.lock` + `crash_detected.flag` | Retry full sequence; flag write is idempotent | Same |
| Phase 0 step 3 fails (can't signal event) | `discovery.lock` + `crash_detected.flag` + `settings.json=false` | Retry: steps 1, 2 succeed (idempotent), step 3 retried, etc. | Same |
| Phase 0 step 4 fails (can't delete lock) | All four artifacts effectively in place; lock survives | Retry full sequence — duplicate writes are idempotent | Same; lock will eventually be deleted |
| User clicks Ignore but doesn't re-enable | `crash_detected.flag` deleted, `settings.json=false` | Module stays disabled across reboots | None |
| User flips toggle ON after Ignore, with offending monitor still attached | Normal startup → BSOD again | Loops back to BSOD path | User must disconnect offending monitor |

## 7. Testing strategy

### 7.1 Unit tests

**Project:** `src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/`

* `CrashDetectionScopeTests`:
  * `Begin_WritesLockFile`: verifies file is created at expected path with correct version/pid/timestamp.
  * `Begin_FailsIfLockAlreadyExists`: ensures `FileMode.CreateNew` enforces uniqueness.
  * `Dispose_DeletesLockFile`: standard happy path.
  * `Dispose_IsIdempotent`: calling Dispose twice does not throw.
  * `Dispose_DoesNotThrowOnDeleteFailure`: simulate lock held by another process → Dispose logs but does not throw.
* `CrashRecoveryTests`:
  * `DetectOrphanAndDisable_ReturnsFalseWhenNoLock`.
  * `DetectOrphanAndDisable_RunsFullSequenceWhenLockPresent`: verify flag write, settings write, event signal, lock delete in correct order.
  * `DetectOrphanAndDisable_LeavesLockIntactIfFlagWriteFails`: simulate IO failure on step 1 → ensure lock survives, exception propagates.
  * Same for failures at steps 2 and 3.
  * `DetectOrphanAndDisable_HandlesUnknownVersionAsOrphan`: forward-compat behavior.

Tests use abstract file-system / event seam (e.g., `IFileSystem`, `IEventSignaler`) so we can inject failures.

### 7.2 Integration / manual QA

**Debug-only crash injection** in `DdcCiController.FetchCapabilitiesInParallelAsync`:

```csharp
#if DEBUG
if (Environment.GetEnvironmentVariable("POWERDISPLAY_SIMULATE_CRASH") == "1")
{
    Environment.FailFast("Simulated crash for quarantine testing");
}
#endif
```

QA scenario:

1. `set POWERDISPLAY_SIMULATE_CRASH=1` and start PowerDisplay.exe.
2. PowerDisplay enters Phase 2 → FailFast → process dies hard.
3. Verify `discovery.lock` exists on disk.
4. `set POWERDISPLAY_SIMULATE_CRASH=` (clear) and restart PowerDisplay.exe.
5. Verify Phase 0 detects orphan: `crash_detected.flag` appears, `settings.json` shows `enabled.PowerDisplay=false`, `discovery.lock` deleted, PowerDisplay.exe exits with code 0.
6. Open Settings UI → PowerDisplayPage → verify error InfoBar visible, page disabled, Ignore button works.
7. Click Ignore → page becomes interactive, toggle still OFF.
8. Toggle to ON → PowerDisplay.exe spawns and runs normally (no crash this time, env var cleared).

### 7.3 Cross-component IPC test

Verify the `POWER_DISPLAY_AUTO_DISABLE_EVENT` round-trip with both processes running:

* Start PowerToys (runner + PowerDisplay module loaded).
* From a small test utility, signal the event (using its well-known name).
* Verify module's `m_enabled` becomes false (e.g., via Settings UI showing toggle as off after refresh).

This mirrors the existing pattern used to test `TOGGLE_POWER_DISPLAY_EVENT`.

## 8. Logging

Following the project's "concise hot-path silence; verbose decision points" convention:

| Location | Level | Message |
|---|---|---|
| `CrashDetectionScope.Begin()` success | Info | `CrashDetectionScope: lock written at <path>` |
| `CrashDetectionScope.Begin()` failure | Error | `CrashDetectionScope: failed to write lock at <path>: <ex>` |
| `CrashDetectionScope.Dispose()` success | Info | `CrashDetectionScope: lock deleted at <path>` |
| `CrashDetectionScope.Dispose()` failure | Warning | `CrashDetectionScope: failed to delete lock at <path>: <ex>` |
| `CrashRecovery.DetectOrphanAndDisable()` no orphan | Trace | `Phase 0: no orphan lock; normal startup` |
| `CrashRecovery.DetectOrphanAndDisable()` orphan found | Warning | `Phase 0: found orphan lock at <path> with content <json>; entering auto-disable sequence` |
| Each strict sequence step | Info | `Phase 0: step <N> (<name>) ok` |
| Strict sequence step failure | Error | `Phase 0: step <N> (<name>) failed: <ex>; sequence aborted, lock retained for retry` |
| Module DLL listener fires | Warning | `PowerDisplay AutoDisable event received — disabling module` |
| Settings UI loads with flag present | Info | `PowerDisplayViewModel: crash flag present, locking page` |
| Settings UI Ignore clicked | Info | `PowerDisplayViewModel: user dismissed crash warning, flag deleted` |

Hot paths (DDC/CI VCP get/set, refresh loop) remain log-silent.

## 9. Out-of-band considerations

* **Multi-user / RDP:** files are under `%LOCALAPPDATA%`, per-user. Each user's PowerDisplay state is independent. If user A crashes, only user A's PowerDisplay is auto-disabled. User B is unaffected.
* **Roaming profiles:** the same per-user property holds. No special handling needed.
* **Backup/restore (PowerToys settings backup):** `crash_detected.flag` and `discovery.lock` should **not** be included in PowerToys backups — they are transient runtime state. If `SettingsBackupAndRestoreUtils` glob-includes them, exclude them explicitly. Verify during plan execution.
* **Uninstall:** if PowerToys is uninstalled, the entire `%LOCALAPPDATA%\Microsoft\PowerToys\` folder is removed by the existing uninstall path. No special cleanup.
* **GPO disabled:** if PowerDisplay is GPO-disabled, the module never spawns PowerDisplay.exe → no Phase 2 → no lock → crash recovery is moot. The `IsCrashLockActive` UI state is still respected (user could see a warning if a flag from before GPO took effect remains).

## 10. What this design explicitly does not do

* Does not identify which monitor caused the crash.
* Does not warn before re-enabling. After Ignore, user may BSOD again — that's their informed choice.
* Does not try to be clever about "is this a real BSOD or did someone TerminateProcess us." Both are treated identically. Manual force-kill of PowerDisplay.exe mid-Phase 2 will be detected as a crash. Recoverable by user via Ignore.
* Does not add PowerDisplay-specific code to the runner main loop. All new C++ code lives in `PowerDisplayModuleInterface.dll`.
* Does not modify any DDC/CI logic or kernel API calls. The fix to the actual BSOD is the kernel team's responsibility.
* Does not implement EDID-based pre-screening or known-bad monitor blocklists. Those are separate ideas (raised earlier in brainstorming) and intentionally deferred to keep this PR atomic.

## 11. Open implementation details (for the plan)

* Exact file path within `App.xaml.cs` or equivalent for the Phase 0 hook.
* Whether to abstract file system / event signaler behind interfaces for testing — recommended but not strictly required.
* The new GUID for `POWER_DISPLAY_AUTO_DISABLE_EVENT`.
* Whether to add a `PowerDisplay.Models` reference to `PowerDisplay.Lib` (so `PathConstants` can use `PowerDisplayPaths` rather than duplicating literals).
* Confirm `PTSettingsHelper::save_general_settings` (C++) and the corresponding C# write path don't conflict; pick one for Phase 0's settings.json write. (Tentative: PowerDisplay.exe is C#, so use the existing C# `SettingsUtils.SaveSettings` mechanism via the global settings file path.)
* Localization keys must be added to all `Resources.resw` files (en-US is canonical; translation pipelines pick it up).

These are flagged for resolution in the implementation plan.
