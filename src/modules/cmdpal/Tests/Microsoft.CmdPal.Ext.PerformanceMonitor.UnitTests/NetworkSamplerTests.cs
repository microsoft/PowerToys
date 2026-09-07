// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using CoreWidgetProvider.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public partial class NetworkSamplerTests
{
    [TestMethod]
    public void MultipleViews_ShareOneNetworkMeasurementPerSecond()
    {
        var clock = new ManualSamplingTimeProvider();
        var provider = new SnapshotProvider(clock);
        var stats = new NetworkStats(provider, "All adapters", clock);
        List<NetworkSnapshot> pageSnapshots = [];
        List<NetworkSnapshot> usageBandSnapshots = [];
        List<NetworkSnapshot> speedBandSnapshots = [];
        using var page = new DataManager(DataType.Network, () => pageSnapshots.Add(stats.GetSnapshot(0)), networkStats: stats);
        using var usageBand = new DataManager(DataType.Network, () => usageBandSnapshots.Add(stats.GetSnapshot(0)), networkStats: stats);
        using var speedBand = new DataManager(DataType.Network, () => speedBandSnapshots.Add(stats.GetSnapshot(0)), networkStats: stats);

        page.Start();
        page.Start();
        clock.Advance(TimeSpan.FromMilliseconds(250));
        usageBand.Start();
        clock.Advance(TimeSpan.FromMilliseconds(250));
        speedBand.Start();
        clock.Advance(TimeSpan.FromMilliseconds(500));

        Assert.AreEqual(2, provider.Reads); // One baseline and one full interval.
        Assert.HasCount(1, clock.Timers);
        Assert.HasCount(1, pageSnapshots);
        Assert.HasCount(1, usageBandSnapshots);
        Assert.HasCount(1, speedBandSnapshots);
        CollectionAssert.AreEqual(pageSnapshots[0].TrafficHistory, usageBandSnapshots[0].TrafficHistory);
        CollectionAssert.AreEqual(pageSnapshots[0].TrafficHistory, speedBandSnapshots[0].TrafficHistory);
        Assert.HasCount(2, pageSnapshots[0].TrafficHistory);
        Assert.AreEqual(512f, pageSnapshots[0].Usage.Sent);
        Assert.AreEqual(1024f, pageSnapshots[0].Usage.Received);

        page.Stop();
        page.Stop();
        clock.Advance(TimeSpan.FromSeconds(1));
        usageBand.Dispose();
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(4, provider.Reads);
        Assert.HasCount(1, pageSnapshots);
        Assert.HasCount(2, usageBandSnapshots);
        Assert.HasCount(3, speedBandSnapshots);

        var history = stats.GetSnapshot(0).TrafficHistory;
        Assert.HasCount(6, history);
        for (var index = 0; index < history.Length; index += 2)
        {
            Assert.AreEqual(512d, history[index].Value);
            Assert.AreEqual(1024d, history[index + 1].Value);
            Assert.AreEqual(history[index].GetTimestamp(), history[index + 1].GetTimestamp());
            if (index > 0)
            {
                Assert.AreEqual(TimeSpan.FromSeconds(1), history[index].GetTimestamp() - history[index - 2].GetTimestamp());
            }
        }

        speedBand.Stop();
        clock.Advance(TimeSpan.FromSeconds(5));
        Assert.AreEqual(4, provider.Reads);
        Assert.IsTrue(clock.Timers[0].IsDisposed);
    }

    [TestMethod]
    public void Restart_PrimesCountersWithoutAddingAZeroOrAveragingThePause()
    {
        var clock = new ManualSamplingTimeProvider();
        var provider = new SnapshotProvider(clock);
        var stats = new NetworkStats(provider, "All adapters", clock);
        using var view = new DataManager(DataType.Network, () => { }, networkStats: stats);
        view.Start();
        clock.Advance(TimeSpan.FromSeconds(1));
        var previous = stats.GetSnapshot(0);
        var oldTimer = clock.Timers[0];

        view.Stop();
        clock.Advance(TimeSpan.FromSeconds(10));
        view.Start();
        clock.Advance(TimeSpan.Zero);
        oldTimer.Fire();
        Assert.AreEqual(3, provider.Reads);
        CollectionAssert.AreEqual(previous.TrafficHistory, stats.GetSnapshot(0).TrafficHistory);

        clock.Advance(TimeSpan.FromSeconds(1));
        var current = stats.GetSnapshot(0);
        Assert.AreEqual(4, provider.Reads);
        Assert.HasCount(4, current.TrafficHistory);
        Assert.AreEqual(TimeSpan.FromSeconds(11), current.TrafficHistory[2].GetTimestamp() - current.TrafficHistory[0].GetTimestamp());
        Assert.AreEqual(512f, current.Usage.Sent);
        Assert.AreEqual(1024f, current.Usage.Received);
    }

    [TestMethod]
    public void OverlappingAndStoppedReads_DoNotAddExtraHistory()
    {
        var clock = new ManualSamplingTimeProvider();
        var provider = new SnapshotProvider(clock);
        var stats = new NetworkStats(provider, "All adapters", clock);
        var notifications = 0;
        using var view = new DataManager(DataType.Network, () => notifications++, networkStats: stats);
        view.Start();
        clock.Advance(TimeSpan.Zero);
        provider.OnRead = () => clock.Timers[0].Fire();
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(2, provider.Reads);
        Assert.AreEqual(1, notifications);

        provider.OnRead = view.Stop;
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(3, provider.Reads);
        Assert.AreEqual(1, notifications);
        Assert.HasCount(2, stats.GetSnapshot(0).TrafficHistory);
        Assert.IsTrue(clock.Timers[0].IsDisposed);
    }

    [TestMethod]
    public void ReadFailure_ReprimesBeforePublishingAnotherInterval()
    {
        var clock = new ManualSamplingTimeProvider();
        var provider = new SnapshotProvider(clock);
        var stats = new NetworkStats(provider, "All adapters", clock);
        var notifications = 0;
        using var view = new DataManager(DataType.Network, () => notifications++, networkStats: stats);
        view.Start();
        clock.Advance(TimeSpan.FromSeconds(1));

        provider.FailNextRead = true;
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.AreEqual(1, notifications);
        Assert.HasCount(2, stats.GetSnapshot(0).TrafficHistory);
        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.AreEqual(2, notifications);
        Assert.HasCount(4, stats.GetSnapshot(0).TrafficHistory);
        Assert.AreEqual(1024f, stats.GetSnapshot(0).Usage.Received);
    }

    [TestMethod]
    public void ObserverFailure_DoesNotStopOtherViews()
    {
        var clock = new ManualSamplingTimeProvider();
        var stats = new NetworkStats(new SnapshotProvider(clock), "All adapters", clock);
        var notifications = 0;
        using var failing = new DataManager(DataType.Network, () => throw new InvalidOperationException("Observer failed"), networkStats: stats);
        using var healthy = new DataManager(DataType.Network, () => notifications++, networkStats: stats);
        failing.Start();
        healthy.Start();
        clock.Advance(TimeSpan.FromSeconds(2));

        Assert.AreEqual(2, notifications);
        Assert.HasCount(4, stats.GetSnapshot(0).TrafficHistory);
    }

    private sealed class SnapshotProvider(ManualSamplingTimeProvider clock) : IPhysicalNetworkInterfaceSnapshotProvider
    {
        public int Reads { get; private set; }

        public bool FailNextRead { get; set; }

        public Action? OnRead { get; set; }

        public IReadOnlyList<PhysicalNetworkInterfaceSnapshot> GetSnapshots()
        {
            Reads++;
            OnRead?.Invoke();
            if (FailNextRead)
            {
                FailNextRead = false;
                throw new InvalidOperationException("Network counters unavailable");
            }

            // Packets arrive in batches: additional polls between seconds would
            // alternate real traffic with near-zero samples and create thin spikes.
            var seconds = (ulong)(clock.GetTimestamp() / TimeSpan.TicksPerSecond);
            return [new(1, Guid.Empty, "Ethernet", seconds * 1024, seconds * 512, 1_000_000)];
        }
    }
}
