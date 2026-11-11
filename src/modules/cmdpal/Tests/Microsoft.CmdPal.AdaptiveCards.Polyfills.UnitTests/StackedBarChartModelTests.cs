// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.AdaptiveCards.Polyfills.UnitTests;

[OSCondition(OperatingSystems.Windows)]
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

        Assert.IsEmpty(warnings, string.Join(", ", warnings));
        Assert.AreEqual("Memory", model.Title);
        Assert.IsFalse(model.ShowLegend);
        Assert.HasCount(2, model.Groups);
        Assert.AreEqual("Peak", model.Groups[1].Title);
        Assert.AreEqual(16d, model.Groups[0].Total);
        Assert.AreEqual(19d, model.MaxTotal);

        var legend = model.GetLegend();
        Assert.HasCount(3, legend);
        Assert.AreEqual(("In use", "accent", 0), legend[0]);
        Assert.AreEqual(("Free", (string?)null, 1), legend[1]);
        Assert.AreEqual(("Standby", (string?)null, 2), legend[2]);
    }

    [TestMethod]
    public void BarsThatAreNotObjectsWarn()
    {
        var warnings = new List<string>();
        var model = StackedBarChartModel.Parse("""{ "type": "Chart.HorizontalBar.Stacked", "data": [1, { "data": [] }] }""", warnings);

        Assert.HasCount(1, warnings);
        Assert.HasCount(1, model.Groups);
    }

    [TestMethod]
    public void MissingDataWarns()
    {
        var warnings = new List<string>();
        StackedBarChartModel.Parse("""{ "type": "Chart.HorizontalBar.Stacked" }""", warnings);

        Assert.HasCount(1, warnings);
    }

    [TestMethod]
    public void TheTitleShowsOnlyWhenAsked()
    {
        var hidden = StackedBarChartModel.Parse("""{ "type": "Chart.HorizontalBar.Stacked", "title": "Memory", "data": [] }""", new List<string>());
        var shown = StackedBarChartModel.Parse("""{ "type": "Chart.HorizontalBar.Stacked", "title": "Memory", "showTitle": true, "data": [] }""", new List<string>());

        Assert.IsFalse(hidden.ShowTitle);
        Assert.IsTrue(shown.ShowTitle);
    }

    [TestMethod]
    [DataRow(-3d, 5d, 5d)]
    [DataRow(-3d, -5d, 0d)]
    public void NegativeSegmentsWarnAndKeepTheirSignedValues(double first, double second, double expectedTotal)
    {
        var warnings = new List<string>();
        var model = StackedBarChartModel.Parse(
            FormattableString.Invariant(
                $$"""{ "data": [{ "title": "Signed", "data": [{ "legend": "A", "value": {{first}} }, { "legend": "B", "value": {{second}} }] }] }"""),
            warnings);

        Assert.HasCount(1, warnings);
        Assert.AreEqual("Chart.HorizontalBar.Stacked does not support negative values; negative segments are not drawn.", warnings[0]);
        Assert.AreEqual(first, model.Groups[0].Data[0].Value);
        Assert.AreEqual(second, model.Groups[0].Data[1].Value);
        Assert.AreEqual(expectedTotal, model.Groups[0].Total);
        Assert.AreEqual(expectedTotal, model.MaxTotal);
    }

    [TestMethod]
    public void ZeroAndPositiveSegmentsDoNotWarn()
    {
        var warnings = new List<string>();
        var model = StackedBarChartModel.Parse(
            """{ "data": [{ "data": [{ "legend": "A", "value": 0 }, { "legend": "B", "value": 5 }] }, { "data": [] }] }""",
            warnings);

        Assert.IsEmpty(warnings);
        Assert.AreEqual(5d, model.Groups[0].Total);
        Assert.AreEqual(0d, model.Groups[1].Total);
        Assert.AreEqual(5d, model.MaxTotal);
    }
}
