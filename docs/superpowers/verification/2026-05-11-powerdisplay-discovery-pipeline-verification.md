# PowerDisplay Discovery Pipeline — Manual Verification

**Date**: 2026-05-11
**Scope**: Verify the per-monitor pipeline refactor of `DdcCiController.DiscoverMonitorsAsync` (commits `e6ec90f..13e3483`)
**Related**:
- Spec: [../specs/2026-05-11-powerdisplay-discovery-pipeline-design.md](../specs/2026-05-11-powerdisplay-discovery-pipeline-design.md)
- Plan: [../plans/2026-05-11-powerdisplay-discovery-pipeline.md](../plans/2026-05-11-powerdisplay-discovery-pipeline.md)

## Why manual

The 86 existing unit tests cover `DisplayClassifier` only. `DiscoverMonitorsAsync` integrates with Win32 DDC/CI (`GetPhysicalMonitorsFromHMONITOR`, `CapabilitiesRequestAndCapabilitiesReply`, `GetVCPFeatureAndVCPFeatureReply`) which is not unit-testable without nontrivial mocking that's out of scope for this refactor. Functional correctness and the headline ~10 s → ~4–5 s wall-clock claim must be verified against real hardware.

---

## Setup

### Build (Release|x64)

From the worktree root:

```bash
"/c/Program Files/Microsoft Visual Studio/18/Enterprise/MSBuild/Current/Bin/MSBuild.exe" \
  "$(pwd)/src/modules/powerdisplay/PowerDisplay/PowerDisplay.csproj" \
  -p:Configuration=Release -p:Platform=x64 -nologo -v:minimal -restore
```

Output binary: `x64\Release\WinUI3Apps\PowerToys.PowerDisplay.exe`.

### Log file location

`%LOCALAPPDATA%\Microsoft\PowerToys\PowerDisplay\Logs\` — newest `*.log`. Each `RefreshMonitorsAsync` invocation produces one `DDC: Discovery start ...` and one `DDC: Discovery complete in {ms}ms ...` line. Watch the file with:

```powershell
Get-Content -Path "$env:LOCALAPPDATA\Microsoft\PowerToys\PowerDisplay\Logs\<filename>.log" -Wait -Tail 20
```

### Extracting the perf number

```powershell
Select-String -Path "$env:LOCALAPPDATA\Microsoft\PowerToys\PowerDisplay\Logs\*.log" `
              -Pattern "DDC: Discovery complete in (\d+)ms" |
  ForEach-Object { $_.Matches[0].Groups[1].Value }
```

### Baseline reference

Pre-refactor measured wall-clock for 3 external monitors: **~10 000 ms**. Spec target: **≤ 5 000 ms** (≥ 40 % reduction). Hard floor: ~4 s (single-monitor `FetchCapabilities` is firmware-bound at I2C+MCCS string fetch).

---

## Test matrix

Each test row: **launch the app fresh** (or click Refresh once warm), let discovery complete, capture the `Discovery complete` log line, then evaluate pass/fail. Record actual numbers in the [Results section](#results-record-here) at the bottom.

### Perf tests

| # | Scenario | Steps | Pass criteria |
|---|---|---|---|
| P1 | 0 external monitors (laptop panel only) | Disconnect all external displays. Launch app. | `Discovery complete` log shows `0 monitors (no handles)` OR `0/N monitors`. Total < **500 ms**. WMI side discovers the internal panel separately. |
| P2 | 1 external monitor | Connect one external. Launch app. | 1 DDC monitor discovered. `Discovery complete` < **5 500 ms** (single-pipeline). |
| P3 | 2 external monitors | Connect two externals. Launch app. | 2 DDC monitors. Wall-clock close to single-pipeline (**≤ 5 500 ms**), not double. |
| P4 | 3 external monitors | Connect three externals. Launch app. | 3 DDC monitors. Wall-clock close to single-pipeline (**≤ 5 500 ms**), not triple. **Headline perf claim**. Compare against pre-refactor ~10 000 ms baseline; expect ≥ 40 % reduction. |
| P5 | Median of 5 runs (3 monitors) | Run P4 five times back-to-back via Refresh button. Take median of the 5 `Discovery complete in {ms}ms` values. | Median ≤ **5 500 ms**. |

### Functional tests

| # | Scenario | Steps | Pass criteria |
|---|---|---|---|
| F1 | Brightness slider — DDC monitor | After discovery, drag brightness slider on a DDC/CI monitor. | Monitor brightness changes physically. Slider value persists. No log errors. |
| F2 | Brightness slider — WMI internal panel | Drag brightness slider on the internal panel. | Internal panel brightness changes. Confirms WMI path unaffected by the refactor. |
| F3 | Contrast / Volume / Color Temperature | Open the controls on a monitor that supports them. Adjust each. | Values apply. `Supports*` flags match the monitor's VCP capabilities. |
| F4 | Input source switch | On a monitor that supports VCP 0x60 (Input Source) and has multiple inputs connected, switch input. | Monitor switches to the selected input. |
| F5 | Power state | On a monitor that supports VCP 0xD6, set power state to Standby. | Monitor enters standby. Restore to On. |
| F6 | Identity Refresh comparison | Note the initial values for `Brightness, Contrast, Volume, ColorTemperature, MonitorNumber, GdiDeviceName, Name` for each monitor. Click Refresh. | Each monitor has the same values after refresh as before. (Confirms `_handleManager.UpdateHandleMap` and the new orchestrator don't re-key incorrectly.) |

### Failure-mode tests

| # | Scenario | Steps | Pass criteria |
|---|---|---|---|
| FM1 | Disconnect during discovery | Click Refresh, then within 2 seconds physically unplug one external monitor's cable. | The unplugged monitor's pipeline either succeeds-then-disappears-on-next-refresh OR fails gracefully (`Logger.LogWarning` or `LogError` with `hMonitor=0x...` or `DevicePath=...` context). Other monitors are discovered normally. App does **not** crash. |
| FM2 | Mirror mode (1 hMonitor → 2 physical) | Set up screen-mirroring across two physical displays from one GPU output (Windows display settings → Duplicate). Launch app. | Both physical monitors are discovered. Log line `DDC: Discovery complete` shows both. Processing within the same hMonitor is sequential (per-physical loop). |
| FM3 | Cancellation mid-discovery | Launch app and immediately close the window before discovery completes (~within 1 s of launch). | No crash, no exception dialog. App exits cleanly. (`OperationCanceledException` propagates to `SafeDiscoverAsync` which returns empty list and logs a Warning.) |
| FM4 | All monitors disconnected | Disconnect every external monitor. Click Refresh. | `Discovery complete` log emits `0 monitors`. No error. WMI side may still report the internal panel. |
| FM5 | DDC-incapable monitor | Connect a monitor known not to support DDC/CI (or a TV via HDMI that doesn't expose VCP). | The monitor is excluded from DDC discovery (caps fetch returns Invalid → `BuildMonitorFromPhysical` returns null). Pipeline does NOT throw. Log shows it was attempted. |

### Threading invariant tests (optional but informative)

| # | Scenario | Steps | Pass criteria |
|---|---|---|---|
| T1 | Confirm parallel fan-out | Look at log timestamps for any per-pipeline messages (e.g., "GetPhysicalMonitors attempt 1 returned null" warnings, if they fire) across 2+ monitors. | Warnings from different monitors appear interleaved in time, not sequentially. (Indicates pipelines ran on different threadpool threads.) |
| T2 | No `Thread.Sleep` blocking | Same as T1; observe whether retry backoff (200 ms `Task.Delay`) delays unrelated pipelines. | A retry on one monitor should not delay another monitor's progress. Hard to test without instrumentation; informational only. |

---

## Results — record here

Copy this block into your verification report (or fill in inline and commit as an amendment to this doc):

```
## Hardware Config

OS Build:
Number of external monitors:
Monitor models/connections:
GPU:

## Perf Results

P1 (0 ext):           {ms}ms — Pass / Fail
P2 (1 ext):           {ms}ms — Pass / Fail
P3 (2 ext):           {ms}ms — Pass / Fail
P4 (3 ext):           {ms}ms — Pass / Fail
P5 (median of 5):     {ms}ms — Pass / Fail
Baseline (pre-refactor, 3 ext): ~10000ms
Reduction:            {pct}%

## Functional Results

F1 Brightness DDC:    Pass / Fail [notes]
F2 Brightness WMI:    Pass / Fail [notes]
F3 Contrast/Vol/CT:   Pass / Fail [notes]
F4 Input source:      Pass / Fail / N/A [notes]
F5 Power state:       Pass / Fail / N/A [notes]
F6 Refresh identity:  Pass / Fail [notes]

## Failure-mode Results

FM1 Disconnect:       Pass / Fail [notes]
FM2 Mirror mode:      Pass / Fail / N/A [notes]
FM3 Cancellation:     Pass / Fail [notes]
FM4 All disconnected: Pass / Fail [notes]
FM5 DDC-incapable:    Pass / Fail / N/A [notes]

## Threading invariants (optional)

T1 Parallel fan-out:  Pass / Fail / Inconclusive
T2 No blocking sleep: Pass / Fail / Inconclusive

## Overall verdict

[ ] All required pass — refactor ready to merge
[ ] Issues found — see notes
```

---

## Failure triage

If something fails, the most actionable signals:

1. **Perf is worse than baseline** → confirm Release|x64 build; Debug builds are dramatically slower. Then check log for repeated retries (e.g., `GetPhysicalMonitors attempt 2 retrying`) — if a single hMonitor retries 3× × 200 ms, that's an extra 400 ms per pipeline.

2. **Discovery completes but monitor count is wrong** → check log for:
   - `Failed to get GDI device name for hMonitor=0x...`
   - `DDC skipping {gdiName}: not in external targets list` (informational; should match Phase 0 internal/external split)
   - `Failed to get physical monitors for {gdiName} after retries`
   - `BuildMonitorFromPhysical exception: ...`

3. **App crashes / hangs during discovery** → grab a dump. Most likely candidates:
   - Win32 `CapabilitiesRequestAndCapabilitiesReply` hung at firmware level (out of scope, monitor-specific)
   - Deadlock in `_handleManager.UpdateHandleMap` lock (the lock semantics are unchanged from pre-refactor, so this would be a preexisting bug surfaced by parallel timing)

4. **`OperationCanceledException` shows up in log unexpectedly** → check that the consumer isn't canceling the token early. `MainViewModel.RefreshMonitorsAsync` shouldn't cancel its own request; if it does, that's a separate bug.

---

## Sign-off

After all required tests pass on your hardware, sign off by either:
- Committing this file with the **Results — record here** block filled in, **or**
- Stating "verified, ready to merge" in the PR description with a link to your verification log

Once signed off, proceed to `superpowers:finishing-a-development-branch` for the merge / PR step.
