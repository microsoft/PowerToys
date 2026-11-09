// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Graphics;

namespace PowerDisplay.UnitTests;

[TestClass]
public class FlyoutPlacementTests
{
    private const int Gap = 12;

    [TestMethod]
    [DataRow(FlyoutPlacementEdge.Bottom, 0, 760, 1000, 40)]
    [DataRow(FlyoutPlacementEdge.Top, 0, 0, 1000, 40)]
    [DataRow(FlyoutPlacementEdge.Left, 0, 0, 40, 800)]
    [DataRow(FlyoutPlacementEdge.Right, 960, 0, 40, 800)]
    public void GetTaskbarEdge_SystemTaskbarRectangle_RecognizesEachEdge(FlyoutPlacementEdge expected, int x, int y, int width, int height)
    {
        var outer = new RectInt32(0, 0, 1000, 800);

        var result = FlyoutPlacementCalculator.GetTaskbarEdge(outer, outer, new RectInt32(x, y, width, height));

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void GetTaskbarEdge_SystemTaskbarOnANegativeOriginDisplay_UsesThatDisplaysBounds()
    {
        var outer = new RectInt32(-1920, -1080, 1920, 1080);
        var taskbar = new RectInt32(-1920, -1080, 40, 1080);

        var result = FlyoutPlacementCalculator.GetTaskbarEdge(outer, outer, taskbar);

        Assert.AreEqual(FlyoutPlacementEdge.Left, result);
    }

    [TestMethod]
    public void GetTaskbarEdge_SystemTaskbarTakesPriorityOverAnotherAppbarsReservedEdge()
    {
        var outer = new RectInt32(0, 0, 1000, 800);
        var work = new RectInt32(80, 40, 920, 760);
        var taskbar = new RectInt32(0, 0, 1000, 40);

        var result = FlyoutPlacementCalculator.GetTaskbarEdge(outer, work, taskbar);

        Assert.AreEqual(FlyoutPlacementEdge.Top, result);
    }

    [TestMethod]
    [DataRow(FlyoutPlacementEdge.Bottom, 0, 0, 1000, 760)]
    [DataRow(FlyoutPlacementEdge.Top, 0, 40, 1000, 760)]
    [DataRow(FlyoutPlacementEdge.Left, 40, 0, 960, 800)]
    [DataRow(FlyoutPlacementEdge.Right, 0, 0, 960, 800)]
    public void GetTaskbarEdge_WithoutSystemTaskbar_UsesTheOnlyReservedEdge(FlyoutPlacementEdge expected, int x, int y, int width, int height)
    {
        var outer = new RectInt32(0, 0, 1000, 800);

        var result = FlyoutPlacementCalculator.GetTaskbarEdge(outer, new RectInt32(x, y, width, height), null);

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    [DataRow(0, 0, 1000, 800)]
    [DataRow(40, 40, 960, 760)]
    [DataRow(40, 40, 920, 720)]
    public void GetTaskbarEdge_WithoutAUniqueReservedEdge_UsesBottom(int x, int y, int width, int height)
    {
        var outer = new RectInt32(0, 0, 1000, 800);

        var result = FlyoutPlacementCalculator.GetTaskbarEdge(outer, new RectInt32(x, y, width, height), null);

        Assert.AreEqual(FlyoutPlacementEdge.Bottom, result);
    }

    [TestMethod]
    [DataRow(0, 40)]
    [DataRow(40, 0)]
    [DataRow(-1, 40)]
    [DataRow(40, -1)]
    public void GetTaskbarEdge_InvalidSystemTaskbarRectangle_UsesWorkArea(int width, int height)
    {
        var outer = new RectInt32(0, 0, 1000, 800);
        var work = new RectInt32(40, 0, 960, 800);

        var result = FlyoutPlacementCalculator.GetTaskbarEdge(outer, work, new RectInt32(0, 760, width, height));

        Assert.AreEqual(FlyoutPlacementEdge.Left, result);
    }

    [TestMethod]
    [DataRow(FlyoutPlacementEdge.Bottom)]
    [DataRow(FlyoutPlacementEdge.Top)]
    [DataRow(FlyoutPlacementEdge.Left)]
    [DataRow(FlyoutPlacementEdge.Right)]
    public void Calculate_PlacesFlyoutOnTheDesktopSideOfEachTaskbar(FlyoutPlacementEdge edge)
    {
        var context = StandardContext(edge);

        var result = FlyoutPlacementCalculator.Calculate(context, 200, 180, Gap);

        Assert.AreEqual(200, result.Width);
        Assert.AreEqual(180, result.Height);
        AssertBesideAnchor(context, result, Gap);
        AssertFitsWorkArea(context.WorkArea, result);
    }

    [TestMethod]
    [DataRow(FlyoutPlacementEdge.Bottom)]
    [DataRow(FlyoutPlacementEdge.Top)]
    [DataRow(FlyoutPlacementEdge.Left)]
    [DataRow(FlyoutPlacementEdge.Right)]
    public void Calculate_IconInsetInsideTaskbar_LeavesAGapFromTheWorkAreaEdge(FlyoutPlacementEdge edge)
    {
        var context = StandardContext(edge) with
        {
            Anchor = edge switch
            {
                FlyoutPlacementEdge.Top => new RectInt32(492, 12, 16, 16),
                FlyoutPlacementEdge.Left => new RectInt32(12, 352, 16, 16),
                FlyoutPlacementEdge.Right => new RectInt32(972, 352, 16, 16),
                _ => new RectInt32(712, 772, 16, 16),
            },
        };

        var result = FlyoutPlacementCalculator.Calculate(context, 200, 180, Gap);

        switch (edge)
        {
            case FlyoutPlacementEdge.Top:
                Assert.AreEqual((long)context.WorkArea.Y + Gap, (long)result.Y);
                break;
            case FlyoutPlacementEdge.Left:
                Assert.AreEqual((long)context.WorkArea.X + Gap, (long)result.X);
                break;
            case FlyoutPlacementEdge.Right:
                Assert.AreEqual(Right(context.WorkArea) - Gap, Right(result));
                break;
            default:
                Assert.AreEqual(Bottom(context.WorkArea) - Gap, Bottom(result));
                break;
        }

        AssertFitsWorkArea(context.WorkArea, result);
    }

    [TestMethod]
    public void Calculate_LeftTaskbarWithBottomCornerIcon_StillPlacesFlyoutToTheRight()
    {
        var context = Context(
            FlyoutPlacementEdge.Left,
            new RectInt32(0, 760, 40, 40),
            new RectInt32(40, 0, 960, 800));

        var result = FlyoutPlacementCalculator.Calculate(context, 200, 180, Gap);

        AssertBesideAnchor(context, result, Gap);
        AssertFitsWorkArea(context.WorkArea, result);
    }

    [TestMethod]
    public void Calculate_TopTaskbarWithRightCornerIcon_StillPlacesFlyoutBelowIt()
    {
        var context = Context(
            FlyoutPlacementEdge.Top,
            new RectInt32(960, 0, 40, 40),
            new RectInt32(0, 40, 1000, 760));

        var result = FlyoutPlacementCalculator.Calculate(context, 200, 180, Gap);

        AssertBesideAnchor(context, result, Gap);
        AssertFitsWorkArea(context.WorkArea, result);
    }

    [TestMethod]
    public void Calculate_OverflowIconInsideWorkArea_RemainsAnchoredToTheIcon()
    {
        var context = Context(
            FlyoutPlacementEdge.Bottom,
            new RectInt32(600, 560, 40, 40),
            new RectInt32(0, 0, 1000, 760));

        var result = FlyoutPlacementCalculator.Calculate(context, 200, 180, Gap);

        AssertBesideAnchor(context, result, Gap);
        AssertFitsWorkArea(context.WorkArea, result);
    }

    [TestMethod]
    public void Calculate_AutoHiddenBottomTaskbar_AcceptsAPointOnTheDisplayEdge()
    {
        var context = Context(
            FlyoutPlacementEdge.Bottom,
            new RectInt32(980, 800, 0, 0),
            new RectInt32(0, 0, 1000, 800));

        var result = FlyoutPlacementCalculator.Calculate(context, 200, 180, Gap);

        Assert.AreEqual(200, result.Width);
        Assert.AreEqual(180, result.Height);
        Assert.AreEqual((long)context.Anchor.Y - Gap, Bottom(result));
        AssertFitsWorkArea(context.WorkArea, result);
    }

    [TestMethod]
    public void Calculate_SecondaryDisplayWithNegativeOrigin_UsesAbsoluteScreenCoordinates()
    {
        var context = Context(
            FlyoutPlacementEdge.Bottom,
            new RectInt32(-240, -40, 40, 40),
            new RectInt32(-1920, -1080, 1920, 1040),
            new RectInt32(-1920, -1080, 1920, 1080));

        var result = FlyoutPlacementCalculator.Calculate(context, 200, 180, Gap);

        Assert.IsTrue(result.X < 0);
        Assert.IsTrue(result.Y < 0);
        AssertBesideAnchor(context, result, Gap);
        AssertFitsWorkArea(context.WorkArea, result);
    }

    [TestMethod]
    [DataRow(1.0, 348, 240, 12)]
    [DataRow(1.5, 522, 360, 18)]
    [DataRow(2.0, 696, 480, 24)]
    public void Calculate_PhysicalPixelInputsAreNotScaledTwice(double dpiScale, int widthPx, int heightPx, int gapPx)
    {
        var context = new FlyoutPlacementContext(
            2,
            new RectInt32(3600, 2100, 48, 60),
            new RectInt32(0, 0, 3840, 2160),
            new RectInt32(0, 0, 3840, 2100),
            dpiScale,
            FlyoutPlacementEdge.Bottom);

        var result = FlyoutPlacementCalculator.Calculate(context, widthPx, heightPx, gapPx);

        Assert.AreEqual(widthPx, result.Width);
        Assert.AreEqual(heightPx, result.Height);
        AssertBesideAnchor(context, result, gapPx);
        AssertFitsWorkArea(context.WorkArea, result);
    }

    [TestMethod]
    public void Calculate_OversizedFlyout_ClampsBothDimensionsInsideWorkAreaMargins()
    {
        var context = Context(
            FlyoutPlacementEdge.Bottom,
            new RectInt32(150, 150, 40, 40),
            new RectInt32(0, 0, 200, 150),
            new RectInt32(0, 0, 200, 190));

        var result = FlyoutPlacementCalculator.Calculate(context, 1000, 1000, Gap);

        Assert.IsTrue(result.Width > 0 && result.Width < 1000);
        Assert.IsTrue(result.Height > 0 && result.Height < 1000);
        Assert.IsTrue(result.X >= context.WorkArea.X + Gap);
        Assert.IsTrue(result.Y >= context.WorkArea.Y + Gap);
        Assert.IsTrue(Right(result) <= Right(context.WorkArea) - Gap);
        Assert.IsTrue(Bottom(result) <= Bottom(context.WorkArea) - Gap);
        AssertFitsWorkArea(context.WorkArea, result);
    }

    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(2, 3)]
    [DataRow(17, 15)]
    public void Calculate_TinyPositiveWorkArea_KeepsAVisiblePixelWithoutLeavingTheDisplay(int width, int height)
    {
        var context = Context(
            FlyoutPlacementEdge.Bottom,
            new RectInt32(-100, -100 + height, 0, 0),
            new RectInt32(-100, -100, width, height),
            new RectInt32(-100, -100, width, height));

        var result = FlyoutPlacementCalculator.Calculate(context, 200, 180, Gap);

        Assert.IsTrue(result.Width >= 1);
        Assert.IsTrue(result.Height >= 1);
        AssertFitsWorkArea(context.WorkArea, result);
    }

    [TestMethod]
    [DataRow(FlyoutPlacementEdge.Bottom)]
    [DataRow(FlyoutPlacementEdge.Top)]
    [DataRow(FlyoutPlacementEdge.Left)]
    [DataRow(FlyoutPlacementEdge.Right)]
    public void Calculate_ContentHeightChanges_PreserveTheAnchorAndDesktopSide(FlyoutPlacementEdge edge)
    {
        var context = StandardContext(edge);

        var smaller = FlyoutPlacementCalculator.Calculate(context, 200, 120, Gap);
        var larger = FlyoutPlacementCalculator.Calculate(context, 200, 240, Gap);

        Assert.AreEqual(120, smaller.Height);
        Assert.AreEqual(240, larger.Height);
        AssertBesideAnchor(context, smaller, Gap);
        AssertBesideAnchor(context, larger, Gap);
        AssertFitsWorkArea(context.WorkArea, smaller);
        AssertFitsWorkArea(context.WorkArea, larger);
    }

    private static FlyoutPlacementContext StandardContext(FlyoutPlacementEdge edge)
        => edge switch
        {
            FlyoutPlacementEdge.Top => Context(edge, new RectInt32(480, 0, 40, 40), new RectInt32(0, 40, 1000, 760)),
            FlyoutPlacementEdge.Left => Context(edge, new RectInt32(0, 340, 40, 40), new RectInt32(40, 0, 960, 800)),
            FlyoutPlacementEdge.Right => Context(edge, new RectInt32(960, 340, 40, 40), new RectInt32(0, 0, 960, 800)),
            _ => Context(edge, new RectInt32(700, 760, 40, 40), new RectInt32(0, 0, 1000, 760)),
        };

    private static FlyoutPlacementContext Context(FlyoutPlacementEdge edge, RectInt32 anchor, RectInt32 workArea, RectInt32? outerBounds = null)
        => new(2, anchor, outerBounds ?? new RectInt32(0, 0, 1000, 800), workArea, 1.0, edge);

    private static void AssertBesideAnchor(FlyoutPlacementContext context, RectInt32 result, int gap)
    {
        switch (context.TaskbarEdge)
        {
            case FlyoutPlacementEdge.Top:
                Assert.AreEqual(Bottom(context.Anchor) + gap, (long)result.Y);
                Assert.IsTrue(Right(result) > context.Anchor.X && result.X < Right(context.Anchor));
                break;
            case FlyoutPlacementEdge.Left:
                Assert.AreEqual(Right(context.Anchor) + gap, (long)result.X);
                Assert.IsTrue(Bottom(result) > context.Anchor.Y && result.Y < Bottom(context.Anchor));
                break;
            case FlyoutPlacementEdge.Right:
                Assert.AreEqual((long)context.Anchor.X - gap, Right(result));
                Assert.IsTrue(Bottom(result) > context.Anchor.Y && result.Y < Bottom(context.Anchor));
                break;
            default:
                Assert.AreEqual((long)context.Anchor.Y - gap, Bottom(result));
                Assert.IsTrue(Right(result) > context.Anchor.X && result.X < Right(context.Anchor));
                break;
        }
    }

    private static void AssertFitsWorkArea(RectInt32 workArea, RectInt32 result)
    {
        Assert.IsTrue(result.Width >= 0 && result.Height >= 0);
        Assert.IsTrue(result.X >= workArea.X && result.Y >= workArea.Y);
        Assert.IsTrue(Right(result) <= Right(workArea));
        Assert.IsTrue(Bottom(result) <= Bottom(workArea));
    }

    private static long Right(RectInt32 rect) => (long)rect.X + rect.Width;

    private static long Bottom(RectInt32 rect) => (long)rect.Y + rect.Height;
}
