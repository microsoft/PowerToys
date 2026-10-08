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
    public void ParsesSchemaProperties()
    {
        var warnings = new List<string>();
        var model = LineChartModel.Parse(
            """
            {
              "type": "Chart.Line",
              "title": "CPU",
              "showTitle": true,
              "xAxisTitle": "60 seconds",
              "yAxisTitle": "% Utilization",
              "color": "categoricalBlue",
              "colorSet": "diverging",
              "yMin": 0,
              "yMax": 100,
              "showLegend": false,
              "data": [{ "legend": "Utilization", "color": "good", "values": [{ "y": 1 }, { "y": 2.5 }] }]
            }
            """,
            warnings);

        Assert.AreEqual(0, warnings.Count, string.Join(", ", warnings));
        Assert.AreEqual("CPU", model.Title);
        Assert.IsTrue(model.ShowTitle);
        Assert.AreEqual("60 seconds", model.XAxisTitle);
        Assert.AreEqual("% Utilization", model.YAxisTitle);
        Assert.AreEqual("categoricalBlue", model.Color);
        Assert.AreEqual("diverging", model.ColorSet);
        Assert.AreEqual(0d, model.YMin);
        Assert.AreEqual(100d, model.YMax);
        Assert.IsFalse(model.ShowLegend);
        Assert.IsFalse(model.ShowsLegend);
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

        Assert.IsFalse(model.ShowTitle);
        Assert.IsTrue(model.ShowLegend);
        Assert.IsNull(model.YMin);
        Assert.IsNull(model.YMax);
    }

    [TestMethod]
    public void PropertiesOutsideTheSchemaAreIgnored()
    {
        var warnings = new List<string>();
        var plain = LineChartModel.Parse("""{ "data": [{ "values": [{ "y": 1 }] }] }""", warnings);
        var extended = LineChartModel.Parse(
            """{ "data": [{ "values": [{ "y": 1 }] }], "fill": "gradient", "curve": "linear", "style": "sparkline", "valueFormat": "percentage", "minHeight": "120px" }""",
            warnings);

        Assert.AreEqual(0, warnings.Count, string.Join(", ", warnings));
        Assert.AreEqual(plain.ShowTitle, extended.ShowTitle);
        Assert.AreEqual(plain.ShowLegend, extended.ShowLegend);
        Assert.AreEqual(plain.Series[0].Points[0], extended.Series[0].Points[0]);
    }

    [TestMethod]
    public void AMissingYIsZero()
    {
        var warnings = new List<string>();
        var model = LineChartModel.Parse("""{ "data": [{ "values": [{}, { "x": "Feb" }, { "y": 4 }] }] }""", warnings);

        Assert.AreEqual(0, warnings.Count, string.Join(", ", warnings));
        var points = model.Series[0].Points;
        Assert.AreEqual(3, points.Count);
        Assert.AreEqual(0d, points[0].Y);
        Assert.AreEqual(0d, points[1].Y);
        Assert.AreEqual(4d, points[2].Y);
    }

    [TestMethod]
    public void ValuesThatArentObjectsWithNumbersWarn()
    {
        var warnings = new List<string>();
        var model = LineChartModel.Parse("""{ "data": [{ "values": [{ "y": null }, 7, { "y": 3 }] }] }""", warnings);

        Assert.AreEqual(2, warnings.Count, string.Join(", ", warnings));
        var points = model.Series[0].Points;
        Assert.AreEqual(2, points.Count);
        Assert.AreEqual(0d, points[0].Y);
        Assert.AreEqual(3d, points[1].Y);
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
    public void TheLegendShowsByDefaultWhenItHasSomethingToShow()
    {
        var named = LineChartModel.Parse("""{ "data": [{ "legend": "Send", "values": [{ "y": 1 }] }] }""", new List<string>());
        var unnamed = LineChartModel.Parse("""{ "data": [{ "values": [{ "y": 1 }] }] }""", new List<string>());
        var several = LineChartModel.Parse("""{ "data": [{ "values": [{ "y": 1 }] }, { "values": [{ "y": 2 }] }] }""", new List<string>());

        Assert.IsTrue(named.ShowsLegend);
        Assert.IsFalse(unnamed.ShowsLegend);
        Assert.IsTrue(several.ShowsLegend);
    }

    [TestMethod]
    [DataRow(0d, false)]
    [DataRow(240d, true)]
    [DataRow(399d, true)]
    [DataRow(400d, false)]
    [DataRow(776d, false)]
    public void NarrowChartsAreCompact(double width, bool isCompact)
    {
        Assert.AreEqual(isCompact, LineChartLayout.IsCompact(width));
    }

    [TestMethod]
    [DataRow("""{ "type": "Chart.Line" }""")]
    [DataRow("""{ "data": "${missing}" }""")]
    [DataRow("""{ "data": [1, 2] }""")]
    [DataRow("""{ "data": [{ "legend": "no values" }] }""")]
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
        var left = LineChartModel.Parse("""{ "yMax": 100, "data": [{ "values": [{ "y": 1 }], "legend": "a" }] }""", new List<string>());
        var right = LineChartModel.Parse("""{ "data": [{ "legend": "a", "values": [{ "y": 1 }] }], "yMax": 100 }""", new List<string>());

        Assert.AreEqual(left.IncrementalState, right.IncrementalState);
    }

    [TestMethod]
    public void IncrementalStateChangesWithData()
    {
        var before = LineChartModel.Parse("""{ "data": [{ "values": [{ "y": 1 }] }] }""", new List<string>());
        var after = LineChartModel.Parse("""{ "data": [{ "values": [{ "y": 2 }] }] }""", new List<string>());

        Assert.AreNotEqual(before.IncrementalState, after.IncrementalState);
    }

    [TestMethod]
    public void ValueExtentSpansAllSeries()
    {
        var model = LineChartModel.Parse(
            """{ "data": [{ "values": [{ "y": 3 }, { "y": 9 }] }, { "values": [{ "y": -2 }, { "y": 4 }] }] }""",
            new List<string>());

        Assert.AreEqual((-2d, 9d), model.GetValueExtent());
    }
}
