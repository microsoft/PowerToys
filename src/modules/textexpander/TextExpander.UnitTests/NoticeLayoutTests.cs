// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

/// <summary>Where the notice goes and how big it is, in physical pixels, without a window.</summary>
[TestClass]
public sealed class NoticeLayoutTests
{
    private static readonly Native.RECT Screen = Rect(0, 0, 1920, 1040);
    private static readonly NoticeLayout.Metrics At100 = NoticeLayout.Metrics.For(96, highContrast: false);

    [TestMethod]
    public void SitsJustUnderTheCaretWithItsArrowPointingUpAtIt()
    {
        var caret = Rect(500, 300, 501, 318);
        NoticeLayout.Placement placement = NoticeLayout.Place(caret, 230, 60, Screen, At100);

        Assert.IsTrue(placement.Below);
        Assert.AreEqual(500, TipX(placement), "The arrow points at the caret.");
        Assert.AreEqual(318 + 2, TipY(placement), "The arrow's tip is 2 pixels under the caret.");
        Assert.AreEqual(TipY(placement) + 7, CardOnScreen(placement).Top, "The card hangs 7 pixels under the tip, the arrow's height.");
        Assert.AreEqual(500 - 18, CardOnScreen(placement).Left, "The arrow sits 18 pixels from the card's left end, as in the design.");
        Assert.AreEqual(230, CardOnScreen(placement).Right - CardOnScreen(placement).Left);
        Assert.AreEqual(60, CardOnScreen(placement).Bottom - CardOnScreen(placement).Top);

        // The window leaves room for the flyout's shadow: 16 at the sides, 8 above and 24 below.
        Assert.AreEqual(Rect(466, 312, 728, 411), placement.Window);
    }

    [TestMethod]
    public void FlipsAboveTheCaretWhenThereIsNoRoomBelow()
    {
        var caret = Rect(500, 1000, 501, 1018);
        NoticeLayout.Placement placement = NoticeLayout.Place(caret, 230, 60, Screen, At100);

        Assert.IsFalse(placement.Below, "Under the caret, it would go past the work area.");
        Assert.AreEqual(500, TipX(placement));
        Assert.AreEqual(1000 - 2, TipY(placement), "The arrow points down at the top of the caret.");
        Assert.AreEqual(TipY(placement) - 7, CardOnScreen(placement).Bottom);
        Assert.IsTrue(CardOnScreen(placement).Top >= Screen.Top);
    }

    [TestMethod]
    public void StaysUnderTheCaretWhenItJustFits()
    {
        // 2 + 7 + 60 pixels below a caret ending 69 pixels above the work area's bottom.
        NoticeLayout.Placement placement = NoticeLayout.Place(Rect(500, 953, 501, 971), 230, 60, Screen, At100);
        Assert.IsTrue(placement.Below);
        Assert.AreEqual(Screen.Bottom, CardOnScreen(placement).Bottom);
        Assert.AreEqual(Screen.Bottom, placement.Window.Bottom, "The shadow stops at the taskbar instead of lying over it.");

        placement = NoticeLayout.Place(Rect(500, 954, 501, 972), 230, 60, Screen, At100);
        Assert.IsFalse(placement.Below, "A pixel lower, it no longer fits.");
    }

    [TestMethod]
    public void IsClampedToTheWorkAreaAndKeepsPointingAtTheCaret()
    {
        NoticeLayout.Placement right = NoticeLayout.Place(Rect(1900, 300, 1901, 318), 230, 60, Screen, At100);
        Assert.AreEqual(Screen.Right, CardOnScreen(right).Right, "Moved left to stay on the screen.");
        Assert.AreEqual(1900, TipX(right), "The arrow moves along the card to keep pointing at the caret.");
        Assert.AreEqual(Screen.Right, right.Window.Right, "The shadow stops at the screen's edge instead of reaching the next monitor.");

        NoticeLayout.Placement left = NoticeLayout.Place(Rect(16, 300, 17, 318), 230, 60, Screen, At100);
        Assert.AreEqual(Screen.Left, CardOnScreen(left).Left, "Moved right to stay on the screen.");
        Assert.AreEqual(16, TipX(left));
        Assert.AreEqual(Screen.Left, left.Window.Left);
    }

    [TestMethod]
    public void TheShadowIsCutAtTheWorkAreaButNeverTheCard()
    {
        // Near the top-left corner: only the shadow's margins are cut.
        NoticeLayout.Placement corner = NoticeLayout.Place(Rect(18, 0, 19, 4), 230, 60, Screen, At100);
        Assert.AreEqual(Rect(0, 0, 230 + 16, 6 + 7 + 60 + 24), corner.Window);
        Assert.AreEqual(Rect(0, 13, 230, 73), corner.Card);
        Assert.AreEqual(18, TipX(corner));

        // A work area narrower than the card: the card stays whole and goes past it.
        NoticeLayout.Placement narrow = NoticeLayout.Place(Rect(100, 300, 101, 318), 230, 60, Rect(0, 0, 200, 1040), At100);
        Assert.AreEqual(0, CardOnScreen(narrow).Left);
        Assert.AreEqual(230, CardOnScreen(narrow).Right);
        Assert.AreEqual(Rect(0, 312, 230, 411), narrow.Window);
    }

    [TestMethod]
    public void KeepsTheArrowOffTheRoundedCorners()
    {
        // A caret right at the screen's edge: the arrow can't reach it without leaving the card's straight edge.
        NoticeLayout.Placement left = NoticeLayout.Place(Rect(2, 300, 3, 318), 230, 60, Screen, At100);
        Assert.AreEqual(8 + 7, TipX(left) - CardOnScreen(left).Left, "The corner's radius, then half the arrow.");

        NoticeLayout.Placement right = NoticeLayout.Place(Rect(1918, 300, 1919, 318), 230, 60, Screen, At100);
        Assert.AreEqual(8 + 7, CardOnScreen(right).Right - TipX(right));
    }

    [TestMethod]
    public void WorksOnAMonitorLeftOfThePrimary()
    {
        var left = Rect(-1920, 0, 0, 1040);
        NoticeLayout.Placement placement = NoticeLayout.Place(Rect(-40, 500, -39, 518), 230, 60, left, At100);

        Assert.AreEqual(0, CardOnScreen(placement).Right, "Kept on its own monitor.");
        Assert.AreEqual(-40, TipX(placement), "Still points at the caret.");
        Assert.IsTrue(placement.Below);
    }

    [TestMethod]
    public void StaysInsideATinyWorkAreaOnTheRoomierSide()
    {
        var shallow = Rect(0, 0, 800, 100);
        NoticeLayout.Placement placement = NoticeLayout.Place(Rect(100, 40, 101, 58), 230, 60, shallow, At100);

        Assert.IsTrue(placement.Below, "40 pixels below the caret and 38 above it: below is roomier.");
        Assert.AreEqual(shallow.Bottom, CardOnScreen(placement).Bottom);
        Assert.IsTrue(TipY(placement) - 0 >= shallow.Top);
        Assert.IsTrue(CardOnScreen(placement).Top >= shallow.Top);

        NoticeLayout.Placement over = NoticeLayout.Place(Rect(100, 44, 101, 62), 230, 60, shallow, At100);
        Assert.IsFalse(over.Below, "36 pixels below and 42 above: above is roomier.");
        Assert.AreEqual(shallow.Top, CardOnScreen(over).Top);
    }

    [TestMethod]
    public void ScalesWithTheMonitorsDpi()
    {
        Assert.AreEqual(new NoticeLayout.Metrics(96, 210, 260, 1, 11, 9, 12, 14, 8, 8, 7, 7, 18, 2, 16, 8, 16, 24), At100);

        // 150%: half pixels round away from zero, so a 7-pixel arrow is 11 pixels, not 10.
        Assert.AreEqual(new NoticeLayout.Metrics(144, 315, 390, 2, 17, 14, 18, 21, 12, 12, 11, 11, 27, 3, 24, 12, 24, 36), NoticeLayout.Metrics.For(144, highContrast: false));

        // 200% is twice 100%.
        NoticeLayout.Metrics at200 = NoticeLayout.Metrics.For(192, highContrast: false);
        Assert.AreEqual(At100.MaxWidth * 2, at200.MaxWidth);
        Assert.AreEqual(At100.ShadowBottom * 2, at200.ShadowBottom);
        Assert.AreEqual(At100.ArrowInset * 2, at200.ArrowInset);

        Assert.AreEqual(9, NoticeLayout.Scale(7, 120), "8.75 at 125%.");
        Assert.AreEqual(1, NoticeLayout.Scale(1, 120), "1.25 at 125%.");
    }

    [TestMethod]
    public void PlacesInPhysicalPixelsAtTheMonitorsScale()
    {
        NoticeLayout.Metrics at150 = NoticeLayout.Metrics.For(144, highContrast: false);
        var caret = Rect(1000, 600, 1002, 627);
        NoticeLayout.Placement placement = NoticeLayout.Place(caret, 345, 90, Rect(0, 0, 2880, 1560), at150);

        Assert.AreEqual(1001, TipX(placement), "The middle of a 2-pixel caret.");
        Assert.AreEqual(627 + 3, TipY(placement), "2 pixels under the caret at 100% are 3 at 150%.");
        Assert.AreEqual(TipY(placement) + 11, CardOnScreen(placement).Top);
        Assert.AreEqual(1001 - 27, CardOnScreen(placement).Left);
        Assert.AreEqual(Rect(1001 - 27 - 24, 630 - 12, 1001 - 27 + 345 + 24, 630 + 11 + 90 + 36), placement.Window);
    }

    [TestMethod]
    public void HighContrastHasAHeavierOutlineAndNoShadow()
    {
        NoticeLayout.Metrics contrast = NoticeLayout.Metrics.For(96, highContrast: true);
        Assert.AreEqual(2, contrast.Border);
        Assert.AreEqual(12, contrast.PaddingX, "The padding is inside the outline.");
        Assert.AreEqual(1, contrast.ShadowLeft);
        Assert.AreEqual(1, contrast.ShadowTop);
        Assert.AreEqual(1, contrast.ShadowRight);
        Assert.AreEqual(1, contrast.ShadowBottom);
    }

    [TestMethod]
    public void LargerTextMakesTheTextIconAndCardLargerAlike()
    {
        // Accessibility > Text size at 150%: lines break where they did, in a card half as wide again.
        NoticeLayout.Metrics larger = NoticeLayout.Metrics.For(96, highContrast: false, textScale: 1.5);
        Assert.AreEqual(18, larger.TextSize);
        Assert.AreEqual(21, larger.IconSize);
        Assert.AreEqual(315, larger.MinWidth);
        Assert.AreEqual(390, larger.MaxWidth);
        Assert.AreEqual(At100.ArrowHeight, larger.ArrowHeight, "The arrow and corners don't change.");
        Assert.AreEqual(At100.CornerRadius, larger.CornerRadius);

        Assert.AreEqual(36, NoticeLayout.Metrics.For(192, highContrast: false, textScale: 1.5).TextSize, "On a 200% monitor as well.");
        Assert.AreEqual(At100, NoticeLayout.Metrics.For(96, highContrast: false, textScale: 0.5), "Never smaller than usual.");
        Assert.AreEqual(27, NoticeLayout.Metrics.For(96, highContrast: false, textScale: 9).TextSize, "Windows' largest is 225%.");
        Assert.AreEqual(At100, NoticeLayout.Metrics.For(96, highContrast: false, textScale: double.NaN));
    }

    [TestMethod]
    public void IsAsWideAsItsLongestLineWithinTheDesignsRange()
    {
        Assert.AreEqual(216, NoticeLayout.MaxTextWidth(At100), "260, less the padding, the icon and the gap after it.");
        Assert.AreEqual(210, NoticeLayout.CardWidth(10, At100), "Never narrower than 210.");
        Assert.AreEqual(244, NoticeLayout.CardWidth(200, At100));
        Assert.AreEqual(260, NoticeLayout.CardWidth(500, At100), "Never wider than 260.");

        Assert.AreEqual(9 + 32 + 9, NoticeLayout.CardHeight(32, 19, At100), "Two lines of text.");
        Assert.AreEqual(9 + 19 + 9, NoticeLayout.CardHeight(0, 19, At100), "At least as tall as the icon's line.");
    }

    private static int TipX(NoticeLayout.Placement placement) => placement.Window.Left + placement.ArrowX;

    private static int TipY(NoticeLayout.Placement placement) => placement.Window.Top + placement.ArrowY;

    private static Native.RECT CardOnScreen(NoticeLayout.Placement placement) => Rect(
        placement.Window.Left + placement.Card.Left,
        placement.Window.Top + placement.Card.Top,
        placement.Window.Left + placement.Card.Right,
        placement.Window.Top + placement.Card.Bottom);

    private static Native.RECT Rect(int left, int top, int right, int bottom) => new() { Left = left, Top = top, Right = right, Bottom = bottom };
}
