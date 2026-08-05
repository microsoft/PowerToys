// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using CoreWidgetProvider.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public class NetworkStatsTests
{
    private static readonly Guid EthernetGuid = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid WifiGuid = new("22222222-2222-2222-2222-222222222222");

    [TestMethod]
    public void ApplySnapshots_AddsAggregateBeforeIndividualAdapters()
    {
        var stats = CreateNetworkStats();
        stats.ApplySnapshots(
            [
                new(1, EthernetGuid, "Ethernet", 10_000, 2_000, 1_000_000),
                new(2, WifiGuid, "Wi-Fi", 20_000, 4_000, 2_000_000),
            ],
            0);

        stats.ApplySnapshots(
            [
                new(1, EthernetGuid, "Ethernet", 60_000, 27_000, 1_000_000),
                new(2, WifiGuid, "Wi-Fi", 120_000, 29_000, 2_000_000),
            ],
            1);

        Assert.AreEqual("All physical network adapters", stats.GetNetworkName(0));
        Assert.AreEqual("Ethernet", stats.GetNetworkName(1));
        Assert.AreEqual("Wi-Fi", stats.GetNetworkName(2));

        var aggregate = stats.GetNetworkUsage(0);
        Assert.AreEqual(50_000f, aggregate.Sent);
        Assert.AreEqual(150_000f, aggregate.Received);
        Assert.AreEqual(8f * 200_000 / 3_000_000, aggregate.Usage, 0.0001f);
    }

    [TestMethod]
    public void ApplySnapshots_TreatsCounterResetAsZeroDelta()
    {
        var stats = CreateNetworkStats();
        stats.ApplySnapshots([new(1, EthernetGuid, "Ethernet", 10_000, 5_000, 1_000_000)], 0);

        stats.ApplySnapshots([new(1, EthernetGuid, "Ethernet", 1_000, 5_500, 1_000_000)], 2);

        var ethernet = stats.GetNetworkUsage(1);
        Assert.AreEqual(0f, ethernet.Received);
        Assert.AreEqual(250f, ethernet.Sent);
    }

    [TestMethod]
    public void ApplySnapshots_ReappearingAdapterStartsWithZeroRate()
    {
        var stats = CreateNetworkStats();
        stats.ApplySnapshots([new(1, EthernetGuid, "USB Ethernet", 10_000, 5_000, 1_000_000)], 0);
        stats.ApplySnapshots([], 1);

        stats.ApplySnapshots([new(1, EthernetGuid, "USB Ethernet", 100_000, 50_000, 1_000_000)], 1);

        var ethernet = stats.GetNetworkUsage(1);
        Assert.AreEqual(0f, ethernet.Received);
        Assert.AreEqual(0f, ethernet.Sent);
    }

    [TestMethod]
    public void AdapterIds_DistinguishAggregateFromMissingAndResolveLateAdapter()
    {
        var stats = CreateNetworkStats();
        var wifiAdapterId = "network-interface:" + WifiGuid.ToString("D");

        Assert.AreEqual(-1, stats.GetNetworkIndex(wifiAdapterId));

        stats.ApplySnapshots(
            [
                new(1, EthernetGuid, "Ethernet", 10_000, 2_000, 1_000_000),
                new(2, WifiGuid, "Wi-Fi", 20_000, 4_000, 2_000_000),
            ],
            0);

        Assert.AreEqual(NetworkStats.AllPhysicalAdaptersId, stats.GetNetworkId(0));
        Assert.AreEqual(0, stats.GetNetworkIndex(NetworkStats.AllPhysicalAdaptersId));
        Assert.AreEqual(1, stats.GetNetworkIndex(stats.GetNetworkId(1)));
        Assert.AreEqual(2, stats.GetNetworkIndex(wifiAdapterId.ToUpperInvariant()));
        Assert.AreEqual(-1, stats.GetNetworkIndex("network-interface:missing"));
    }

    [TestMethod]
    public void ApplySnapshots_UnknownLinkSpeedStillContributesBytesAndCapsAggregateUsage()
    {
        var stats = CreateNetworkStats();
        stats.ApplySnapshots(
            [
                new(1, EthernetGuid, "Ethernet", 0, 0, 1_000_000),
                new(2, WifiGuid, "Unknown speed", 0, 0, 0),
            ],
            0);

        stats.ApplySnapshots(
            [
                new(1, EthernetGuid, "Ethernet", 100_000, 0, 1_000_000),
                new(2, WifiGuid, "Unknown speed", 50_000, 0, 0),
            ],
            1);

        var aggregate = stats.GetNetworkUsage(0);
        Assert.AreEqual(150_000f, aggregate.Received);
        Assert.AreEqual(1f, aggregate.Usage);
    }

    [TestMethod]
    public void GetKnownLinkSpeed_IgnoresUnknownSpeedSentinel()
    {
        Assert.AreEqual(1_000_000UL, PhysicalNetworkInterfaceSnapshotProvider.GetKnownLinkSpeed(ulong.MaxValue, 1_000_000));
        Assert.AreEqual(2_000_000UL, PhysicalNetworkInterfaceSnapshotProvider.GetKnownLinkSpeed(2_000_000, ulong.MaxValue));
        Assert.AreEqual(0UL, PhysicalNetworkInterfaceSnapshotProvider.GetKnownLinkSpeed(ulong.MaxValue, ulong.MaxValue));
    }

    private static NetworkStats CreateNetworkStats()
    {
        return new NetworkStats(new UnusedSnapshotProvider(), "All physical network adapters");
    }

    private sealed class UnusedSnapshotProvider : IPhysicalNetworkInterfaceSnapshotProvider
    {
        public IReadOnlyList<PhysicalNetworkInterfaceSnapshot> GetSnapshots()
        {
            throw new InvalidOperationException("This provider is not used by these tests.");
        }
    }
}
