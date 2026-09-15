// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Microsoft.CmdPal.Common;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class NetworkStats
{
    internal const string AllPhysicalAdaptersId = "all-physical-network-adapters";
    private const string PhysicalAdapterIdPrefix = "network-interface:";
    private readonly IPhysicalNetworkInterfaceSnapshotProvider _snapshotProvider;
    private readonly string _allAdaptersName;
    private readonly Dictionary<ulong, CounterSample> _previousSamples = new();
    private readonly Dictionary<ulong, List<float>> _chartValues = new();
    private readonly List<float> _allAdaptersChartValues = new();
    private readonly List<ulong> _missingInterfaceIds = new();
    private NetworkAdapterData[] _networkAdapters = [];
    private long? _lastTimestamp;
    private bool _snapshotReadFailureLogged;

    public sealed class Data
    {
        public float Usage
        {
            get; init;
        }

        public float Sent
        {
            get; init;
        }

        public float Received
        {
            get; init;
        }
    }

    private sealed record NetworkAdapterData(string Id, string Name, Data Usage, List<float> ChartValues);

    private readonly record struct CounterSample(ulong ReceivedBytes, ulong SentBytes);

    public NetworkStats()
        : this(new PhysicalNetworkInterfaceSnapshotProvider(), Resources.GetResource("All_Physical_Network_Adapters"))
    {
        GetData();
    }

    internal NetworkStats(IPhysicalNetworkInterfaceSnapshotProvider snapshotProvider, string allAdaptersName)
    {
        _snapshotProvider = snapshotProvider;
        _allAdaptersName = allAdaptersName;
        _networkAdapters = [new(AllPhysicalAdaptersId, _allAdaptersName, new Data(), _allAdaptersChartValues)];
    }

    public void GetData()
    {
        try
        {
            var snapshots = _snapshotProvider.GetSnapshots();
            var timestamp = Stopwatch.GetTimestamp();
            var elapsedSeconds = _lastTimestamp is long previousTimestamp
                ? Stopwatch.GetElapsedTime(previousTimestamp, timestamp).TotalSeconds
                : 0;

            ApplySnapshots(snapshots, elapsedSeconds);
            _lastTimestamp = timestamp;
        }
        catch (Exception ex)
        {
            if (!_snapshotReadFailureLogged)
            {
                _snapshotReadFailureLogged = true;
                CoreLogger.LogError("Failed while reading physical network interface statistics.", ex);
            }
        }
    }

    internal void ApplySnapshots(IReadOnlyList<PhysicalNetworkInterfaceSnapshot> snapshots, double elapsedSeconds)
    {
        var currentInterfaceIds = new HashSet<ulong>(snapshots.Count);
        var adapterMeasurements = new List<(PhysicalNetworkInterfaceSnapshot Snapshot, Data Usage)>(snapshots.Count);
        double totalSent = 0;
        double totalReceived = 0;
        double totalBandwidth = 0;

        foreach (var snapshot in snapshots)
        {
            currentInterfaceIds.Add(snapshot.InterfaceLuid);

            var sent = 0d;
            var received = 0d;
            if (elapsedSeconds > 0 && _previousSamples.TryGetValue(snapshot.InterfaceLuid, out var previousSample))
            {
                sent = GetBytesPerSecond(previousSample.SentBytes, snapshot.SentBytes, elapsedSeconds);
                received = GetBytesPerSecond(previousSample.ReceivedBytes, snapshot.ReceivedBytes, elapsedSeconds);
            }

            _previousSamples[snapshot.InterfaceLuid] = new(snapshot.ReceivedBytes, snapshot.SentBytes);

            var usage = CreateUsage(sent, received, snapshot.LinkSpeed);
            adapterMeasurements.Add((snapshot, usage));
            totalSent += sent;
            totalReceived += received;
            totalBandwidth += snapshot.LinkSpeed;
        }

        RemoveMissingInterfaces(currentInterfaceIds);

        var aggregateUsage = CreateUsage(totalSent, totalReceived, totalBandwidth);
        AddChartValue(_allAdaptersChartValues, aggregateUsage.Usage);

        var adapters = new NetworkAdapterData[adapterMeasurements.Count + 1];
        adapters[0] = new(AllPhysicalAdaptersId, _allAdaptersName, aggregateUsage, _allAdaptersChartValues);

        for (var index = 0; index < adapterMeasurements.Count; index++)
        {
            var (snapshot, usage) = adapterMeasurements[index];
            if (!_chartValues.TryGetValue(snapshot.InterfaceLuid, out var chartValues))
            {
                chartValues = new List<float>();
                _chartValues.Add(snapshot.InterfaceLuid, chartValues);
            }

            AddChartValue(chartValues, usage.Usage);
            adapters[index + 1] = new(GetPhysicalAdapterId(snapshot), snapshot.Name, usage, chartValues);
        }

        Volatile.Write(ref _networkAdapters, adapters);
    }

    public string CreateNetImageUrl(int netChartIndex)
    {
        var adapters = Volatile.Read(ref _networkAdapters);
        var resolvedIndex = ResolveIndex(netChartIndex, adapters.Length);
        return ChartHelper.CreateImageUrl(adapters[resolvedIndex].ChartValues, ChartHelper.ChartType.Net);
    }

    public string GetNetworkName(int networkIndex)
    {
        var adapters = Volatile.Read(ref _networkAdapters);
        return adapters[ResolveIndex(networkIndex, adapters.Length)].Name;
    }

    public string GetNetworkId(int networkIndex)
    {
        var adapters = Volatile.Read(ref _networkAdapters);
        return adapters[ResolveIndex(networkIndex, adapters.Length)].Id;
    }

    public int GetNetworkIndex(string adapterId)
    {
        var adapters = Volatile.Read(ref _networkAdapters);
        for (var index = 0; index < adapters.Length; index++)
        {
            if (string.Equals(adapters[index].Id, adapterId, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    public Data GetNetworkUsage(int networkIndex)
    {
        var adapters = Volatile.Read(ref _networkAdapters);
        return adapters[ResolveIndex(networkIndex, adapters.Length)].Usage;
    }

    public int GetPrevNetworkIndex(int networkIndex)
    {
        var adapterCount = Volatile.Read(ref _networkAdapters).Length;
        if (adapterCount == 0)
        {
            return 0;
        }

        return networkIndex <= 0 || networkIndex >= adapterCount ? adapterCount - 1 : networkIndex - 1;
    }

    public int GetNextNetworkIndex(int networkIndex)
    {
        var adapterCount = Volatile.Read(ref _networkAdapters).Length;
        if (adapterCount == 0 || networkIndex < 0 || networkIndex >= adapterCount - 1)
        {
            return 0;
        }

        return networkIndex + 1;
    }

    private static double GetBytesPerSecond(ulong previousValue, ulong currentValue, double elapsedSeconds)
    {
        return currentValue >= previousValue ? (currentValue - previousValue) / elapsedSeconds : 0;
    }

    private static int ResolveIndex(int requestedIndex, int adapterCount)
    {
        return (uint)requestedIndex < (uint)adapterCount ? requestedIndex : 0;
    }

    private static string GetPhysicalAdapterId(PhysicalNetworkInterfaceSnapshot snapshot)
    {
        return snapshot.InterfaceGuid != Guid.Empty
            ? PhysicalAdapterIdPrefix + snapshot.InterfaceGuid.ToString("D")
            : PhysicalAdapterIdPrefix + snapshot.InterfaceLuid.ToString("X16", CultureInfo.InvariantCulture);
    }

    private static Data CreateUsage(double sent, double received, double bandwidth)
    {
        var usage = bandwidth > 0 ? Math.Min(8 * (sent + received) / bandwidth, 1) : 0;
        return new Data
        {
            Sent = (float)sent,
            Received = (float)received,
            Usage = (float)usage,
        };
    }

    private static void AddChartValue(List<float> chartValues, float usage)
    {
        lock (chartValues)
        {
            ChartHelper.AddNextChartValue(usage * 100, chartValues);
        }
    }

    private void RemoveMissingInterfaces(HashSet<ulong> currentInterfaceIds)
    {
        _missingInterfaceIds.Clear();
        foreach (var interfaceId in _previousSamples.Keys)
        {
            if (!currentInterfaceIds.Contains(interfaceId))
            {
                _missingInterfaceIds.Add(interfaceId);
            }
        }

        foreach (var interfaceId in _missingInterfaceIds)
        {
            _previousSamples.Remove(interfaceId);
            _chartValues.Remove(interfaceId);
        }
    }
}
