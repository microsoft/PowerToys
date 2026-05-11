# PowerDisplay Discovery Pipeline Redesign

**Date**: 2026-05-11
**Branch**: `yuleng/pd/discovery/1`
**Author**: yuleng

## Context

[DdcCiController.DiscoverMonitorsAsync](../../../src/modules/powerdisplay/PowerDisplay.Lib/Drivers/DDC/DdcCiController.cs) is currently structured as three horizontal phases run in sequence:

1. **Phase 1** — `CollectCandidateMonitorsAsync`: iterate `EnumDisplayMonitors` handles, call `GetPhysicalMonitorsFromHMONITOR` for each (sequential).
2. **Phase 2** — `FetchCapabilitiesInParallelAsync`: parallel `Task.WhenAll` of `DdcCiNative.FetchCapabilities` over candidates (already parallel, but barrier afterwards).
3. **Phase 3** — `CreateValidMonitors`: iterate fetch results, for each valid monitor call up to 6 `Initialize*` VCP reads (sequential within and across monitors).

Measured wall-clock for 3 external monitors: **~10 seconds**.

The breakdown:
- Phase 0 (`DisplayConfigInventory.GetAllMonitorDisplayInfo`): <100 ms
- Phase 1: ~0.6–3 s (sequential `GetPhysicalMonitorsFromHMONITOR`, 300 ms–1 s each, plus retry delays)
- Phase 2: ~4–5 s (parallel; the 4 s/monitor is firmware-bound at I2C+MCCS string fetch)
- Phase 3: ~1.8–4 s (sequential across monitors × 6 sequential VCP reads × ~100 ms each)

Three structural problems:

1. **Phase 1 is serialized** across hMonitors despite each `GetPhysicalMonitorsFromHMONITOR` being independent.
2. **Phase 3 is serialized** across monitors, and its 6 `Initialize*` calls per monitor are also serial — the cross-monitor serialization is the avoidable cost.
3. **Phase 2→3 barrier**: even after Phase 2 parallelism, the slowest caps fetch blocks all Phase 3 VCP inits. Monitors whose caps come back early sit idle.

## Goals

- Eliminate cross-monitor serial waits in Phase 1 and Phase 3.
- Eliminate the Phase 2→3 barrier.
- Maintain failure isolation: one monitor failing doesn't kill discovery for others.
- Maintain cancellation semantics: ct signals abort the entire discovery, no partial results.
- Maintain diagnostic logging fidelity (per-monitor errors retain context).
- Single-file refactor (`DdcCiController.cs`). No `MonitorManager` or external caller changes.
- No new dependencies.

Target: 3-monitor wall-clock from ~10 s → **~4–5 s** (bounded by single-monitor Phase 2 firmware time, which we don't address in this work).

## Non-goals

- **Cross-discovery caching of `CapabilitiesRaw`** (would let refresh skip the ~4 s caps fetch). Separate future work; needs an invalidation strategy.
- **Speeding up a single monitor's caps fetch**. The ~4 s I2C+MCCS string fetch is firmware-bound; no software change can reduce it.
- **New unit-test infrastructure**. Existing `DisplayClassifierTests` (86 tests) stay green; integration testing remains manual.

## Design

### Architecture — vertical per-monitor pipelines

Replace the three horizontal phases with **N parallel vertical pipelines**, one per hMonitor:

```
externalTargets
    │
    ▼  BuildGdiLookup + EnumerateMonitorHandles      (sync, fast, main thread)
    │
    ▼  Task.WhenAll(handles.Select(h => DiscoverFromHandleAsync(h, lookup, ct)))
    │
    │   ┌─ DiscoverFromHandleAsync(hM₁) ──────────────────┐   threadpool, parallel
    │   │  GetGdiDeviceName + lookup filter               │
    │   │  await GetPhysicalMonitorsWithRetryAsync (Task.Run inside)
    │   │  for each (physical, info) on this hM:          │   sequential within hM
    │   │     await Task.Run(BuildMonitorFromPhysical):   │
    │   │       FetchCapabilities  (~4 s I2C)             │
    │   │       CreateMonitorFromPhysical                 │
    │   │       UpdateCapabilitiesFromVcp                 │
    │   │       Initialize {InputSource, ColorTemp,       │
    │   │                   PowerState, Contrast,         │
    │   │                   Volume, Brightness}           │
    │   └─────────────────────────────────────────────────┘
    │   ┌─ DiscoverFromHandleAsync(hM₂) ──────────────────┐   independent threadpool slot
    │   │   …                                              │
    │   └─────────────────────────────────────────────────┘
    │   …
    ▼
    Flatten + UpdateHandleMap(once, main thread)
    ▼
    List<Monitor>
```

**Key properties**:

1. **No inter-phase barrier.** Each pipeline runs to completion independently.
2. **Single fan-out point** at the top via `Task.WhenAll`.
3. **Mirror mode safety**: within one hMonitor, multiple physical monitors process **sequentially** (they share the GDI source and I2C arbitration). Cross-hMonitor is parallel.
4. **Per-physical failure isolation** at the granularity where exceptions actually originate (I2C / VCP reads).

### Async/threading model — Pattern B (async throughout, Task.Run at sync boundaries)

```csharp
public async Task<IEnumerable<Monitor>> DiscoverMonitorsAsync(
    IReadOnlyList<MonitorDisplayInfo> targets,
    CancellationToken cancellationToken = default)
{
    var targetsByGdi = BuildGdiLookup(targets);
    var handles = EnumerateMonitorHandles();

    var pipelines = handles
        .Select(h => DiscoverFromHandleAsync(h, targetsByGdi, cancellationToken))
        .ToList();
    var results = await Task.WhenAll(pipelines);

    var monitors = results.SelectMany(r => r).ToList();
    _handleManager.UpdateHandleMap(monitors.ToDictionary(m => m.Id, m => m.Handle));
    return monitors;
}

private async Task<IReadOnlyList<Monitor>> DiscoverFromHandleAsync(
    IntPtr hMonitor,
    IReadOnlyDictionary<string, List<MonitorDisplayInfo>> targetsByGdi,
    CancellationToken ct)
{
    try
    {
        ct.ThrowIfCancellationRequested();
        var gdiName = GetGdiDeviceName(hMonitor);
        if (string.IsNullOrEmpty(gdiName) ||
            !targetsByGdi.TryGetValue(gdiName, out var infos))
        {
            return Array.Empty<Monitor>();
        }

        // Slow Win32 wrapped in Task.Run inside the helper; retry backoff via Task.Delay.
        var physicals = await GetPhysicalMonitorsWithRetryAsync(hMonitor, ct);
        if (physicals == null || physicals.Length == 0)
        {
            return Array.Empty<Monitor>();
        }

        // Sequential within an hMonitor (shared I2C arbitration), parallel across hMonitors
        // via the outer Task.WhenAll.
        var monitors = new List<Monitor>();
        for (int i = 0; i < physicals.Length && i < infos.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var physical = physicals[i];
            var info = infos[i];
            var monitor = await Task.Run(
                () => BuildMonitorFromPhysical(physical, info),
                ct);
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
        Logger.LogError(
            $"DDC: pipeline exception for hMonitor=0x{hMonitor:X}: {ex.Message}");
        return Array.Empty<Monitor>();
    }
}

private async Task<PHYSICAL_MONITOR[]?> GetPhysicalMonitorsWithRetryAsync(
    IntPtr hMonitor, CancellationToken ct)
{
    const int maxRetries = 3;
    const int retryDelayMs = 200;
    PHYSICAL_MONITOR[]? lastResult = null;

    for (int attempt = 0; attempt < maxRetries; attempt++)
    {
        if (attempt > 0)
        {
            await Task.Delay(retryDelayMs, ct); // non-blocking backoff
        }

        var (monitors, hasNullHandles) = await Task.Run(
            () =>
            {
                var m = _discoveryHelper.GetPhysicalMonitors(hMonitor, out bool nulls);
                return (m, nulls);
            },
            ct);

        if (monitors != null && !hasNullHandles)
        {
            return monitors;
        }
        lastResult = monitors;
        if (attempt < maxRetries - 1)
        {
            Logger.LogWarning(
                $"DDC: GetPhysicalMonitors attempt {attempt + 1} returned null or had NULL handles, retrying");
        }
    }
    return lastResult;
}

// Pure synchronous heavy work — caller wraps in Task.Run.
private Monitor? BuildMonitorFromPhysical(
    PHYSICAL_MONITOR physical, MonitorDisplayInfo info)
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
                InitializeInputSource(monitor, physical.HPhysicalMonitor);
            if (monitor.SupportsColorTemperature)
                InitializeColorTemperature(monitor, physical.HPhysicalMonitor);
            if (monitor.SupportsPowerState)
                InitializePowerState(monitor, physical.HPhysicalMonitor);
            if (monitor.SupportsContrast)
                InitializeContrast(monitor, physical.HPhysicalMonitor);
            if (monitor.SupportsVolume)
                InitializeVolume(monitor, physical.HPhysicalMonitor);
        }
        if (monitor.SupportsBrightness)
            InitializeBrightness(monitor, physical.HPhysicalMonitor);

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

**Threading invariants**:

- `Task.Run` appears at **exactly two sites**: wrapping the sync `GetPhysicalMonitors` Win32 call, and wrapping the sync `BuildMonitorFromPhysical` chunk. Both are unavoidable threadpool dispatches for truly synchronous I/O.
- `Task.Delay` (non-blocking backoff) is the only delay primitive — **no `Thread.Sleep`**.
- The outer `Task.WhenAll` provides parallelism. Each pipeline's first `await` (inside `GetPhysicalMonitorsWithRetryAsync`) dispatches that pipeline onto a separate threadpool slot.
- `DiscoverFromHandleAsync`'s pre-await code (`GetGdiDeviceName` + dict lookup, ~ms) runs on the calling thread sequentially for all pipelines. Total <1 ms for N=4; negligible.

### Method-level API changes (single file: `DdcCiController.cs`)

**Removed**:

| Symbol | Reason |
|---|---|
| `CollectCandidateMonitorsAsync` | Phase 1 is no longer a discrete stage |
| `FetchCapabilitiesInParallelAsync` | Phase 2 is no longer a discrete stage |
| `CreateValidMonitors` | Phase 3 is no longer a discrete stage |
| `CandidateMonitor` record struct | No intermediate list materialized |
| `GetPhysicalMonitorsWithRetryAsync` (old, `Task.Delay` outside Task.Run) | Replaced by version above |

**Added**:

| Symbol | Signature |
|---|---|
| `DiscoverFromHandleAsync` | `private async Task<IReadOnlyList<Monitor>> DiscoverFromHandleAsync(IntPtr, IReadOnlyDictionary<string, List<MonitorDisplayInfo>>, CancellationToken)` |
| `BuildMonitorFromPhysical` | `private Monitor? BuildMonitorFromPhysical(PHYSICAL_MONITOR, MonitorDisplayInfo)` |
| `BuildGdiLookup` | `private static Dictionary<string, List<MonitorDisplayInfo>> BuildGdiLookup(IReadOnlyList<MonitorDisplayInfo>)` — extracts the existing inline grouping logic |

**Reused unchanged**:

`EnumerateMonitorHandles`, `GetGdiDeviceName`, `Initialize{Brightness,Contrast,Volume,ColorTemperature,InputSource,PowerState}`, `TryGetVcpFeature`, `UpdateMonitorCapabilitiesFromVcp`, `_discoveryHelper.CreateMonitorFromPhysical`, `_handleManager.UpdateHandleMap`, all VCP get/set methods used outside of discovery.

**Rewritten**:

`DiscoverMonitorsAsync` — body shrinks to ~10 lines of orchestration as shown above.

### Failure isolation

```
DiscoverMonitorsAsync                       no catch (relies on SafeDiscoverAsync upstream)
    │
    ├─ DiscoverFromHandleAsync(hM)          try/catch — exception → empty list for this hM
    │     │                                   (does NOT catch OperationCanceledException
    │     │                                    or OutOfMemoryException; those propagate)
    │     │
    │     └─ BuildMonitorFromPhysical       try/catch — exception → null for this physical
    │                                        (does NOT catch OutOfMemoryException)
    │
    └─ Task.WhenAll(...)                    no AggregateException because inner exceptions caught;
                                            only OperationCanceledException can bubble up
```

`OperationCanceledException` reaches `Task.WhenAll`, propagates to `DiscoverMonitorsAsync`, caught by `MonitorManager.SafeDiscoverAsync` → returns empty list. Same observable behavior as today.

### Cancellation

- `ct` passed to all `Task.Run`, `Task.Delay`, and `await Task.WhenAll`.
- `ct.ThrowIfCancellationRequested()` at three checkpoints inside each pipeline:
  - Top of `DiscoverFromHandleAsync`
  - Before each iteration of the per-physical loop
  - (Win32 calls themselves are uncancellable; check happens between them, not during.)
- Semantic: cancel = abort discovery, no partial results returned. Matches current behavior.

### Logging

| Level | Message | When |
|---|---|---|
| Info | `DDC: Discovery start — {N} candidate handles, {M} external targets` | top of `DiscoverMonitorsAsync` |
| Warn | `DDC: hMonitor=0x{X} GDI not in targets, skip` | pipeline filter miss (existing) |
| Warn | `DDC: GetPhysicalMonitors attempt {n} returned null or had NULL handles, retrying` | inside retry helper |
| Warn | `DDC: GetPhysicalMonitors failed after retries for hMonitor=0x{X}` | retry exhausted |
| Warn | `DDC: [DevicePath={p}] FetchCapabilities returned Invalid` | per-physical caps-invalid (new diagnostic) |
| Error | `DDC: pipeline exception hMonitor=0x{X}: {message}` | per-hMonitor exception caught |
| Error | `DDC: [DevicePath={p}] BuildMonitorFromPhysical exception: {message}` | per-physical exception caught |
| Info | `DDC: Discovery complete in {ms}ms — {success}/{candidates} monitors` | bottom of `DiscoverMonitorsAsync` |

The final-line `Stopwatch` measurement is the verification primitive for the ~10 s → ~4–5 s claim.

## Verification

### Manual test matrix

| Scenario | Expected |
|---|---|
| 0 external monitors (laptop panel only) | DDC returns empty; total < 500 ms |
| 1 external monitor | 1 monitor with populated VCP values |
| 2 external monitors | 2 monitors; wall-clock ≈ max(single pipeline), not 2× |
| 3 external monitors | 3 monitors; wall-clock ≈ max(single pipeline), not 3× |
| Mirror mode (1 hMonitor → 2 physical) | 2 monitors, processed sequentially within the same hMonitor |
| Refresh during disconnect/reconnect | Failed monitor skipped; others discovered normally |

### Functional invariants (compare to pre-refactor behavior)

- Discovered monitor count for a given hardware config is identical.
- Each `Monitor` has the same `CapabilitiesRaw`, `Supports{Brightness,Contrast,Volume,ColorTemperature,InputSource,PowerState}` flags, `Current*` values, and `*VcpMax` raw maximums.
- `_handleManager` ends with the same `monitorId → handle` map.

### Performance verification

Single metric: the new `Discovery complete in {ms}ms` log line. Compare median of 5 runs before/after on the same hardware. Target: ≥ 40% reduction at 3-monitor configurations.

### Tests

86 existing `DisplayClassifier` unit tests must remain green. No new tests required for this work (integration with Win32 DDC/CI is not unit-testable without nontrivial mocking infrastructure, which is out of scope).

## Risk & rollback

| Risk | Mitigation |
|---|---|
| Mirror mode I2C contention | Per-physical loop is sequential within an hMonitor. Same constraint as before. |
| `_handleManager` concurrent writes | New design writes to `_handleManager` **once** from `DiscoverMonitorsAsync` after `Task.WhenAll` returns — single-threaded write site. |
| Exception swallowing hides real bugs | `Exception ex when ex is not OutOfMemoryException` (and not `OperationCanceledException` at the hMonitor level) — matches the pattern already used elsewhere in this file. Exceptions are logged at Error level with stack-free message; if needed we can add `ex.StackTrace` to the log. |
| Cancellation racing with `_handleManager.UpdateHandleMap` | `ct.ThrowIfCancellationRequested()` only runs before the update; if cancellation fires mid-pipeline, `Task.WhenAll` throws before reaching the update line. |

**Rollback**: change is isolated to `DdcCiController.cs`. `MonitorManager`, `IMonitorController`, `IDdcController`, the WMI side, and ViewModels are untouched. `git revert` of the single commit suffices.

## Out of scope (followups)

- **Caps caching across discovery runs.** Adding a per-`DevicePath` cache of `CapabilitiesRaw` + `VcpCapabilitiesInfo` (with EDID-hash-keyed invalidation) would let refresh skip the firmware-bound 4 s for unchanged hardware. Estimated separate work: ~80 lines + invalidation policy decision.
- **Single-monitor caps speedup**: not addressable in software (firmware/I2C-bound).
