# PowerDisplay auto-rescan on max-compat toggle — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** When the user toggles **Maximum compatibility mode** in Settings → PowerDisplay → Advanced (and confirms, if a dialog is shown), automatically trigger a full `MonitorManager.DiscoverMonitorsAsync()` pass on the PowerDisplay module side so the new flag takes effect without requiring a hot-plug or enable-switch toggle.

**Architecture:** A new dedicated Windows named event `RescanPowerDisplayMonitorsEvent` (Settings UI → PowerDisplay module) is signalled by the Settings page's `MaxCompatibilityMode_Toggled` handler after the user's choice finalizes. PowerDisplay.exe registers a listener that calls the existing `MainViewModel.RefreshMonitorsAsync()`. The existing `SettingsUpdatedPowerDisplayEvent` (which runs the lightweight `ApplySettingsFromUI`) is unchanged.

**Tech Stack:** C# (.NET 9, WinUI 3), C++/WinRT (interop constants), Windows named events (`EventWaitHandle`), MSBuild.

**Spec:** [docs/superpowers/specs/2026-05-13-powerdisplay-maxcap-rescan-design.md](../specs/2026-05-13-powerdisplay-maxcap-rescan-design.md)

---

## Test strategy

The codebase has no unit-test precedent for IPC-event plumbing or for WinUI 3 code-behind UI handlers in the Settings.UI project. The existing PowerDisplay test suite covers parser logic only ([POWERDISPLAY_MAXCOMPAT_VERIFICATION.md:8-9](../../../POWERDISPLAY_MAXCOMPAT_VERIFICATION.md#L8-L9): *"Manual hardware testing is required because the new code paths depend on physical I²C behavior. The unit-test suite covers the parser only."*).

This plan therefore uses **build verification + manual integration testing**, not new automated tests. Every code-edit task ends with a build step that catches type errors / missing references; the final task is the hardware-in-the-loop smoke test from the verification doc.

---

## File map

| File | Role |
|---|---|
| `src/common/interop/shared_constants.h` | Native GUID string for the new event (consumed by C++/WinRT projection only). |
| `src/common/interop/Constants.cpp` / `.h` / `.idl` | C++/WinRT projection exposing `Constants.RescanPowerDisplayMonitorsEvent()` to C# code. |
| `src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs` | Module-side: register `NativeEventWaiter` listener that fires `vm.RefreshMonitorsAsync()`. |
| `src/settings-ui/Settings.UI/ViewModels/PowerDisplayViewModel.cs` | UI-side: public `SignalRescanRequest()` wrapping the existing private `SignalNamedEvent` helper. |
| `src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml.cs` | UI-side: rewrite `MaxCompatibilityMode_Toggled` to call `SignalRescanRequest()` after dialog confirms (or no dialog needed). |
| `POWERDISPLAY_MAXCOMPAT_VERIFICATION.md` | Update verification checklist section 3 to reflect that no manual rediscovery is needed. |

---

## Task 1: Define the new named event constant (4 files, 1 commit)

The C++ shared constant must exist before any C# code can reference `Constants.RescanPowerDisplayMonitorsEvent()`. All four files in this task touch the interop layer and ship together.

**Files:**
- Modify: `src/common/interop/shared_constants.h:161-167` — add a new line in the PowerDisplay event block
- Modify: `src/common/interop/Constants.h:69-77` — add a method declaration
- Modify: `src/common/interop/Constants.idl:66-74` — mirror the declaration in IDL
- Modify: `src/common/interop/Constants.cpp:254-277` — add the method implementation

- [ ] **Step 1: Add the GUID constant in `shared_constants.h`**

In [src/common/interop/shared_constants.h](../../../src/common/interop/shared_constants.h), find the PowerDisplay event block (currently lines 161-167). Locate the existing line:

```cpp
    const wchar_t HOTKEY_UPDATED_POWER_DISPLAY_EVENT[] = L"Local\\PowerToysPowerDisplay-HotkeyUpdatedEvent-9d5f3a2b-7e1c-4b8a-6f3d-2a9e5c7b1d4f";
```

Insert **immediately after** that line:

```cpp
    const wchar_t RESCAN_POWER_DISPLAY_MONITORS_EVENT[] = L"Local\\PowerToysPowerDisplay-RescanMonitorsEvent-7f3e8c5a-1d4b-4a9e-bc6f-5d8a2b9e3c4f";
```

The GUID `7f3e8c5a-1d4b-4a9e-bc6f-5d8a2b9e3c4f` was verified non-colliding with other names in this repo before this plan was written.

- [ ] **Step 2: Add the WinRT method declaration in `Constants.h`**

In [src/common/interop/Constants.h](../../../src/common/interop/Constants.h), find the line:

```cpp
        static hstring HotkeyUpdatedPowerDisplayEvent();
```

Insert **immediately after**:

```cpp
        static hstring RescanPowerDisplayMonitorsEvent();
```

- [ ] **Step 3: Mirror the declaration in `Constants.idl`**

In [src/common/interop/Constants.idl](../../../src/common/interop/Constants.idl), find the line:

```idl
            static String HotkeyUpdatedPowerDisplayEvent();
```

Insert **immediately after**:

```idl
            static String RescanPowerDisplayMonitorsEvent();
```

- [ ] **Step 4: Implement the method in `Constants.cpp`**

In [src/common/interop/Constants.cpp](../../../src/common/interop/Constants.cpp), find the existing implementation:

```cpp
    hstring Constants::HotkeyUpdatedPowerDisplayEvent()
    {
        return CommonSharedConstants::HOTKEY_UPDATED_POWER_DISPLAY_EVENT;
    }
```

Insert **immediately after** that closing brace:

```cpp
    hstring Constants::RescanPowerDisplayMonitorsEvent()
    {
        return CommonSharedConstants::RESCAN_POWER_DISPLAY_MONITORS_EVENT;
    }
```

- [ ] **Step 5: Build the interop project**

From a *Developer Command Prompt for VS* (any shell with `msbuild` on PATH), in the repo root:

```pwsh
msbuild -restore -p:RestorePackagesConfig=true -p:Platform=x64 -p:Configuration=Debug -m src\common\interop\PowerToys.Interop.vcxproj /tl
```

Expected: Build succeeds. No warnings about new symbols.

If the build fails with `error C2065: 'RESCAN_POWER_DISPLAY_MONITORS_EVENT': undeclared identifier` you missed Step 1. If it fails with mismatched method between `.h`, `.idl`, and `.cpp`, you missed Step 2, 3, or 4.

- [ ] **Step 6: Commit**

```pwsh
git add src/common/interop/shared_constants.h src/common/interop/Constants.h src/common/interop/Constants.idl src/common/interop/Constants.cpp
git commit -m @'
feat(PowerDisplay): add RescanPowerDisplayMonitorsEvent IPC event

New Settings UI -> PowerDisplay module named event for explicitly
requesting a full DiscoverMonitorsAsync pass. Separates from
SettingsUpdatedPowerDisplayEvent so the lightweight settings-changed
handler stays out of the heavy hardware-discovery path.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
'@
```

---

## Task 2: Module-side listener registration (1 file, 1 commit)

Once the event constant exists, register a listener on the module side so that signalled events actually do something. Order: this **must** come after Task 1; can come before or after Task 3/4. The system tolerates the signaller side missing (signal dropped) but not a typo here.

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs:113`

- [ ] **Step 1: Register the new event in `OnLaunched`**

In [src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs](../../../src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs), find the existing line:

```csharp
                RegisterViewModelEvent(Constants.PowerDisplaySendSettingsTelemetryEvent(), vm => vm.SendSettingsTelemetry(), "SendSettingsTelemetry");
```

Insert **immediately after** that line:

```csharp
                RegisterViewModelEvent(
                    Constants.RescanPowerDisplayMonitorsEvent(),
                    vm => _ = vm.RefreshMonitorsAsync(),
                    "RescanMonitors");
```

The `_ =` discards the returned `Task` (fire-and-forget); this is safe because `RefreshMonitorsAsync` swallows its own exceptions internally ([MainViewModel.Monitors.cs:124-131](../../../src/modules/powerdisplay/PowerDisplay/ViewModels/MainViewModel.Monitors.cs#L124-L131)).

- [ ] **Step 2: Build PowerDisplay**

```pwsh
msbuild -restore -p:RestorePackagesConfig=true -p:Platform=x64 -p:Configuration=Debug -m src\modules\powerdisplay\PowerDisplay\PowerDisplay.csproj /tl
```

Expected: Build succeeds. If `Constants.RescanPowerDisplayMonitorsEvent()` is not found, Task 1 was incomplete — go back and verify all four files in Task 1.

- [ ] **Step 3: Commit**

```pwsh
git add src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs
git commit -m @'
feat(PowerDisplay): listen for RescanPowerDisplayMonitorsEvent

Register a NativeEventWaiter handler that fires
MainViewModel.RefreshMonitorsAsync() when the Settings UI signals
a rescan. The existing IsScanning guard inside RefreshMonitorsAsync
drops the call if a discovery is already in progress.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
'@
```

---

## Task 3: Settings UI — expose `SignalRescanRequest()` on the ViewModel (1 file, 1 commit)

The page code-behind needs a public method to signal the new event. The existing private `SignalNamedEvent` helper does the actual `EventWaitHandle.Set()`; we just expose a typed wrapper.

**Files:**
- Modify: `src/settings-ui/Settings.UI/ViewModels/PowerDisplayViewModel.cs:393-397`

- [ ] **Step 1: Add `SignalRescanRequest` next to `SignalSettingsUpdated`**

In [src/settings-ui/Settings.UI/ViewModels/PowerDisplayViewModel.cs](../../../src/settings-ui/Settings.UI/ViewModels/PowerDisplayViewModel.cs), find the existing method (currently around lines 390-397):

```csharp
        /// <summary>
        /// Signal PowerDisplay.exe that settings have been updated and need to be applied
        /// </summary>
        private void SignalSettingsUpdated()
        {
            SignalNamedEvent(Constants.SettingsUpdatedPowerDisplayEvent());
            Logger.LogInfo("Signaled SettingsUpdatedPowerDisplayEvent for feature visibility change");
        }
```

Insert **immediately after** the closing brace of `SignalSettingsUpdated`:

```csharp
        /// <summary>
        /// Signal PowerDisplay.exe to perform a full hardware rescan. Used when a
        /// setting changes that affects monitor discovery (currently: max-compatibility
        /// mode). Distinct from <see cref="SignalSettingsUpdated"/>, which only fires
        /// the lightweight settings-applied path on the module side.
        /// </summary>
        public void SignalRescanRequest()
        {
            SignalNamedEvent(Constants.RescanPowerDisplayMonitorsEvent());
            Logger.LogInfo("Signaled RescanPowerDisplayMonitorsEvent (max-compat toggle finalized)");
        }
```

Make sure the access modifier is `public` (the existing `SignalSettingsUpdated` is `private` — we deliberately differ because the page code-behind needs to call this one).

- [ ] **Step 2: Build Settings.UI**

```pwsh
msbuild -restore -p:RestorePackagesConfig=true -p:Platform=x64 -p:Configuration=Debug -m src\settings-ui\Settings.UI\PowerToys.Settings.csproj /tl
```

Expected: Build succeeds.

- [ ] **Step 3: Commit**

```pwsh
git add src/settings-ui/Settings.UI/ViewModels/PowerDisplayViewModel.cs
git commit -m @'
feat(Settings): expose SignalRescanRequest on PowerDisplayViewModel

Public wrapper around the existing private SignalNamedEvent helper,
targeting the new RescanPowerDisplayMonitorsEvent. Distinct from the
private SignalSettingsUpdated so callers can express the heavier
"trigger a hardware rediscovery" intent explicitly.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
'@
```

---

## Task 4: Settings UI — rewrite `MaxCompatibilityMode_Toggled` to call `SignalRescanRequest()` (1 file, 1 commit)

This is the user-facing trigger. The handler is rewritten to: (a) guard against the re-entry caused by `HandleDangerousFeatureClickAsync`'s revert path, (b) capture the clicked-into state before awaiting the dialog, (c) signal the rescan event only when the change is final (not reverted by cancel).

**Files:**
- Modify: `src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml.cs:255-261`

- [ ] **Step 1: Replace `MaxCompatibilityMode_Toggled` with the new implementation**

In [src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml.cs](../../../src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml.cs), find the existing handler (currently around lines 255-261):

```csharp
        private async void MaxCompatibilityMode_Toggled(object sender, RoutedEventArgs e)
        {
            await HandleDangerousFeatureClickAsync(
                sender,
                "PowerDisplay_MaxCompatibility",
                value => ViewModel.MaxCompatibilityMode = value);
        }
```

Replace it with:

```csharp
        private async void MaxCompatibilityMode_Toggled(object sender, RoutedEventArgs e)
        {
            // Guard against the re-entry caused by HandleDangerousFeatureClickAsync's revert()
            // path synchronously setting toggleSwitch.IsOn = false, which fires Toggled again.
            if (_isRestoringDangerousFeatureControl)
            {
                return;
            }

            if (sender is not ToggleSwitch toggleSwitch)
            {
                return;
            }

            bool clickedTo = toggleSwitch.IsOn;

            await HandleDangerousFeatureClickAsync(
                sender,
                "PowerDisplay_MaxCompatibility",
                value => ViewModel.MaxCompatibilityMode = value);

            // If the user clicked the toggle ON and then cancelled the confirmation
            // dialog, HandleDangerousFeatureClickAsync has reverted IsOn back to false.
            // Net effect: no change, no rescan needed.
            if (clickedTo && !toggleSwitch.IsOn)
            {
                return;
            }

            ViewModel.SignalRescanRequest();
        }
```

Notes:
- `ToggleSwitch` is from `Microsoft.UI.Xaml.Controls`. It is already in scope — `HandleDangerousFeatureClickAsync` references it at line 280 of the same file.
- `_isRestoringDangerousFeatureControl` is a private field already in this class (the dangerous-feature dialog uses it to suppress re-entrant Click/Toggled events during revert).
- `ViewModel` is the existing page's typed `Microsoft.PowerToys.Settings.UI.ViewModels.PowerDisplayViewModel`.

- [ ] **Step 2: Build Settings.UI**

```pwsh
msbuild -restore -p:RestorePackagesConfig=true -p:Platform=x64 -p:Configuration=Debug -m src\settings-ui\Settings.UI\PowerToys.Settings.csproj /tl
```

Expected: Build succeeds. If `SignalRescanRequest` not found, Task 3 was missed.

- [ ] **Step 3: Commit**

```pwsh
git add src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml.cs
git commit -m @'
feat(Settings): rescan on max-compat toggle once user confirms

MaxCompatibilityMode_Toggled now signals the new
RescanPowerDisplayMonitorsEvent so the module performs a full
DiscoverMonitorsAsync pass with the freshly-toggled flag. Cancelling
the dangerous-feature confirmation dialog skips the signal (net no
change). A reentrance guard prevents the revert() path inside
HandleDangerousFeatureClickAsync from triggering a spurious rescan
when it synchronously re-fires Toggled.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
'@
```

---

## Task 5: Update the verification checklist (1 file, 1 commit)

The existing manual verification doc tells users to "trigger a fresh discovery (toggle the PowerDisplay enable switch off → on, or hot-plug the monitor)" after enabling max-compat. After this change, no manual rediscovery is needed — toggling the switch is sufficient. Update the doc and add coverage for the auto-rescan + cancel-no-signal cases.

**Files:**
- Modify: `POWERDISPLAY_MAXCOMPAT_VERIFICATION.md`

- [ ] **Step 1: Rewrite section 3 to reflect the auto-rescan**

In [POWERDISPLAY_MAXCOMPAT_VERIFICATION.md](../../../POWERDISPLAY_MAXCOMPAT_VERIFICATION.md), find the existing section starting:

```markdown
## 3. Maximum compatibility mode recovers permanently-broken-caps monitors

- [ ] Toggle **Maximum compatibility mode** ON in Settings.
- [ ] Trigger a fresh discovery (toggle the PowerDisplay enable switch off → on, or hot-plug the monitor).
- [ ] In the log, verify the following sequence appears for the affected monitor:
```

Replace the *first two bullets* (everything from `- [ ] Toggle` through `- [ ] Trigger a fresh discovery ...`) with:

```markdown
- [ ] Toggle **Maximum compatibility mode** ON in Settings. Confirm the warning dialog with **Enable**.
- [ ] A full rescan should trigger automatically — no hot-plug or enable-switch toggle is needed. In the PowerDisplay log, verify that immediately after the dialog confirmation:
  - `[NativeEventWaiter] Event SIGNALED: Local\PowerToysPowerDisplay-RescanMonitorsEvent-...`
  - `IsScanning` transitions true → false (visible in the open flyout as the spinner).
```

(Leave the existing bullet that begins `- [ ] In the log, verify the following sequence appears for the affected monitor:` and everything after it intact.)

- [ ] **Step 2: Add a new section 7 covering toggle-OFF and cancel-no-signal cases**

At the end of section 6 (the "Clean up" section), **before** the `---` line that separates sections from the "What this verification does NOT cover" footer, insert a new section:

```markdown
## 7. Auto-rescan trigger — cancel and toggle-OFF cases

These verify the new toggle-driven rescan behaviour.

- [ ] Toggle **Maximum compatibility mode** ON, then click **Cancel** in the warning dialog. Verify:
  - The toggle returns to OFF.
  - `settings.json` shows `"max_compatibility_mode": false`.
  - The log does **not** contain any `Event SIGNALED: ...RescanMonitorsEvent-...` lines for this toggle attempt (no rescan was triggered).
- [ ] Toggle ON, confirm with **Enable**. After the auto-rescan completes (section 3 covers the log evidence), toggle **OFF**. Verify:
  - The log contains a new `Event SIGNALED: ...RescanMonitorsEvent-...` line.
  - `IsScanning` transitions true → false again.
  - If a monitor was only previously visible thanks to the probe path, it disappears from the flyout after the rescan completes (no usable cap string + max-compat OFF = no entry).
```

- [ ] **Step 3: Commit**

```pwsh
git add POWERDISPLAY_MAXCOMPAT_VERIFICATION.md
git commit -m @'
docs(PowerDisplay): update verification for auto-rescan on max-compat toggle

Section 3 no longer requires a manual enable-switch toggle or
hot-plug to apply max-compat. New section 7 covers the
cancel-no-signal path and the toggle-OFF rescan.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
'@
```

---

## Task 6: Manual integration test (no file changes, no commit)

Build the full solution and run the relevant sections of the verification checklist end-to-end on real hardware.

- [ ] **Step 1: Full solution build**

```pwsh
msbuild -restore -p:RestorePackagesConfig=true -p:Platform=x64 -p:Configuration=Debug -m PowerToys.slnx /tl
```

Expected: Build succeeds across all projects. Typical clean build is ~13-14 minutes per [doc/devdocs/development/debugging.md:24](../../../doc/devdocs/development/debugging.md#L24).

- [ ] **Step 2: Launch PowerToys**

```pwsh
.\src\runner\bin\x64\Debug\PowerToys.exe
```

- [ ] **Step 3: Open the relevant log files for live inspection**

PowerDisplay log:

```pwsh
Get-Content "$env:LOCALAPPDATA\Microsoft\PowerToys\PowerDisplay\Logs\*\log.txt" -Wait -Tail 50
```

Settings log:

```pwsh
Get-Content "$env:LOCALAPPDATA\Microsoft\PowerToys\PowerToys Settings\Logs\*\*.log" -Wait -Tail 50
```

- [ ] **Step 4: Run section 3 of the verification doc** (max-compat ON triggers auto-rescan)

Follow the updated bullets in [POWERDISPLAY_MAXCOMPAT_VERIFICATION.md](../../../POWERDISPLAY_MAXCOMPAT_VERIFICATION.md) section 3. Expected log line in PowerDisplay log:

```
[NativeEventWaiter] Event SIGNALED: Local\PowerToysPowerDisplay-RescanMonitorsEvent-7f3e8c5a-1d4b-4a9e-bc6f-5d8a2b9e3c4f
```

- [ ] **Step 5: Run section 7** (cancel-no-signal and toggle-OFF cases)

Follow the new section 7 added in Task 5.

- [ ] **Step 6: Sanity-check regression coverage**

Walk through section 5 of the verification doc (regression — working monitors still behave correctly) to confirm nothing unrelated broke.

- [ ] **Step 7: Report any failures**

If any expected log line is missing or the rescan does not happen, before debugging:
1. Confirm with `WinObj` (per [doc/devdocs/development/debugging.md:68-72](../../../doc/devdocs/development/debugging.md#L68-L72)) that **both** processes have handles on `Local\PowerToysPowerDisplay-RescanMonitorsEvent-...`. If only Settings has the handle, the listener registration in Task 2 didn't run — check PowerDisplay log for `[NativeEventWaiter] Background thread started for event: Local\PowerToysPowerDisplay-RescanMonitorsEvent-...` near startup.
2. Confirm the GUID string is identical in `shared_constants.h` and what shows in the log.

---

## Roll-back plan

If a problem is discovered after merging:
- Revert Task 4's commit (the page handler change) — disables the trigger; rest of the plumbing is dormant and harmless.
- The new event listener (Task 2) and constants (Task 1) remain idle. No `settings.json` schema change to roll back.
