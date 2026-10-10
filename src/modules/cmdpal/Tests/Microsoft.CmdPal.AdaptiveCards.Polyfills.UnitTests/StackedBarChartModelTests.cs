// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.AdaptiveCards.Polyfills.UnitTests;

[TestClass]
public class StackedBarChartModelTests
{
    [TestMethod]
    public void ParsesBarsAndSharesLegendColors()
    {
        var warnings = new List<string>();
        var model = StackedBarChartModel.Parse(
            """
            {
              "type": "Chart.HorizontalBar.Stacked",
              "title": "Memory",
              "showLegend": false,
              "data": [
                { "title": "Now", "data": [{ "legend": "In use", "value": 12, "color": "accent" }, { "legend": "Free", "value": 4 }] },
                { "title": "Peak", "data": [{ "legend": "Free", "value": 1 }, { "legend": "Standby", "value": 3 }, { "legend": "In use", "value": 15 }] }
              ]
            }
            """,
            warnings);

        Assert.AreEqual(0, warnings.Count, string.Join(", ", warnings));
        Assert.AreEqual("Memory", model.Title);
        Assert.IsFalse(model.ShowLegend);
        Assert.AreEqual(2, model.Groups.Count);
        Assert.AreEqual("Peak", model.Groups[1].Title);
        Assert.AreEqual(16d, model.Groups[0].Total);
        Assert.AreEqual(19d, model.MaxTotal);

        var legend = model.GetLegend();
        Assert.AreEqual(3, legend.Count);
        Assert.AreEqual(("In use", "accent", 0), legend[0]);
        Assert.AreEqual(("Free", (string?)null, 1), legend[1]);
        Assert.AreEqual(("Standby", (string?)null, 2), legend[2]);
    }

    [TestMethod]
    public void BarsThatAreNotObjectsWarn()
    {
        var warnings = new List<string>();
        var model = StackedBarChartModel.Parse("""{ "type": "Chart.HorizontalBar.Stacked", "data": [1, { "data": [] }] }""", warnings);

        Assert.AreEqual(1, warnings.Count);
        Assert.AreEqual(1, model.Groups.Count);
    }

    [TestMethod]
    public void MissingDataWarns()
    {
        var warnings = new List<string>();
        StackedBarChartModel.Parse("""{ "type": "Chart.HorizontalBar.Stacked" }""", warnings);

        Assert.AreEqual(1, warnings.Count);
    }

    [TestMethod]
    public void TheTitleShowsOnlyWhenAsked()
    {
        var hidden = StackedBarChartModel.Parse("""{ "type": "Chart.HorizontalBar.Stacked", "title": "Memory", "data": [] }""", new List<string>());
        var shown = StackedBarChartModel.Parse("""{ "type": "Chart.HorizontalBar.Stacked", "title": "Memory", "showTitle": true, "data": [] }""", new List<string>());

        Assert.IsFalse(hidden.ShowTitle);
        Assert.IsTrue(shown.ShowTitle);
    }
}
