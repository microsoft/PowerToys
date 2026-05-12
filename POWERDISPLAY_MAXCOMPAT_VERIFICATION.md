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

- [ ] Toggle **Maximum compatibility mode** ON in Settings.
- [ ] Trigger a fresh discovery (toggle the PowerDisplay enable switch off → on, or hot-plug the monitor).
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

---

## What this verification does NOT cover

- Hardware that returns *wrong* VCP values when probed. The probe accepts any `true` return from `GetVCPFeatureAndVCPFeatureReply`; if a monitor responds to brightness probe but rejects the write, that monitor will get added with a non-functional brightness slider. Users with such monitors should leave the toggle OFF.
- Performance under heavy load (many external monitors). Retries serialize per-physical-monitor, so the worst case discovery time for a fully-broken monitor is roughly 3 × (~4 s caps fetch + 1 s wait) ≈ 14 s per monitor. Discovery across hMonitors still runs concurrently via `Task.WhenAll`.
