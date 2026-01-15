// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class GPUStats : IDisposable
{
    // GPU counters
    private readonly Dictionary<int, List<PerformanceCounter>> _gpuCounters = new();

    private readonly List<Data> _stats = new();

    public sealed class Data
    {
        public string? Name { get; set; }

        public int PhysId { get; set; }

        public float Usage { get; set; }

        public float Temperature { get; set; }

        public List<float> GpuChartValues { get; set; } = new();
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

        _gpuCounters.Clear();

        var pcg = new PerformanceCounterCategory("GPU Engine");
        var instanceNames = pcg.GetInstanceNames();

        foreach (var instanceName in instanceNames)
        {
            if (!instanceName.EndsWith("3D", StringComparison.InvariantCulture))
            {
                continue;
            }

            var utilizationCounters = pcg.GetCounters(instanceName)
                .Where(x => x.CounterName.StartsWith("Utilization Percentage", StringComparison.InvariantCulture));

            foreach (var counter in utilizationCounters)
            {
                var counterKey = counter.InstanceName;

                // skip these values
                GetKeyValueFromCounterKey("pid", ref counterKey);
                GetKeyValueFromCounterKey("luid", ref counterKey);

                int phys;
                var success = int.TryParse(GetKeyValueFromCounterKey("phys", ref counterKey), out phys);
                if (success)
                {
                    GetKeyValueFromCounterKey("eng", ref counterKey);
                    var engtype = GetKeyValueFromCounterKey("engtype", ref counterKey);
                    if (engtype != "3D")
                    {
                        continue;
                    }

                    if (!_gpuCounters.TryGetValue(phys, out var value))
                    {
                        value = new();
                        _gpuCounters.Add(phys, value);
                    }

                    value.Add(counter);
                }
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
        // For now, we'll just use the indicies as the GPU names.
        _stats.Clear();
        foreach (var (k, v) in _gpuCounters)
        {
            var id = k;
            var counters = v;
            _stats.Add(new Data() { PhysId = id, Name = "GPU " + id });
        }
    }

    public void GetData()
    {
        foreach (var gpu in _stats)
        {
            List<PerformanceCounter>? counters;
            var success = _gpuCounters.TryGetValue(gpu.PhysId, out counters);

            if (success && counters != null)
            {
                // TODO: This outer try/catch should be replaced with more secure locking around shared resources.
                try
                {
                    var sum = 0.0f;
                    var countersToRemove = new List<PerformanceCounter>();
                    foreach (var counter in counters)
                    {
                        try
                        {
                            // NextValue() can throw an InvalidOperationException if the counter is no longer there.
                            sum += counter.NextValue();
                        }
                        catch (InvalidOperationException)
                        {
                            // We can't modify the list during the loop, so save it to remove at the end.
                            // _log.Information(ex, "Failed to get next value, remove");
                            countersToRemove.Add(counter);
                        }
                        catch (Exception)
                        {
                            // _log.Error(ex, "Error going through process counters.");
                        }
                    }

                    foreach (var counter in countersToRemove)
                    {
                        counters.Remove(counter);
                        counter.Dispose();
                    }

                    gpu.Usage = sum / 100;
                    lock (gpu.GpuChartValues)
                    {
                        ChartHelper.AddNextChartValue(sum, gpu.GpuChartValues);
                    }
                }
                catch (Exception)
                {
                    // _log.Error(ex, "Error summing process counters.");
                }
            }
        }
    }

    internal string CreateGPUImageUrl(int gpuChartIndex)
    {
        return ChartHelper.CreateImageUrl(_stats.ElementAt(gpuChartIndex).GpuChartValues, ChartHelper.ChartType.GPU);
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
            return "--";
        }

        var temperature = _stats[gpuActiveIndex].Temperature;
        if (temperature == 0)
        {
            return "--";
        }

        return temperature.ToString("0.", CultureInfo.InvariantCulture) + " \x00B0C";
    }

    private string GetKeyValueFromCounterKey(string key, ref string counterKey)
    {
        if (!counterKey.StartsWith(key, StringComparison.InvariantCulture))
        {
            return "error";
        }

        counterKey = counterKey.Substring(key.Length + 1);
        if (key.Equals("engtype", StringComparison.Ordinal))
        {
            return counterKey;
        }

        var pos = counterKey.IndexOf('_');
        if (key.Equals("luid", StringComparison.Ordinal))
        {
            pos = counterKey.IndexOf('_', pos + 1);
        }

        var retValue = counterKey.Substring(0, pos);
        counterKey = counterKey.Substring(pos + 1);
        return retValue;
    }

    public void Dispose()
    {
        foreach (var counterPair in _gpuCounters)
        {
            foreach (var counter in counterPair.Value)
            {
                counter.Dispose();
            }
        }
    }
}
