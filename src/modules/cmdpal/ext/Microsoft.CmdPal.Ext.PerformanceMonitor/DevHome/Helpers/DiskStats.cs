// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CmdPal.Common;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class DiskStats : PerformanceCounterSourceBase, IDisposable
{
    private readonly Dictionary<string, List<PerformanceCounter>> _diskCounters = new();
    private bool _diskCounterReadFailureLogged;

    private Dictionary<string, Data> DiskUsages { get; set; } = new();

    private Dictionary<string, List<float>> DiskChartValues { get; set; } = new();

    public sealed class Data
    {
        public float Usage
        {
            get; set;
        }

        public float Read
        {
            get; set;
        }

        public float Written
        {
            get; set;
        }
    }

    public DiskStats()
    {
        InitDiskPerfCounters();
    }

    private void InitDiskPerfCounters()
    {
        try
        {
            var perfCounterCategory = CreatePerformanceCounterCategory("PhysicalDisk");
            if (perfCounterCategory is null)
            {
                return;
            }

            var instanceNames = perfCounterCategory.GetInstanceNames();
            foreach (var instanceName in instanceNames)
            {
                if (string.Equals(instanceName, "_Total", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    var bytesRead = CreatePerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", instanceName, logFailure: false);
                    var bytesWritten = CreatePerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", instanceName, logFailure: false);
                    var diskTime = CreatePerformanceCounter("PhysicalDisk", "% Disk Time", instanceName, logFailure: false);
                    if (bytesRead is null || bytesWritten is null || diskTime is null)
                    {
                        bytesRead?.Dispose();
                        bytesWritten?.Dispose();
                        diskTime?.Dispose();
                        continue;
                    }

                    var instanceCounters = new List<PerformanceCounter> { bytesRead, bytesWritten, diskTime };
                    _diskCounters.Add(instanceName, instanceCounters);
                    DiskChartValues.Add(instanceName, new List<float>());
                    DiskUsages.Add(instanceName, new Data());
                }
                catch (Exception)
                {
                    // Skip interfaces whose counters cannot be initialized.
                }
            }
        }
        catch (Exception ex)
        {
            CoreLogger.LogError("Failed to initialize disk performance counters.", ex);
        }
    }

    public void GetData()
    {
        foreach (var diskCounterWithName in _diskCounters)
        {
            try
            {
                var read = diskCounterWithName.Value[0].NextValue();
                var written = diskCounterWithName.Value[1].NextValue();

                var diskTimePercent = Math.Clamp(diskCounterWithName.Value[2].NextValue(), 0f, 100f);
                var name = diskCounterWithName.Key;

                DiskUsages[name].Read = read;
                DiskUsages[name].Written = written;
                DiskUsages[name].Usage = diskTimePercent / 100f;

                var chartValues = DiskChartValues[name];
                lock (chartValues)
                {
                    ChartHelper.AddNextChartValue(diskTimePercent, chartValues);
                }
            }
            catch (Exception ex)
            {
                LogFailureOnce(ref _diskCounterReadFailureLogged, "Failed while reading disk performance counters.", ex);
            }
        }
    }

    public string CreateDiskImageUrl(int diskChartIndex)
    {
        var chartValues = DiskChartValues.ElementAt(diskChartIndex).Value;
        lock (chartValues)
        {
            return ChartHelper.CreateImageUrl(chartValues, ChartHelper.ChartType.Dis);
        }
    }

    public string GetDiskName(int diskIndex)
    {
        if (DiskChartValues.Count <= diskIndex)
        {
            return string.Empty;
        }

        return DiskChartValues.ElementAt(diskIndex).Key;
    }

    public Data GetDiskUsage(int diskIndex)
    {
        if (DiskChartValues.Count <= diskIndex)
        {
            return new Data();
        }

        var currDiskName = DiskChartValues.ElementAt(diskIndex).Key;
        if (!DiskUsages.TryGetValue(currDiskName, out var value))
        {
            return new Data();
        }

        return value;
    }

    public int GetPrevDiskIndex(int diskIndex)
    {
        if (DiskChartValues.Count == 0)
        {
            return 0;
        }

        if (diskIndex == 0)
        {
            return DiskChartValues.Count - 1;
        }

        return diskIndex - 1;
    }

    public int GetNextDiskIndex(int diskIndex)
    {
        if (DiskChartValues.Count == 0)
        {
            return 0;
        }

        if (diskIndex == DiskChartValues.Count - 1)
        {
            return 0;
        }

        return diskIndex + 1;
    }

    public void Dispose()
    {
        foreach (var counterPair in _diskCounters)
        {
            foreach (var counter in counterPair.Value)
            {
                counter.Dispose();
            }
        }
    }
}
