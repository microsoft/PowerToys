// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class GraphContentTests
{
    [TestMethod]
    public void LineSmoothing_DefaultsToStraightSegments()
    {
        ILineGraphContent content = new LineGraphContent([]);
        Assert.AreEqual(0d, content.Smoothing);
    }

    [TestMethod]
    [DataRow(0d)]
    [DataRow(0.75d)]
    [DataRow(1d)]
    public void LineSmoothing_AcceptsAmountsInRange(double smoothing)
    {
        ILineGraphContent content = new LineGraphContent([]) { Smoothing = smoothing };
        Assert.AreEqual(smoothing, content.Smoothing);
    }

    [TestMethod]
    [DataRow(-0.01d)]
    [DataRow(1.01d)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void LineSmoothing_RejectsInvalidAmounts(double smoothing)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = new LineGraphContent([]) { Smoothing = smoothing });
    }

    [TestMethod]
    [DataRow(GraphLineStyle.Solid)]
    [DataRow(GraphLineStyle.Dashed)]
    [DataRow(GraphLineStyle.Dotted)]
    public void Configuration_PreservesDisplayNamesAndCopiesSeriesDescriptions(GraphLineStyle lineStyle)
    {
        GraphSeriesInfo[] series = [Series("Used"), Series("Available")];
        series[1].LineStyle = lineStyle;
        series[1].IsReadoutOnly = true;
        series[1].ReadoutValueSuffix = " GB";
        ILineGraphContent line = new LineGraphContent(series) { DisplayName = "Line graph" };
        IVerticalUsageBarContent bar = new VerticalUsageBarContent(series) { DisplayName = "Usage bar" };
        IDoughnutGraphContent doughnut = new DoughnutGraphContent(series) { DisplayName = "Doughnut graph" };

        series[0] = Series("Changed");
        series[1].LineStyle = GraphLineStyle.Solid;
        series[1].IsReadoutOnly = false;
        series[1].ReadoutValueSuffix = " changed";
        line.GetSeries()[0] = series[0];
        line.GetSeries()[1].LineStyle = GraphLineStyle.Solid;
        bar.GetSeries()[0] = series[0];
        doughnut.GetSeries()[0] = series[0];

        Assert.AreEqual("Used", line.GetSeries()[0].Name);
        Assert.AreEqual("Used", bar.GetSeries()[0].Name);
        Assert.AreEqual("Used", doughnut.GetSeries()[0].Name);
        Assert.AreEqual(GraphLineStyle.Solid, line.GetSeries()[0].LineStyle);
        Assert.AreEqual(lineStyle, line.GetSeries()[1].LineStyle);
        Assert.AreEqual(lineStyle, bar.GetSeries()[1].LineStyle);
        Assert.AreEqual(lineStyle, doughnut.GetSeries()[1].LineStyle);
        Assert.IsFalse(line.GetSeries()[0].IsReadoutOnly);
        Assert.IsTrue(line.GetSeries()[1].IsReadoutOnly);
        Assert.AreEqual(" GB", line.GetSeries()[1].ReadoutValueSuffix);
        Assert.AreEqual("Line graph", line.DisplayName);
        Assert.AreEqual("Usage bar", bar.DisplayName);
        Assert.AreEqual("Doughnut graph", doughnut.DisplayName);
    }

    [TestMethod]
    public void Configuration_RejectsAnUnknownLineStyle()
    {
        var series = Series("CPU");
        series.LineStyle = (GraphLineStyle)int.MaxValue;
        Assert.ThrowsExactly<ArgumentException>(() => _ = new LineGraphContent([series]));
    }

    [TestMethod]
    public void LineSnapshot_AcceptsIndependentInterleavedTimelines()
    {
        ILineGraphContent content = new LineGraphContent([Series("CPU"), Series("Memory")]);
        GraphSample[] samples = [Sample(0, 0, 20), Sample(1, 1, 60), Sample(0, 2, 40)];

        ((LineGraphContent)content).SetSnapshot(samples);

        CollectionAssert.AreEqual(samples, content.GetSnapshot());
    }

    [TestMethod]
    public void LineSnapshot_IsCopiedAndLateReadersReceiveRetainedHistory()
    {
        var content = new LineGraphContent([Series("CPU")]);
        GraphSample[] samples = [Sample(0, 0, 20)];
        content.SetSnapshot(samples);
        var firstRead = content.GetSnapshot();

        samples[0] = Sample(0, 0, 99);
        content.GetSnapshot()[0] = samples[0];
        Assert.AreEqual(20, content.GetSnapshot()[0].Value);

        content.SetSnapshot([Sample(0, 0, 20), Sample(0, 1, 40)]);
        content.SetSnapshot([Sample(0, 0, 20), Sample(0, 1, 40), Sample(0, 2, 60)]);

        Assert.HasCount(1, firstRead);
        Assert.AreEqual(20, firstRead[0].Value);
        GraphSample[] latest = [Sample(0, 0, 20), Sample(0, 1, 40), Sample(0, 2, 60)];
        CollectionAssert.AreEqual(latest, content.GetSnapshot());
    }

    [TestMethod]
    public void LineSnapshot_PublishesBeforeOneNotificationAndCanClear()
    {
        var content = new LineGraphContent([Series("CPU")]);
        List<string> notifications = [];
        List<int> observedCounts = [];
        content.PropChanged += (_, args) =>
        {
            notifications.Add(args.PropertyName);
            observedCounts.Add(content.GetSnapshot().Length);
        };

        content.SetSnapshot([Sample(0, 0, 20)]);
        content.SetSnapshot([]);

        string[] expectedNotifications = ["Data", "Data"];
        int[] expectedCounts = [1, 0];
        CollectionAssert.AreEqual(expectedNotifications, notifications);
        CollectionAssert.AreEqual(expectedCounts, observedCounts);
        Assert.HasCount(1, content.GetSeries());
    }

    [TestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void LineSnapshot_RejectsNonFiniteValuesWithoutPublishing(double value)
    {
        AssertRejectedLineSnapshot([Sample(0, 0, value)]);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void LineSnapshot_RejectsNonIncreasingTimestampsWithinASeries(int seconds)
    {
        AssertRejectedLineSnapshot([Sample(0, 0, 20), Sample(1, 1, 60), Sample(0, seconds, 40)]);
    }

    [TestMethod]
    public void LineSnapshot_RejectsAnUnknownSeriesIndex()
    {
        AssertRejectedLineSnapshot([Sample(uint.MaxValue, 0, 20)]);
    }

    [TestMethod]
    public void LineSnapshot_PreservesOutOfRangeMeasurements()
    {
        var content = new LineGraphContent([Series("CPU")]);
        GraphSample[] samples = [Sample(0, 0, -1), Sample(0, 1, 101)];

        content.SetSnapshot(samples);

        CollectionAssert.AreEqual(samples, content.GetSnapshot());
    }

    [TestMethod]
    public void VerticalSnapshot_PublishesHeadlineTextAndStackTogether()
    {
        var content = new VerticalUsageBarContent([Series("Used"), Series("Available")]);
        double[] values = [40, 60];
        List<string> notifications = [];
        var observedValue = double.NaN;
        var observedText = string.Empty;
        double[] observedStack = [];
        content.PropChanged += (_, args) =>
        {
            notifications.Add(args.PropertyName);
            observedStack = content.GetSnapshot(out observedValue, out observedText);
        };

        content.SetSnapshot(40, "40%", values);
        values[0] = 99;
        var snapshot = content.GetSnapshot(out var value, out var text);
        content.GetSnapshot(out _, out _)[0] = 77;

        Assert.AreEqual(40, value);
        Assert.AreEqual("40%", text);
        Assert.AreEqual(40, observedValue);
        Assert.AreEqual("40%", observedText);
        Assert.AreEqual(60, observedStack[1]);
        Assert.AreEqual(40, content.GetSnapshot(out _, out _)[0]);
        string[] expectedNotifications = ["Data"];
        CollectionAssert.AreEqual(expectedNotifications, notifications);

        content.SetSnapshot(20, "20%", [0, 0]);

        Assert.AreEqual(40, snapshot[0]);
        Assert.AreEqual(60, snapshot[1]);
        Assert.HasCount(2, content.GetSeries());
        Assert.AreEqual(0, content.GetSnapshot(out value, out text)[0]);
        Assert.AreEqual(20, value);
        Assert.AreEqual("20%", text);
    }

    [TestMethod]
    public void VerticalSnapshot_SupportsASimpleMeterWithANonZeroMinimum()
    {
        IVerticalUsageBarContent model = new VerticalUsageBarContent([], minimum: -20, maximum: 80);
        var content = (VerticalUsageBarContent)model;

        Assert.IsEmpty(model.GetSnapshot(out var initialValue, out _));
        Assert.AreEqual(-20, initialValue);
        content.SetSnapshot(10, "10 C");

        Assert.IsEmpty(model.GetSnapshot(out var value, out var text));
        Assert.AreEqual(10, value);
        Assert.AreEqual("10 C", text);
    }

    [TestMethod]
    public void DoughnutSnapshot_PreservesRawValuesAndPublishesCenterTextTogether()
    {
        var content = new DoughnutGraphContent([Series("Used"), Series("Available")]);
        double[] values = [2, 6];
        List<string> notifications = [];
        var observedValue = string.Empty;
        var observedLabel = string.Empty;
        double[] observedValues = [];
        content.PropChanged += (_, args) =>
        {
            notifications.Add(args.PropertyName);
            observedValues = content.GetSnapshot(out observedValue, out observedLabel);
        };

        content.SetSnapshot(values, "25%", "used");
        values[0] = 99;
        var firstRead = content.GetSnapshot(out var centerValue, out var centerLabel);
        content.GetSnapshot(out _, out _)[0] = 77;

        Assert.AreEqual(2, firstRead[0]);
        Assert.AreEqual(6, firstRead[1]);
        Assert.AreEqual(2, content.GetSnapshot(out _, out _)[0]);
        Assert.AreEqual("25%", centerValue);
        Assert.AreEqual("used", centerLabel);
        Assert.AreEqual("25%", observedValue);
        Assert.AreEqual("used", observedLabel);
        Assert.AreEqual(2, observedValues[0]);
        string[] expectedNotifications = ["Data"];
        CollectionAssert.AreEqual(expectedNotifications, notifications);

        content.SetSnapshot([0, 0], "0%", "used");

        Assert.AreEqual(2, firstRead[0]);
        double[] cleared = [0, 0];
        CollectionAssert.AreEqual(cleared, content.GetSnapshot(out centerValue, out centerLabel));
        Assert.AreEqual("0%", centerValue);
        Assert.HasCount(2, content.GetSeries());
    }

    [TestMethod]
    public void DoughnutSnapshot_AcceptsLargeFiniteValuesWithoutSummingThem()
    {
        var content = new DoughnutGraphContent([Series("First"), Series("Second")]);
        double[] values = [double.MaxValue, double.MaxValue];

        content.SetSnapshot(values, "50%", "first");

        CollectionAssert.AreEqual(values, content.GetSnapshot(out _, out _));
    }

    [TestMethod]
    [DataRow(-1.0)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void ValueSnapshots_RejectInvalidContributionsWithoutPublishing(double value)
    {
        var bar = new VerticalUsageBarContent([Series("Used")]);
        var doughnut = new DoughnutGraphContent([Series("Used")]);
        bar.SetSnapshot(40, "40%", [40]);
        doughnut.SetSnapshot([40], "40%", "used");
        var notificationCount = 0;
        bar.PropChanged += (_, _) => notificationCount++;
        doughnut.PropChanged += (_, _) => notificationCount++;

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => bar.SetSnapshot(99, "99%", [value]));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => doughnut.SetSnapshot([value], "99%", "changed"));

        Assert.AreEqual(40, bar.GetSnapshot(out var headline, out var text)[0]);
        Assert.AreEqual(40, headline);
        Assert.AreEqual("40%", text);
        Assert.AreEqual(40, doughnut.GetSnapshot(out var centerValue, out var centerLabel)[0]);
        Assert.AreEqual("40%", centerValue);
        Assert.AreEqual("used", centerLabel);
        Assert.AreEqual(0, notificationCount);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(3)]
    public void ValueSnapshots_RequireOneContributionPerConfiguredSeries(int valueCount)
    {
        var bar = new VerticalUsageBarContent([Series("Used"), Series("Available")]);
        var doughnut = new DoughnutGraphContent([Series("Used"), Series("Available")]);
        bar.SetSnapshot(40, "40%", [40, 60]);
        doughnut.SetSnapshot([40, 60], "40%", "used");

        Assert.ThrowsExactly<ArgumentException>(() => bar.SetSnapshot(99, "99%", new double[valueCount]));
        Assert.ThrowsExactly<ArgumentException>(() => doughnut.SetSnapshot(new double[valueCount]));

        Assert.HasCount(2, bar.GetSnapshot(out var value, out _));
        Assert.AreEqual(40, value);
        Assert.HasCount(2, doughnut.GetSnapshot(out var centerValue, out _));
        Assert.AreEqual("40%", centerValue);
    }

    [TestMethod]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.NegativeInfinity)]
    public void VerticalSnapshot_RejectsNonFiniteHeadlineValues(double value)
    {
        var content = new VerticalUsageBarContent();
        content.SetSnapshot(40, "40%");

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => content.SetSnapshot(value, "invalid"));

        content.GetSnapshot(out var headline, out var text);
        Assert.AreEqual(40, headline);
        Assert.AreEqual("40%", text);
    }

    [TestMethod]
    public void EmptyConfigurations_AllowEmptySnapshots()
    {
        var line = new LineGraphContent([]);
        var bar = new VerticalUsageBarContent();
        var doughnut = new DoughnutGraphContent([]);

        line.SetSnapshot([]);
        bar.SetSnapshot(0, string.Empty);
        doughnut.SetSnapshot([]);

        Assert.IsEmpty(line.GetSnapshot());
        Assert.IsEmpty(bar.GetSnapshot(out _, out _));
        Assert.IsEmpty(doughnut.GetSnapshot(out _, out _));
    }

    [TestMethod]
    [DataRow(0.0, 0.0)]
    [DataRow(1.0, 0.0)]
    [DataRow(double.NaN, 100.0)]
    [DataRow(0.0, double.PositiveInfinity)]
    [DataRow(-double.MaxValue, double.MaxValue)]
    public void Ranges_MustHaveAPositiveFiniteSpan(double minimum, double maximum)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = new LineGraphContent([], minimum, maximum));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = new VerticalUsageBarContent([], minimum, maximum));
    }

    [TestMethod]
    public void ConcurrentReads_KeepSnapshotTextAndValuesFromTheSamePublication()
    {
        var bar = new VerticalUsageBarContent([Series("Used"), Series("Available")]);
        var doughnut = new DoughnutGraphContent([Series("Used"), Series("Available")]);
        bar.SetSnapshot(0, "0", [0, 100]);
        doughnut.SetSnapshot([0, 100], "0", "snapshot:0");
        using var start = new Barrier(2);

        Parallel.Invoke(
            () =>
            {
                start.SignalAndWait();
                for (var index = 1; index <= 2000; index++)
                {
                    var value = index % 101;
                    var text = value.ToString(CultureInfo.InvariantCulture);
                    bar.SetSnapshot(value, text, [value, 100 - value]);
                    doughnut.SetSnapshot([value, 100 - value], text, "snapshot:" + text);
                }
            },
            () =>
            {
                start.SignalAndWait();
                for (var index = 0; index < 2000; index++)
                {
                    var contributions = bar.GetSnapshot(out var headline, out var text);
                    Assert.AreEqual(headline, contributions[0]);
                    Assert.AreEqual(headline.ToString(CultureInfo.InvariantCulture), text);
                    Assert.AreEqual(100, contributions[0] + contributions[1]);

                    var slices = doughnut.GetSnapshot(out var centerValue, out var centerLabel);
                    Assert.AreEqual(slices[0].ToString(CultureInfo.InvariantCulture), centerValue);
                    Assert.AreEqual("snapshot:" + centerValue, centerLabel);
                    Assert.AreEqual(100, slices[0] + slices[1]);
                }
            });
    }

    private static void AssertRejectedLineSnapshot(GraphSample[] samples)
    {
        var content = new LineGraphContent([Series("CPU"), Series("Memory")]);
        GraphSample[] original = [Sample(0, 0, 20)];
        content.SetSnapshot(original);
        List<string> notifications = [];
        content.PropChanged += (_, args) => notifications.Add(args.PropertyName);

        Assert.ThrowsExactly<ArgumentException>(() => content.SetSnapshot(samples));

        CollectionAssert.AreEqual(original, content.GetSnapshot());
        Assert.IsEmpty(notifications);
    }

    private static GraphSeriesInfo Series(string name) => new() { Name = name };

    private static GraphSample Sample(uint seriesIndex, int seconds, double value) => new()
    {
        SeriesIndex = seriesIndex,
        Timestamp = DateTimeOffset.UnixEpoch.AddSeconds(seconds),
        Value = value,
    };
}
