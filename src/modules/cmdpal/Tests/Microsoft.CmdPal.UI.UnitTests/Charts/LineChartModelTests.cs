// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests.Charts;

[TestClass]
public class LineChartModelTests
{
    [TestMethod]
    public void ParsesStandardAndExtensionProperties()
    {
        var warnings = new List<string>();
        var model = LineChartModel.Parse(
            """
            {
              "type": "Chart.Line",
              "title": "CPU",
              "xAxisTitle": "60 seconds",
              "yAxisTitle": "% Utilization",
              "color": "categoricalBlue",
              "colorSet": "diverging",
              "yMin": 0,
              "yMax": 100,
              "valueFormat": "percentage",
              "fill": "gradient",
              "curve": "linear",
              "style": "sparkline",
              "showLegend": true,
              "minHeight": "120px",
              "data": [{ "legend": "Utilization", "color": "good", "values": [{ "y": 1 }, { "y": 2.5 }] }]
            }
            """,
            warnings);

        Assert.AreEqual(0, warnings.Count, string.Join(", ", warnings));
        Assert.AreEqual("CPU", model.Title);
        Assert.AreEqual("60 seconds", model.XAxisTitle);
        Assert.AreEqual("% Utilization", model.YAxisTitle);
        Assert.AreEqual("categoricalBlue", model.Color);
        Assert.AreEqual("diverging", model.ColorSet);
        Assert.AreEqual(0d, model.YMin);
        Assert.AreEqual(100d, model.YMax);
        Assert.AreEqual(ChartValueFormat.Percentage, model.ValueFormat);
        Assert.AreEqual(ChartFill.Gradient, model.Fill);
        Assert.AreEqual(ChartInterpolation.Linear, model.Interpolation);
        Assert.AreEqual(ChartStyle.Sparkline, model.Style);
        Assert.AreEqual(true, model.ShowLegend);
        Assert.AreEqual(120d, model.MinHeight);
        Assert.AreEqual(1, model.Series.Count);
        Assert.AreEqual("Utilization", model.Series[0].Legend);
        Assert.AreEqual("good", model.Series[0].Color);
        Assert.AreEqual(2.5, model.Series[0].Points[1].Y);
        Assert.AreEqual(2, model.SlotCount);
    }

    [TestMethod]
    public void DefaultsMatchTheAdaptiveCardsSchema()
    {
        var model = LineChartModel.Parse("""{ "type": "Chart.Line", "data": [] }""", new List<string>());

        Assert.AreEqual(ChartFill.None, model.Fill);
        Assert.AreEqual(ChartStyle.Default, model.Style);
        Assert.AreEqual(ChartValueFormat.Number, model.ValueFormat);
        Assert.IsNull(model.YMin);
        Assert.IsNull(model.YMax);
        Assert.IsFalse(model.ShowsLegend);
    }

    [TestMethod]
    public void NullSamplesBecomeGaps()
    {
        var model = LineChartModel.Parse(
            """{ "data": [{ "values": [{ "y": null }, {}, { "y": 4 }, 7] }] }""",
            new List<string>());

        var points = model.Series[0].Points;
        Assert.AreEqual(4, points.Count);
        Assert.IsNull(points[0].Y);
        Assert.IsNull(points[1].Y);
        Assert.AreEqual(4d, points[2].Y);
        Assert.AreEqual(7d, points[3].Y);
    }

    [TestMethod]
    public void XValuesBecomeLabels()
    {
        var model = LineChartModel.Parse(
            """{ "data": [{ "values": [{ "x": "Jan", "y": 1 }, { "x": "Feb", "y": 2 }] }] }""",
            new List<string>());

        Assert.IsTrue(model.HasPointLabels);
        Assert.AreEqual("Jan", model.Series[0].Points[0].Label);
    }

    [TestMethod]
    public void MultipleSeriesShowALegendByDefault()
    {
        var model = LineChartModel.Parse(
            """{ "data": [{ "legend": "Send", "values": [1] }, { "legend": "Receive", "values": [2] }] }""",
            new List<string>());

        Assert.IsTrue(model.ShowsLegend);
    }

    [TestMethod]
    [DataRow("""{ "type": "Chart.Line" }""")]
    [DataRow("""{ "data": "${missing}" }""")]
    [DataRow("""{ "data": [1, 2] }""")]
    [DataRow("""{ "data": [{ "legend": "no values" }] }""")]
    [DataRow("""{ "data": [], "fill": "sparkles" }""")]
    public void InvalidContentWarnsWithoutThrowing(string json)
    {
        var warnings = new List<string>();

        LineChartModel.Parse(json, warnings);

        Assert.IsTrue(warnings.Count > 0);
    }

    [TestMethod]
    public void MalformedJsonReturnsEmptyModel()
    {
        var warnings = new List<string>();

        var model = LineChartModel.Parse("{ not json", warnings);

        Assert.AreSame(LineChartModel.Empty, model);
        Assert.AreEqual(1, warnings.Count);
    }

    [TestMethod]
    public void IncrementalStateIgnoresPropertyOrder()
    {
        var left = LineChartModel.Parse("""{ "yMax": 100, "data": [{ "values": [1], "legend": "a" }] }""", new List<string>());
        var right = LineChartModel.Parse("""{ "data": [{ "legend": "a", "values": [1] }], "yMax": 100 }""", new List<string>());

        Assert.AreEqual(left.IncrementalState, right.IncrementalState);
    }

    [TestMethod]
    public void IncrementalStateChangesWithData()
    {
        var before = LineChartModel.Parse("""{ "data": [{ "values": [1] }] }""", new List<string>());
        var after = LineChartModel.Parse("""{ "data": [{ "values": [2] }] }""", new List<string>());

        Assert.AreNotEqual(before.IncrementalState, after.IncrementalState);
    }

    [TestMethod]
    public void ValueExtentSpansAllSeries()
    {
        var model = LineChartModel.Parse(
            """{ "data": [{ "values": [3, null, 9] }, { "values": [-2, 4] }] }""",
            new List<string>());

        Assert.AreEqual((-2d, 9d), model.GetValueExtent());
    }
}
