# PowerDisplay — auto-rescan on max-compatibility toggle: design

## Problem

Opening **Settings → PowerDisplay → Advanced → Maximum compatibility mode** flips
`PowerDisplaySettings.Properties.MaxCompatibilityMode` and fires the existing
`SettingsUpdatedPowerDisplayEvent` named event. The module's
`ApplySettingsFromUI()` reads the new flag and pushes it to the DDC controller
via `MonitorManager.SetMaxCompatibilityMode(...)` — but the flag only affects
**the next** call to `DiscoverMonitorsAsync()`. No discovery is triggered
automatically.

Result: the user toggles the switch ON expecting previously-undetectable
monitors to appear, but nothing visibly changes. The verification doc currently
instructs the user to "toggle the PowerDisplay enable switch off → on, or
hot-plug the monitor" to force a discovery pass. That manual step should go
away — toggling max-compat (in either direction) should automatically trigger a
full hardware rescan.

## Goal

When the user toggles **Maximum compatibility mode** ON or OFF in Settings, and
the change is finalized (i.e. not reverted by cancelling the confirmation
dialog), trigger one full `MonitorManager.DiscoverMonitorsAsync()` pass on the
PowerDisplay module side so the new flag takes effect immediately.

## Out of scope

- Caching the previous max-compat value on the module side to detect changes
  inside `ApplySettingsFromUI()`. (Considered, rejected — Settings UI knows the
  intent explicitly; module-side diff couples the rescan trigger to a
  particular settings field and makes the trigger invisible to readers of
  Settings UI code.)
- Changing the existing `SettingsUpdatedPowerDisplayEvent` semantics. It stays
  lightweight (no hardware rediscovery), driven by the same `ApplySettingsFromUI()`.
- Changing the dialog confirmation flow. The current
  `HandleDangerousFeatureClickAsync` logic (show dialog on enable, revert
  control + setter on cancel) is kept as-is.
- Hot-plug behaviour. `DisplayChangeWatcher` continues to drive its own
  refreshes.
- Toggle-driven rescan signals during the brief mid-state of a cancelled
  enable. (Cancelled enable = net no-op from the hardware's perspective; see
  "Cancellation race" below.)

## Approach

Add a new dedicated named event `RescanPowerDisplayMonitorsEvent` that travels
Settings UI → PowerDisplay module. The Settings UI page code-behind signals it
**after** the user's max-compat change finalizes. The module registers a
listener that calls `MainViewModel.RefreshMonitorsAsync()` — which already does
the right thing (sets `IsScanning`, forwards the flag to the DDC controller,
calls `DiscoverMonitorsAsync`, updates the UI, releases scanning).

```text
┌─────────────────────────────┐                     ┌─────────────────────────────┐
│ Settings UI (Settings.UI)   │                     │ PowerDisplay.exe (module)   │
├─────────────────────────────┤                     ├─────────────────────────────┤
│                             │                     │                             │
│ User toggles Max-compat     │                     │                             │
│  ─ x:Bind TwoWay sets       │                     │                             │
│    VM.MaxCompatibilityMode  │                     │                             │
│    → save settings.json     │                     │                             │
│    → SignalSettingsUpdated  │ ── SettingsUpdated  │                             │
│                             │    PowerDisplayEvt ─→ ApplySettingsFromUI()       │
│                             │                     │   (lightweight, no rescan)  │
│ ─ Toggled handler runs      │                     │                             │
│   ┌─ dialog (if turning ON) │                     │                             │
│   ├─ user confirms          │                     │                             │
│   │  OR turning OFF (no dlg)│                     │                             │
│   └─ SignalNamedEvent(      │ ── Rescan           │                             │
│        RescanPowerDisplay   │    PowerDisplay     │                             │
│        MonitorsEvent)       │    MonitorsEvent ──→  vm.RefreshMonitorsAsync()   │
│                             │                     │   ─ SetMaxCompatibilityMode │
│                             │                     │   ─ DiscoverMonitorsAsync   │
│                             │                     │   ─ UpdateMonitorList       │
└─────────────────────────────┘                     └─────────────────────────────┘
```

Two events fire in series: the existing `SettingsUpdatedPowerDisplayEvent`
(written by the VM property setter) and the new `RescanPowerDisplayMonitorsEvent`
(written by the Toggled handler after confirmation). Their handlers serialize
naturally on the module's dispatcher queue: `ApplySettingsFromUI` runs first
(lightweight), then `RefreshMonitorsAsync` runs (heavy). If they happen to
overlap, `RefreshMonitorsAsync`'s `IsScanning` guard skips the second call —
acceptable because the first one already picks up the new flag (every
discovery re-reads it via `MainViewModel.Monitors.cs:113`).

## Components

### 1. New named event constant

Add `RescanPowerDisplayMonitorsEvent` to the existing event family. Naming
deliberately distinct from `RefreshPowerDisplayMonitorsEvent` — the latter
travels module → UI and asks UI to reload `settings.json`. The new one
travels UI → module and asks the module to rediscover hardware.

| File | Change |
|------|--------|
| `src/common/interop/shared_constants.h` | Add `const wchar_t RESCAN_POWER_DISPLAY_MONITORS_EVENT[] = L"Local\\PowerToysPowerDisplay-RescanMonitorsEvent-<fresh-guid>";` next to the other PowerDisplay event GUIDs. The implementation step will mint a fresh GUID (any unique v4 GUID — collisions with other named events are the only correctness concern, not the GUID's bit pattern). |
| `src/common/interop/Constants.h` | Add `static hstring RescanPowerDisplayMonitorsEvent();` after `HotkeyUpdatedPowerDisplayEvent()`. |
| `src/common/interop/Constants.idl` | Mirror the new method in the WinRT IDL after `HotkeyUpdatedPowerDisplayEvent()`. |
| `src/common/interop/Constants.cpp` | Implement the method, returning `CommonSharedConstants::RESCAN_POWER_DISPLAY_MONITORS_EVENT`. |

### 2. Settings UI: signal the event after the user's choice finalizes

#### `src/settings-ui/Settings.UI/ViewModels/PowerDisplayViewModel.cs`

Add a public method that wraps the existing private `SignalNamedEvent` helper:

```csharp
public void SignalRescanRequest()
{
    SignalNamedEvent(Constants.RescanPowerDisplayMonitorsEvent());
    Logger.LogInfo("Signaled RescanPowerDisplayMonitorsEvent (max-compat toggle finalized)");
}
```

Keep `SignalNamedEvent` private — it's an internal helper. The new public
method is the only entry point.

#### `src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml.cs`

Rewrite `MaxCompatibilityMode_Toggled` (currently 7 lines, will become ~15)
to:

1. Bail out immediately when we're inside a `_isRestoringDangerousFeatureControl`
   re-entry (otherwise the revert's synchronous re-fire would signal a rescan
   for a user who cancelled).
2. Capture the post-click state (`clickedTo = toggleSwitch.IsOn`).
3. Await the existing `HandleDangerousFeatureClickAsync` (no change to its
   behaviour or signature).
4. After await, if the user clicked into ON (`clickedTo`) and the toggle is now
   OFF (`!toggle.IsOn`), the dialog was cancelled — bail out without signalling.
5. Otherwise (turn-OFF, or turn-ON-confirmed), call `ViewModel.SignalRescanRequest()`.

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

No XAML change is required — the `Toggled="MaxCompatibilityMode_Toggled"` hook
is already on the ToggleSwitch from earlier in this branch.

### 3. PowerDisplay module: register the listener

#### `src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs`

Inside `OnLaunched`, alongside the existing `RegisterWindowEvent` /
`RegisterViewModelEvent` calls (`App.xaml.cs:95-117`), add:

```csharp
RegisterViewModelEvent(
    Constants.RescanPowerDisplayMonitorsEvent(),
    vm => _ = vm.RefreshMonitorsAsync(),
    "RescanMonitors");
```

`RegisterViewModelEvent` already marshals to the UI dispatcher.
`RefreshMonitorsAsync` is `public async Task` (`MainViewModel.Monitors.cs:102`)
and is safe to fire-and-forget — it handles its own exceptions and
`IsScanning` state. If a scan is already in progress, the new call returns
early at the `IsScanning` guard, which is acceptable behaviour (see
"Concurrency" below).

## Behaviour

### Cancellation race

When the user clicks ON, the property setter writes `settings.json` and fires
`SettingsUpdatedPowerDisplayEvent` **before** the confirmation dialog
appears. So even if the user cancels, the module briefly sees
`MaxCompatibilityMode = true` and pushes it to the DDC controller. This
mid-state is **not observable as a behaviour change** because:

- `MonitorManager.SetMaxCompatibilityMode` just sets a field on the controller
  (`MonitorManager.cs:79-85`). It doesn't trigger discovery.
- The next discovery is the only thing the flag affects, and we don't trigger
  one in this branch (only `SettingsUpdatedPowerDisplayEvent` fires here, not
  our new rescan event).
- When the user cancels, the property reverts to `false` and fires another
  `SettingsUpdatedPowerDisplayEvent`. The DDC controller is pushed back to
  `false`.

So cancellation = perfectly silent at the hardware level. We only signal the
new rescan event when the change is final.

### Concurrency

If the user toggles rapidly (ON, OFF, ON, ...) faster than discovery
completes, `RefreshMonitorsAsync`'s `IsScanning` guard at
`MainViewModel.Monitors.cs:104-107` drops subsequent calls until the in-flight
scan finishes. The dropped toggles will still take effect: each discovery
re-reads the flag from `settings.json` at line 113, so whichever value is
persisted when the next scan starts wins.

This is acceptable. The alternative — queueing — adds complexity for a
scenario users are very unlikely to hit (probing each handle takes seconds).

### Failure modes

- `EventWaitHandle` constructor or `Set()` failure inside `SignalNamedEvent`:
  caught and logged at line 408 of `PowerDisplayViewModel.cs`. User sees no
  rescan but the persisted flag is correct; next hot-plug or restart applies
  it.
- `RefreshMonitorsAsync` throws: caught inside the method at
  `MainViewModel.Monitors.cs:124-131`, IsScanning released, error logged.
- Module not running: the named event is created lazily on the Settings UI
  side via `EventWaitHandle(..., eventName)` and signalled. With no listener,
  the signal is dropped (auto-reset). The next time the module launches it
  re-reads `settings.json` at `InitializeAsync` and runs a discovery with the
  correct flag — same as today.

## Verification

Add a new section to `POWERDISPLAY_MAXCOMPAT_VERIFICATION.md` (or rewrite
section 3 in place):

- [ ] Toggle **Maximum compatibility mode** ON, confirm dialog.
- [ ] **Without** any other action (no hot-plug, no enable-switch toggle),
      observe in `log.txt`:
  - `[NativeEventWaiter] Event SIGNALED: ...RescanMonitorsEvent-...`
  - The `IsScanning` state goes true → false (visible in UI if flyout is open).
  - The expected `[max-compat] caps unusable .../recovered monitor ...` log
    lines from section 3 of the existing checklist.
- [ ] Toggle **OFF**. Same observation: rescan triggers automatically;
      max-compat-only monitors should disappear after the rescan completes if
      they have no usable cap string.
- [ ] Toggle **ON**, **cancel** the dialog. Observe:
  - No `RescanMonitorsEvent` signal in the log.
  - Monitor list unchanged.
  - `settings.json` shows `max_compatibility_mode: false`.

## Files changed (summary)

| # | Path | Lines |
|---|------|-------|
| 1 | `src/common/interop/shared_constants.h` | +1 |
| 2 | `src/common/interop/Constants.h` | +1 |
| 3 | `src/common/interop/Constants.idl` | +1 |
| 4 | `src/common/interop/Constants.cpp` | +4 |
| 5 | `src/settings-ui/Settings.UI/ViewModels/PowerDisplayViewModel.cs` | +6 |
| 6 | `src/settings-ui/Settings.UI/SettingsXAML/Views/PowerDisplayPage.xaml.cs` | -7, +22 |
| 7 | `src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs` | +4 |
| 8 | `POWERDISPLAY_MAXCOMPAT_VERIFICATION.md` | +~10 |

Net additions ~50 lines. No new files, no new dependencies, no project
reference changes.
