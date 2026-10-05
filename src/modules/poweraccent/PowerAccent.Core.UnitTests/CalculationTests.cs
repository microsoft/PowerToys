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

    // Representative inputs to a pure function, in DIP, chosen to look like the WinUI 3 Selector: a
    // cell of at least 48, 51 taken up outside the list and a 648 floor while the description row
    // shows. GetToolbarWidth never sees these as constants - MainWindow reads the real chrome off
    // the live surface - so these values pin the arithmetic, not the Selector's XAML.
    private const double MinItemWidth = 48;
    private const double ChromeWidth = 51;
    private const double DescriptionMinWidth = 648;
    private const double MaxWidth = 1770;   // 1920 DIP display minus the 150 screen padding

    // A bar whose glyphs all measure exactly the 48px cell: the measurement and the item-count floor
    // agree, so this pins the common case rather than which of the two won.
    [TestMethod]
    public void GetToolbarWidth_NarrowGlyphs_HugsTheItemCount()
    {
        var width = Calculation.GetToolbarWidth(
            measuredContentWidth: 18 * MinItemWidth,
            itemCount: 18,
            MinItemWidth,
            ChromeWidth,
            descriptionMinWidth: 0,
            MaxWidth);

        Assert.AreEqual((18 * MinItemWidth) + ChromeWidth, width);
    }

    // The regression this guards (issue #49488): the cell is a MinWidth, not a fixed width, so wide
    // glyphs (₹, ‰, ﷼, CJK fallbacks) grow it. The measured width has to win over count * 48,
    // otherwise the window is narrower than its own content and the list silently scrolls.
    [TestMethod]
    public void GetToolbarWidth_WideGlyphs_UsesMeasuredWidthOverItemCount()
    {
        // 20 cells that measured 60 wide instead of the 48 minimum.
        var width = Calculation.GetToolbarWidth(
            measuredContentWidth: 20 * 60,
            itemCount: 20,
            MinItemWidth,
            ChromeWidth,
            descriptionMinWidth: 0,
            MaxWidth);

        Assert.AreEqual((20 * 60) + ChromeWidth, width);
    }

    // The narrowest crossing point of the same regression: one DIP over the item-count floor still
    // has to come from the measurement. This is the cheapest case that fails if GetToolbarWidth ever
    // goes back to sizing from the item count alone.
    [TestMethod]
    public void GetToolbarWidth_MeasuredOneDipOverEstimate_UsesMeasuredWidth()
    {
        var width = Calculation.GetToolbarWidth(
            measuredContentWidth: (12 * MinItemWidth) + 1,
            itemCount: 12,
            MinItemWidth,
            ChromeWidth,
            descriptionMinWidth: 0,
            MaxWidth);

        Assert.AreEqual((12 * MinItemWidth) + 1 + ChromeWidth, width);
    }

    // The floor is a floor in both directions: a list whose containers are only partly realized
    // measures less than the whole bar needs, and the item-count estimate has to win instead.
    [TestMethod]
    public void GetToolbarWidth_MeasuredBelowEstimate_KeepsTheItemCountFloor()
    {
        var width = Calculation.GetToolbarWidth(
            measuredContentWidth: 500,   // 12 fully realized cells would be 576
            itemCount: 12,
            MinItemWidth,
            ChromeWidth,
            descriptionMinWidth: 0,
            MaxWidth);

        Assert.AreEqual((12 * MinItemWidth) + ChromeWidth, width);
    }

    // A list that has not realized its containers measures 0. The item-count estimate is a valid
    // lower bound, so it must be used rather than collapsing the bar to a single cell.
    [TestMethod]
    public void GetToolbarWidth_UnmeasuredList_FallsBackToItemCount()
    {
        var width = Calculation.GetToolbarWidth(
            measuredContentWidth: 0,
            itemCount: 12,
            MinItemWidth,
            ChromeWidth,
            descriptionMinWidth: 0,
            MaxWidth);

        Assert.AreEqual((12 * MinItemWidth) + ChromeWidth, width);
    }

    // Longer character sets stop growing at the display's maximum and scroll instead.
    [TestMethod]
    public void GetToolbarWidth_ContentWiderThanDisplay_ClampsToMaxWidth()
    {
        var width = Calculation.GetToolbarWidth(
            measuredContentWidth: 90 * MinItemWidth,
            itemCount: 90,
            MinItemWidth,
            ChromeWidth,
            descriptionMinWidth: 0,
            MaxWidth);

        Assert.AreEqual(MaxWidth, width);
    }

    // The Unicode description row needs a readable line, so it widens a short bar - but only up.
    [TestMethod]
    public void GetToolbarWidth_ShortBarWithDescription_WidensToDescriptionMinimum()
    {
        var width = Calculation.GetToolbarWidth(
            measuredContentWidth: 3 * MinItemWidth,
            itemCount: 3,
            MinItemWidth,
            ChromeWidth,
            DescriptionMinWidth,
            MaxWidth);

        Assert.AreEqual(DescriptionMinWidth, width);
    }

    [TestMethod]
    public void GetToolbarWidth_LongBarWithDescription_KeepsTheContentWidth()
    {
        var width = Calculation.GetToolbarWidth(
            measuredContentWidth: 20 * MinItemWidth,
            itemCount: 20,
            MinItemWidth,
            ChromeWidth,
            DescriptionMinWidth,
            MaxWidth);

        Assert.AreEqual((20 * MinItemWidth) + ChromeWidth, width);
    }

    // The display always wins over the description row's minimum. A portrait 1080x1920 panel at 150%
    // gives (1080 / 1.5) - 150 = 570 usable DIP, which is narrower than DescriptionMinWidth.
    [TestMethod]
    public void GetToolbarWidth_DescriptionMinimumWiderThanDisplay_ClampsToDisplay()
    {
        var width = Calculation.GetToolbarWidth(
            measuredContentWidth: 3 * MinItemWidth,
            itemCount: 3,
            MinItemWidth,
            ChromeWidth,
            DescriptionMinWidth,
            maxWidth: 570);

        Assert.AreEqual(570.0, width);
    }

    // A display too narrow to hold even one cell would invert the clamp bounds; the narrowest bar
    // that can still draw a glyph - one cell plus the chrome around it - wins.
    [TestMethod]
    public void GetToolbarWidth_DisplayNarrowerThanOneCell_FallsBackToOneCell()
    {
        var width = Calculation.GetToolbarWidth(
            measuredContentWidth: MinItemWidth,
            itemCount: 1,
            MinItemWidth,
            ChromeWidth,
            descriptionMinWidth: 0,
            maxWidth: 10);

        Assert.AreEqual(MinItemWidth + ChromeWidth, width);
    }

    // The only input that drives the result BELOW the floor, and so the only one that pins the lower
    // clamp bound: every other case reaches it with a width that is already >= one cell plus chrome,
    // where the upper Math.Max alone satisfies the assertion. An empty list is defensive rather than
    // reachable - PowerAccent only summons for a letter with a non-empty mapping - but the branch
    // exists, and without this case Math.Clamp(width, floorWidth, ...) can be weakened to
    // Math.Clamp(width, 0, ...) with every other test still green.
    [TestMethod]
    public void GetToolbarWidth_EmptyList_FallsBackToOneCellPlusChrome()
    {
        var width = Calculation.GetToolbarWidth(
            measuredContentWidth: 0,
            itemCount: 0,
            MinItemWidth,
            ChromeWidth,
            descriptionMinWidth: 0,
            MaxWidth);

        Assert.AreEqual(MinItemWidth + ChromeWidth, width);
    }
}
