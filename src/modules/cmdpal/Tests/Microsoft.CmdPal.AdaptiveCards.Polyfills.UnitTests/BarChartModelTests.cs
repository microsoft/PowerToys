// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.AdaptiveCards.Polyfills.UnitTests;

[OSCondition(OperatingSystems.Windows)]
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

        Assert.IsEmpty(warnings, string.Join(", ", warnings));
        Assert.AreEqual(BarOrientation.Vertical, model.Orientation);
        Assert.AreEqual("Cores", model.Title);
        Assert.IsFalse(model.ShowTitle);
        Assert.AreEqual("Core", model.XAxisTitle);
        Assert.AreEqual("% Utilization", model.YAxisTitle);
        Assert.AreEqual("categoricalTeal", model.Color);
        Assert.IsTrue(model.ShowBarValues);
        Assert.AreEqual(0d, model.YMin);
        Assert.AreEqual(100d, model.YMax);
        Assert.HasCount(3, model.Data);
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

        Assert.IsEmpty(warnings, string.Join(", ", warnings));
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

        Assert.IsEmpty(warnings, string.Join(", ", warnings));
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

        Assert.HasCount(1, warnings);
        Assert.AreEqual(BarDisplayMode.AbsoluteWithAxis, model.DisplayMode);
        Assert.AreEqual(BarOrientation.Horizontal, model.Orientation);
    }

    [TestMethod]
    [DataRow("AbsoluteWithAxis")]
    [DataRow("AbsoluteNoAxis")]
    [DataRow("PartToWhole")]
    public void NegativeHorizontalValuesWarnAndRemainInTheModel(string displayMode)
    {
        var warnings = new List<string>();
        var model = BarChartModel.Parse(
            $$"""{ "displayMode": "{{displayMode}}", "data": [{ "x": "Loss", "y": -3 }, { "x": "Gain", "y": 5 }] }""",
            BarOrientation.Horizontal,
            warnings);

        Assert.HasCount(1, warnings);
        Assert.AreEqual("Chart.HorizontalBar does not support negative values; negative values are drawn as zero.", warnings[0]);
        Assert.AreEqual(-3d, model.Data[0].Value);
        Assert.AreEqual((-3d, 5d), model.GetValueExtent());
        Assert.AreEqual(5d, model.GetPositiveTotal());
    }

    [TestMethod]
    public void NegativeVerticalValuesDoNotWarn()
    {
        var warnings = new List<string>();
        var model = BarChartModel.Parse(
            """{ "data": [{ "x": "Loss", "y": -3 }, { "x": "Gain", "y": 5 }] }""",
            BarOrientation.Vertical,
            warnings);

        Assert.IsEmpty(warnings);
        Assert.AreEqual((-3d, 5d), model.GetValueExtent());
    }

    [TestMethod]
    [DataRow(-3d, -5d, -5d, 0d)]
    [DataRow(-3d, 5d, -4d, 6d)]
    [DataRow(3d, 5d, 0d, 5d)]
    public void AutomaticVerticalRangeIncludesZero(double first, double second, double expectedMin, double expectedMax)
    {
        var model = new BarChartModel
        {
            Orientation = BarOrientation.Vertical,
            Data = [new("A", first, null), new("B", second, null)],
        };

        var range = model.GetAxisRange();

        Assert.AreEqual(expectedMin, range.Min);
        Assert.AreEqual(expectedMax, range.Max);
    }

    [TestMethod]
    public void FixedVerticalRangeIsPreserved()
    {
        var model = new BarChartModel
        {
            Orientation = BarOrientation.Vertical,
            YMin = -10,
            YMax = -1,
            Data = [new("A", -3, null)],
        };

        var range = model.GetAxisRange();

        Assert.AreEqual(-10d, range.Min);
        Assert.AreEqual(-1d, range.Max);
    }

    [TestMethod]
    [DataRow(-3d)]
    [DataRow(5d)]
    public void HorizontalRangeStartsAtZero(double second)
    {
        var model = new BarChartModel
        {
            Orientation = BarOrientation.Horizontal,
            Data = [new("Loss", -5, null), new("Other", second, null)],
        };

        var range = model.GetAxisRange();

        Assert.AreEqual(0d, range.Min);
        Assert.AreEqual(0d, range.Normalize(0));
        Assert.IsGreaterThan(range.Min, range.Max);
        Assert.IsGreaterThanOrEqualTo(second, range.Max);
    }
}
