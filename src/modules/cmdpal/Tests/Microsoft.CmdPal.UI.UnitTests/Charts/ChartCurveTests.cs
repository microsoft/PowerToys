// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests.Charts;

[TestClass]
public class ChartCurveTests
{
    [TestMethod]
    public void FewerThanTwoPointsHaveNoSegments()
    {
        Assert.AreEqual(0, ChartCurve.CreateMonotoneSegments([]).Length);
        Assert.AreEqual(0, ChartCurve.CreateMonotoneSegments([new ChartPoint(0, 0)]).Length);
    }

    [TestMethod]
    public void TwoPointsMakeAStraightSegment()
    {
        var segments = ChartCurve.CreateMonotoneSegments([new ChartPoint(0, 0), new ChartPoint(3, 3)]);

        Assert.AreEqual(1, segments.Length);
        Assert.AreEqual(new ChartPoint(1, 1), segments[0].Control1);
        Assert.AreEqual(new ChartPoint(2, 2), segments[0].Control2);
        Assert.AreEqual(new ChartPoint(3, 3), segments[0].End);
    }

    [TestMethod]
    public void CurveNeverOvershootsNeighboringSamples()
    {
        // A spike followed by flat data is where ordinary splines overshoot.
        ChartPoint[] points =
        [
            new(0, 100), new(10, 100), new(20, 0), new(30, 100), new(40, 100), new(50, 90),
        ];

        var segments = ChartCurve.CreateMonotoneSegments(points);

        for (var i = 0; i < segments.Length; i++)
        {
            var low = Math.Min(points[i].Y, points[i + 1].Y);
            var high = Math.Max(points[i].Y, points[i + 1].Y);
            foreach (var control in new[] { segments[i].Control1, segments[i].Control2 })
            {
                Assert.IsTrue(control.Y >= low - 1e-9 && control.Y <= high + 1e-9, $"Segment {i} overshoots: {control.Y}");
            }
        }
    }

    [TestMethod]
    public void FlatRunStaysFlat()
    {
        var segments = ChartCurve.CreateMonotoneSegments([new ChartPoint(0, 5), new ChartPoint(1, 5), new ChartPoint(2, 5)]);

        Assert.IsTrue(segments.All(s => s.Control1.Y == 5 && s.Control2.Y == 5));
    }
}
