// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.AdaptiveCards.Polyfills.UnitTests;

[TestClass]
public class ChartArcTests
{
    [TestMethod]
    public void GaugeAngleRunsFromLeftToRight()
    {
        Assert.AreEqual(180d, ChartArc.GaugeAngle(0));
        Assert.AreEqual(90d, ChartArc.GaugeAngle(0.5));
        Assert.AreEqual(0d, ChartArc.GaugeAngle(1));
        Assert.AreEqual(0d, ChartArc.GaugeAngle(2));
    }

    [TestMethod]
    public void DonutAngleStartsAtTheTopAndRunsClockwise()
    {
        Assert.AreEqual(90d, ChartArc.DonutAngle(0));
        Assert.AreEqual(0d, ChartArc.DonutAngle(0.25));
        Assert.AreEqual(-270d, ChartArc.DonutAngle(1));
    }

    [TestMethod]
    public void PointAtUsesScreenCoordinates()
    {
        var center = new ChartPoint(10, 10);
        var top = ChartArc.PointAt(center, 5, 90);
        var left = ChartArc.PointAt(center, 5, 180);

        Assert.AreEqual(10d, top.X, 1e-9);
        Assert.AreEqual(5d, top.Y, 1e-9);
        Assert.AreEqual(5d, left.X, 1e-9);
        Assert.AreEqual(10d, left.Y, 1e-9);
    }

    [TestMethod]
    public void SpansLeaveGapsBetweenVisibleSlices()
    {
        var spans = ChartArc.GetSpans([0.5, 0, 0.5], 0.02);

        Assert.AreEqual(0.01, spans[0].Start, 1e-9);
        Assert.AreEqual(0.49, spans[0].End, 1e-9);
        Assert.AreEqual(spans[1].Start, spans[1].End);
        Assert.AreEqual(0.51, spans[2].Start, 1e-9);
        Assert.AreEqual(0.99, spans[2].End, 1e-9);
    }

    [TestMethod]
    public void SingleSliceHasNoGap()
    {
        var spans = ChartArc.GetSpans([1], 0.02);

        Assert.AreEqual((0d, 1d), spans[0]);
    }

    [TestMethod]
    public void SlicesSmallerThanTheGapAreHidden()
    {
        var spans = ChartArc.GetSpans([0.99, 0.01], 0.02);

        Assert.AreEqual(spans[1].Start, spans[1].End);
    }

    [TestMethod]
    public void OpenArcsHaveNoGapAtTheirEnds()
    {
        var spans = ChartArc.GetSpans([0.6, 0.25, 0.15], 0.02, closed: false);

        Assert.AreEqual(0d, spans[0].Start, 1e-9);
        Assert.AreEqual(0.59, spans[0].End, 1e-9);
        Assert.AreEqual(0.61, spans[1].Start, 1e-9);
        Assert.AreEqual(1d, spans[2].End, 1e-9);
    }
}
