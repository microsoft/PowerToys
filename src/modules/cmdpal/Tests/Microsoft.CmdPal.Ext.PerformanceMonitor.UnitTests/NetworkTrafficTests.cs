// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CoreWidgetProvider.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public partial class NetworkTrafficTests
{
    [TestMethod]
    public void Sampling_UsesElapsedTimeAndPublishesPairedRawRatesWithUtilization()
    {
        var clock = new ManualSamplingTimeProvider();
        var provider = new SnapshotProvider { Snapshots = [new(1, Guid.Empty, "Ethernet", 0, 0, 1_000_000)] };
        var stats = new NetworkStats(provider, "All adapters", clock);
        stats.GetData();
        clock.Advance(TimeSpan.FromMilliseconds(250));
        provider.Snapshots = [new(1, Guid.Empty, "Ethernet", 2500, 500, 1_000_000)];
        stats.GetData();

        var snapshot = stats.GetSnapshot(1);
        Assert.AreEqual("Ethernet", snapshot.Name);
        Assert.AreEqual(2000f, snapshot.Usage.Sent);
        Assert.AreEqual(10_000f, snapshot.Usage.Received);
        Assert.AreEqual(9.6d, snapshot.UtilizationHistory[^1].Value, 0.001);
        Assert.HasCount(4, snapshot.TrafficHistory);
        Assert.AreEqual(0u, snapshot.TrafficHistory[^2].SeriesIndex);
        Assert.AreEqual(1u, snapshot.TrafficHistory[^1].SeriesIndex);
        Assert.AreEqual(2000d, snapshot.TrafficHistory[^2].Value);
        Assert.AreEqual(10_000d, snapshot.TrafficHistory[^1].Value);
        Assert.AreEqual(snapshot.UtilizationHistory[^1].GetTimestamp(), snapshot.TrafficHistory[^2].GetTimestamp());
        Assert.AreEqual(snapshot.TrafficHistory[^2].GetTimestamp(), snapshot.TrafficHistory[^1].GetTimestamp());
        Assert.AreEqual(clock.GetUtcNow(), snapshot.TrafficHistory[^1].GetTimestamp());
        CollectionAssert.AreEqual(snapshot.TrafficHistory, stats.GetSnapshot(0).TrafficHistory);

        clock.Advance(TimeSpan.FromSeconds(1));
        provider.Snapshots = [new(1, Guid.Empty, "Ethernet", 100, 1000, 1_000_000)];
        stats.GetData();
        Assert.AreEqual(500d, stats.GetSnapshot(1).TrafficHistory[^2].Value);
        Assert.AreEqual(0d, stats.GetSnapshot(1).TrafficHistory[^1].Value);
        Assert.HasCount(4, snapshot.TrafficHistory);
        Assert.AreEqual(10_000d, snapshot.TrafficHistory[^1].Value);
        snapshot.TrafficHistory[0].Value = -1;
        Assert.AreEqual(0d, stats.GetSnapshot(1).TrafficHistory[0].Value);
    }

    [TestMethod]
    public void TrafficHistory_FollowsAdapterIdentityAcrossReorderingAndRemoval()
    {
        var clock = new ManualSamplingTimeProvider();
        var stats = new NetworkStats(new SnapshotProvider(), "All adapters", clock);
        stats.ApplySnapshots([new(1, Guid.Empty, "Ethernet", 0, 0, 1_000_000), new(2, Guid.Empty, "Wi-Fi", 0, 0, 1_000_000)], 0);
        clock.Advance(TimeSpan.FromSeconds(1));
        stats.ApplySnapshots([new(1, Guid.Empty, "Ethernet", 2000, 1000, 1_000_000), new(2, Guid.Empty, "Wi-Fi", 4000, 3000, 1_000_000)], 1);
        var ethernet = stats.GetSnapshot(1);
        var wifi = stats.GetSnapshot(2);
        Assert.AreEqual(4000d, stats.GetSnapshot(0).TrafficHistory[^2].Value);
        Assert.AreEqual(6000d, stats.GetSnapshot(0).TrafficHistory[^1].Value);

        clock.Advance(TimeSpan.FromSeconds(1));
        stats.ApplySnapshots([new(2, Guid.Empty, "Wi-Fi", 5000, 3500, 1_000_000), new(1, Guid.Empty, "Ethernet", 6000, 2000, 1_000_000)], 1);
        Assert.AreEqual(ethernet.TrafficHistory[^2], stats.GetSnapshot(2).TrafficHistory[^4]);
        Assert.AreEqual(wifi.TrafficHistory[^1], stats.GetSnapshot(1).TrafficHistory[^3]);
        Assert.AreEqual("Ethernet", stats.GetSnapshot(2).Name);

        clock.Advance(TimeSpan.FromSeconds(1));
        stats.ApplySnapshots([], 1);
        clock.Advance(TimeSpan.FromSeconds(1));
        stats.ApplySnapshots([new(1, Guid.Empty, "Ethernet", 20_000, 10_000, 1_000_000)], 1);
        var reappeared = stats.GetSnapshot(1);
        Assert.HasCount(2, reappeared.TrafficHistory);
        Assert.AreEqual(0d, reappeared.TrafficHistory[0].Value);
        Assert.AreEqual(0d, reappeared.TrafficHistory[1].Value);
    }

    [TestMethod]
    public void TrafficPage_SharesAdapterCommandsAndUpdatesUnitsWithoutResampling()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"PerformanceMonitorTrafficTests-{Guid.NewGuid():N}.json");
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var settings = new SettingsManager(filePath);
            var clock = new ManualSamplingTimeProvider();
            var provider = new SnapshotProvider();
            var stats = new NetworkStats(provider, "All adapters", clock);
            stats.ApplySnapshots([new(1, Guid.Empty, "Ethernet", 0, 0, 1_000_000), new(2, Guid.Empty, "Wi-Fi", 0, 0, 1_000_000)], 0);
            clock.Advance(TimeSpan.FromSeconds(1));
            stats.ApplySnapshots([new(1, Guid.Empty, "Ethernet", 10_000, 2000, 1_000_000), new(2, Guid.Empty, "Wi-Fi", 5000, 1000, 1_000_000)], 1);
            using var source = new SystemNetworkUsageWidgetPage(settings, stats);
            source.UpdateWidget();
            using var traffic = new SystemNetworkTrafficWidgetPage(source, settings);
            var content = traffic.GetContent();
            var graph = (ILineGraphContent)content[0];
            Assert.IsTrue(graph.AutoScaleMaximum);
            Assert.AreEqual(1d, graph.GetValueScales()[0].Divisor);
            Assert.AreEqual(" bps", graph.GetValueScales()[0].Suffix);
            Assert.AreEqual(1000d, graph.GetValueScales()[1].Divisor);
            Assert.AreEqual(" Kbps", graph.GetValueScales()[1].Suffix);
            Assert.AreEqual(UsageHistory.Duration, graph.HistoryDuration);
            Assert.HasCount(2, graph.GetSeries());
            Assert.AreEqual(24_000d, graph.GetSnapshot()[^2].Value);
            Assert.AreEqual(120_000d, graph.GetSnapshot()[^1].Value);
            Assert.AreEqual("24.0 Kbps", traffic.GetUpSpeed());
            Assert.AreEqual("120.0 Kbps", traffic.GetDownSpeed());
            Assert.AreEqual(traffic.GetDownSpeed(), GraphValueFormatter.Format(graph.GetSnapshot()[^1].Value, valueScales: graph.GetValueScales()));
            Assert.AreSame(source.Commands, traffic.Commands);
            Assert.AreEqual(source.CurrentSnapshot.TrafficHistory[^1].GetTimestamp(), graph.GetSnapshot()[^1].GetTimestamp());

            var notifications = 0;
            TypedEventHandler<object, IItemsChangedEventArgs> handler = (_, _) => notifications++;
            traffic.ItemsChanged += handler;
            traffic.PopActivate(); // Keep this content test independent of the periodic timer.
            try
            {
                var form = (IFormContent)settings.Settings.ToContent()[0];
                form.SubmitForm("""{"performanceMonitor.NetworkSpeedUnit":"BytesPerSecond"}""", "{}");
                var bytesGraph = (ILineGraphContent)traffic.GetContent()[0];
                Assert.AreNotSame(graph, bytesGraph);
                Assert.AreSame(content[1], traffic.GetContent()[1]);
                Assert.AreEqual(1000d, bytesGraph.GetValueScales()[1].Divisor);
                Assert.AreEqual(" KB/s", bytesGraph.GetValueScales()[1].Suffix);
                Assert.AreEqual(3000d, bytesGraph.GetSnapshot()[^2].Value);
                Assert.AreEqual(15_000d, bytesGraph.GetSnapshot()[^1].Value);
                Assert.AreEqual("3.0 KB/s", traffic.GetUpSpeed());
                Assert.AreEqual("15.0 KB/s", traffic.GetDownSpeed());
                Assert.AreEqual(traffic.GetDownSpeed(), GraphValueFormatter.Format(bytesGraph.GetSnapshot()[^1].Value, valueScales: bytesGraph.GetValueScales()));
                Assert.AreEqual(120_000d, graph.GetSnapshot()[^1].Value);
                Assert.AreEqual(1, notifications);
                form.SubmitForm("""{"performanceMonitor.NetworkSpeedUnit":"BytesPerSecond"}""", "{}");
                Assert.AreSame(bytesGraph, traffic.GetContent()[0]);
                Assert.AreEqual(1, notifications);

                ((IInvokableCommand)((ICommandContextItem)traffic.Commands[1]).Command).Invoke(traffic);
                Assert.AreEqual("Ethernet", source.CurrentSnapshot.Name);
                Assert.AreEqual(2000d, bytesGraph.GetSnapshot()[^2].Value);
                Assert.AreEqual(10_000d, bytesGraph.GetSnapshot()[^1].Value);
                Assert.Contains("Ethernet", ((IFormContent)traffic.GetContent()[1]).DataJson);

                form.SubmitForm("""{"performanceMonitor.NetworkSpeedUnit":"BinaryBytesPerSecond"}""", "{}");
                var binaryGraph = (ILineGraphContent)traffic.GetContent()[0];
                Assert.AreEqual(1024d, binaryGraph.GetValueScales()[1].Divisor);
                Assert.AreEqual(" KiB/s", binaryGraph.GetValueScales()[1].Suffix);
                Assert.AreEqual(1024d, binaryGraph.Maximum);
                Assert.AreEqual("2.0 KiB/s", traffic.GetUpSpeed());
                Assert.AreEqual("9.8 KiB/s", traffic.GetDownSpeed());
                Assert.AreEqual(traffic.GetDownSpeed(), GraphValueFormatter.Format(binaryGraph.GetSnapshot()[^1].Value, valueScales: binaryGraph.GetValueScales()));
                Assert.AreEqual(2, notifications);
                Assert.AreEqual(0, provider.Reads);

                traffic.Dispose();
                form.SubmitForm("""{"performanceMonitor.NetworkSpeedUnit":"BitsPerSecond"}""", "{}");
                ((IInvokableCommand)((ICommandContextItem)source.Commands[1]).Command).Invoke(source);
                Assert.AreEqual("Wi-Fi", source.CurrentSnapshot.Name);
                Assert.AreSame(binaryGraph, traffic.GetContent()[0]);
                Assert.AreEqual(10_000d, binaryGraph.GetSnapshot()[^1].Value);
                Assert.AreEqual(2, notifications);
            }
            finally
            {
                traffic.ItemsChanged -= handler;
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            File.Delete(filePath);
        }
    }

    [TestMethod]
    public void NetworkBands_KeepSeparateUtilizationAndTrafficDestinations()
    {
        var settings = new SettingsManager(Path.Combine(Path.GetTempPath(), $"PerformanceMonitorTrafficTests-{Guid.NewGuid():N}.json"));
        using var usage = new PerformanceWidgetsPage(settings, isBandPage: true, singleMetric: PerformanceMetricKind.Network);
        using var speed = new PerformanceWidgetsPage(settings, isBandPage: true, singleMetric: PerformanceMetricKind.NetworkSpeed);
        var usageItems = usage.GetItems();
        var speedItems = speed.GetItems();
        Assert.HasCount(1, usageItems);
        Assert.HasCount(2, speedItems);
        Assert.AreEqual("com.microsoft.cmdpal.network_widget", usageItems[0].Command.Id);
        Assert.AreEqual("com.microsoft.cmdpal.network_traffic_widget", speedItems[0].Command.Id);
        Assert.AreSame(speedItems[0].Command, speedItems[1].Command);
        Assert.AreNotEqual(usage.Id, speed.Id);
    }

    private sealed class SnapshotProvider : IPhysicalNetworkInterfaceSnapshotProvider
    {
        public IReadOnlyList<PhysicalNetworkInterfaceSnapshot> Snapshots { get; set; } = [];

        public int Reads { get; private set; }

        public IReadOnlyList<PhysicalNetworkInterfaceSnapshot> GetSnapshots()
        {
            Reads++;
            return Snapshots;
        }
    }
}
