# PowerDisplay — Cap-string retry + Maximum compatibility mode: manual verification

This branch (`yuleng/pd/maxcap`) adds two related changes to the PowerDisplay DDC/CI discovery pipeline:

1. **Cap-string fetch retry (always on)** — `DdcCiController.FetchCapabilitiesWithFallbackAsync` retries up to **3 attempts** with `await Task.Delay(1000)` between attempts. Auto-recovers monitors that fail transient I²C reads.
2. **Maximum compatibility mode (opt-in toggle)** — when the cap string is still empty or unparseable after retries, `DdcCiNative.ProbeSupportedVcpFeatures` probes VCP `0x10` / `0x12` / `0x62` / `0xD6` directly and reconstructs the supported feature list from whatever responds. Toggle lives in **Settings → PowerDisplay → Advanced → Maximum compatibility mode**.
3. **Brightness gate removed** — the legacy "monitor must advertise VCP 0x10 to be valid" check is gone. Any monitor with at least one supported VCP code is kept. The brightness slider in the per-monitor flyout now binds its `Visibility` to `ShowBrightness` so it's hidden when the monitor doesn't support brightness.

Manual hardware testing is required because the new code paths depend on physical I²C behavior. The unit-test suite covers the parser only.

## Test environment

- [ ] Windows 11 host with built PowerToys binaries on this branch.
- [ ] At least one external monitor that has previously been undetectable due to a length-0 cap string (the user-reported case driving this work).
- [ ] Optionally: a second external monitor with normal DDC/CI for regression baseline.

Launch the built binary:

```powershell
.\src\runner\bin\x64\Debug\PowerToys.exe
```

PowerDisplay log path: `%LOCALAPPDATA%\Microsoft\PowerToys\PowerDisplay\Logs\<version>\log.txt`
Settings file: `%LOCALAPPDATA%\Microsoft\PowerToys\PowerDisplay\settings.json`

---

## 1. New toggle appears, defaults OFF

- [ ] Open **Settings → PowerDisplay**.
- [ ] A new **Advanced** group sits between the existing flyout settings group and the **Custom VCP value names** group.
- [ ] The group contains exactly one SettingsCard titled **Maximum compatibility mode** with the description starting "Some monitors do not return a standard capabilities string...".
- [ ] The ToggleSwitch is in the OFF state.
- [ ] Open `settings.json` and confirm `"max_compatibility_mode": false` is present at the top level under `properties`.

## 2. Retry alone recovers transient-failure monitors

Keep the toggle OFF for this section. Plug in the previously-undetectable monitor.

- [ ] Open the PowerDisplay flyout (Win+Ctrl+Shift+P) and wait for discovery to complete.
- [ ] Open the log file. Look for entries like:
  - `DDC: cap string fetch attempt 1 returned empty (handle=0x…); retrying in 1000ms`
  - If attempt 2 or 3 succeeds, the monitor appears in the flyout with its DDC/CI-advertised feature controls.
- [ ] If the monitor consistently fails all 3 retries, log will contain `DDC: cap string still empty after 3 attempts (handle=0x…)` — proceed to section 3.

## 3. Maximum compatibility mode recovers permanently-broken-caps monitors

- [ ] Toggle **Maximum compatibility mode** ON in Settings. Confirm the warning dialog with **Enable**.
- [ ] A full rescan should trigger automatically — no hot-plug or enable-switch toggle is needed. In the PowerDisplay log, verify that immediately after the dialog confirmation:
  - `[NativeEventWaiter] Event SIGNALED: Local\PowerToysPowerDisplay-RescanMonitorsEvent-...`
  - `IsScanning` transitions true → false (visible in the open flyout as the spinner).
- [ ] In the log, verify the following sequence appears for the affected monitor:
  - `DDC: cap string still empty after 3 attempts (handle=0x…)`
  - `DDC: [max-compat] caps unusable for handle=0x…; probing VCP features directly`
  - `DDC: [max-compat] recovered monitor (handle=0x…) with N probed feature(s)` (where N ≥ 1)
- [ ] Open the flyout. The previously-missing monitor now appears.
- [ ] Only the controls for features that probed successfully are visible (brightness, contrast, volume, power state — color preset and input source are intentionally NOT probed because their value sets cannot be reconstructed from `GetVCPFeatureAndVCPFeatureReply`).
- [ ] Drag the visible sliders and confirm the monitor responds.

## 4. Brightness-less monitor handling

Only applicable if you have a monitor that advertises caps but **does not** include VCP `0x10`. Skip if not available and note this case as not exercised.

- [ ] The monitor appears in the PowerDisplay flyout (this would not have happened before the brightness-gate removal).
- [ ] The brightness slider is **hidden** for this monitor (previously it would have been visible but non-functional — the fix in commit `8ad67381ef` addresses this by binding `Visibility` to `ShowBrightness`).
- [ ] Other controls the monitor does support (contrast / volume / etc.) remain visible and functional.

## 5. Regression — working monitors still behave correctly

Walk through the golden path on a fully working monitor, with the toggle OFF first then ON:

- [ ] Brightness slider works (only if monitor supports it).
- [ ] Contrast slider works (only if monitor supports it).
- [ ] Volume slider works (only if monitor supports it).
- [ ] Color temperature picker works (only if monitor supports it).
- [ ] Input source switch works (only if monitor supports it).
- [ ] Power state switch works (only if monitor supports it).
- [ ] Saving and applying a profile produces the expected values on the monitor.
- [ ] Hot-plug a monitor; it appears in the list after the configured refresh delay.

If anything regresses, the most likely cause is the `BuildMonitorFromPhysical` signature change or the `DiscoverFromHandleAsync` rewiring — re-check commits `90b8968c00` and `8ad67381ef`.

## 6. Clean up

- [ ] Toggle **Maximum compatibility mode** OFF.
- [ ] Confirm `settings.json` shows `"max_compatibility_mode": false`.
- [ ] Confirm Settings UI reflects OFF state after Settings is closed and reopened.

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

---

## 8. Display-state rescan + VCP write retry (sleep/wake recovery)

This section verifies the changes from
`docs/superpowers/plans/2026-05-13-powerdisplay-display-state-rescan.md`:

- `GUID_CONSOLE_DISPLAY_STATE` replaces `SystemEvents.PowerModeChanged`.
- UI locks immediately on display-on transition, ahead of the debounce delay.
- `SetVCPFeature` retries 3× with 200ms spacing.

Run each subsection on **a representative S3 device** (`powercfg /a` shows
S3 available, S0ix unavailable). If a second machine is available with the
opposite power profile (S0ix available), repeat sections 8.2 and 8.3 there
as a separate pass.

### 8.1 Cold-boot baseline — no false wake-trigger

- [ ] Launch PowerToys; open the PowerDisplay flyout.
- [ ] In `%LOCALAPPDATA%\Microsoft\PowerToys\PowerDisplay\Logs\<version>\log.txt`,
      look for `[DisplayChangeWatcher] Subscribed to GUID_CONSOLE_DISPLAY_STATE`.
- [ ] **Negative check:** the log must NOT contain
      `[DisplayChangeWatcher] Console display ON (was on); ...` —
      the subscription's initial state echo (if any) must not trigger a
      rescan because `_lastDisplayState` is seeded to `DisplayStateOn`.
- [ ] Confirm monitors appear normally after the initial discovery.

### 8.2 System sleep / resume

- [ ] Open the flyout, ensure at least one external monitor is detected
      with controls visible (brightness or input source).
- [ ] Run `rundll32 powrprof.dll,SetSuspendState 0,1,0` or use Start menu
      → Sleep to enter S3.
- [ ] Wait ≥ 30 seconds. Wake the machine (mouse / keyboard).
- [ ] **Within 1 second** of seeing the desktop, observe the PowerDisplay
      flyout:
  - The flyout must show the "Scanning monitors..." spinner — proving
    `IsScanning = true` fired synchronously, well before the 5-second
    debounce.
  - All previously-interactive controls must be disabled (greyed out).
- [ ] Wait for the spinner to clear (~5-10s depending on
      `MonitorRefreshDelay` setting and hardware).
- [ ] Verify the log shows in order:
  1. `[DisplayChangeWatcher] Console display ON (was off); firing ResumeDetected and scheduling rescan`
  2. `[MainViewModel] Wake detected — locking UI ahead of rediscovery`
  3. `DDC: Discovery start — ... candidate handles, ... external targets`
  4. `DDC: Discovery complete in ...ms — N/M monitors`
- [ ] Verify input-source switch (or any other control) works on the first
      try after the spinner clears.

### 8.3 Idle blank → wake (system did NOT sleep)

This is the scenario the user originally reported.

- [ ] In Settings → System → Power & battery → Screen and sleep, set
      "On battery / On AC: Turn off screen after" to **1 minute** for the
      duration of this test.
- [ ] Open the PowerDisplay flyout; verify monitors are interactive.
- [ ] Walk away for >1 minute. Screen turns off but system does NOT sleep
      (verify by checking system-time clock continues; this is **idle blank
      only**).
- [ ] Move the mouse or press a key to wake the display.
- [ ] **Expected — this is the bug being fixed:** the flyout transitions
      to the scanning spinner immediately, performs a rediscovery, and
      restores interactive controls with fresh handles.
- [ ] Verify log entries match section 8.2 step 5 — `Console display ON
      (was off)` must appear.
- [ ] **Negative control:** before this fix, the log would NOT contain a
      `Console display ON` line in this scenario because the underlying
      signal source (`PowerModeChanged.Resume`) does not fire when system
      never sleeps. Confirm the new behaviour by checking the log.
- [ ] Restore the original screen-off timeout when finished.

### 8.4 Laptop lid open / close (laptops only)

- [ ] Settings → System → Power & battery → Lid behaviour: configure
      "When I close the lid: Turn off the display" (NOT "Sleep").
- [ ] Open flyout, close lid → screen off → wait 10s → open lid.
- [ ] Same expected outcome as 8.3: UI locks on lid open, rediscovers,
      and restores interactive controls.
- [ ] Restore original lid behaviour.

### 8.5 SetVCPFeature retry — induced bus failure

This step is hard to reproduce reliably without specialised hardware. If you
have a monitor known to fail VCP writes occasionally right after wake,
exercise it here. Otherwise, this is a code-review-only check:

- [ ] In a debug build, breakpoint `SetVcpFeatureAsync` and step through
      one user-initiated brightness/input change. Confirm the retry loop
      executes correctly on a forced `SetVCPFeature returned false`
      (modify the local result to false during stepping).
- [ ] Verify log shows `DDC: SetVCPFeature(VCP=0xNN) attempt 1 failed
      (lastError=0xN); retrying in 200ms`.
- [ ] Verify subsequent successful attempt logs `DDC: SetVCPFeature(VCP=0xNN)
      succeeded on attempt 2`.

### 8.6 Regression — no double-rescan or thrash

- [ ] With the flyout open, hot-plug a monitor in/out three times rapidly.
- [ ] Verify only one rescan triggers after the debounce settles (proves
      `ScheduleDisplayChanged` debounce still coalesces).
- [ ] Toggle screen off/on three times rapidly via Win+L lock screen.
- [ ] Verify same coalescing — UI locks, debounce settles, one rediscovery.
- [ ] Check Task Manager: PowerToys.PowerDisplay.exe CPU usage idles back
      to near-zero within 5s after the last event.

### 8.7 Modern Standby (S0ix) — opportunistic

If you have access to a Modern Standby device (Surface, recent Intel Evo
laptop, etc.) where `powercfg /a` reports `Standby (S0 Low Power Idle)
Network Connected` as available:

- [ ] Repeat section 8.2. The expected behaviour is **identical** to S3 —
      this is the whole point of switching signal sources. If the spinner
      does not appear on wake or the log does not show `Console display
      ON`, the fix has regressed on S0ix.

---

## What this verification does NOT cover

- Hardware that returns *wrong* VCP values when probed. The probe accepts any `true` return from `GetVCPFeatureAndVCPFeatureReply`; if a monitor responds to brightness probe but rejects the write, that monitor will get added with a non-functional brightness slider. Users with such monitors should leave the toggle OFF.
- Performance under heavy load (many external monitors). Retries serialize per-physical-monitor, so the worst case discovery time for a fully-broken monitor is roughly 3 × (~4 s caps fetch + 1 s wait) ≈ 14 s per monitor. Discovery across hMonitors still runs concurrently via `Task.WhenAll`.
