// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests.Charts;

[TestClass]
public class BarChartModelTests
{
    [TestMethod]
    public void ParsesBars()
    {
        var warnings = new List<string>();
        var model = BarChartModel.Parse(
            """
            {
              "type": "Chart.VerticalBar",
              "title": "Cores",
              "xAxisTitle": "Core",
              "yAxisTitle": "% Utilization",
              "color": "categoricalTeal",
              "showBarValues": true,
              "yMin": 0,
              "yMax": 100,
              "data": [{ "x": "0", "y": 12 }, { "x": "1", "y": 87.5, "color": "attention" }, { "x": "2", "y": 40 }]
            }
            """,
            BarOrientation.Vertical,
            warnings);

        Assert.AreEqual(0, warnings.Count, string.Join(", ", warnings));
        Assert.AreEqual(BarOrientation.Vertical, model.Orientation);
        Assert.AreEqual("Cores", model.Title);
        Assert.IsFalse(model.ShowTitle);
        Assert.AreEqual("Core", model.XAxisTitle);
        Assert.AreEqual("% Utilization", model.YAxisTitle);
        Assert.AreEqual("categoricalTeal", model.Color);
        Assert.IsTrue(model.ShowBarValues);
        Assert.AreEqual(0d, model.YMin);
        Assert.AreEqual(100d, model.YMax);
        Assert.AreEqual(3, model.Data.Count);
        Assert.AreEqual("1", model.Data[1].Label);
        Assert.AreEqual("attention", model.Data[1].Color);
        Assert.AreEqual((12d, 87.5), model.GetValueExtent());
    }

    [TestMethod]
    public void EmptyDataHasNoExtent()
    {
        var (min, max) = new BarChartModel().GetValueExtent();

        Assert.IsTrue(double.IsNaN(min));
        Assert.IsTrue(double.IsNaN(max));
    }

    [TestMethod]
    [DataRow("AbsoluteWithAxis", "AbsoluteWithAxis")]
    [DataRow("absoluteNoAxis", "AbsoluteNoAxis")]
    [DataRow("PartToWhole", "PartToWhole")]
    public void HorizontalBarsReadTheirDisplayMode(string displayMode, string expected)
    {
        var warnings = new List<string>();
        var model = BarChartModel.Parse(
            $$"""{ "type": "Chart.HorizontalBar", "displayMode": "{{displayMode}}", "showTitle": true, "data": [{ "x": "a", "y": 3 }, { "x": "b", "y": 1 }] }""",
            BarOrientation.Horizontal,
            warnings);

        Assert.AreEqual(0, warnings.Count, string.Join(", ", warnings));
        Assert.AreEqual(expected, model.DisplayMode.ToString());
        Assert.IsTrue(model.ShowTitle);
        Assert.AreEqual(4d, model.GetPositiveTotal());
    }

    [TestMethod]
    public void HorizontalBarsIgnoreVerticalBarProperties()
    {
        var warnings = new List<string>();
        var model = BarChartModel.Parse(
            """{ "type": "Chart.HorizontalBar", "showBarValues": true, "yMin": 0, "yMax": 100, "data": [] }""",
            BarOrientation.Horizontal,
            warnings);

        Assert.AreEqual(0, warnings.Count, string.Join(", ", warnings));
        Assert.IsFalse(model.ShowBarValues);
        Assert.IsNull(model.YMin);
        Assert.IsNull(model.YMax);
        Assert.AreEqual(BarDisplayMode.AbsoluteWithAxis, model.DisplayMode);
    }

    [TestMethod]
    public void UnknownDisplayModeWarns()
    {
        var warnings = new List<string>();
        var model = BarChartModel.Parse("""{ "type": "Chart.HorizontalBar", "displayMode": "stacked", "data": [] }""", BarOrientation.Horizontal, warnings);

        Assert.AreEqual(1, warnings.Count);
        Assert.AreEqual(BarDisplayMode.AbsoluteWithAxis, model.DisplayMode);
        Assert.AreEqual(BarOrientation.Horizontal, model.Orientation);
    }
}
