// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using CoreWidgetProvider.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.PerformanceMonitor.UnitTests;

[TestClass]
public class UsageHistoryTests
{
    [TestMethod]
    public void Add_PublishesTotalAndKernelAtTheSameSampleTime()
    {
        var clock = new TestTimeProvider();
        var history = new UsageHistory(seriesCount: 2, timeProvider: clock);
        history.Add(65, 12);
        clock.Advance(TimeSpan.FromSeconds(1));
        history.Add(80, 20);

        var samples = history.GetSnapshot();
        Assert.HasCount(4, samples);
        Assert.AreEqual(0u, samples[0].SeriesIndex);
        Assert.AreEqual(1u, samples[1].SeriesIndex);
        Assert.AreEqual(65d, samples[0].Value);
        Assert.AreEqual(12d, samples[1].Value);
        Assert.AreEqual(samples[0].GetTimestamp(), samples[1].GetTimestamp());
        Assert.AreEqual(samples[2].GetTimestamp(), samples[3].GetTimestamp());
        Assert.AreEqual(TimeSpan.FromSeconds(1), samples[2].GetTimestamp() - samples[0].GetTimestamp());

        var graph = new LineGraphContent([new GraphSeriesInfo { Name = "Total" }, new GraphSeriesInfo { Name = "Kernel" }]);
        graph.SetSnapshot(samples);
        CollectionAssert.AreEqual(samples, graph.GetSnapshot());
    }

    [TestMethod]
    public void Add_KeepsTimestampsIncreasingWhenWallClockMovesBackwards()
    {
        var clock = new TestTimeProvider();
        var history = new UsageHistory(timeProvider: clock);
        history.Add(20);
        var first = history.GetSnapshot()[0];

        clock.UtcNow = clock.UtcNow.AddHours(-1);
        clock.Advance(TimeSpan.FromSeconds(1));
        history.Add(30);

        Assert.AreEqual(first.GetTimestamp().AddSeconds(1), history.GetSnapshot()[1].GetTimestamp());
    }

    [TestMethod]
    public void Add_RetainsOneMinuteAndTwoBoundaryObservationsForEverySeries()
    {
        var clock = new TestTimeProvider();
        var history = new UsageHistory(seriesCount: 2, timeProvider: clock);
        var start = clock.UtcNow;
        for (var second = 0; second <= 70; second++)
        {
            history.Add(second, second / 2d);
            clock.Advance(TimeSpan.FromSeconds(1));
        }

        var samples = history.GetSnapshot();
        Assert.HasCount(126, samples);
        Assert.AreEqual(start.AddSeconds(8), samples[0].GetTimestamp());
        Assert.AreEqual(start.AddSeconds(70), samples[^1].GetTimestamp());

        clock.Advance(TimeSpan.FromMinutes(5));
        history.Add(40, 10);
        Assert.HasCount(6, history.GetSnapshot());
    }

    [TestMethod]
    public void Add_SkipsNonFiniteAndDuplicateTimeObservations()
    {
        var clock = new TestTimeProvider();
        var history = new UsageHistory(seriesCount: 2, timeProvider: clock);
        history.Add(50, 10);
        history.Add(60, 20);
        clock.Advance(TimeSpan.FromSeconds(1));
        history.Add(double.NaN, 20);
        history.Add(60, double.PositiveInfinity);
        history.Add(60, 20);

        var samples = history.GetSnapshot();
        Assert.HasCount(4, samples);
        Assert.AreEqual(50d, samples[0].Value);
        Assert.AreEqual(60d, samples[2].Value);
    }

    [TestMethod]
    public void GetSnapshot_PreservesTimestampsAndDoesNotExposeRetainedStorage()
    {
        var clock = new TestTimeProvider();
        var history = new UsageHistory(timeProvider: clock);
        history.Add(45);
        var samples = history.GetSnapshot();
        var original = samples[0];
        samples[0] = default;
        clock.Advance(TimeSpan.FromSeconds(10));

        var refreshed = history.GetSnapshot();
        Assert.HasCount(1, refreshed);
        Assert.AreEqual(original, refreshed[0]);
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private long _timestamp;

        public DateTimeOffset UtcNow { get; set; } = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public override DateTimeOffset GetUtcNow() => UtcNow;

        public void Advance(TimeSpan elapsed)
        {
            _timestamp += elapsed.Ticks;
            UtcNow += elapsed;
        }
    }
}
