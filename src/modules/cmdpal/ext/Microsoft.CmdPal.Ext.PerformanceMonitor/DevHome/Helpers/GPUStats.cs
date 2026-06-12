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

    public void DiscoverGPUsFromCounters()
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

            foreach (var instanceName in instanceNames)
            {
                if (!instanceName.EndsWith(EngineType3D, StringComparison.InvariantCulture))
                {
                    continue;
                }

                if (TryGetLuidKey(instanceName, out var luidKey))
                {
                    AddGpuIfNew(luidKey);
                }
            }
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

            // Accumulate usage per physical GPU, keyed by adapter LUID.
            var gpuUsage = new Dictionary<long, float>();
            var currentSamples = new Dictionary<string, CounterSample>();

            foreach (InstanceData instance in utilizationData.Values)
            {
                var instanceName = instance.InstanceName;
                if (!instanceName.EndsWith(EngineType3D, StringComparison.InvariantCulture))
                {
                    continue;
                }

                if (!TryGetLuidKey(instanceName, out var luidKey))
                {
                    continue;
                }

                // A GPU can register counter instances after we were constructed
                // (e.g. a discrete GPU coming out of an idle low-power state), so
                // keep discovering here rather than only in the constructor.
                AddGpuIfNew(luidKey);

                var sample = instance.Sample;
                currentSamples[instanceName] = sample;

                if (_previousSamples.TryGetValue(instanceName, out var prevSample))
                {
                    try
                    {
                        var cookedValue = CounterSampleCalculator.ComputeCounterValue(prevSample, sample);
                        gpuUsage[luidKey] = gpuUsage.GetValueOrDefault(luidKey) + cookedValue;
                    }
                    catch (Exception)
                    {
                        // Skip this instance on calculation error.
                    }
                }
            }

            // Swap samples - stale entries are automatically cleaned up
            _previousSamples = currentSamples;

            // Update stats
            foreach (var gpu in _stats)
            {
                var sum = gpuUsage.TryGetValue(gpu.LuidKey, out var usage) ? usage : 0f;
                gpu.Usage = sum / 100;
                lock (gpu.GpuChartValues)
                {
                    ChartHelper.AddNextChartValue(sum, gpu.GpuChartValues);
                }
            }
        }
        catch (Exception ex)
        {
            LogFailureOnce(ref _gpuReadFailureLogged, "Failed while reading GPU performance counters.", ex);
        }
    }

    private void AddGpuIfNew(long luidKey)
    {
        if (!_knownLuids.Add(luidKey))
        {
            return;
        }

        _adaptersByLuid.TryGetValue(luidKey, out var info);

        // Hide software adapters (e.g. the Microsoft Basic Render Driver / WARP):
        // they report 3D engine activity but aren't a GPU the user cares about.
        if (info.IsSoftware)
        {
            return;
        }

        var name = string.IsNullOrEmpty(info.Description)
            ? GpuNamePrefix + _stats.Count
            : info.Description;

        _stats.Add(new Data() { LuidKey = luidKey, Name = name });
    }

    internal string CreateGPUImageUrl(int gpuChartIndex)
    {
        if (_stats.Count <= gpuChartIndex)
        {
            return string.Empty;
        }

        return ChartHelper.CreateImageUrl(_stats[gpuChartIndex].GpuChartValues, ChartHelper.ChartType.GPU);
    }

    internal string GetGPUName(int gpuActiveIndex)
    {
        if (_stats.Count <= gpuActiveIndex)
        {
            return string.Empty;
        }

        return _stats[gpuActiveIndex].Name ?? string.Empty;
    }

    internal int GetPrevGPUIndex(int gpuActiveIndex)
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

    internal int GetNextGPUIndex(int gpuActiveIndex)
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

    internal float GetGPUUsage(int gpuActiveIndex, string gpuActiveEngType)
    {
        if (_stats.Count <= gpuActiveIndex)
        {
            return 0;
        }

        return _stats[gpuActiveIndex].Usage;
    }

    internal string GetGPUTemperature(int gpuActiveIndex)
    {
        // MG Jan 2026: This code was lifted from the old Dev Home codebase.
        // However, the performance counters for GPU temperature are not being
        // collected. So this function always returns "--" for now.
        //
        // I have not done the code archeology to figure out why they were
        // removed.
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

    private static bool TryGetLuidKey(string instanceName, out long luidKey)
    {
        // Instance names look like:
        //   pid_1234_luid_0x00000000_0x0001766D_phys_0_eng_0_engtype_3D
        // We only need the LUID; advance past the pid token, then read the luid.
        var counterKey = instanceName;
        GetKeyValueFromCounterKey(KeyPid, ref counterKey);
        var luid = GetKeyValueFromCounterKey(KeyLuid, ref counterKey);

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
