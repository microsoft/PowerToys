// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.CmdPal.Ext.PerformanceMonitor;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class GPUStats : PerformanceCounterSourceBase, IDisposable
{
    // Performance counter category & counter names
    private const string GpuEngineCategoryName = "GPU Engine";
    private const string UtilizationPercentageCounter = "Utilization Percentage";
    private const string GpuAdapterMemoryCategoryName = "GPU Adapter Memory";
    private const string DedicatedUsageCounter = "Dedicated Usage";
    private const string SharedUsageCounter = "Shared Usage";

    private static readonly CompositeFormat TemperatureFormat = CompositeFormat.Parse("{0:0.} \u00B0C");

    /// <summary>
    /// The engine types the card shows, in Task Manager's order. The first, 3D, is the GPU's
    /// overall utilization.
    /// </summary>
    internal static readonly string[] EngineTypes = ["3D", "Copy", "VideoDecode", "VideoEncode"];

    private static readonly string[] EngineTypeSuffixes = ["3D", "engtype_Copy", "engtype_VideoDecode", "engtype_VideoEncode"];

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
    private readonly PerformanceCounterCategory? _gpuAdapterMemoryCategory;

    // Friendly adapter names (and software flag) keyed by LUID, resolved via DXGI.
    private readonly Dictionary<long, GpuAdapterNames.AdapterInfo> _adaptersByLuid;

    // LUIDs we've already turned into a _stats entry, or deliberately skipped
    // (e.g. software adapters). Used to discover GPUs at most once each.
    private readonly HashSet<long> _knownLuids = [];

    private readonly List<Data> _stats = [];

    // Guards structural access to _stats, _knownLuids, and _adaptersByLuid. They
    // are mutated on the perf-counter timer thread (GetData -> DiscoverGpus) but
    // read on the UI / command thread (GetGPUDisplayInfo, GetPrev/NextGPUIndex, etc.),
    // and List<T> is not safe for concurrent add/read.
    private readonly object _statsLock = new();

    // Previous raw samples for computing cooked (delta-based) values
    private Dictionary<string, CounterSample> _previousSamples = [];
    private bool _gpuEnumerationFailureLogged;
    private bool _gpuReadFailureLogged;
    private bool _gpuMemoryReadFailureLogged;

    internal sealed record DisplayInfo(string Name, string ShortName, int AdapterCount);

    /// <summary>An adapter's memory use and size, in bytes. Sizes are zero when unknown.</summary>
    internal readonly record struct MemoryInfo(ulong DedicatedUsed, ulong DedicatedTotal, ulong SharedUsed, ulong SharedTotal);

    /// <summary>The utilization of one engine type, as an index into <see cref="EngineTypes"/>.</summary>
    internal readonly record struct EngineUsage(int Type, float Percent);

    public sealed class Data
    {
        public string? Name { get; set; }

        public string ShortName { get; init; } = string.Empty;

        public long LuidKey { get; set; }

        public float Usage { get; set; }

        public float Temperature { get; set; }

        public List<float> GpuChartValues { get; set; } = [];

        public ulong DedicatedMemoryTotal { get; init; }

        public ulong SharedMemoryTotal { get; init; }

        public ulong DedicatedMemoryUsed { get; set; }

        public ulong SharedMemoryUsed { get; set; }

        /// <summary>Gets or sets the utilization of each engine type the adapter has, in percent.</summary>
        public EngineUsage[] Engines { get; set; } = [];
    }

    public GPUStats()
        : this(GpuAdapterNames.GetByLuid(), [])
    {
        _gpuEngineCategory = CreatePerformanceCounterCategory(GpuEngineCategoryName);
        _gpuAdapterMemoryCategory = CreatePerformanceCounterCategory(GpuAdapterMemoryCategoryName, logFailure: false);
        DiscoverGPUsFromCounters();
    }

    internal GPUStats(Dictionary<long, GpuAdapterNames.AdapterInfo> adaptersByLuid, IEnumerable<long> gpuLuids)
    {
        _adaptersByLuid = adaptersByLuid;
        lock (_statsLock)
        {
            foreach (var luid in gpuLuids)
            {
                AddGpuLocked(luid);
            }
        }
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

            // Accumulate utilization for each (adapter, engine) pair. Each instance
            // (pid_<pid>_luid_<luid>_phys_<phys>_eng_<engId>_engtype_3D) reports the percentage
            // of wall-clock time a single process spent on that engine. Summing across processes
            // for the same engine is correct (gives total engine utilization). Summing across
            // multiple engines of the same type on the same adapter, however, is NOT - that produced
            // values >100% in the dock under heavy GPU load (issue #48677). Mirroring Task Manager,
            // we take the maximum utilization per adapter and engine type and clamp to [0, 100].
            // The 3D engines give the adapter's overall utilization. This parallels the CPU fix in
            // #46381, which switched to a counter that is naturally bounded to 0-100%. Adapters are
            // keyed by LUID rather than the "phys_N" token, which is effectively always 0 even on
            // multi-GPU machines and so cannot tell adapters apart.
            var perEngineUsage = new Dictionary<(long Luid, string EngineId, int Type), float>();
            var currentSamples = new Dictionary<string, CounterSample>();
            var seenLuids = new HashSet<long>();

            foreach (InstanceData instance in utilizationData.Values)
            {
                var instanceName = instance.InstanceName;
                var engineType = GetEngineType(instanceName);
                if (engineType < 0)
                {
                    continue;
                }

                if (!TryGetLuidAndEngine(instanceName, out var luidKey, out var engineId))
                {
                    continue;
                }

                // Just record which adapters we saw; discovery of new ones is
                // batched after the loop so we don't take _statsLock per instance
                // (there can be hundreds of instances per tick). Adapters are
                // discovered from their 3D engines; other engine types add detail.
                if (engineType == 0)
                {
                    seenLuids.Add(luidKey);
                }

                var sample = instance.Sample;
                currentSamples[instanceName] = sample;

                // Record every engine, even before it has a value, so the card lists the
                // engine types the adapter has.
                var key = (luidKey, engineId, engineType);
                var engineUsage = perEngineUsage.GetValueOrDefault(key);
                if (_previousSamples.TryGetValue(instanceName, out var prevSample))
                {
                    try
                    {
                        var cookedValue = CounterSampleCalculator.ComputeCounterValue(prevSample, sample);
                        if (!float.IsNaN(cookedValue) && !float.IsInfinity(cookedValue) && cookedValue >= 0f)
                        {
                            engineUsage += cookedValue;
                        }
                    }
                    catch (Exception)
                    {
                        // Skip this instance on calculation error.
                    }
                }

                perEngineUsage[key] = engineUsage;
            }

            // Swap samples - stale entries are automatically cleaned up
            _previousSamples = currentSamples;

            // Discover adapters we haven't seen before. Batched: one lock check
            // per tick, and any DXGI name enumeration happens off the lock. New
            // adapters land in _stats before the update loop so they get a value
            // this tick.
            DiscoverGpus(seenLuids);

            var adapterEngines = ReduceEngineUsage(perEngineUsage);
            var adapterMemory = ReadAdapterMemory();

            // Update stats
            lock (_statsLock)
            {
                foreach (var gpu in _stats)
                {
                    var engines = adapterEngines.TryGetValue(gpu.LuidKey, out var found) ? found : [];
                    var usage = 0f;
                    foreach (var engine in engines)
                    {
                        if (engine.Type == 0)
                        {
                            usage = engine.Percent;
                        }
                    }

                    gpu.Usage = usage / 100f;
                    gpu.Engines = engines;
                    if (adapterMemory.TryGetValue(gpu.LuidKey, out var memory))
                    {
                        gpu.DedicatedMemoryUsed = memory.Dedicated;
                        gpu.SharedMemoryUsed = memory.Shared;
                    }

                    lock (gpu.GpuChartValues)
                    {
                        ChartHelper.AddNextChartValue(usage, gpu.GpuChartValues, PerformanceChartData.HistoryLength);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogFailureOnce(ref _gpuReadFailureLogged, "Failed while reading GPU performance counters.", ex);
        }
    }

    /// <summary>Returns the index into <see cref="EngineTypes"/> of an engine instance, or -1.</summary>
    internal static int GetEngineType(string instanceName)
    {
        for (var type = 0; type < EngineTypeSuffixes.Length; type++)
        {
            if (instanceName.EndsWith(EngineTypeSuffixes[type], StringComparison.Ordinal))
            {
                return type;
            }
        }

        return -1;
    }

    /// <summary>
    /// Reduces per-engine utilization to one value per adapter and engine type: the busiest
    /// engine of that type, clamped to 0-100. Engine types the adapter doesn't have are omitted.
    /// </summary>
    internal static Dictionary<long, EngineUsage[]> ReduceEngineUsage(Dictionary<(long Luid, string EngineId, int Type), float> perEngineUsage)
    {
        var maxima = new Dictionary<long, float[]>();
        foreach (var ((luid, _, type), value) in perEngineUsage)
        {
            if (!maxima.TryGetValue(luid, out var values))
            {
                values = new float[EngineTypes.Length];
                Array.Fill(values, float.NaN);
                maxima[luid] = values;
            }

            var clamped = Math.Clamp(value, 0f, 100f);
            values[type] = float.IsNaN(values[type]) ? clamped : Math.Max(values[type], clamped);
        }

        var result = new Dictionary<long, EngineUsage[]>(maxima.Count);
        foreach (var (luid, values) in maxima)
        {
            var engines = new List<EngineUsage>(values.Length);
            for (var type = 0; type < values.Length; type++)
            {
                if (!float.IsNaN(values[type]))
                {
                    engines.Add(new EngineUsage(type, values[type]));
                }
            }

            result[luid] = engines.ToArray();
        }

        return result;
    }

    /// <summary>Reads each adapter's dedicated and shared memory use, in bytes, keyed by LUID.</summary>
    private Dictionary<long, (ulong Dedicated, ulong Shared)> ReadAdapterMemory()
    {
        var memory = new Dictionary<long, (ulong Dedicated, ulong Shared)>();
        if (_gpuAdapterMemoryCategory is null)
        {
            return memory;
        }

        try
        {
            var categoryData = _gpuAdapterMemoryCategory.ReadCategory();
            if (categoryData.Contains(DedicatedUsageCounter))
            {
                foreach (InstanceData instance in categoryData[DedicatedUsageCounter].Values)
                {
                    if (TryGetAdapterLuid(instance.InstanceName, out var luidKey))
                    {
                        memory[luidKey] = (ToBytes(instance.RawValue), memory.GetValueOrDefault(luidKey).Shared);
                    }
                }
            }

            if (categoryData.Contains(SharedUsageCounter))
            {
                foreach (InstanceData instance in categoryData[SharedUsageCounter].Values)
                {
                    if (TryGetAdapterLuid(instance.InstanceName, out var luidKey))
                    {
                        memory[luidKey] = (memory.GetValueOrDefault(luidKey).Dedicated, ToBytes(instance.RawValue));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogFailureOnce(ref _gpuMemoryReadFailureLogged, "Failed while reading GPU memory performance counters.", ex);
        }

        return memory;
    }

    private static ulong ToBytes(long rawValue) => rawValue > 0 ? (ulong)rawValue : 0;

    /// <summary>Reads the LUID from a GPU Adapter Memory instance name, such as <c>luid_0x00000000_0x0001766D_phys_0</c>.</summary>
    internal static bool TryGetAdapterLuid(string instanceName, out long luidKey)
    {
        luidKey = 0;
        var parts = instanceName.Split('_');
        return parts.Length >= 3
            && string.Equals(parts[0], KeyLuid, StringComparison.Ordinal)
            && TryParseLuidKey($"{parts[1]}_{parts[2]}", out luidKey);
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

        _stats.Add(new Data()
        {
            LuidKey = luidKey,
            Name = name,
            ShortName = GpuAdapterNames.GetShortName(name),
            DedicatedMemoryTotal = info.DedicatedVideoMemory,
            SharedMemoryTotal = info.SharedSystemMemory,
        });
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

    /// <summary>Returns a copy of the utilization history (percent), oldest first.</summary>
    internal float[] GetGPUHistory(int gpuChartIndex)
    {
        lock (_statsLock)
        {
            if (_stats.Count <= gpuChartIndex)
            {
                return [];
            }

            return PerformanceChartData.Snapshot(_stats[gpuChartIndex].GpuChartValues);
        }
    }

    internal MemoryInfo GetGPUMemory(int gpuActiveIndex)
    {
        lock (_statsLock)
        {
            if ((uint)gpuActiveIndex >= (uint)_stats.Count)
            {
                return default;
            }

            var gpu = _stats[gpuActiveIndex];
            return new(gpu.DedicatedMemoryUsed, gpu.DedicatedMemoryTotal, gpu.SharedMemoryUsed, gpu.SharedMemoryTotal);
        }
    }

    /// <summary>Returns the utilization of each engine type the adapter has, in <see cref="EngineTypes"/> order.</summary>
    internal EngineUsage[] GetGPUEngines(int gpuActiveIndex)
    {
        lock (_statsLock)
        {
            return (uint)gpuActiveIndex < (uint)_stats.Count ? _stats[gpuActiveIndex].Engines : [];
        }
    }

    internal DisplayInfo GetGPUDisplayInfo(int gpuActiveIndex)
    {
        lock (_statsLock)
        {
            // Include idle hardware that has not produced a performance-counter instance yet.
            var hardwareCount = 0;
            foreach (var adapter in _adaptersByLuid.Values)
            {
                if (!adapter.IsSoftware)
                {
                    hardwareCount++;
                }
            }

            var adapterCount = Math.Max(hardwareCount, _stats.Count);
            if ((uint)gpuActiveIndex >= (uint)_stats.Count)
            {
                return new(string.Empty, string.Empty, adapterCount);
            }

            var gpu = _stats[gpuActiveIndex];
            var shortName = gpu.ShortName;
            for (var index = 0; index < _stats.Count; index++)
            {
                if (index != gpuActiveIndex && string.Equals(shortName, _stats[index].ShortName, StringComparison.OrdinalIgnoreCase))
                {
                    shortName += " (" + gpuActiveIndex.ToString(CultureInfo.InvariantCulture) + ")";
                    break;
                }
            }

            return new(gpu.Name ?? string.Empty, shortName, adapterCount);
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
