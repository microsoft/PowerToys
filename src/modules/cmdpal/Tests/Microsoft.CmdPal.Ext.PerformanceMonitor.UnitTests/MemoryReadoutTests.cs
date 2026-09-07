// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text.Json;
using CoreWidgetProvider.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public partial class MemoryReadoutTests
{
    private const ulong Gigabyte = 1024UL * 1024 * 1024;

    [TestMethod]
    public void History_RetainsPairedAmountsWhenCapacityChanges()
    {
        var clock = new TestTimeProvider();
        using var stats = new MemoryStats(clock);
        stats.ApplyPhysicalMemory(32 * Gigabyte, 8 * Gigabyte);
        clock.Advance();
        stats.ApplyPhysicalMemory(64 * Gigabyte, 40 * Gigabyte);

        var samples = stats.MemoryHistory.GetSnapshot();
        Assert.HasCount(6, samples);
        AssertObservation(samples, 0, 75, 24, 8);
        AssertObservation(samples, 3, 37.5, 24, 40);
        Assert.AreEqual(TimeSpan.FromSeconds(1), samples[3].GetTimestamp() - samples[0].GetTimestamp());
        Assert.AreEqual(64 * Gigabyte, stats.AllMem);
        Assert.AreEqual(24 * Gigabyte, stats.UsedMem);
        Assert.AreEqual(40 * Gigabyte, stats.AvailableMem);
        Assert.AreEqual(0.375f, stats.MemUsage);
    }

    [TestMethod]
    [DataRow(0, 100d, 32d, 0d)]
    [DataRow(32, 0d, 0d, 32d)]
    public void PhysicalMemory_HandlesEmptyAndFullAvailability(int availableGigabytes, double percent, double used, double available)
    {
        using var stats = new MemoryStats();
        stats.ApplyPhysicalMemory(32 * Gigabyte, (ulong)availableGigabytes * Gigabyte);

        AssertObservation(stats.MemoryHistory.GetSnapshot(), 0, percent, used, available);
    }

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(8, 9)]
    public void InvalidPhysicalMemory_DoesNotPublishOrOverwriteTheLastReading(int totalGigabytes, int availableGigabytes)
    {
        var clock = new TestTimeProvider();
        using var stats = new MemoryStats(clock);
        stats.ApplyPhysicalMemory(32 * Gigabyte, 8 * Gigabyte);
        clock.Advance();
        stats.ApplyPhysicalMemory((ulong)totalGigabytes * Gigabyte, (ulong)availableGigabytes * Gigabyte);

        var samples = stats.MemoryHistory.GetSnapshot();
        Assert.HasCount(3, samples);
        AssertObservation(samples, 0, 75, 24, 8);
        Assert.AreEqual(32 * Gigabyte, stats.AllMem);
        Assert.AreEqual(8 * Gigabyte, stats.AvailableMem);
    }

    [TestMethod]
    public void MemoryPage_PlotsPercentAndPublishesHistoricalGigabyteReadouts()
    {
        var clock = new TestTimeProvider();
        using var stats = new MemoryStats(clock);
        using var page = new SystemMemoryUsageWidgetPage(stats);
        stats.ApplyPhysicalMemory(32 * Gigabyte, 8 * Gigabyte);
        page.UpdateWidget();
        var content = page.GetContent();
        var graph = (ILineGraphContent)content[0];
        var descriptors = graph.GetSeries();

        Assert.AreEqual(0d, graph.Minimum);
        Assert.AreEqual(100d, graph.Maximum);
        Assert.AreEqual("%", graph.ValueSuffix);
        Assert.HasCount(3, descriptors);
        Assert.IsFalse(descriptors[0].IsReadoutOnly);
        Assert.IsTrue(descriptors[1].IsReadoutOnly);
        Assert.IsTrue(descriptors[2].IsReadoutOnly);
        Assert.AreEqual(" GB", descriptors[1].ReadoutValueSuffix);
        Assert.AreEqual(" GB", descriptors[2].ReadoutValueSuffix);

        clock.Advance();
        stats.ApplyPhysicalMemory(32 * Gigabyte, 12 * Gigabyte);
        page.UpdateWidget();
        Assert.AreSame(graph, page.GetContent()[0]);
        var samples = graph.GetSnapshot();
        AssertObservation(samples, 0, 75, 24, 8);
        AssertObservation(samples, 3, 62.5, 20, 12);

        using var data = JsonDocument.Parse(((IFormContent)content[1]).DataJson);
        Assert.AreEqual(20d.ToString("0.00", CultureInfo.CurrentCulture) + " GB", data.RootElement.GetProperty("usedMem").GetString());
        Assert.AreEqual(12d.ToString("0.00", CultureInfo.CurrentCulture) + " GB", data.RootElement.GetProperty("availableMem").GetString());
        Assert.AreEqual(32d.ToString("0.00", CultureInfo.CurrentCulture) + " GB", data.RootElement.GetProperty("allMem").GetString());
    }

    private static void AssertObservation(GraphSample[] samples, int offset, double percent, double used, double available)
    {
        Assert.AreEqual(0u, samples[offset].SeriesIndex);
        Assert.AreEqual(1u, samples[offset + 1].SeriesIndex);
        Assert.AreEqual(2u, samples[offset + 2].SeriesIndex);
        Assert.AreEqual(samples[offset].GetTimestamp(), samples[offset + 1].GetTimestamp());
        Assert.AreEqual(samples[offset].GetTimestamp(), samples[offset + 2].GetTimestamp());
        Assert.AreEqual(percent, samples[offset].Value);
        Assert.AreEqual(used, samples[offset + 1].Value);
        Assert.AreEqual(available, samples[offset + 2].Value);
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public override DateTimeOffset GetUtcNow() => new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        public void Advance() => _timestamp += TimeSpan.TicksPerSecond;
    }
}
