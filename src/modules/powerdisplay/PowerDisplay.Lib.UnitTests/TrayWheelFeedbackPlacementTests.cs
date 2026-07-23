// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Common.Services;
using Windows.Graphics;

namespace PowerDisplay.UnitTests;

[TestClass]
public class TrayWheelFeedbackPlacementTests
{
    private static readonly RectInt32 Outer = new(0, 0, 1000, 800);
    private static readonly RectInt32 Work = new(0, 0, 1000, 760);

    [TestMethod]
    public void Calculate_BottomEdge_PositionsAboveIcon()
    {
        var result = Calculate(new TrayIconBounds(700, 760, 740, 800));

        Assert.AreEqual(new RectInt32(620, 702, 200, 50), result);
    }

    [TestMethod]
    public void Calculate_TopEdge_PositionsBelowIcon()
    {
        var result = Calculate(new TrayIconBounds(480, 0, 520, 40));

        Assert.AreEqual(new RectInt32(400, 48, 200, 50), result);
    }

    [TestMethod]
    public void Calculate_LeftEdge_PositionsRightOfIcon()
    {
        var result = Calculate(new TrayIconBounds(0, 350, 40, 390));

        Assert.AreEqual(new RectInt32(48, 345, 200, 50), result);
    }

    [TestMethod]
    public void Calculate_RightEdge_PositionsLeftOfIcon()
    {
        var result = Calculate(new TrayIconBounds(960, 350, 1000, 390));

        Assert.AreEqual(new RectInt32(752, 345, 200, 50), result);
    }

    [TestMethod]
    public void Calculate_ClampsToWorkArea()
    {
        var result = Calculate(new TrayIconBounds(0, 760, 40, 800));

        Assert.AreEqual(new RectInt32(0, 702, 200, 50), result);
    }

    [TestMethod]
    public void Calculate_OverflowIconStillUsesNearestOuterEdge()
    {
        var result = TrayWheelFeedbackPlacement.Calculate(
            new TrayIconBounds(-1940, 100, -1900, 140),
            new RectInt32(-1920, 0, 1920, 1080),
            new RectInt32(-1920, 0, 1920, 1040),
            200,
            50,
            8);

        Assert.AreEqual(new RectInt32(-1892, 95, 200, 50), result);
    }

    [TestMethod]
    public void Calculate_TiePrefersBottom()
    {
        var squareOuter = new RectInt32(0, 0, 800, 800);
        var squareWork = new RectInt32(0, 0, 800, 760);
        var result = TrayWheelFeedbackPlacement.Calculate(
            new TrayIconBounds(380, 380, 420, 420),
            squareOuter,
            squareWork,
            200,
            50,
            8);

        Assert.AreEqual(new RectInt32(300, 322, 200, 50), result);
    }

    [TestMethod]
    public void Calculate_NegativeMonitorCoordinates_AreHandled()
    {
        var result = TrayWheelFeedbackPlacement.Calculate(
            new TrayIconBounds(-1880, 1000, -1840, 1040),
            new RectInt32(-1920, 0, 1920, 1080),
            new RectInt32(-1920, 0, 1920, 1040),
            200,
            50,
            8);

        Assert.AreEqual(new RectInt32(-1920, 942, 200, 50), result);
    }

    private static RectInt32 Calculate(TrayIconBounds icon)
        => TrayWheelFeedbackPlacement.Calculate(icon, Outer, Work, 200, 50, 8);
}
