// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UnitTests;

/// <summary>The notice's shape, outline and shadow, as premultiplied pixels for a layered window.</summary>
[TestClass]
public sealed class NoticeRendererTests
{
    private const int Width = 100;
    private const int Height = 90;
    private const uint White = 0x00FFFFFF;
    private const uint Outline = 0xE0E0E0;

    // A card from (10, 17) to (90, 70) with 8-pixel corners, and an arrow pointing up at (30, 10).
    private static readonly NoticeRenderer.Shape PointingUp = new(10, 17, 90, 70, Radius: 8, ArrowX: 30, ArrowY: 10, ArrowHalfWidth: 7, ArrowHeight: 7, ArrowUp: true, Border: 1);

    private static readonly NoticeRenderer.Shadow[] Flyout = NoticePainter.Shadows(96, NoticeTheme.LightPalette);

    [TestMethod]
    public void TheCardIsOpaqueAndKeepsTheTextDrawnOnIt()
    {
        uint[] pixels = Card(PointingUp, Flyout, (50, 45, 0x00112233));

        Assert.AreEqual(0xFF112233u, At(pixels, 50, 45), "Text drawn on the card is kept, now opaque.");
        Assert.AreEqual(0xFFFFFFFFu, At(pixels, 60, 40), "The card's color, opaque.");
    }

    [TestMethod]
    public void TheArrowJoinsTheCardWithoutAnOutlineBetweenThem()
    {
        uint[] pixels = Card(PointingUp, Flyout);

        Assert.AreEqual(0xFFFFFFFFu, At(pixels, 30, 14), "Inside the arrow.");
        Assert.AreEqual(0xFFFFFFFFu, At(pixels, 30, 17), "Where the arrow meets the card, the card's top edge has no outline.");
        Assert.AreEqual(0xFF000000u | Outline, At(pixels, 60, 17), "Elsewhere along the top, it does.");
        Assert.AreEqual(0u, At(pixels, 30, 2), "Above the arrow's tip is clear.");
    }

    [TestMethod]
    public void TheOutlineRunsAlongTheEdgeAndTheShadowFallsBelow()
    {
        uint[] pixels = Card(PointingUp, Flyout);

        Assert.AreEqual(0xFF000000u | Outline, At(pixels, 10, 45), "The left edge.");
        Assert.AreEqual(0xFF000000u | Outline, At(pixels, 89, 45), "The right edge.");
        Assert.AreEqual(0xFF000000u | Outline, At(pixels, 50, 69), "The bottom edge.");

        uint below = At(pixels, 50, 76);
        Assert.AreEqual(0u, below & 0x00FFFFFF, "The shadow is black.");
        Assert.IsTrue(Alpha(below) is >= 10 and <= 40, $"6 pixels under the card, the shadow is faint: {Alpha(below)}.");
        Assert.IsTrue(Alpha(At(pixels, 50, 72)) > Alpha(below), "It fades away from the card.");

        Assert.AreEqual(0u, At(pixels, 50, 5), "The shadow falls below the card, not above it.");
        Assert.AreEqual(0u, At(pixels, 0, 0));
    }

    [TestMethod]
    public void AnArrowPointingDownHangsUnderTheCard()
    {
        var pointingDown = new NoticeRenderer.Shape(10, 10, 90, 63, Radius: 8, ArrowX: 30, ArrowY: 70, ArrowHalfWidth: 7, ArrowHeight: 7, ArrowUp: false, Border: 1);
        uint[] pixels = Card(pointingDown, []);

        Assert.AreEqual(0xFFFFFFFFu, At(pixels, 30, 66), "Inside the arrow.");
        Assert.AreEqual(0xFFFFFFFFu, At(pixels, 30, 62), "No outline where it meets the card.");
        Assert.AreEqual(0u, At(pixels, 30, 72), "Below the tip is clear.");
        Assert.AreEqual(0u, At(pixels, 60, 66), "Beside the arrow is clear.");
    }

    [TestMethod]
    public void HighContrastHasAHeavierOutlineAndNoShadow()
    {
        var contrast = PointingUp with { Border = 2 };
        uint[] pixels = Card(contrast, NoticePainter.Shadows(96, NoticeTheme.Choose(true, true, 0x000000, 0xFFFF00)));

        Assert.AreEqual(0xFF000000u | Outline, At(pixels, 10, 45));
        Assert.AreEqual(0xFF000000u | Outline, At(pixels, 11, 45), "Two pixels thick.");
        Assert.AreEqual(0xFFFFFFFFu, At(pixels, 12, 45));
        Assert.AreEqual(0u, At(pixels, 9, 45), "Clear right outside the outline.");
        Assert.AreEqual(0u, At(pixels, 50, 76), "No shadow.");
    }

    [TestMethod]
    public void ColorsArePremultipliedAndCornersAreSmooth()
    {
        uint[] pixels = Card(PointingUp, Flyout);
        foreach (uint pixel in pixels)
        {
            uint alpha = Alpha(pixel);
            Assert.IsTrue(((pixel >> 16) & 0xFF) <= alpha && ((pixel >> 8) & 0xFF) <= alpha && (pixel & 0xFF) <= alpha, $"0x{pixel:X8} isn't premultiplied.");
        }

        // The corner's curve passes through this pixel, so it's partly covered.
        uint corner = At(pixels, 12, 19);
        Assert.IsTrue(Alpha(corner) is > 0 and < 255, $"Anti-aliased: 0x{corner:X8}.");
        Assert.IsTrue(Alpha(At(pixels, 10, 17)) < 255, "The rounded corner cuts off the rectangle's corner.");
    }

    [TestMethod]
    public void TheShadowFollowsTheComplementaryErrorFunction()
    {
        Assert.AreEqual(1, NoticeRenderer.Erfc(0), 1e-6);
        Assert.AreEqual(0.157299, NoticeRenderer.Erfc(1), 1e-5);
        Assert.AreEqual(1.842701, NoticeRenderer.Erfc(-1), 1e-5);
        Assert.AreEqual(0, NoticeRenderer.Erfc(6), 1e-6);
        Assert.AreEqual(2, NoticeRenderer.Erfc(-6), 1e-6);
    }

    [TestMethod]
    public void DistanceIsNegativeInsideAndPositiveOutside()
    {
        Assert.IsTrue(NoticeRenderer.Distance(50, 45, PointingUp) < -20);
        Assert.AreEqual(0, NoticeRenderer.Distance(10, 45, PointingUp), 1e-4, "On the left edge.");
        Assert.AreEqual(5, NoticeRenderer.Distance(50, 75, PointingUp), 1e-4, "5 pixels under the card.");
        Assert.IsTrue(NoticeRenderer.Distance(30, 12, PointingUp) < 0, "Inside the arrow.");
        Assert.IsTrue(NoticeRenderer.Distance(30, 8, PointingUp) > 0, "Past its tip.");
    }

    private static uint[] Card(NoticeRenderer.Shape shape, NoticeRenderer.Shadow[] shadows, params (int X, int Y, uint Color)[] drawn)
    {
        var pixels = new uint[Width * Height];
        Array.Fill(pixels, White);
        foreach ((int x, int y, uint color) in drawn)
        {
            pixels[(y * Width) + x] = color;
        }

        NoticeRenderer.Compose(pixels, Width, Height, shape, Outline, shadows);
        return pixels;
    }

    private static uint At(uint[] pixels, int x, int y) => pixels[(y * Width) + x];

    private static uint Alpha(uint pixel) => pixel >> 24;
}
