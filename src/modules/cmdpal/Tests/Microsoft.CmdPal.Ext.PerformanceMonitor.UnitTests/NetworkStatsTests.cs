// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using CoreWidgetProvider.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Win32.NetworkManagement.Ndis;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public class NetworkStatsTests
{
    private static readonly Guid EthernetGuid = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid WifiGuid = new("22222222-2222-2222-2222-222222222222");
    private static readonly string EthernetId = "network-interface:" + EthernetGuid.ToString("D");
    private static readonly string WifiId = "network-interface:" + WifiGuid.ToString("D");

    [TestMethod]
    public void ApplySnapshots_AddsPhysicalAggregateBeforeIndividualAdapters()
    {
        var stats = CreateNetworkStats();
        stats.ApplySnapshots(
            [
                new(1, EthernetGuid, "Ethernet", 10_000, 2_000, 1_000_000, true),
                new(2, WifiGuid, "Wi-Fi", 20_000, 4_000, 2_000_000, true),
            ],
            0);
        stats.ApplySnapshots(
            [
                new(1, EthernetGuid, "Ethernet", 60_000, 27_000, 1_000_000, true),
                new(2, WifiGuid, "Wi-Fi", 120_000, 29_000, 2_000_000, true),
            ],
            1);

        var aggregate = stats.GetNetworkAdapter(NetworkStats.AllPhysicalAdaptersId);
        Assert.AreEqual("All physical network adapters", aggregate.Name);
        Assert.AreEqual(50_000f, aggregate.Usage.Sent);
        Assert.AreEqual(150_000f, aggregate.Usage.Received);
        Assert.AreEqual(8f * 200_000 / 3_000_000, aggregate.Usage.Usage, 0.0001f);
        Assert.AreEqual(EthernetId, stats.GetAdjacentNetworkId(aggregate.Id, forward: true));
        Assert.AreEqual(WifiId, stats.GetAdjacentNetworkId(aggregate.Id, forward: false));
    }

    [TestMethod]
    public void ApplySnapshots_VirtualTrafficAndLinkSpeedDoNotContributeToPhysicalTotal()
    {
        var stats = CreateNetworkStats();
        stats.ApplySnapshots(
            [
                new(1, EthernetGuid, "Ethernet", 0, 0, 1_000_000_000, true),
                new(2, WifiGuid, "vEthernet (WSL)", 0, 0, 10_000_000_000, false),
            ],
            0);
        stats.ApplySnapshots(
            [
                new(1, EthernetGuid, "Ethernet", 100_000_000, 25_000_000, 1_000_000_000, true),
                new(2, WifiGuid, "vEthernet (WSL)", 100_000_000, 25_000_000, 10_000_000_000, false),
            ],
            1);

        var total = stats.GetNetworkAdapter(NetworkStats.AllPhysicalAdaptersId).Usage;
        Assert.AreEqual(100_000_000f, total.Received);
        Assert.AreEqual(25_000_000f, total.Sent);
        Assert.AreEqual(1f, total.Usage);

        var virtualAdapter = stats.GetNetworkAdapter(WifiId);
        Assert.AreEqual("vEthernet (WSL)", virtualAdapter.Name);
        Assert.AreEqual(100_000_000f, virtualAdapter.Usage.Received);
        Assert.AreEqual(0.1f, virtualAdapter.Usage.Usage, 0.0001f);
    }

    [TestMethod]
    public void ApplySnapshots_VirtualOnlyTrafficRemainsAvailableIndividually()
    {
        var stats = CreateNetworkStats();
        stats.ApplySnapshots([new(2, WifiGuid, "VPN", 0, 0, 10_000_000_000, false)], 0);
        stats.ApplySnapshots([new(2, WifiGuid, "VPN", 100_000, 25_000, 10_000_000_000, false)], 1);

        var total = stats.GetNetworkAdapter(NetworkStats.AllPhysicalAdaptersId).Usage;
        Assert.AreEqual(0f, total.Sent);
        Assert.AreEqual(0f, total.Received);
        Assert.AreEqual(0f, total.Usage);
        Assert.AreEqual(100_000f, stats.GetNetworkAdapter(WifiId).Usage.Received);
    }

    [TestMethod]
    public void ApplySnapshots_TreatsCounterResetAsZeroDelta()
    {
        var stats = CreateNetworkStats();
        stats.ApplySnapshots([new(1, EthernetGuid, "Ethernet", 10_000, 5_000, 1_000_000, true)], 0);
        stats.ApplySnapshots([new(1, EthernetGuid, "Ethernet", 1_000, 5_500, 1_000_000, true)], 2);

        var ethernet = stats.GetNetworkAdapter(EthernetId).Usage;
        Assert.AreEqual(0f, ethernet.Received);
        Assert.AreEqual(250f, ethernet.Sent);
    }

    [TestMethod]
    public void ApplySnapshots_ReappearingAdapterStartsWithZeroRate()
    {
        var stats = CreateNetworkStats();
        stats.ApplySnapshots([new(1, EthernetGuid, "USB Ethernet", 10_000, 5_000, 1_000_000, true)], 0);
        stats.ApplySnapshots([], 1);
        stats.ApplySnapshots([new(1, EthernetGuid, "USB Ethernet", 100_000, 50_000, 1_000_000, true)], 1);

        var ethernet = stats.GetNetworkAdapter(EthernetId).Usage;
        Assert.AreEqual(0f, ethernet.Received);
        Assert.AreEqual(0f, ethernet.Sent);
    }

    [TestMethod]
    public void AdapterIds_FallBackToAggregateAndResolveLateAdapter()
    {
        var stats = CreateNetworkStats();
        Assert.AreEqual(NetworkStats.AllPhysicalAdaptersId, stats.GetNetworkAdapter(WifiId).Id);

        stats.ApplySnapshots([new(2, WifiGuid, "Wi-Fi", 20_000, 4_000, 2_000_000, true)], 0);

        Assert.AreEqual(WifiId, stats.GetNetworkAdapter(WifiId.ToUpperInvariant()).Id);
        Assert.AreEqual(NetworkStats.AllPhysicalAdaptersId, stats.GetNetworkAdapter("network-interface:missing").Id);
    }

    [TestMethod]
    public void ApplySnapshots_UnknownLinkSpeedStillContributesBytesAndCapsAggregateUsage()
    {
        var stats = CreateNetworkStats();
        stats.ApplySnapshots(
            [
                new(1, EthernetGuid, "Ethernet", 0, 0, 1_000_000, true),
                new(2, WifiGuid, "Unknown speed", 0, 0, 0, true),
            ],
            0);
        stats.ApplySnapshots(
            [
                new(1, EthernetGuid, "Ethernet", 100_000, 0, 1_000_000, true),
                new(2, WifiGuid, "Unknown speed", 50_000, 0, 0, true),
            ],
            1);

        var aggregate = stats.GetNetworkAdapter(NetworkStats.AllPhysicalAdaptersId).Usage;
        Assert.AreEqual(150_000f, aggregate.Received);
        Assert.AreEqual(1f, aggregate.Usage);
    }

    [TestMethod]
    public void GetKnownLinkSpeed_IgnoresUnknownSpeedSentinel()
    {
        Assert.AreEqual(1_000_000UL, NetworkInterfaceSnapshotProvider.GetKnownLinkSpeed(IF_OPER_STATUS.IfOperStatusUp, ulong.MaxValue, 1_000_000));
        Assert.AreEqual(2_000_000UL, NetworkInterfaceSnapshotProvider.GetKnownLinkSpeed(IF_OPER_STATUS.IfOperStatusUp, 2_000_000, ulong.MaxValue));
        Assert.AreEqual(0UL, NetworkInterfaceSnapshotProvider.GetKnownLinkSpeed(IF_OPER_STATUS.IfOperStatusUp, ulong.MaxValue, ulong.MaxValue));
    }

    private static NetworkStats CreateNetworkStats() => new(new UnusedSnapshotProvider(), "All physical network adapters");

    private sealed class UnusedSnapshotProvider : INetworkInterfaceSnapshotProvider
    {
        public IReadOnlyList<NetworkInterfaceSnapshot> GetSnapshots() => throw new InvalidOperationException("This provider is not used by these tests.");
    }
}
