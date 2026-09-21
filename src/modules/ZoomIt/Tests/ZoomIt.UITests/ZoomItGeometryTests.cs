// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.ZoomIt.UITests;

[TestClass]
[TestCategory("ZoomIt")]
public sealed class ZoomItGeometryTests
{
    [TestMethod]
    [DataRow(1920, 1080, 1920, 1080)]
    [DataRow(2544, 1369, 2544, 1370)]
    [DataRow(2559, 1439, 2560, 1440)]
    [DataRow(1919, 1080, 1920, 1080)]
    [DataRow(3840, 2161, 3840, 2162)]
    public void RecordingDimensionsRoundOddPixelsUp(int width, int height, int expectedWidth, int expectedHeight)
    {
        Assert.AreEqual(new Size(expectedWidth, expectedHeight), ZoomItGeometry.FullScreenRecordingSize(new Size(width, height)));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public void CenteredPaddedTimerScalesWithItsGlyphs(int scale)
    {
        var display = new Size(2544 * scale, 1369 * scale);
        var ink = new Rectangle(1134 * scale, 622 * scale, 347 * scale, 149 * scale);
        Assert.IsTrue(ZoomItGeometry.IsTimerCentered(ink, display));
    }

    [TestMethod]
    [DataRow(75, 0, true)]
    [DataRow(76, 0, false)]
    [DataRow(-75, 0, true)]
    [DataRow(-76, 0, false)]
    [DataRow(0, 75, true)]
    [DataRow(0, 76, false)]
    [DataRow(0, -75, true)]
    [DataRow(0, -76, false)]
    public void TimerCenterCheckEnforcesFontRelativeBoundary(int offsetX, int offsetY, bool expected)
    {
        var display = new Size(2544, 1370);
        var ink = new Rectangle(1097 + offsetX, 610 + offsetY, 350, 150);
        Assert.AreEqual(expected, ZoomItGeometry.IsTimerCentered(ink, display));
    }

    [TestMethod]
    public void TimerCenterCheckRejectsShiftedAndCornerPositions()
    {
        var display = new Size(2544, 1369);
        var centered = new Rectangle(1134, 622, 347, 149);
        Point[] offsets = [new(-149, 0), new(149, 0), new(0, -149), new(0, 149)];
        foreach (var offset in offsets)
        {
            var shifted = centered;
            shifted.Offset(offset);
            Assert.IsFalse(ZoomItGeometry.IsTimerCentered(shifted, display), $"Unexpected centered result after shifting by {offset}.");
        }

        Assert.IsFalse(ZoomItGeometry.IsTimerCentered(new Rectangle(50, 50, 347, 149), display));
        Assert.IsFalse(ZoomItGeometry.IsTimerCentered(Rectangle.Empty, display));
    }
}
