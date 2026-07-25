// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.CmdPal.Common;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class NetworkStats : PerformanceCounterSourceBase, IDisposable
{
    private const float MinimumSwitchDeltaBytesPerSecond = 128;
    private const float SwitchHysteresisRatio = 0.25f;

    private readonly Dictionary<string, List<PerformanceCounter>> _networkCounters = new();
    private readonly List<string> _networkNames = [];
    private readonly List<Data> _networkUsagesByIndex = [];
    private readonly object _statsLock = new();
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

        public float Bandwidth
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
                    var usage = new Data();
                    _networkCounters.Add(instanceName, instanceCounters);
                    _networkNames.Add(instanceName);
                    _networkUsagesByIndex.Add(usage);
                    NetChartValues.Add(instanceName, new List<float>());
                    NetworkUsages.Add(instanceName, usage);
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
        foreach (var networkCounterWithName in _networkCounters)
        {
            try
            {
                var sent = networkCounterWithName.Value[0].NextValue();
                var received = networkCounterWithName.Value[1].NextValue();
                var bandWidth = networkCounterWithName.Value[2].NextValue();
                var usage = bandWidth > 0 ? 8 * (sent + received) / bandWidth : 0;
                var name = networkCounterWithName.Key;
                lock (_statsLock)
                {
                    var data = NetworkUsages[name];
                    data.Sent = bandWidth > 0 ? sent : 0;
                    data.Received = bandWidth > 0 ? received : 0;
                    data.Usage = usage;
                    data.Bandwidth = Math.Max(0, bandWidth);
                }

                var chartValues = NetChartValues[name];
                lock (chartValues)
                {
                    ChartHelper.AddNextChartValue(usage * 100, chartValues);
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
        if (netChartIndex < 0 || netChartIndex >= _networkNames.Count)
        {
            return string.Empty;
        }

        return ChartHelper.CreateImageUrl(NetChartValues[_networkNames[netChartIndex]], ChartHelper.ChartType.Net);
    }

    public string GetNetworkName(int networkIndex)
    {
        if (networkIndex < 0 || networkIndex >= _networkNames.Count)
        {
            return string.Empty;
        }

        return _networkNames[networkIndex];
    }

    public Data GetNetworkUsage(int networkIndex)
    {
        lock (_statsLock)
        {
            if (networkIndex < 0 || networkIndex >= _networkUsagesByIndex.Count)
            {
                return new Data();
            }

            var value = _networkUsagesByIndex[networkIndex];
            return new Data
            {
                Usage = value.Usage,
                Sent = value.Sent,
                Received = value.Received,
                Bandwidth = value.Bandwidth,
            };
        }
    }

    public int GetSelectableNetworkCount()
    {
        lock (_statsLock)
        {
            return CountSelectableNetworks(_networkUsagesByIndex);
        }
    }

    internal static int CountSelectableNetworks(IReadOnlyList<Data> networkUsages)
    {
        var count = 0;
        foreach (var networkUsage in networkUsages)
        {
            if (IsSelectableNetwork(networkUsage))
            {
                count++;
            }
        }

        return count;
    }

    public int GetBusiestNetworkIndex(int fallbackIndex)
    {
        lock (_statsLock)
        {
            return SelectBusiestNetworkIndex(_networkUsagesByIndex, fallbackIndex);
        }
    }

    internal static int SelectBusiestNetworkIndex(IReadOnlyList<Data> networkUsages, int fallbackIndex)
    {
        if (networkUsages.Count == 0)
        {
            return 0;
        }

        var selectedIndex = Math.Clamp(fallbackIndex, 0, networkUsages.Count - 1);
        var fallbackIsSelectable = IsSelectableNetwork(networkUsages[selectedIndex]);
        if (!fallbackIsSelectable)
        {
            for (var index = 0; index < networkUsages.Count; index++)
            {
                if (IsSelectableNetwork(networkUsages[index]))
                {
                    selectedIndex = index;
                    break;
                }
            }
        }

        var selectedThroughput = GetThroughput(networkUsages[selectedIndex]);
        var busiestIndex = selectedIndex;
        var busiestThroughput = selectedThroughput;
        for (var index = 0; index < networkUsages.Count; index++)
        {
            var candidate = networkUsages[index];
            if (!IsSelectableNetwork(candidate))
            {
                continue;
            }

            var candidateThroughput = GetThroughput(candidate);
            if (candidateThroughput > busiestThroughput)
            {
                busiestIndex = index;
                busiestThroughput = candidateThroughput;
            }
        }

        if (!fallbackIsSelectable)
        {
            return busiestIndex;
        }

        var switchDelta = Math.Max(MinimumSwitchDeltaBytesPerSecond, selectedThroughput * SwitchHysteresisRatio);
        return busiestThroughput > selectedThroughput + switchDelta
            ? busiestIndex
            : selectedIndex;
    }

    public int GetPrevNetworkIndex(int networkIndex)
    {
        lock (_statsLock)
        {
            return SelectPreviousNetworkIndex(_networkUsagesByIndex, networkIndex);
        }
    }

    public int GetNextNetworkIndex(int networkIndex)
    {
        lock (_statsLock)
        {
            return SelectNextNetworkIndex(_networkUsagesByIndex, networkIndex);
        }
    }

    internal static int SelectPreviousNetworkIndex(IReadOnlyList<Data> networkUsages, int currentIndex) =>
        SelectAdjacentNetworkIndex(networkUsages, currentIndex, -1);

    internal static int SelectNextNetworkIndex(IReadOnlyList<Data> networkUsages, int currentIndex) =>
        SelectAdjacentNetworkIndex(networkUsages, currentIndex, 1);

    private static int SelectAdjacentNetworkIndex(IReadOnlyList<Data> networkUsages, int currentIndex, int direction)
    {
        if (networkUsages.Count == 0)
        {
            return 0;
        }

        var selectedIndex = Math.Clamp(currentIndex, 0, networkUsages.Count - 1);
        for (var offset = 1; offset <= networkUsages.Count; offset++)
        {
            var candidateIndex = (selectedIndex + (direction * offset) + networkUsages.Count) % networkUsages.Count;
            if (IsSelectableNetwork(networkUsages[candidateIndex]))
            {
                return candidateIndex;
            }
        }

        return selectedIndex;
    }

    private static float GetThroughput(Data networkUsage) =>
        Math.Max(0, networkUsage.Sent) + Math.Max(0, networkUsage.Received);

    private static bool IsSelectableNetwork(Data networkUsage) => Math.Max(0, networkUsage.Bandwidth) > 0;

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
