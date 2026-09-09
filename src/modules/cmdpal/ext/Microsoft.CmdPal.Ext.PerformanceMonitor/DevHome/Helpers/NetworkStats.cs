// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.CmdPal.Common;
using Microsoft.CommandPalette.Extensions;

namespace CoreWidgetProvider.Helpers;

internal sealed partial class NetworkStats
{
    internal const string AllPhysicalAdaptersId = "all-physical-network-adapters";
    private const string PhysicalAdapterIdPrefix = "network-interface:";
    private readonly IPhysicalNetworkInterfaceSnapshotProvider _snapshotProvider;
    private readonly string _allAdaptersName;
    private readonly Dictionary<ulong, CounterSample> _previousSamples = new();
    private readonly Dictionary<ulong, UsageHistory> _histories = new();
    private readonly UsageHistory _allAdaptersHistory;
    private readonly TimeProvider _timeProvider;
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

    private sealed record NetworkAdapterData(string Id, string Name, Data Usage, GraphSample[] History);

    private readonly record struct CounterSample(ulong ReceivedBytes, ulong SentBytes);

    internal NetworkSampler Sampler { get; }

    internal readonly record struct Observation(IReadOnlyList<PhysicalNetworkInterfaceSnapshot> Interfaces, long Timestamp);

    public NetworkStats()
        : this(new PhysicalNetworkInterfaceSnapshotProvider(), Resources.GetResource("All_Physical_Network_Adapters"))
    {
        GetData();
    }

    internal NetworkStats(IPhysicalNetworkInterfaceSnapshotProvider snapshotProvider, string allAdaptersName, TimeProvider? timeProvider = null)
    {
        _snapshotProvider = snapshotProvider;
        _allAdaptersName = allAdaptersName;
        _timeProvider = timeProvider ?? TimeProvider.System;
        Sampler = new NetworkSampler(this, _timeProvider);
        _allAdaptersHistory = new UsageHistory(seriesCount: 3, _timeProvider);
        _networkAdapters = [new(AllPhysicalAdaptersId, _allAdaptersName, new Data(), [])];
    }

    public void GetData()
    {
        try
        {
            ApplyObservation(ReadObservation());
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

    internal Observation ReadObservation() => new(_snapshotProvider.GetSnapshots(), _timeProvider.GetTimestamp());

    internal void ResetSamplingInterval(Observation observation)
    {
        var currentInterfaceIds = new HashSet<ulong>(observation.Interfaces.Count);
        foreach (var snapshot in observation.Interfaces)
        {
            currentInterfaceIds.Add(snapshot.InterfaceLuid);
            _previousSamples[snapshot.InterfaceLuid] = new(snapshot.ReceivedBytes, snapshot.SentBytes);
        }

        RemoveMissingInterfaces(currentInterfaceIds);
        _lastTimestamp = observation.Timestamp;
    }

    internal void ApplyObservation(Observation observation)
    {
        var elapsedSeconds = _lastTimestamp is long previousTimestamp
            ? _timeProvider.GetElapsedTime(previousTimestamp, observation.Timestamp).TotalSeconds
            : 0;

        ApplySnapshots(observation.Interfaces, elapsedSeconds);
        _lastTimestamp = observation.Timestamp;
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
        _allAdaptersHistory.Add(aggregateUsage.Usage * 100, aggregateUsage.Sent, aggregateUsage.Received);

        var adapters = new NetworkAdapterData[adapterMeasurements.Count + 1];
        adapters[0] = new(AllPhysicalAdaptersId, _allAdaptersName, aggregateUsage, _allAdaptersHistory.GetSnapshot());

        for (var index = 0; index < adapterMeasurements.Count; index++)
        {
            var (snapshot, usage) = adapterMeasurements[index];
            if (!_histories.TryGetValue(snapshot.InterfaceLuid, out var history))
            {
                history = new UsageHistory(seriesCount: 3, _timeProvider);
                _histories.Add(snapshot.InterfaceLuid, history);
            }

            history.Add(usage.Usage * 100, usage.Sent, usage.Received);
            adapters[index + 1] = new(GetPhysicalAdapterId(snapshot), snapshot.Name, usage, history.GetSnapshot());
        }

        Volatile.Write(ref _networkAdapters, adapters);
    }

    public GraphSample[] GetNetworkHistory(int networkIndex) => GetSnapshot(networkIndex).UtilizationHistory;

    public NetworkSnapshot GetSnapshot(int networkIndex)
    {
        var adapters = Volatile.Read(ref _networkAdapters);
        var adapter = adapters[ResolveIndex(networkIndex, adapters.Length)];
        var utilization = new GraphSample[adapter.History.Length / 3];
        var traffic = new GraphSample[utilization.Length * 2];
        var utilizationIndex = 0;
        var trafficIndex = 0;
        foreach (var sample in adapter.History)
        {
            if (sample.SeriesIndex == 0)
            {
                utilization[utilizationIndex++] = sample;
            }
            else
            {
                var rate = sample;
                rate.SeriesIndex--;
                traffic[trafficIndex++] = rate;
            }
        }

        return new NetworkSnapshot(adapter.Name, adapter.Usage, utilization, traffic);
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
            _histories.Remove(interfaceId);
        }
    }
}
