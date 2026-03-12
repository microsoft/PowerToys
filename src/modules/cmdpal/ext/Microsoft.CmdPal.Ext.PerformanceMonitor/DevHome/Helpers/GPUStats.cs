// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class GPUStats : IDisposable
{
    // Performance counter category & counter names
    private const string GpuEngineCategoryName = "GPU Engine";
    private const string UtilizationPercentageCounter = "Utilization Percentage";

    private static readonly CompositeFormat TemperatureFormat = CompositeFormat.Parse("{0:0.} \u00B0C");

    // Instance-name key tokens
    private const string KeyPid = "pid";
    private const string KeyLuid = "luid";
    private const string KeyPhys = "phys";
    private const string KeyEngineType = "engtype";

    // Engine type filter
    private const string EngineType3D = "3D";

    // Display strings
    private const string GpuNamePrefix = "GPU ";
    private const string TemperatureUnavailable = "--";

    // Batch read via category - single kernel transition per tick
    private readonly PerformanceCounterCategory _gpuEngineCategory = new(GpuEngineCategoryName);

    // Discovered physical GPU IDs
    private readonly HashSet<int> _knownPhysIds = [];

    private readonly List<Data> _stats = [];

    // Previous raw samples for computing cooked (delta-based) values
    private Dictionary<string, CounterSample> _previousSamples = [];

    public sealed class Data
    {
        public string? Name { get; set; }

        public int PhysId { get; set; }

        public float Usage { get; set; }

        public float Temperature { get; set; }

        public List<float> GpuChartValues { get; set; } = [];
    }

    public GPUStats()
    {
        GetGPUPerfCounters();
        LoadGPUsFromCounters();
    }

    public void GetGPUPerfCounters()
    {
        // There are really 4 different things we should be tracking the usage
        // of. Similar to how the instance name ends with `3D`, the following
        // suffixes are important.
        //
        // * `3D`
        // * `VideoEncode`
        // * `VideoDecode`
        // * `VideoProcessing`
        //
        // We could totally put each of those sets of counters into their own
        // set. That's what we should do, so that we can report the sum of those
        // numbers as the total utilization, and then have them broken out in
        // the card template and in the details metadata.
        _knownPhysIds.Clear();

        var instanceNames = _gpuEngineCategory.GetInstanceNames();

        foreach (var instanceName in instanceNames)
        {
            if (!instanceName.EndsWith(EngineType3D, StringComparison.InvariantCulture))
            {
                continue;
            }

            var counterKey = instanceName;

            // skip these values
            GetKeyValueFromCounterKey(KeyPid, ref counterKey);
            GetKeyValueFromCounterKey(KeyLuid, ref counterKey);

            if (int.TryParse(GetKeyValueFromCounterKey(KeyPhys, ref counterKey), out var phys))
            {
                _knownPhysIds.Add(phys);
            }
        }
    }

    public void LoadGPUsFromCounters()
    {
        // The old dev home code tracked GPU stats by querying WMI for the list
        // of GPUs, and then matching them up with the performance counter IDs.
        //
        // We can't use WMI here, because it drags in a dependency on
        // Microsoft.Management.Infrastructure, which is not compatible with
        // AOT.
        //
        // For now, we'll just use the indices as the GPU names.
        _stats.Clear();
        foreach (var id in _knownPhysIds)
        {
            _stats.Add(new Data() { PhysId = id, Name = GpuNamePrefix + id });
        }
    }

    public void GetData()
    {
        try
        {
            // Single batch read - one kernel transition for ALL GPU Engine instances
            var categoryData = _gpuEngineCategory.ReadCategory();

            if (!categoryData.Contains(UtilizationPercentageCounter))
            {
                return;
            }

            var utilizationData = categoryData[UtilizationPercentageCounter];

            // Accumulate usage per physical GPU
            var gpuUsage = new Dictionary<int, float>();
            var currentSamples = new Dictionary<string, CounterSample>();

            foreach (InstanceData instance in utilizationData.Values)
            {
                var instanceName = instance.InstanceName;
                if (!instanceName.EndsWith(EngineType3D, StringComparison.InvariantCulture))
                {
                    continue;
                }

                var counterKey = instanceName;
                GetKeyValueFromCounterKey(KeyPid, ref counterKey);
                GetKeyValueFromCounterKey(KeyLuid, ref counterKey);

                if (!int.TryParse(GetKeyValueFromCounterKey(KeyPhys, ref counterKey), out var phys))
                {
                    continue;
                }

                var sample = instance.Sample;
                currentSamples[instanceName] = sample;

                if (_previousSamples.TryGetValue(instanceName, out var prevSample))
                {
                    try
                    {
                        var cookedValue = CounterSampleCalculator.ComputeCounterValue(prevSample, sample);
                        gpuUsage[phys] = gpuUsage.GetValueOrDefault(phys) + cookedValue;
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
                var sum = gpuUsage.TryGetValue(gpu.PhysId, out var usage) ? usage : 0f;
                gpu.Usage = sum / 100;
                lock (gpu.GpuChartValues)
                {
                    ChartHelper.AddNextChartValue(sum, gpu.GpuChartValues);
                }
            }
        }
        catch (Exception)
        {
            // Ignore errors from ReadCategory (e.g., category not available).
        }
    }

    internal string CreateGPUImageUrl(int gpuChartIndex)
    {
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

    private string GetKeyValueFromCounterKey(string key, ref string counterKey)
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
