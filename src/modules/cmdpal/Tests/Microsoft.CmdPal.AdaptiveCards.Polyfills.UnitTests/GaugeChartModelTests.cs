// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.AdaptiveCards.Polyfills.UnitTests;

[TestClass]
public class GaugeChartModelTests
{
    [TestMethod]
    public void ParsesValueRangeAndOptions()
    {
        var warnings = new List<string>();
        var model = GaugeChartModel.Parse(
            """
            {
              "type": "Chart.Gauge",
              "title": "CPU",
              "subLabel": "Utilization",
              "value": 42,
              "min": 0,
              "max": 200,
              "valueFormat": "fraction",
              "showLegend": false,
              "showMinMax": false,
              "showNeedle": false,
              "showTitle": true,
              "colorSet": "diverging"
            }
            """,
            warnings);

        Assert.AreEqual(0, warnings.Count, string.Join(", ", warnings));
        Assert.AreEqual("CPU", model.Title);
        Assert.IsTrue(model.ShowTitle);
        Assert.AreEqual("Utilization", model.SubLabel);
        Assert.AreEqual(42d, model.Value);
        Assert.AreEqual(200d, model.Max);
        Assert.AreEqual(GaugeValueFormat.Fraction, model.ValueFormat);
        Assert.IsFalse(model.ShowLegend);
        Assert.IsFalse(model.ShowMinMax);
        Assert.IsFalse(model.ShowNeedle);
        Assert.AreEqual("diverging", model.ColorSet);
        Assert.AreEqual(0.21, model.Fraction, 1e-9);
        Assert.AreEqual("42/200", model.FormatValue(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void DefaultsMatchTheAdaptiveCardsSchema()
    {
        var model = GaugeChartModel.Parse("""{ "type": "Chart.Gauge", "title": "CPU" }""", new List<string>());

        Assert.IsFalse(model.ShowTitle);
        Assert.IsTrue(model.ShowLegend);
        Assert.IsTrue(model.ShowMinMax);
        Assert.IsTrue(model.ShowNeedle);
        Assert.AreEqual(GaugeValueFormat.Percentage, model.ValueFormat);
        Assert.AreEqual(0d, model.Value);
    }

    [TestMethod]
    public void SegmentSizesSetTheDefaultMaximum()
    {
        var warnings = new List<string>();
        var model = GaugeChartModel.Parse(
            """
            {
              "type": "Chart.Gauge",
              "value": 70,
              "segments": [
                { "legend": "Low", "size": 50, "color": "good" },
                { "legend": "Medium", "size": 30, "color": "warning" },
                { "legend": "High", "size": 20, "color": "attention" }
              ]
            }
            """,
            warnings);

        Assert.AreEqual(0, warnings.Count, string.Join(", ", warnings));
        Assert.AreEqual(3, model.Segments.Count);
        Assert.AreEqual(30d, model.Segments[1].Value);
        Assert.AreEqual("warning", model.Segments[1].Color);
        Assert.AreEqual(100d, model.Max);
        Assert.AreEqual(1, model.GetActiveSegment());
        Assert.AreEqual("70%", model.FormatValue(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void TheMaximumIsInTheLastSegment()
    {
        var model = new GaugeChartModel
        {
            Value = 100,
            Segments = [new("Low", 50, null), new("High", 50, null)],
        };

        Assert.AreEqual(1, model.GetActiveSegment());
    }

    [TestMethod]
    public void ValueBelowTheSegmentsHasNoActiveSegment()
    {
        var model = new GaugeChartModel
        {
            Value = -5,
            Segments = [new("Low", 50, null), new("High", 50, null)],
        };

        Assert.AreEqual(-1, model.GetActiveSegment());
    }

    [TestMethod]
    public void FractionIsClamped()
    {
        Assert.AreEqual(1d, new GaugeChartModel { Value = 150 }.Fraction);
        Assert.AreEqual(0d, new GaugeChartModel { Value = -10 }.Fraction);
        Assert.AreEqual("100%", new GaugeChartModel { Value = 150 }.FormatValue(CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void InvalidRangeWarnsAndFallsBack()
    {
        var warnings = new List<string>();
        var model = GaugeChartModel.Parse("""{ "type": "Chart.Gauge", "value": 5, "min": 10, "max": 10 }""", warnings);

        Assert.AreEqual(1, warnings.Count);
        Assert.AreEqual(10d, model.Min);
        Assert.AreEqual(110d, model.Max);
    }

    [TestMethod]
    public void MalformedJsonWarns()
    {
        var warnings = new List<string>();
        var model = GaugeChartModel.Parse("{", warnings);

        Assert.AreEqual(1, warnings.Count);
        Assert.AreEqual(0d, model.Value);
    }

    [TestMethod]
    public void IncrementalStateIgnoresPropertyOrder()
    {
        var warnings = new List<string>();
        var first = GaugeChartModel.Parse("""{ "type": "Chart.Gauge", "value": 5, "max": 10 }""", warnings);
        var second = GaugeChartModel.Parse("""{ "max": 10, "value": 5, "type": "Chart.Gauge" }""", warnings);
        var changed = GaugeChartModel.Parse("""{ "max": 10, "value": 6, "type": "Chart.Gauge" }""", warnings);

        Assert.AreEqual(first.IncrementalState, second.IncrementalState);
        Assert.AreNotEqual(first.IncrementalState, changed.IncrementalState);
    }
}
