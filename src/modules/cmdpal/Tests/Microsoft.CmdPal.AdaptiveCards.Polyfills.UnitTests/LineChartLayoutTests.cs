// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests.Charts;

[TestClass]
public class LineChartLayoutTests
{
    private static readonly ChartAxisRange PercentRange = new(0, 100, 25);

    [TestMethod]
    public void SamplesSpanThePlotWidth()
    {
        var layout = new LineChartLayout(108, 100, PercentRange, SlotCount: 11, TopPadding: 0, RightPadding: 8, BottomPadding: 0);

        Assert.AreEqual(10, layout.SlotWidth, 1e-9);
        Assert.AreEqual(0, layout.MapX(0));
        Assert.AreEqual(100, layout.MapX(10));
    }

    [TestMethod]
    public void ValuesMapFromBottomToTop()
    {
        var layout = new LineChartLayout(100, 110, PercentRange, SlotCount: 2, TopPadding: 10, RightPadding: 0, BottomPadding: 0);

        Assert.AreEqual(110, layout.MapY(0));
        Assert.AreEqual(60, layout.MapY(50));
        Assert.AreEqual(10, layout.MapY(100));
        Assert.AreEqual(10, layout.MapY(250), "Values above the range clamp to the top.");
    }

    [TestMethod]
    public void ShorterSeriesAreRightAligned()
    {
        var layout = new LineChartLayout(40, 100, PercentRange, SlotCount: 5, TopPadding: 0, RightPadding: 0, BottomPadding: 0);
        LineChartPoint[] points = [new(null, 10), new(null, 20)];

        var mapped = layout.MapPoints(points);

        Assert.AreEqual(2, mapped.Length);
        Assert.AreEqual(30, mapped[0].X);
        Assert.AreEqual(40, mapped[1].X);
    }

    [TestMethod]
    public void ShiftByOneSampleIsDetected()
    {
        var before = Model(0, 1, 2, 3);
        var after = Model(1, 2, 3, 4);

        Assert.IsTrue(LineChartUpdate.IsScrolledByOne(before, after));
    }

    [TestMethod]
    public void OtherChangesAreNotAScroll()
    {
        Assert.IsFalse(LineChartUpdate.IsScrolledByOne(Model(1, 2, 3, 4), Model(1, 2, 3, 5)));
        Assert.IsFalse(LineChartUpdate.IsScrolledByOne(Model(1, 2, 3), Model(2, 3, 4, 5)));
        Assert.IsFalse(LineChartUpdate.IsScrolledByOne(LineChartModel.Empty, Model(1, 2)));
    }

    private static LineChartModel Model(params int[] values) =>
        LineChartModel.Parse(
            $$"""{ "data": [{ "values": [{{string.Join(", ", values.Select(value => $$"""{ "y": {{value}} }"""))}}] }] }""",
            new List<string>());
}
