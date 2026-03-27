// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CmdPal.Common;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class NetworkStats : PerformanceCounterSourceBase, IDisposable
{
    private readonly Dictionary<string, List<PerformanceCounter>> _networkCounters = new();
    private bool _networkCounterReadFailureLogged;

    private Dictionary<string, Data> NetworkUsages { get; set; } = new();

    private Dictionary<string, List<float>> NetChartValues { get; set; } = new();

    public sealed class Data
    {
        public float Usage
        {
            get; set;
        }

        public float Sent
        {
            get; set;
        }

        public float Received
        {
            get; set;
        }
    }

    public NetworkStats()
    {
        InitNetworkPerfCounters();
    }

    private void InitNetworkPerfCounters()
    {
        try
        {
            var perfCounterCategory = CreatePerformanceCounterCategory("Network Interface");
            if (perfCounterCategory is null)
            {
                return;
            }

            var instanceNames = perfCounterCategory.GetInstanceNames();
            foreach (var instanceName in instanceNames)
            {
                try
                {
                    var bytesSent = CreatePerformanceCounter("Network Interface", "Bytes Sent/sec", instanceName, logFailure: false);
                    var bytesReceived = CreatePerformanceCounter("Network Interface", "Bytes Received/sec", instanceName, logFailure: false);
                    var currentBandwidth = CreatePerformanceCounter("Network Interface", "Current Bandwidth", instanceName, logFailure: false);
                    if (bytesSent is null || bytesReceived is null || currentBandwidth is null)
                    {
                        bytesSent?.Dispose();
                        bytesReceived?.Dispose();
                        currentBandwidth?.Dispose();
                        continue;
                    }

                    var instanceCounters = new List<PerformanceCounter> { bytesSent, bytesReceived, currentBandwidth };
                    _networkCounters.Add(instanceName, instanceCounters);
                    NetChartValues.Add(instanceName, new List<float>());
                    NetworkUsages.Add(instanceName, new Data());
                }
                catch (Exception)
                {
                    // Skip interfaces whose counters cannot be initialized.
                }
            }
        }
        catch (Exception ex)
        {
            CoreLogger.LogError("Failed to initialize network performance counters.", ex);
        }
    }

    public void GetData()
    {
        float maxUsage = 0;
        foreach (var networkCounterWithName in _networkCounters)
        {
            try
            {
                var sent = networkCounterWithName.Value[0].NextValue();
                var received = networkCounterWithName.Value[1].NextValue();
                var bandWidth = networkCounterWithName.Value[2].NextValue();
                if (bandWidth == 0)
                {
                    continue;
                }

                var usage = 8 * (sent + received) / bandWidth;
                var name = networkCounterWithName.Key;
                NetworkUsages[name].Sent = sent;
                NetworkUsages[name].Received = received;
                NetworkUsages[name].Usage = usage;

                var chartValues = NetChartValues[name];
                lock (chartValues)
                {
                    ChartHelper.AddNextChartValue(usage * 100, chartValues);
                }

                if (usage > maxUsage)
                {
                    maxUsage = usage;
                }
            }
            catch (Exception ex)
            {
                LogFailureOnce(ref _networkCounterReadFailureLogged, "Failed while reading network performance counters.", ex);
            }
        }
    }

    public string CreateNetImageUrl(int netChartIndex)
    {
        return ChartHelper.CreateImageUrl(NetChartValues.ElementAt(netChartIndex).Value, ChartHelper.ChartType.Net);
    }

    public string GetNetworkName(int networkIndex)
    {
        if (NetChartValues.Count <= networkIndex)
        {
            return string.Empty;
        }

        return NetChartValues.ElementAt(networkIndex).Key;
    }

    public Data GetNetworkUsage(int networkIndex)
    {
        if (NetChartValues.Count <= networkIndex)
        {
            return new Data();
        }

        var currNetworkName = NetChartValues.ElementAt(networkIndex).Key;
        if (!NetworkUsages.TryGetValue(currNetworkName, out var value))
        {
            return new Data();
        }

        return value;
    }

    public int GetPrevNetworkIndex(int networkIndex)
    {
        if (NetChartValues.Count == 0)
        {
            return 0;
        }

        if (networkIndex == 0)
        {
            return NetChartValues.Count - 1;
        }

        return networkIndex - 1;
    }

    public int GetNextNetworkIndex(int networkIndex)
    {
        if (NetChartValues.Count == 0)
        {
            return 0;
        }

        if (networkIndex == NetChartValues.Count - 1)
        {
            return 0;
        }

        return networkIndex + 1;
    }

    public void Dispose()
    {
        foreach (var counterPair in _networkCounters)
        {
            foreach (var counter in counterPair.Value)
            {
                counter.Dispose();
            }
        }
    }
}
