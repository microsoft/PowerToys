// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class GPUStats : PerformanceCounterSourceBase, IDisposable
{
    // Performance counter category & counter names
    private const string GpuEngineCategoryName = "GPU Engine";
    private const string UtilizationPercentageCounter = "Utilization Percentage";

    private static readonly CompositeFormat TemperatureFormat = CompositeFormat.Parse("{0:0.} \u00B0C");

    // Instance-name key tokens
    private const string KeyPid = "pid";
    private const string KeyLuid = "luid";
    private const string KeyEngineType = "engtype";

    // Engine type filter
    private const string EngineType3D = "3D";

    // Instance-name key tokens for the physical adapter slot and engine index
    private const string KeyPhys = "phys";
    private const string KeyEng = "eng";

    // Display strings
    private const string GpuNamePrefix = "GPU ";
    private const string TemperatureUnavailable = "--";

    // Batch read via category - single kernel transition per tick
    private readonly PerformanceCounterCategory? _gpuEngineCategory;

    // Friendly adapter names (and software flag) keyed by LUID, resolved via DXGI.
    private readonly Dictionary<long, GpuAdapterNames.AdapterInfo> _adaptersByLuid;

    // LUIDs we've already turned into a _stats entry, or deliberately skipped
    // (e.g. software adapters). Used to discover GPUs at most once each.
    private readonly HashSet<long> _knownLuids = [];

    private readonly List<Data> _stats = [];

    // Guards structural access to _stats, _knownLuids, and _adaptersByLuid. They
    // are mutated on the perf-counter timer thread (GetData -> DiscoverGpus) but
    // read on the UI / command thread (GetGPUName, GetPrev/NextGPUIndex, etc.),
    // and List<T> is not safe for concurrent add/read.
    private readonly object _statsLock = new();

    // Previous raw samples for computing cooked (delta-based) values
    private Dictionary<string, CounterSample> _previousSamples = [];
    private bool _gpuEnumerationFailureLogged;
    private bool _gpuReadFailureLogged;

    public sealed class Data
    {
        public string? Name { get; set; }

        public long LuidKey { get; set; }

        public float Usage { get; set; }

        public float Temperature { get; set; }

        public List<float> GpuChartValues { get; set; } = [];
    }

    public GPUStats()
    {
        _gpuEngineCategory = CreatePerformanceCounterCategory(GpuEngineCategoryName);
        _adaptersByLuid = GpuAdapterNames.GetByLuid();

        DiscoverGPUsFromCounters();
    }

    private void DiscoverGPUsFromCounters()
    {
        if (_gpuEngineCategory is null)
        {
            return;
        }

        try
        {
            // The old Dev Home code keyed GPUs by the "phys_N" token in the
            // instance name, assuming it enumerated physical adapters. On modern
            // Windows that token is effectively always "phys_0" - even on machines
            // with multiple discrete GPUs - so every adapter collapsed into a
            // single bucket and Prev/Next GPU had nothing to cycle through. The
            // real per-adapter identifier is the LUID, so we key on that instead.
            var instanceNames = _gpuEngineCategory.GetInstanceNames();

            var seenLuids = new HashSet<long>();
            foreach (var instanceName in instanceNames)
            {
                if (!instanceName.EndsWith(EngineType3D, StringComparison.InvariantCulture))
                {
                    continue;
                }

                if (TryGetLuidAndEngine(instanceName, out var luidKey, out _))
                {
                    seenLuids.Add(luidKey);
                }
            }

            DiscoverGpus(seenLuids);
        }
        catch (Exception ex)
        {
            LogFailureOnce(ref _gpuEnumerationFailureLogged, "Failed while enumerating GPU performance counters.", ex);
        }
    }

    public void GetData()
    {
        if (_gpuEngineCategory is null)
        {
            return;
        }

        try
        {
            // Single batch read - one kernel transition for ALL GPU Engine instances
            var categoryData = _gpuEngineCategory.ReadCategory();

            if (!categoryData.Contains(UtilizationPercentageCounter))
            {
                return;
            }

            var utilizationData = categoryData[UtilizationPercentageCounter];

            // Accumulate utilization for each (adapter, 3D engine) pair. Each instance
            // (pid_<pid>_luid_<luid>_phys_<phys>_eng_<engId>_engtype_3D) reports the percentage
            // of wall-clock time a single process spent on that engine. Summing across processes
            // for the same engine is correct (gives total engine utilization). Summing across
            // multiple 3D engines on the same adapter, however, is NOT - that produced values
            // >100% in the dock under heavy GPU load (issue #48677). Mirroring Task Manager,
            // we take the maximum 3D engine utilization per adapter and clamp to [0, 100].
            // This parallels the CPU fix in #46381, which switched to a counter that is
            // naturally bounded to 0-100%. Adapters are keyed by LUID rather than the
            // "phys_N" token, which is effectively always 0 even on multi-GPU machines and
            // so cannot tell adapters apart.
            var perEngineUsage = new Dictionary<(long Luid, string EngineId), float>();
            var currentSamples = new Dictionary<string, CounterSample>();
            var seenLuids = new HashSet<long>();

            foreach (InstanceData instance in utilizationData.Values)
            {
                var instanceName = instance.InstanceName;
                if (!instanceName.EndsWith(EngineType3D, StringComparison.InvariantCulture))
                {
                    continue;
                }

                if (!TryGetLuidAndEngine(instanceName, out var luidKey, out var engineId))
                {
                    continue;
                }

                // Just record which adapters we saw; discovery of new ones is
                // batched after the loop so we don't take _statsLock per instance
                // (there can be hundreds of instances per tick).
                seenLuids.Add(luidKey);

                var sample = instance.Sample;
                currentSamples[instanceName] = sample;

                if (_previousSamples.TryGetValue(instanceName, out var prevSample))
                {
                    try
                    {
                        var cookedValue = CounterSampleCalculator.ComputeCounterValue(prevSample, sample);
                        if (float.IsNaN(cookedValue) || float.IsInfinity(cookedValue) || cookedValue < 0f)
                        {
                            continue;
                        }

                        var key = (luidKey, engineId);
                        perEngineUsage[key] = perEngineUsage.GetValueOrDefault(key) + cookedValue;
                    }
                    catch (Exception)
                    {
                        // Skip this instance on calculation error.
                    }
                }
            }

            // Swap samples - stale entries are automatically cleaned up
            _previousSamples = currentSamples;

            // Discover adapters we haven't seen before. Batched: one lock check
            // per tick, and any DXGI name enumeration happens off the lock. New
            // adapters land in _stats before the update loop so they get a value
            // this tick.
            DiscoverGpus(seenLuids);

            // Reduce per-engine values to a single 0-100 utilization per adapter (max across engines).
            var gpuUsage = new Dictionary<long, float>();
            foreach (var kvp in perEngineUsage)
            {
                if (kvp.Value > gpuUsage.GetValueOrDefault(kvp.Key.Luid))
                {
                    gpuUsage[kvp.Key.Luid] = kvp.Value;
                }
            }

            // Update stats
            lock (_statsLock)
            {
                foreach (var gpu in _stats)
                {
                    var raw = gpuUsage.TryGetValue(gpu.LuidKey, out var usage) ? usage : 0f;
                    var clamped = Math.Clamp(raw, 0f, 100f);
                    gpu.Usage = clamped / 100f;
                    lock (gpu.GpuChartValues)
                    {
                        ChartHelper.AddNextChartValue(clamped, gpu.GpuChartValues);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogFailureOnce(ref _gpuReadFailureLogged, "Failed while reading GPU performance counters.", ex);
        }
    }

    // Adds any newly-seen adapters to _stats. Called once per tick with the set
    // of LUIDs observed this tick, so the hot per-instance path never touches the
    // stats lock. The slow part - DXGI name enumeration for an adapter that only
    // appeared after construction (e.g. an eGPU hot-plug) - is done outside the
    // lock, since _statsLock is also held by the UI / command accessors.
    private void DiscoverGpus(HashSet<long> seenLuids)
    {
        List<long>? newLuids = null;
        var needDxgiRefresh = false;

        lock (_statsLock)
        {
            foreach (var luidKey in seenLuids)
            {
                if (_knownLuids.Contains(luidKey))
                {
                    continue;
                }

                (newLuids ??= []).Add(luidKey);
                if (!_adaptersByLuid.ContainsKey(luidKey))
                {
                    needDxgiRefresh = true;
                }
            }
        }

        // Common case: nothing new, so we took the lock exactly once this tick.
        if (newLuids is null)
        {
            return;
        }

        // A newly-seen adapter that isn't in the cached name map registered
        // after we were constructed, so re-enumerate DXGI to pick up its friendly
        // name. Done outside the lock because enumeration can be slow.
        var refreshedNames = needDxgiRefresh ? GpuAdapterNames.GetByLuid() : null;

        lock (_statsLock)
        {
            if (refreshedNames is not null)
            {
                foreach (var adapter in refreshedNames)
                {
                    _adaptersByLuid[adapter.Key] = adapter.Value;
                }
            }

            foreach (var luidKey in newLuids)
            {
                AddGpuLocked(luidKey);
            }
        }
    }

    // Adds a single adapter to _stats. The caller must hold _statsLock, and must
    // already have a name for this LUID cached in _adaptersByLuid if one exists.
    private void AddGpuLocked(long luidKey)
    {
        if (!_knownLuids.Add(luidKey))
        {
            return;
        }

        _adaptersByLuid.TryGetValue(luidKey, out var info);

        // Hide software adapters (Microsoft Basic Render Driver / WARP) only when
        // there's a real GPU to show instead. On VMs / RDP / headless boxes the
        // software adapter is the only one present, so keep it rather than
        // leaving the band with nothing to display.
        if (info.IsSoftware && HasHardwareAdapter())
        {
            return;
        }

        var name = string.IsNullOrEmpty(info.Description)
            ? GpuNamePrefix + _stats.Count
            : info.Description;

        _stats.Add(new Data() { LuidKey = luidKey, Name = name });
    }

    // True if DXGI reports at least one non-software adapter in the system. DXGI
    // enumerates hardware adapters regardless of power state, so this stays
    // correct even when the real GPU is idle and hasn't produced counters yet.
    // Caller must hold _statsLock (reads _adaptersByLuid).
    private bool HasHardwareAdapter()
    {
        foreach (var adapter in _adaptersByLuid.Values)
        {
            if (!adapter.IsSoftware)
            {
                return true;
            }
        }

        return false;
    }

    internal string CreateGPUImageUrl(int gpuChartIndex)
    {
        lock (_statsLock)
        {
            if (_stats.Count <= gpuChartIndex)
            {
                return string.Empty;
            }

            return ChartHelper.CreateImageUrl(_stats[gpuChartIndex].GpuChartValues, ChartHelper.ChartType.GPU);
        }
    }

    internal string GetGPUName(int gpuActiveIndex)
    {
        lock (_statsLock)
        {
            if (_stats.Count <= gpuActiveIndex)
            {
                return string.Empty;
            }

            return _stats[gpuActiveIndex].Name ?? string.Empty;
        }
    }

    internal int GetPrevGPUIndex(int gpuActiveIndex)
    {
        lock (_statsLock)
        {
            if (_stats.Count == 0)
            {
                return 0;
            }

            if (gpuActiveIndex == 0)
            {
                return _stats.Count - 1;
            }

            return gpuActiveIndex - 1;
        }
    }

    internal int GetNextGPUIndex(int gpuActiveIndex)
    {
        lock (_statsLock)
        {
            if (_stats.Count == 0)
            {
                return 0;
            }

            if (gpuActiveIndex == _stats.Count - 1)
            {
                return 0;
            }

            return gpuActiveIndex + 1;
        }
    }

    internal float GetGPUUsage(int gpuActiveIndex, string gpuActiveEngType)
    {
        lock (_statsLock)
        {
            if (_stats.Count <= gpuActiveIndex)
            {
                return 0;
            }

            return _stats[gpuActiveIndex].Usage;
        }
    }

    internal string GetGPUTemperature(int gpuActiveIndex)
    {
        // MG Jan 2026: This code was lifted from the old Dev Home codebase.
        // However, the performance counters for GPU temperature are not being
        // collected. So this function always returns "--" for now.
        //
        // I have not done the code archeology to figure out why they were
        // removed.
        lock (_statsLock)
        {
            if (_stats.Count <= gpuActiveIndex)
            {
                return TemperatureUnavailable;
            }

            var temperature = _stats[gpuActiveIndex].Temperature;
            if (temperature == 0)
            {
                return TemperatureUnavailable;
            }

            return string.Format(CultureInfo.InvariantCulture, TemperatureFormat.Format, temperature);
        }
    }

    private static bool TryGetLuidAndEngine(string instanceName, out long luidKey, out string engineId)
    {
        // Instance names look like:
        //   pid_1234_luid_0x00000000_0x0001766D_phys_0_eng_0_engtype_3D
        // Advance past pid, read the luid, skip phys, then read the engine index.
        luidKey = 0;
        engineId = string.Empty;

        var counterKey = instanceName;
        GetKeyValueFromCounterKey(KeyPid, ref counterKey);
        var luid = GetKeyValueFromCounterKey(KeyLuid, ref counterKey);
        GetKeyValueFromCounterKey(KeyPhys, ref counterKey);
        engineId = GetKeyValueFromCounterKey(KeyEng, ref counterKey);

        if (string.IsNullOrEmpty(engineId) || engineId == "error")
        {
            return false;
        }

        return TryParseLuidKey(luid, out luidKey);
    }

    private static bool TryParseLuidKey(string luid, out long luidKey)
    {
        luidKey = 0;

        // The luid token is "0x{HighPart}_0x{LowPart}", matching DXGI's
        // LUID.HighPart / LUID.LowPart so the key lines up with GpuAdapterNames.
        var separator = luid.IndexOf('_');
        if (separator < 0)
        {
            return false;
        }

        if (!TryParseHex(luid.AsSpan(0, separator), out var high) ||
            !TryParseHex(luid.AsSpan(separator + 1), out var low))
        {
            return false;
        }

        luidKey = ((long)high << 32) | low;
        return true;
    }

    private static bool TryParseHex(ReadOnlySpan<char> token, out uint value)
    {
        if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            token = token[2..];
        }

        return uint.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
    }

    private static string GetKeyValueFromCounterKey(string key, ref string counterKey)
    {
        if (!counterKey.StartsWith(key, StringComparison.InvariantCulture))
        {
            return "error";
        }

        counterKey = counterKey.Substring(key.Length + 1);
        if (key.Equals(KeyEngineType, StringComparison.Ordinal))
        {
            return counterKey;
        }

        var pos = counterKey.IndexOf('_');
        if (key.Equals(KeyLuid, StringComparison.Ordinal))
        {
            pos = counterKey.IndexOf('_', pos + 1);
        }

        var retValue = counterKey.Substring(0, pos);
        counterKey = counterKey.Substring(pos + 1);
        return retValue;
    }

    public void Dispose()
    {
        _previousSamples.Clear();
    }
}
