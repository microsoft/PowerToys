// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.AdaptiveCards.Polyfills.UnitTests;

[TestClass]
public class DonutChartModelTests
{
    [TestMethod]
    public void ParsesDataAndCenterLabel()
    {
        var warnings = new List<string>();
        var model = DonutChartModel.Parse(
            """
            {
              "type": "Chart.Donut",
              "title": "Memory",
              "value": "57%",
              "colorSet": "categorical",
              "showLegend": false,
              "data": [
                { "legend": "In use", "value": 12, "color": "accent" },
                { "legend": "Free", "value": 4 }
              ]
            }
            """,
            isPie: false,
            warnings);

        Assert.AreEqual(0, warnings.Count, string.Join(", ", warnings));
        Assert.IsFalse(model.IsPie);
        Assert.AreEqual("Memory", model.Title);
        Assert.AreEqual("57%", model.CenterLabel);
        Assert.AreEqual("categorical", model.ColorSet);
        Assert.IsFalse(model.ShowLegend);
        Assert.AreEqual(2, model.Data.Count);
        Assert.AreEqual("accent", model.Data[0].Color);
        Assert.IsNull(model.Data[1].Color);
        Assert.AreEqual(16d, model.Total);
        var shares = model.GetShares();
        Assert.AreEqual(2, shares.Count);
        Assert.AreEqual(0.75, shares[0]);
        Assert.AreEqual(0.25, shares[1]);
    }

    [TestMethod]
    public void PieHasNoCenterLabel()
    {
        var warnings = new List<string>();
        var model = DonutChartModel.Parse("""{ "type": "Chart.Pie", "value": "x", "data": [] }""", isPie: true, warnings);

        Assert.IsTrue(model.IsPie);
        Assert.IsNull(model.CenterLabel);
    }

    [TestMethod]
    public void NegativeValuesAreEmptySlices()
    {
        var model = new DonutChartModel { Data = [new("A", -3, null), new("B", 1, null)] };

        Assert.AreEqual(1d, model.Total);
        var shares = model.GetShares();
        Assert.AreEqual(0d, shares[0]);
        Assert.AreEqual(1d, shares[1]);
    }

    [TestMethod]
    public void AllZeroDataHasZeroShares()
    {
        var model = new DonutChartModel { Data = [new("A", 0, null)] };

        Assert.AreEqual(0d, model.GetShares()[0]);
    }

    [TestMethod]
    public void MissingDataWarns()
    {
        var warnings = new List<string>();
        DonutChartModel.Parse("""{ "type": "Chart.Donut" }""", isPie: false, warnings);

        Assert.AreEqual(1, warnings.Count);
    }

    [TestMethod]
    public void TheTitleShowsOnlyWhenAsked()
    {
        var hidden = DonutChartModel.Parse("""{ "type": "Chart.Donut", "title": "Storage", "data": [] }""", isPie: false, new List<string>());
        var shown = DonutChartModel.Parse("""{ "type": "Chart.Pie", "title": "Storage", "showTitle": true, "data": [] }""", isPie: true, new List<string>());

        Assert.IsFalse(hidden.ShowTitle);
        Assert.IsTrue(shown.ShowTitle);
        Assert.IsTrue(hidden.ShowLegend);
    }
}
