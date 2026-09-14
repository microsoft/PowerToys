// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScreenTranslator.Core.Layout;
using ScreenTranslator.Core.Translation;

namespace ScreenTranslator.UnitTests;

[TestClass]
public class OverlayLayoutHelperTests
{
    [TestMethod]
    public void PhysicalToDip_TransformsCorrectly_WithScale1()
    {
        PhysicalRect physical = new(100, 200, 300, 400);
        PhysicalRect screenBounds = new(0, 0, 1920, 1080);

        var (leftDip, topDip, widthDip, heightDip) = OverlayLayoutHelper.PhysicalToDip(
            physical, screenBounds, 1.0, 1.0);

        Assert.AreEqual(100.0, leftDip, 0.001);
        Assert.AreEqual(200.0, topDip, 0.001);
        Assert.AreEqual(300.0, widthDip, 0.001);
        Assert.AreEqual(400.0, heightDip, 0.001);
    }

    [TestMethod]
    public void PhysicalToDip_TransformsCorrectly_WithHighDpiScale()
    {
        PhysicalRect physical = new(300, 600, 450, 300);
        PhysicalRect screenBounds = new(0, 0, 3840, 2160);

        var (leftDip, topDip, widthDip, heightDip) = OverlayLayoutHelper.PhysicalToDip(
            physical, screenBounds, 1.5, 1.5);

        Assert.AreEqual(200.0, leftDip, 0.001);
        Assert.AreEqual(400.0, topDip, 0.001);
        Assert.AreEqual(300.0, widthDip, 0.001);
        Assert.AreEqual(200.0, heightDip, 0.001);
    }

    [TestMethod]
    public void MultiMonitor_PhysicalToDip_OffsetsRelativeToScreenOrigin()
    {
        // Second monitor placed to the right of primary monitor
        PhysicalRect screenBounds = new(1920, 0, 1920, 1080);
        PhysicalRect physicalRect = new(2020, 100, 200, 50);

        var (leftDip, topDip, widthDip, heightDip) = OverlayLayoutHelper.PhysicalToDip(
            physicalRect, screenBounds, 1.0, 1.0);

        Assert.AreEqual(100.0, leftDip, 0.001);
        Assert.AreEqual(100.0, topDip, 0.001);
        Assert.AreEqual(200.0, widthDip, 0.001);
        Assert.AreEqual(50.0, heightDip, 0.001);
    }

    [TestMethod]
    public void DipToPhysical_RoundTripsAccurately()
    {
        PhysicalRect screenBounds = new(1920, 0, 3840, 2160);
        double dpiX = 2.0;
        double dpiY = 2.0;

        double leftDip = 50.0;
        double topDip = 100.0;
        double widthDip = 400.0;
        double heightDip = 200.0;

        PhysicalRect physical = OverlayLayoutHelper.DipToPhysical(
            leftDip, topDip, widthDip, heightDip, screenBounds, dpiX, dpiY);

        Assert.AreEqual(2020.0, physical.X, 0.001);
        Assert.AreEqual(200.0, physical.Y, 0.001);
        Assert.AreEqual(800.0, physical.Width, 0.001);
        Assert.AreEqual(400.0, physical.Height, 0.001);

        var (roundtripLeftDip, roundtripTopDip, roundtripWidthDip, roundtripHeightDip) =
            OverlayLayoutHelper.PhysicalToDip(physical, screenBounds, dpiX, dpiY);

        Assert.AreEqual(leftDip, roundtripLeftDip, 0.001);
        Assert.AreEqual(topDip, roundtripTopDip, 0.001);
        Assert.AreEqual(widthDip, roundtripWidthDip, 0.001);
        Assert.AreEqual(heightDip, roundtripHeightDip, 0.001);
    }

    [TestMethod]
    public void CalculateEstimatedFontSize_ScalesWithHeightAndClamps()
    {
        // Standard line height: 20 DIP * 0.85 = 17.0
        double size1 = OverlayLayoutHelper.CalculateEstimatedFontSize(20.0);
        Assert.AreEqual(17.0, size1, 0.001);

        // Tiny line height clamped to minimum
        double sizeMin = OverlayLayoutHelper.CalculateEstimatedFontSize(5.0);
        Assert.AreEqual(9.0, sizeMin, 0.001);

        // Very large line height clamped to maximum
        double sizeMax = OverlayLayoutHelper.CalculateEstimatedFontSize(100.0);
        Assert.AreEqual(48.0, sizeMax, 0.001);

        // Zero / negative height returns default 12.0
        double sizeZero = OverlayLayoutHelper.CalculateEstimatedFontSize(0.0);
        Assert.AreEqual(12.0, sizeZero, 0.001);
    }

    [TestMethod]
    public void GetContrastingTextColorArgb_UsesDarkTextOnLightBackground()
    {
        Assert.AreEqual(0xFF000000u, OverlayAppearanceHelper.GetContrastingTextColorArgb(0xFFFFFFFFu));
        Assert.AreEqual(0xFFFFFFFFu, OverlayAppearanceHelper.GetContrastingTextColorArgb(0xFF202020u));
    }

    [TestMethod]
    public void GetContrastingTextColorArgb_UsesStableArgbValues()
    {
        Assert.AreEqual(0xFF000000u, OverlayAppearanceHelper.GetContrastingTextColorArgb(0xFFF0F0F0u));
        Assert.AreEqual(0xFFFFFFFFu, OverlayAppearanceHelper.GetContrastingTextColorArgb(0xFF101010u));
    }

    [TestMethod]
    public void CombineWordRects_EmptyOrNull_ReturnsEmpty()
    {
        Assert.IsTrue(OverlayLayoutHelper.CombineWordRects(null!).IsEmpty);
        Assert.IsTrue(OverlayLayoutHelper.CombineWordRects(new List<PhysicalRect>()).IsEmpty);
    }

    [TestMethod]
    public void CombineWordRects_MultipleWords_ReturnsEnclosingUnion()
    {
        var words = new List<PhysicalRect>
        {
            new(10, 20, 30, 15),
            new(45, 20, 40, 15),
            new(90, 20, 50, 15),
        };

        var combined = OverlayLayoutHelper.CombineWordRects(words);

        Assert.AreEqual(10.0, combined.Left, 0.001);
        Assert.AreEqual(20.0, combined.Top, 0.001);
        Assert.AreEqual(140.0, combined.Right, 0.001);
        Assert.AreEqual(35.0, combined.Bottom, 0.001);
        Assert.AreEqual(130.0, combined.Width, 0.001);
        Assert.AreEqual(15.0, combined.Height, 0.001);
    }

    [TestMethod]
    public void GroupAdjacentTextLines_MergesWrappedSentence()
    {
        var lines = new List<TranslationLine>
        {
            new("This is a long sentence that", new PhysicalRect(100, 100, 420, 24), 0.9),
            new("wraps onto another line.", new PhysicalRect(102, 130, 330, 24), 0.8),
        };

        IReadOnlyList<TranslationLine> grouped = OverlayLayoutHelper.GroupAdjacentTextLines(lines);

        Assert.HasCount(1, grouped);
        Assert.AreEqual("This is a long sentence that wraps onto another line.", grouped[0].Text);
        Assert.AreEqual(2, grouped[0].SourceLineCount);
        Assert.AreEqual(100.0, grouped[0].BoundingBox.Left, 0.001);
        Assert.AreEqual(154.0, grouped[0].BoundingBox.Bottom, 0.001);
        Assert.AreEqual(0.85, grouped[0].Confidence, 0.001);
    }

    [TestMethod]
    public void GroupAdjacentTextLines_KeepsDifferentFontSizeLinesSeparate()
    {
        var lines = new List<TranslationLine>
        {
            new("Large heading", new PhysicalRect(100, 100, 260, 36), 0.9),
            new("Smaller body text", new PhysicalRect(102, 144, 320, 20), 0.9),
        };

        IReadOnlyList<TranslationLine> grouped = OverlayLayoutHelper.GroupAdjacentTextLines(lines);

        Assert.HasCount(2, grouped);
        Assert.IsTrue(grouped.Any(line => line.Text == "Large heading"));
        Assert.IsTrue(grouped.Any(line => line.Text == "Smaller body text"));
    }

    [TestMethod]
    public void GroupAdjacentTextLines_MergesMinorLineHeightVariation()
    {
        var lines = new List<TranslationLine>
        {
            new("First wrapped line", new PhysicalRect(100, 100, 260, 24), 0.9),
            new("Second wrapped line", new PhysicalRect(102, 130, 280, 22), 0.9),
        };

        IReadOnlyList<TranslationLine> grouped = OverlayLayoutHelper.GroupAdjacentTextLines(lines);

        Assert.HasCount(1, grouped);
        Assert.AreEqual("First wrapped line Second wrapped line", grouped[0].Text);
    }

    [TestMethod]
    public void GroupAdjacentTextLines_MergesSameBaselineFragmentsWithPunctuation()
    {
        var lines = new List<TranslationLine>
        {
            new("This is one sentence", new PhysicalRect(100, 100, 230, 24)),
            new(".", new PhysicalRect(334, 101, 7, 23)),
            new("It continues here", new PhysicalRect(350, 100, 180, 24)),
        };

        IReadOnlyList<TranslationLine> grouped = OverlayLayoutHelper.GroupAdjacentTextLines(lines);

        Assert.HasCount(1, grouped);
        Assert.AreEqual("This is one sentence. It continues here", grouped[0].Text);
        Assert.AreEqual(1, grouped[0].SourceLineCount);
        Assert.AreEqual(100.0, grouped[0].BoundingBox.Left, 0.001);
        Assert.AreEqual(530.0, grouped[0].BoundingBox.Right, 0.001);
    }

    [TestMethod]
    public void GroupAdjacentTextLines_MergesInlineMarkdownStyles()
    {
        var lines = new List<TranslationLine>
        {
            new("Use the", new PhysicalRect(100, 100, 82, 24)),
            new("inline code", new PhysicalRect(205, 105, 118, 18)),
            new("option", new PhysicalRect(355, 96, 76, 30)),
            new("here.", new PhysicalRect(456, 101, 58, 23)),
        };

        IReadOnlyList<TranslationLine> grouped = OverlayLayoutHelper.GroupAdjacentTextLines(lines);

        Assert.HasCount(1, grouped);
        Assert.AreEqual("Use the inline code option here.", grouped[0].Text);
        Assert.AreEqual(1, grouped[0].SourceLineCount);
    }

    [TestMethod]
    public void GroupAdjacentTextLines_MergesRaisedInlineFragment()
    {
        var lines = new List<TranslationLine>
        {
            new("Read the note", new PhysicalRect(100, 100, 130, 24)),
            new("1", new PhysicalRect(238, 89, 9, 14)),
            new("before continuing.", new PhysicalRect(255, 100, 180, 24)),
        };

        IReadOnlyList<TranslationLine> grouped = OverlayLayoutHelper.GroupAdjacentTextLines(lines);

        Assert.HasCount(1, grouped);
        Assert.AreEqual("Read the note 1 before continuing.", grouped[0].Text);
    }

    [TestMethod]
    public void GroupAdjacentTextLines_KeepsLargeSameBaselineGapSeparate()
    {
        var lines = new List<TranslationLine>
        {
            new("Left label", new PhysicalRect(100, 100, 120, 24)),
            new("Right label", new PhysicalRect(600, 100, 130, 24)),
        };

        IReadOnlyList<TranslationLine> grouped = OverlayLayoutHelper.GroupAdjacentTextLines(lines);

        Assert.HasCount(2, grouped);
    }

    [TestMethod]
    public void GroupAdjacentTextLines_KeepsSeparateColumnsApart()
    {
        var lines = new List<TranslationLine>
        {
            new("Left column", new PhysicalRect(100, 100, 180, 24)),
            new("Right column", new PhysicalRect(600, 101, 180, 24)),
            new("Left continuation", new PhysicalRect(102, 132, 210, 24)),
            new("Right continuation", new PhysicalRect(602, 133, 220, 24)),
        };

        IReadOnlyList<TranslationLine> grouped = OverlayLayoutHelper.GroupAdjacentTextLines(lines);

        Assert.HasCount(2, grouped);
        Assert.IsTrue(grouped.Any(line => line.Text == "Left column Left continuation"));
        Assert.IsTrue(grouped.Any(line => line.Text == "Right column Right continuation"));
    }

    [TestMethod]
    public void GroupAdjacentTextLines_JoinsHyphenatedLineWithoutSpace()
    {
        var lines = new List<TranslationLine>
        {
            new("multi-", new PhysicalRect(100, 100, 80, 20)),
            new("line", new PhysicalRect(100, 125, 60, 20)),
        };

        IReadOnlyList<TranslationLine> grouped = OverlayLayoutHelper.GroupAdjacentTextLines(lines);

        Assert.HasCount(1, grouped);
        Assert.AreEqual("multiline", grouped[0].Text);
    }

    [TestMethod]
    public void ClampToScreen_ClampsOutOfBoundsRect()
    {
        PhysicalRect screen = new(0, 0, 1920, 1080);
        PhysicalRect outOfBounds = new(-50, -20, 200, 100);

        var clamped = OverlayLayoutHelper.ClampToScreen(outOfBounds, screen);

        Assert.AreEqual(0.0, clamped.Left, 0.001);
        Assert.AreEqual(0.0, clamped.Top, 0.001);
        Assert.AreEqual(150.0, clamped.Right, 0.001);
        Assert.AreEqual(80.0, clamped.Bottom, 0.001);
    }

    [TestMethod]
    public void FindContainingScreen_IdentifiesCorrectScreen()
    {
        var screens = new List<PhysicalRect>
        {
            new(0, 0, 1920, 1080),
            new(1920, 0, 2560, 1440),
        };

        var screen1 = OverlayLayoutHelper.FindContainingScreen(new PhysicalPoint(500, 500), screens);
        Assert.IsNotNull(screen1);
        Assert.AreEqual(0.0, screen1.Value.X);

        var screen2 = OverlayLayoutHelper.FindContainingScreen(new PhysicalPoint(2500, 500), screens);
        Assert.IsNotNull(screen2);
        Assert.AreEqual(1920.0, screen2.Value.X);
    }
}
