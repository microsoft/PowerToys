// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.UITestAutomationNext.UnitTests;

[TestClass]
public sealed class VisualAssertTests
{
    [TestMethod]
    [DataRow(93.75, false)]
    [DataRow(94.99, false)]
    [DataRow(95.0, true)]
    [DataRow(100.0, true)]
    public void DefaultSimilarityThresholdRemains95(double similarity, bool expected)
    {
        Assert.AreEqual(expected, VisualAssert.IsSimilar(similarity));
    }

    [TestMethod]
    [DataRow(92.99, 93, false)]
    [DataRow(93.0, 93, true)]
    [DataRow(93.75, 93, true)]
    [DataRow(93.75, 95, false)]
    [DataRow(0.99, 1, false)]
    [DataRow(1.0, 1, true)]
    [DataRow(99.99, 100, false)]
    [DataRow(100.0, 100, true)]
    public void ExplicitSimilarityThresholdUsesInclusiveBoundary(double similarity, int threshold, bool expected)
    {
        Assert.AreEqual(expected, VisualAssert.IsSimilar(similarity, threshold));
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(0)]
    [DataRow(101)]
    public void InvalidSimilarityThresholdIsRejected(int threshold)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => VisualAssert.IsSimilar(100, threshold));
    }

    [TestMethod]
    [DataRow(32, 32)]
    [DataRow(19, 11)]
    [DataRow(1, 1)]
    public void IdenticalImagesHaveFullStructuralSimilarity(int width, int height)
    {
        using var baseline = CreatePattern(width, height);
        using var actual = baseline.Clone(new Rectangle(0, 0, width, height), PixelFormat.Format32bppArgb);

        Assert.AreEqual(100d, VisualAssert.CalculateSimilarity(baseline, actual), 0.000001);
    }

    [TestMethod]
    public void SolidBlackAndWhiteDoNotCollideLikeAverageHashes()
    {
        using var baseline = CreateSolid(32, 32, Color.White);
        using var actual = CreateSolid(32, 32, Color.Black);

        Assert.IsTrue(VisualAssert.CalculateSimilarity(baseline, actual) < 1d);
    }

    [TestMethod]
    public void TransparentCaptureDoesNotMatchOpaquePixelsWithTheSameRgb()
    {
        using var baseline = CreateSolid(32, 32, Color.Black);
        using var actual = new Bitmap(32, 32, PixelFormat.Format32bppArgb);

        Assert.IsTrue(VisualAssert.CalculateSimilarity(baseline, actual) < 1d);
    }

    [TestMethod]
    public void MissingPreviewFailsTheNormal95PercentThreshold()
    {
        using var baseline = CreatePreview();
        using var actual = CreateSolid(baseline.Width, baseline.Height, Color.WhiteSmoke);

        Assert.IsFalse(VisualAssert.IsSimilar(VisualAssert.CalculateSimilarity(baseline, actual)));
    }

    [TestMethod]
    public void OppositeStructureDoesNotMatchEvenWithTheSameAverageBrightness()
    {
        using var baseline = CreatePattern(64, 64);
        using var actual = new Bitmap(64, 64);
        for (var y = 0; y < actual.Height; y++)
        {
            for (var x = 0; x < actual.Width; x++)
            {
                var color = baseline.GetPixel(x, y);
                actual.SetPixel(x, y, Color.FromArgb(255 - color.R, 255 - color.G, 255 - color.B));
            }
        }

        Assert.IsTrue(VisualAssert.CalculateSimilarity(baseline, actual) < 5d);
    }

    [TestMethod]
    public void FrameVariationIsMeasuredWithoutChangingUnrelatedContentStatistics()
    {
        using var baseline = CreatePreview();
        using var actual = CreatePreview();
        SetFrame(baseline, Color.DarkSlateGray);
        SetFrame(actual, Color.DarkGray);

        var similarity = VisualAssert.CalculateSimilarity(baseline, actual);

        Assert.IsTrue(VisualAssert.IsSimilar(similarity));
        Assert.IsTrue(similarity < 100d, "Frame pixels must still affect the result; they are not cropped or masked.");
    }

    [TestMethod]
    public void SmallColorRoundingDifferencesDoNotFailTheVisualComparison()
    {
        using var baseline = CreateSolid(32, 32, Color.FromArgb(120, 160, 200));
        using var actual = CreateSolid(32, 32, Color.FromArgb(121, 159, 201));

        var similarity = VisualAssert.CalculateSimilarity(baseline, actual);

        Assert.IsTrue(VisualAssert.IsSimilar(similarity));
        Assert.IsTrue(similarity < 100d);
    }

    [TestMethod]
    public void PartialEdgeBlocksIncludeTheLastPixel()
    {
        using var baseline = CreateSolid(19, 11, Color.White);
        using var actual = CreateSolid(19, 11, Color.White);
        actual.SetPixel(18, 10, Color.Black);

        Assert.IsTrue(VisualAssert.CalculateSimilarity(baseline, actual) < 100d);
    }

    [TestMethod]
    public void DifferentDimensionsAreRejectedRatherThanRescaledToTheSameHash()
    {
        using var baseline = CreateSolid(32, 32, Color.White);
        using var actual = CreateSolid(64, 32, Color.White);

        var exception = Assert.ThrowsExactly<ArgumentException>(() => VisualAssert.CalculateSimilarity(baseline, actual));

        Assert.AreEqual("testImage", exception.ParamName);
    }

    [TestMethod]
    public void PixelFormatAndStridePaddingDoNotChangeTheComparison()
    {
        using var baseline = new Bitmap(19, 11, PixelFormat.Format24bppRgb);
        using var actual = new Bitmap(19, 11, PixelFormat.Format32bppArgb);
        foreach (var image in new[] { baseline, actual })
        {
            using var graphics = Graphics.FromImage(image);
            graphics.Clear(Color.CornflowerBlue);
        }

        Assert.AreEqual(100d, VisualAssert.CalculateSimilarity(baseline, actual), 0.000001);
    }

    private static Bitmap CreateSolid(int width, int height, Color color)
    {
        var image = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(image);
        graphics.Clear(color);
        return image;
    }

    private static Bitmap CreatePattern(int width, int height)
    {
        var image = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                image.SetPixel(x, y, ((x / 2) + (y / 2)) % 2 == 0 ? Color.Black : Color.White);
            }
        }

        return image;
    }

    private static Bitmap CreatePreview()
    {
        var image = CreateSolid(256, 256, Color.WhiteSmoke);
        using var preview = CreatePattern(128, 128);
        using var graphics = Graphics.FromImage(image);
        graphics.DrawImageUnscaled(preview, 64, 64);
        return image;
    }

    private static void SetFrame(Bitmap image, Color color)
    {
        using var graphics = Graphics.FromImage(image);
        using var pen = new Pen(color);
        graphics.DrawRectangle(pen, 0, 0, image.Width - 1, image.Height - 1);
    }
}
