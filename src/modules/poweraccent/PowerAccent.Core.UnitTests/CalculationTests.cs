// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerAccent.Core;
using PowerAccent.Core.Services;
using PowerAccent.Core.Tools;

namespace PowerAccent.Core.UnitTests;

/// <summary>
/// Exercises the pure anchor / DPI geometry in <see cref="Calculation"/>. These are the math that
/// the WinUI 3 Selector feeds into AppWindow.Move/Resize, so a regression here silently mis-places
/// the accent popup (the classic high-DPI / multi-monitor "double scaling" failure mode).
/// </summary>
[TestClass]
public sealed class CalculationTests
{
    // offset baked into Calculation: the gap from the screen edge for the edge anchors.
    private const int Offset = 24;

    // A 1920x1080 primary monitor rooted at the virtual-desktop origin.
    private static readonly Rect PrimaryScreen = new(0, 0, 1920, 1080);

    // A one-row accent bar, in DIP.
    private static readonly Size Window = new(200, 52);

    // At 100% scaling (dpi = 1.0) the physical window size equals the DIP size, so each of the nine
    // anchors lands at an easily hand-checkable coordinate.
    [DataTestMethod]
    [DataRow(Position.TopLeft, 24.0, 24.0)]
    [DataRow(Position.Top, 860.0, 24.0)]
    [DataRow(Position.TopRight, 1696.0, 24.0)]
    [DataRow(Position.Left, 24.0, 514.0)]
    [DataRow(Position.Center, 860.0, 514.0)]
    [DataRow(Position.Right, 1696.0, 514.0)]
    [DataRow(Position.BottomLeft, 24.0, 1004.0)]
    [DataRow(Position.Bottom, 860.0, 1004.0)]
    [DataRow(Position.BottomRight, 1696.0, 1004.0)]
    public void GetRawCoordinatesFromPosition_AtDpi1_PlacesEachAnchor(Position position, double expectedX, double expectedY)
    {
        var point = Calculation.GetRawCoordinatesFromPosition(position, PrimaryScreen, Window, dpi: 1.0);

        Assert.AreEqual(expectedX, point.X, "X for " + position);
        Assert.AreEqual(expectedY, point.Y, "Y for " + position);
    }

    // At 150% scaling the physical window is 300x78. The centered anchors must subtract HALF of the
    // scaled size (not the DIP size) and the right/bottom anchors must subtract the FULL scaled size
    // plus the offset - this is exactly where a missing/extra dpi factor shows up.
    [DataTestMethod]
    [DataRow(Position.TopLeft, 24.0, 24.0)]
    [DataRow(Position.Center, 810.0, 501.0)]
    [DataRow(Position.BottomRight, 1596.0, 978.0)]
    public void GetRawCoordinatesFromPosition_AtDpi150Percent_ScalesWindowFootprint(Position position, double expectedX, double expectedY)
    {
        var point = Calculation.GetRawCoordinatesFromPosition(position, PrimaryScreen, Window, dpi: 1.5);

        Assert.AreEqual(expectedX, point.X, "X for " + position);
        Assert.AreEqual(expectedY, point.Y, "Y for " + position);
    }

    // A secondary 2560x1440 monitor to the right of the primary at 200% scaling. Verifies the screen
    // origin (screen.X / screen.Y) is honored for every anchor, not just the primary-at-origin case.
    [DataTestMethod]
    [DataRow(Position.TopLeft, 1944.0, 24.0)]
    [DataRow(Position.Center, 3000.0, 668.0)]
    [DataRow(Position.BottomRight, 4056.0, 1312.0)]
    public void GetRawCoordinatesFromPosition_OnOffsetMonitor_HonorsScreenOrigin(Position position, double expectedX, double expectedY)
    {
        var secondaryScreen = new Rect(1920, 0, 2560, 1440);

        var point = Calculation.GetRawCoordinatesFromPosition(position, secondaryScreen, Window, dpi: 2.0);

        Assert.AreEqual(expectedX, point.X, "X for " + position);
        Assert.AreEqual(expectedY, point.Y, "Y for " + position);
    }

    // A monitor positioned to the LEFT of the primary has a negative virtual-desktop X origin. The
    // edge anchors must still be offset relative to that negative origin.
    [TestMethod]
    public void GetRawCoordinatesFromPosition_OnNegativeOriginMonitor_OffsetsFromScreenEdge()
    {
        var leftScreen = new Rect(-1920, 0, 1920, 1080);

        var topLeft = Calculation.GetRawCoordinatesFromPosition(Position.TopLeft, leftScreen, Window, dpi: 1.0);
        Assert.AreEqual(-1920 + Offset, topLeft.X);
        Assert.AreEqual(Offset, topLeft.Y);

        var bottomRight = Calculation.GetRawCoordinatesFromPosition(Position.BottomRight, leftScreen, Window, dpi: 1.0);
        Assert.AreEqual(-1920 + 1920 - (Window.Width + Offset), bottomRight.X);
        Assert.AreEqual(1080 - (Window.Height + Offset), bottomRight.Y);
    }

    [TestMethod]
    public void GetRawCoordinatesFromPosition_UnknownPosition_Throws()
    {
        Assert.ThrowsException<NotImplementedException>(
            () => Calculation.GetRawCoordinatesFromPosition((Position)999, PrimaryScreen, Window, dpi: 1.0));
    }

    // Caret-relative placement centers the window horizontally on the caret and sits it 20px above.
    [TestMethod]
    public void GetRawCoordinatesFromCaret_WithRoom_CentersAboveCaret()
    {
        var caret = new Point(960, 540);

        var point = Calculation.GetRawCoordinatesFromCaret(caret, PrimaryScreen, Window);

        Assert.AreEqual(960 - (Window.Width / 2), point.X);   // 860
        Assert.AreEqual(540 - Window.Height - 20, point.Y);   // 468
    }

    // Near the left edge the window would overflow off-screen, so X clamps to the screen's left edge.
    [TestMethod]
    public void GetRawCoordinatesFromCaret_NearLeftEdge_ClampsToScreenLeft()
    {
        var caret = new Point(50, 540);

        var point = Calculation.GetRawCoordinatesFromCaret(caret, PrimaryScreen, Window);

        Assert.AreEqual(PrimaryScreen.X, point.X);
    }

    // Near the right edge X clamps so the window's right side sits on the screen's right edge.
    [TestMethod]
    public void GetRawCoordinatesFromCaret_NearRightEdge_ClampsToScreenRight()
    {
        var caret = new Point(1900, 540);

        var point = Calculation.GetRawCoordinatesFromCaret(caret, PrimaryScreen, Window);

        Assert.AreEqual(PrimaryScreen.X + PrimaryScreen.Width - Window.Width, point.X);  // 1720
    }

    // When there is no room above the caret (top would land off-screen) the window flips to 20px
    // BELOW the caret instead of being clipped at the top.
    [TestMethod]
    public void GetRawCoordinatesFromCaret_NoRoomAbove_FlipsBelowCaret()
    {
        var caret = new Point(960, 10);

        var point = Calculation.GetRawCoordinatesFromCaret(caret, PrimaryScreen, Window);

        Assert.AreEqual(caret.Y + 20, point.Y);   // 30
    }
}
