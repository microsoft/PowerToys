# PowerDisplay Discovery Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor `DdcCiController.DiscoverMonitorsAsync` from three horizontal phases (collect → fetch caps → init VCPs) into N parallel per-monitor vertical pipelines, eliminating cross-monitor serial waits and the Phase 2→3 barrier.

**Architecture:** Single-file refactor in `src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs`. Each hMonitor runs its own async pipeline (filter → `GetPhysicalMonitorsWithRetryAsync` → per-physical `BuildMonitorFromPhysical`). All pipelines fan out via `Task.WhenAll`. Sync I/O blocks (`GetPhysicalMonitors`, `BuildMonitorFromPhysical`) wrapped in `Task.Run` at their boundaries; retry backoff uses `Task.Delay` (non-blocking). No external API change; `MonitorManager`, `IMonitorController`, `IDdcController`, WMI side, and ViewModels untouched.

**Tech Stack:** C# / .NET 10, MSBuild, MSTest (86 existing unit tests).

**Spec:** [docs/superpowers/specs/2026-05-11-powerdisplay-discovery-pipeline-design.md](../specs/2026-05-11-powerdisplay-discovery-pipeline-design.md)

**Conventions:**
- Working directory is the worktree root: `C:\Users\yuleng\PowerToys\.claude\worktrees\yuleng+pd+discovery+1`
- All file paths are relative to that root.
- Build/test command (shorthand `MSB` throughout):
  ```bash
  "/c/Program Files/Microsoft Visual Studio/18/Enterprise/MSBuild/Current/Bin/MSBuild.exe"
  ```

---

## File Structure

Single file modified:
- **Modify**: `src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs`

By the end of the plan this file will have:
- **Added** private methods: `BuildGdiLookup`, `BuildMonitorFromPhysical`, `DiscoverFromHandleAsync`
- **Modified** methods: `DiscoverMonitorsAsync`, `GetPhysicalMonitorsWithRetryAsync`
- **Removed**: `CollectCandidateMonitorsAsync`, `FetchCapabilitiesInParallelAsync`, `CreateValidMonitors`, `CandidateMonitor` (record struct)

Net line-count change: roughly −80, +120 = +40 lines (more inline comments, more explicit log lines).

---

## Task 1: Add `BuildGdiLookup` private helper

Extract the existing `targetsByGdiName` LINQ expression (currently inline in `CollectCandidateMonitorsAsync`) into a named static helper. Dead code in this commit — wired up in Task 4. Pure refactor preparation.

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs`

- [ ] **Step 1: Add the helper method**

  Open `src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs`. Locate `EnumerateMonitorHandles` (around line 320 in the current file). **Immediately after** the closing brace of `EnumerateMonitorHandles`, add:

  ```csharp
  /// <summary>
  /// Group external targets by GDI device name (case-insensitive) into a lookup keyed by name.
  /// Mirror mode can have multiple targets share one GDI source — hence the value is a List.
  /// </summary>
  private static Dictionary<string, List<MonitorDisplayInfo>> BuildGdiLookup(
      IReadOnlyList<MonitorDisplayInfo> externalTargets)
  {
      return externalTargets
          .Where(t => !string.IsNullOrEmpty(t.GdiDeviceName))
          .GroupBy(t => t.GdiDeviceName, StringComparer.OrdinalIgnoreCase)
          .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
  }
  ```

- [ ] **Step 2: Build + run unit tests**

  ```bash
  "/c/Program Files/Microsoft Visual Studio/18/Enterprise/MSBuild/Current/Bin/MSBuild.exe" \
    "$(pwd)/src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplay.Lib.UnitTests.csproj" \
    -p:Configuration=Release -p:Platform=x64 -t:VSTest -nologo -v:minimal -restore 2>&1 | tail -5
  ```

  Expected: `Passed! - Failed: 0, Passed: 86, Skipped: 0`.

- [ ] **Step 3: Commit**

  ```bash
  git add src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs
  git commit -m "$(cat <<'EOF'
  refactor(PowerDisplay): extract BuildGdiLookup helper from DDC discovery

  Extracts the inline GDI-name grouping expression into a named static
  helper. No callers yet — will be wired up in the discovery pipeline
  refactor that replaces the three-phase model with per-monitor pipelines.
  Pure refactor preparation; no behavior change.

  Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
  EOF
  )"
  ```

---

## Task 2: Add `BuildMonitorFromPhysical` private helper

Add the full per-physical-monitor pipeline as a sync method: `FetchCapabilities` → `CreateMonitorFromPhysical` → caps wiring → `Initialize*`. Dead code in this commit — wired up in Task 4. The per-physical try/catch lives here.

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs`

- [ ] **Step 1: Add the helper method**

  Locate `Initialize*` group of methods (around line 396 in the current file, starting with `InitializeInputSource`). **Immediately before** `InitializeInputSource`'s `/// <summary>` doc comment, add:

  ```csharp
  /// <summary>
  /// Full per-physical-monitor pipeline: fetch DDC/CI capabilities (slow I2C, ~4 s),
  /// construct the Monitor object, and read the supported VCP feature values.
  /// Returns null if capabilities are invalid, the Monitor can't be constructed, or
  /// any exception occurs (logged at Error level with the device path).
  /// </summary>
  /// <remarks>
  /// Pure synchronous work — callers wrap this in <see cref="Task.Run"/> to dispatch
  /// to the threadpool. Within a single physical monitor the VCP reads serialize on
  /// one I2C bus; parallelism across physical monitors happens at the caller.
  /// </remarks>
  private Monitor? BuildMonitorFromPhysical(PHYSICAL_MONITOR physical, MonitorDisplayInfo info)
  {
      try
      {
          var capResult = DdcCiNative.FetchCapabilities(physical.HPhysicalMonitor);
          if (!capResult.IsValid)
          {
              return null;
          }

          var monitor = _discoveryHelper.CreateMonitorFromPhysical(physical, info);
          if (monitor == null)
          {
              return null;
          }

          if (!string.IsNullOrEmpty(capResult.CapabilitiesString))
          {
              monitor.CapabilitiesRaw = capResult.CapabilitiesString;
          }

          if (capResult.VcpCapabilitiesInfo != null)
          {
              monitor.VcpCapabilitiesInfo = capResult.VcpCapabilitiesInfo;
              UpdateMonitorCapabilitiesFromVcp(monitor, capResult.VcpCapabilitiesInfo);

              if (monitor.SupportsInputSource)
              {
                  InitializeInputSource(monitor, physical.HPhysicalMonitor);
              }
              if (monitor.SupportsColorTemperature)
              {
                  InitializeColorTemperature(monitor, physical.HPhysicalMonitor);
              }
              if (monitor.SupportsPowerState)
              {
                  InitializePowerState(monitor, physical.HPhysicalMonitor);
              }
              if (monitor.SupportsContrast)
              {
                  InitializeContrast(monitor, physical.HPhysicalMonitor);
              }
              if (monitor.SupportsVolume)
              {
                  InitializeVolume(monitor, physical.HPhysicalMonitor);
              }
          }
          if (monitor.SupportsBrightness)
          {
              InitializeBrightness(monitor, physical.HPhysicalMonitor);
          }

          return monitor;
      }
      catch (Exception ex) when (ex is not OutOfMemoryException)
      {
          Logger.LogError(
              $"DDC: [DevicePath={info.DevicePath}] BuildMonitorFromPhysical exception: {ex.Message}");
          return null;
      }
  }
  ```

- [ ] **Step 2: Build + run unit tests**

  ```bash
  "/c/Program Files/Microsoft Visual Studio/18/Enterprise/MSBuild/Current/Bin/MSBuild.exe" \
    "$(pwd)/src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplay.Lib.UnitTests.csproj" \
    -p:Configuration=Release -p:Platform=x64 -t:VSTest -nologo -v:minimal -restore 2>&1 | tail -5
  ```

  Expected: `Passed! - Failed: 0, Passed: 86, Skipped: 0`.

- [ ] **Step 3: Commit**

  ```bash
  git add src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs
  git commit -m "$(cat <<'EOF'
  refactor(PowerDisplay): add BuildMonitorFromPhysical per-monitor pipeline

  Adds a sync method that does the full per-physical-monitor work:
  FetchCapabilities (slow I2C), CreateMonitorFromPhysical, capability
  flag wiring, and Initialize* for each supported VCP feature.
  Wraps everything in a try/catch with DevicePath-scoped error logging.

  No callers yet — will replace the inline Phase 2 + Phase 3 logic when
  the orchestrator is rewritten. Pure refactor preparation.

  Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
  EOF
  )"
  ```

---

## Task 3: Wrap sync `GetPhysicalMonitors` call in `Task.Run` inside the retry helper

`GetPhysicalMonitorsWithRetryAsync` already uses `await Task.Delay` for backoff (non-blocking). The remaining sync gap is the call to `_discoveryHelper.GetPhysicalMonitors(...)` — currently it runs on whatever thread happens to be executing the method, which serializes N pipelines on the calling thread before the first await. Wrapping that call in `await Task.Run(...)` forces dispatch to the threadpool, so N pipelines run in parallel after Task 4 wires them up.

Behavior is unchanged in the current call site (`CollectCandidateMonitorsAsync` awaits in a `foreach`, still sequential), but the threading primitive is correct for the new pipeline.

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs`

- [ ] **Step 1: Replace `GetPhysicalMonitorsWithRetryAsync` body**

  Locate `GetPhysicalMonitorsWithRetryAsync` (around line 530 in the current file). Replace the **entire method body** (from `{` after the signature line through the matching `}`) with:

  ```csharp
      const int maxRetries = 3;
      const int retryDelayMs = 200;
      PHYSICAL_MONITOR[]? lastResult = null;

      for (int attempt = 0; attempt < maxRetries; attempt++)
      {
          if (attempt > 0)
          {
              await Task.Delay(retryDelayMs, cancellationToken);
          }

          // Sync Win32 call wrapped on the threadpool so concurrent callers
          // (one per hMonitor pipeline) dispatch to separate threads rather
          // than serializing on the calling thread before the first await.
          var (monitors, hasNullHandles) = await Task.Run(
              () =>
              {
                  var m = _discoveryHelper.GetPhysicalMonitors(hMonitor, out bool nulls);
                  return (m, nulls);
              },
              cancellationToken);

          if (monitors != null && !hasNullHandles)
          {
              return monitors;
          }

          lastResult = monitors;

          if (monitors != null && hasNullHandles && attempt < maxRetries - 1)
          {
              Logger.LogWarning($"DDC: Some monitors had NULL handles on attempt {attempt + 1}, will retry");
              continue;
          }

          if (monitors == null && attempt < maxRetries - 1)
          {
              Logger.LogWarning($"DDC: GetPhysicalMonitors returned null on attempt {attempt + 1}, will retry");
              continue;
          }

          if (monitors != null && hasNullHandles)
          {
              Logger.LogWarning($"DDC: NULL handles still present after {maxRetries} attempts, using filtered result");
          }

          return monitors;
      }

      return lastResult;
  ```

  (The signature line `private async Task<PHYSICAL_MONITOR[]?> GetPhysicalMonitorsWithRetryAsync(IntPtr hMonitor, CancellationToken cancellationToken)` and its doc comment are unchanged.)

- [ ] **Step 2: Build + run unit tests**

  ```bash
  "/c/Program Files/Microsoft Visual Studio/18/Enterprise/MSBuild/Current/Bin/MSBuild.exe" \
    "$(pwd)/src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplay.Lib.UnitTests.csproj" \
    -p:Configuration=Release -p:Platform=x64 -t:VSTest -nologo -v:minimal -restore 2>&1 | tail -5
  ```

  Expected: `Passed! - Failed: 0, Passed: 86, Skipped: 0`.

- [ ] **Step 3: Commit**

  ```bash
  git add src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs
  git commit -m "$(cat <<'EOF'
  refactor(PowerDisplay): wrap sync GetPhysicalMonitors in Task.Run for parallel dispatch

  GetPhysicalMonitorsWithRetryAsync still awaits Task.Delay for backoff,
  but the call to _discoveryHelper.GetPhysicalMonitors itself ran sync
  on the calling thread until the first await. With the upcoming per-
  hMonitor pipeline this would serialize all N pipelines on whatever
  thread called Task.WhenAll. Wrapping in await Task.Run dispatches the
  sync Win32 call to the threadpool, so concurrent callers each get
  their own slot.

  Behavior under the current single-foreach caller is unchanged.

  Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
  EOF
  )"
  ```

---

## Task 4: Add `DiscoverFromHandleAsync` per-hMonitor pipeline

Adds the async method that represents one full pipeline: GDI-name filter → `GetPhysicalMonitorsWithRetryAsync` → per-physical loop awaiting `Task.Run(BuildMonitorFromPhysical)`. Wraps everything in a try/catch that lets `OperationCanceledException` and `OutOfMemoryException` propagate but returns an empty list for any other exception. Dead code in this commit — wired up in Task 5.

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs`

- [ ] **Step 1: Add the helper method**

  Locate `GetPhysicalMonitorsWithRetryAsync` (around line 530 in the current file). **Immediately after** its closing brace, add:

  ```csharp
  /// <summary>
  /// Full per-hMonitor pipeline: GDI-name filter, get physical handles, and for each
  /// matching physical run <see cref="BuildMonitorFromPhysical"/> on the threadpool.
  /// Physical monitors that share an hMonitor (mirror mode) process sequentially —
  /// they share the GDI source and I2C arbitration. Parallelism across hMonitors is
  /// the caller's job (see <see cref="DiscoverMonitorsAsync"/>'s Task.WhenAll).
  /// </summary>
  /// <remarks>
  /// Catches all exceptions except <see cref="OperationCanceledException"/> and
  /// <see cref="OutOfMemoryException"/> — those propagate to Task.WhenAll and the
  /// surrounding MonitorManager.SafeDiscoverAsync wrapper.
  /// </remarks>
  private async Task<IReadOnlyList<Monitor>> DiscoverFromHandleAsync(
      IntPtr hMonitor,
      IReadOnlyDictionary<string, List<MonitorDisplayInfo>> targetsByGdi,
      CancellationToken cancellationToken)
  {
      try
      {
          cancellationToken.ThrowIfCancellationRequested();

          var gdiName = GetGdiDeviceName(hMonitor);
          if (string.IsNullOrEmpty(gdiName))
          {
              Logger.LogWarning($"DDC: Failed to get GDI device name for hMonitor 0x{hMonitor:X}");
              return Array.Empty<Monitor>();
          }

          if (!targetsByGdi.TryGetValue(gdiName, out var matchingInfos))
          {
              // GDI name not in the external targets list — either a Phase 0 internal
              // panel or a target QueryDisplayConfig didn't enumerate. Skip BEFORE the
              // expensive GetPhysicalMonitorsFromHMONITOR call.
              Logger.LogDebug($"DDC skipping {gdiName}: not in external targets list");
              return Array.Empty<Monitor>();
          }

          var physicals = await GetPhysicalMonitorsWithRetryAsync(hMonitor, cancellationToken);
          if (physicals == null || physicals.Length == 0)
          {
              Logger.LogWarning($"DDC: Failed to get physical monitors for {gdiName} after retries");
              return Array.Empty<Monitor>();
          }

          var monitors = new List<Monitor>();
          for (int i = 0; i < physicals.Length; i++)
          {
              if (i >= matchingInfos.Count)
              {
                  Logger.LogWarning(
                      $"DDC: Physical monitor index {i} exceeds available QueryDisplayConfig entries " +
                      $"({matchingInfos.Count}) for {gdiName}");
                  break;
              }

              cancellationToken.ThrowIfCancellationRequested();
              var physical = physicals[i];
              var info = matchingInfos[i];

              // Heavy sync block (~4 s caps fetch + up to 6 × ~100 ms VCP reads on this
              // one I2C bus). Dispatch to the threadpool; await it before the next physical
              // because they share the same hMonitor's I2C arbitration.
              var monitor = await Task.Run(
                  () => BuildMonitorFromPhysical(physical, info),
                  cancellationToken);

              if (monitor != null)
              {
                  monitors.Add(monitor);
              }
          }

          return monitors;
      }
      catch (Exception ex) when (
          ex is not OperationCanceledException &&
          ex is not OutOfMemoryException)
      {
          Logger.LogError($"DDC: pipeline exception for hMonitor=0x{hMonitor:X}: {ex.Message}");
          return Array.Empty<Monitor>();
      }
  }
  ```

- [ ] **Step 2: Build + run unit tests**

  ```bash
  "/c/Program Files/Microsoft Visual Studio/18/Enterprise/MSBuild/Current/Bin/MSBuild.exe" \
    "$(pwd)/src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplay.Lib.UnitTests.csproj" \
    -p:Configuration=Release -p:Platform=x64 -t:VSTest -nologo -v:minimal -restore 2>&1 | tail -5
  ```

  Expected: `Passed! - Failed: 0, Passed: 86, Skipped: 0`.

- [ ] **Step 3: Commit**

  ```bash
  git add src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs
  git commit -m "$(cat <<'EOF'
  refactor(PowerDisplay): add DiscoverFromHandleAsync per-monitor pipeline

  Adds the async method that owns one full per-hMonitor discovery flow:
  GDI-name filter, physical-handle retrieval, and per-physical
  BuildMonitorFromPhysical dispatched via Task.Run. Wraps in a
  try/catch that propagates OperationCanceledException and
  OutOfMemoryException but returns an empty list for any other failure,
  logging with hMonitor context.

  Not yet wired up — the orchestrator switch happens in the next commit.

  Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
  EOF
  )"
  ```

---

## Task 5: Rewrite `DiscoverMonitorsAsync`; remove dead phase code

Switch the orchestrator from three sequential phases to one `Task.WhenAll` over per-handle pipelines. Add `Stopwatch` instrumentation that produces the perf verification log line. Delete the now-unused `CollectCandidateMonitorsAsync`, `FetchCapabilitiesInParallelAsync`, `CreateValidMonitors`, and the `CandidateMonitor` record struct.

**Files:**
- Modify: `src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs`

- [ ] **Step 1: Add `using System.Diagnostics;` for `Stopwatch`**

  At the top of the file, the existing `using` block looks like:

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Runtime.InteropServices;
  using System.Threading;
  using System.Threading.Tasks;
  using ManagedCommon;
  ```

  Insert `using System.Diagnostics;` after `using System.Collections.Generic;`:

  ```csharp
  using System;
  using System.Collections.Generic;
  using System.Diagnostics;
  using System.Linq;
  using System.Runtime.InteropServices;
  using System.Threading;
  using System.Threading.Tasks;
  using ManagedCommon;
  ```

- [ ] **Step 2: Replace `DiscoverMonitorsAsync` body**

  Locate `DiscoverMonitorsAsync` (around line 285 in the current file). Replace its **entire body** (from `{` after the signature line through the matching `}`) with:

  ```csharp
      var stopwatch = Stopwatch.StartNew();

      var handles = EnumerateMonitorHandles();
      var targetsByGdi = BuildGdiLookup(targets);
      Logger.LogInfo(
          $"DDC: Discovery start — {handles.Count} candidate handles, {targets.Count} external targets");

      if (handles.Count == 0)
      {
          Logger.LogInfo($"DDC: Discovery complete in {stopwatch.ElapsedMilliseconds}ms — 0 monitors (no handles)");
          return Enumerable.Empty<Monitor>();
      }

      var pipelines = handles
          .Select(h => DiscoverFromHandleAsync(h, targetsByGdi, cancellationToken))
          .ToList();
      var results = await Task.WhenAll(pipelines);

      var monitors = results.SelectMany(r => r).ToList();
      _handleManager.UpdateHandleMap(monitors.ToDictionary(m => m.Id, m => m.Handle));

      Logger.LogInfo(
          $"DDC: Discovery complete in {stopwatch.ElapsedMilliseconds}ms — " +
          $"{monitors.Count}/{handles.Count} monitors");
      return monitors;
  ```

  Also update the method's doc comment (above its signature) so it matches the new model:

  ```csharp
  /// <summary>
  /// Discovers external DDC/CI-managed monitors. Each enumerated hMonitor runs its own
  /// async pipeline (filter → physical-handle retrieval → caps fetch + VCP init); all
  /// pipelines run concurrently via Task.WhenAll. Caller (MonitorManager) supplies the
  /// pre-filtered external-target list from Phase 0.
  /// </summary>
  /// <param name="targets">External-only display targets (pre-filtered by MonitorManager Phase 0).</param>
  /// <param name="cancellationToken">Cancellation token.</param>
  /// <returns>List of DDC/CI-managed external monitors.</returns>
  ```

- [ ] **Step 3: Delete the dead phase methods and record struct**

  Delete the following declarations from the file:

  - The `CandidateMonitor` record struct (declared near the top of the class, currently around lines 40–43):
    ```csharp
    private readonly record struct CandidateMonitor(
        IntPtr Handle,
        PHYSICAL_MONITOR PhysicalMonitor,
        MonitorDisplayInfo MonitorInfo);
    ```

  - The `CollectCandidateMonitorsAsync` method (currently around lines 228–293), including its doc comment.

  - The `FetchCapabilitiesInParallelAsync` method (currently around lines 295–312), including its doc comment.

  - The `CreateValidMonitors` method (currently around lines 314–394), including its doc comment.

  After deletion, search the file for the strings `CandidateMonitor`, `CollectCandidateMonitorsAsync`, `FetchCapabilitiesInParallelAsync`, `CreateValidMonitors` — there should be **zero remaining matches** anywhere in the file.

  ```bash
  grep -n 'CandidateMonitor\|CollectCandidateMonitorsAsync\|FetchCapabilitiesInParallelAsync\|CreateValidMonitors' \
    src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs
  ```

  Expected: no output.

- [ ] **Step 4: Build PowerDisplay.Lib + UnitTests, run tests**

  ```bash
  "/c/Program Files/Microsoft Visual Studio/18/Enterprise/MSBuild/Current/Bin/MSBuild.exe" \
    "$(pwd)/src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/PowerDisplay.Lib.UnitTests.csproj" \
    -p:Configuration=Release -p:Platform=x64 -t:VSTest -nologo -v:minimal -restore 2>&1 | tail -5
  ```

  Expected: `Passed! - Failed: 0, Passed: 86, Skipped: 0`.

- [ ] **Step 5: Build the PowerDisplay main app**

  ```bash
  "/c/Program Files/Microsoft Visual Studio/18/Enterprise/MSBuild/Current/Bin/MSBuild.exe" \
    "$(pwd)/src/modules/powerdisplay/PowerDisplay/PowerDisplay.csproj" \
    -p:Configuration=Release -p:Platform=x64 -nologo -v:minimal -restore 2>&1 | tail -5
  ```

  Expected: final line `PowerDisplay -> ...\WinUI3Apps\PowerToys.PowerDisplay.dll` with no build errors above.

- [ ] **Step 6: Commit**

  ```bash
  git add src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs
  git commit -m "$(cat <<'EOF'
  refactor(PowerDisplay): replace 3-phase DDC discovery with per-monitor pipelines

  DiscoverMonitorsAsync now fans out one async pipeline per enumerated
  hMonitor via Task.WhenAll. Each pipeline runs to completion
  independently (filter → physical-handle retrieval → caps fetch +
  VCP init), eliminating the cross-monitor serialization in old Phase 1
  + Phase 3 and the Phase 2→3 barrier where late-completing caps fetches
  blocked all VCP inits.

  Removes the now-dead CollectCandidateMonitorsAsync,
  FetchCapabilitiesInParallelAsync, CreateValidMonitors, and the
  CandidateMonitor record struct. Adds a Stopwatch + final-line log
  ("DDC: Discovery complete in {ms}ms — {n}/{m} monitors") that's the
  primary perf-verification primitive.

  Target: 3-monitor wall-clock 10s → 4-5s (bounded by single-monitor
  caps fetch ~4s, which is firmware-bound and not addressed here).

  No external API change. MonitorManager, IMonitorController,
  IDdcController, WMI side, and ViewModels untouched. 86/86 unit tests
  pass.

  Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
  EOF
  )"
  ```

---

## Task 6: Manual verification

The 86 unit tests don't exercise `DiscoverMonitorsAsync` (they cover `DisplayClassifier` only). Functional correctness must be verified by running the app against real hardware. This task has no code steps — it's a checklist that the implementer (or a reviewer) runs after the refactor commits land.

**Files:** none.

- [ ] **Step 1: Run the PowerDisplay app and trigger discovery**

  Launch `x64\Release\WinUI3Apps\PowerToys.PowerDisplay.exe` (built by Task 5). On launch the app calls `MainViewModel.RefreshMonitorsAsync` which calls `MonitorManager.DiscoverMonitorsAsync`.

  Capture the log file (typically under `%LOCALAPPDATA%\Microsoft\PowerToys\PowerDisplay\Logs`). Look for the new line:

  ```
  DDC: Discovery complete in {ms}ms — {n}/{m} monitors
  ```

  Record the `{ms}` value. Compare against a pre-refactor baseline (the user reported ~10 s for 3 external monitors before this refactor).

- [ ] **Step 2: Validate the functional matrix**

  For each scenario, confirm the listed expectation:

  | Scenario | Expected |
  |---|---|
  | 0 external monitors (laptop only) | DDC log shows `0 monitors`; WMI side discovers the internal panel; total app-level refresh time < 1 s |
  | 1 external monitor | 1 DDC monitor; brightness slider responds; capabilities populated (Contrast/Volume/InputSource/PowerState/ColorTemp flags reflect the monitor's MCCS support) |
  | 2 external monitors | 2 DDC monitors; wall-clock close to `max(single pipeline)`, **not** the sum of two pipelines |
  | 3 external monitors | 3 DDC monitors; wall-clock close to `max(single pipeline)`, **not** 3× one pipeline |
  | Mirror mode (1 hMonitor → 2 physical) | 2 monitors discovered; per-physical processing serial within that hMonitor; log shows both DevicePaths |
  | Refresh while a monitor is unplugged mid-discovery | Failed monitor skipped with a per-physical or per-hMonitor error log; other monitors discovered normally |

- [ ] **Step 3: Functional invariant spot-checks**

  Compare two or three monitors before/after the refactor (re-launch the app on the same hardware) and confirm:

  - `MonitorViewModel.Name` matches
  - `Brightness`, `Contrast`, `Volume`, `ColorTemperature` initial values match
  - `Supports*` flags match
  - Brightness slider drag still adjusts the monitor (round-trip through `MonitorManager.SetBrightnessAsync`)
  - Power-off (VCP 0xD6) still works on a monitor that supports it

- [ ] **Step 4: Push the branch**

  Once the checklist above passes:

  ```bash
  git push
  ```

---

## Self-Review

**Spec coverage:** every named symbol in the spec — `BuildGdiLookup` (Task 1), `BuildMonitorFromPhysical` (Task 2), `GetPhysicalMonitorsWithRetryAsync` (Task 3 modifies; signature unchanged), `DiscoverFromHandleAsync` (Task 4), `DiscoverMonitorsAsync` (Task 5 rewrites), `Stopwatch` log line (Task 5), per-pipeline try/catch and per-physical try/catch (Tasks 2 & 4), removed `CollectCandidateMonitorsAsync` / `FetchCapabilitiesInParallelAsync` / `CreateValidMonitors` / `CandidateMonitor` (Task 5), cancellation `ThrowIfCancellationRequested` checkpoints (Task 4), `_handleManager.UpdateHandleMap` single-write at end (Task 5). The verification matrix from the spec lives in Task 6.

**Placeholder scan:** no TBD/TODO/"similar to". Every code step has the full code an engineer would type. Every command has the exact form.

**Type consistency:** `BuildMonitorFromPhysical(PHYSICAL_MONITOR, MonitorDisplayInfo)` returns `Monitor?` consistently across Tasks 2 and 4. `DiscoverFromHandleAsync` returns `IReadOnlyList<Monitor>` consistently between Tasks 4 and 5. `BuildGdiLookup` returns `Dictionary<string, List<MonitorDisplayInfo>>`, consumed as `IReadOnlyDictionary<string, List<MonitorDisplayInfo>>` parameter in `DiscoverFromHandleAsync` — variance is correct (Dictionary implements IReadOnlyDictionary).
