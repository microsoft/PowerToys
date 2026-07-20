// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerOCR.Core.Imaging;

namespace PowerOCR.Core.UnitTests;

[TestClass]
public sealed class BitmapPreprocessorTests
{
    [TestMethod]
    public void Prepare_SmallBitmap_PadsToMinimumAndTracksOffset()
    {
        using var source = new Bitmap(10, 10, PixelFormat.Format32bppArgb);
        source.SetPixel(0, 0, Color.White);
        source.SetPixel(5, 5, Color.Red);
        var processor = new BitmapPreprocessor();

        using PreparedBitmap prepared = processor.Prepare(source, 1.0);

        Assert.AreEqual(80, prepared.Bitmap.Width);
        Assert.AreEqual(80, prepared.Bitmap.Height);
        Assert.AreEqual(8, prepared.OffsetX);
        Assert.AreEqual(8, prepared.OffsetY);
        Assert.AreEqual(Color.Red.ToArgb(), prepared.Bitmap.GetPixel(13, 13).ToArgb());
    }

    [TestMethod]
    public void Prepare_LargeBitmap_ScalesDimensions()
    {
        using var source = new Bitmap(100, 50, PixelFormat.Format32bppArgb);
        var processor = new BitmapPreprocessor();

        using PreparedBitmap prepared = processor.Prepare(source, 1.5);

        Assert.AreEqual(150, prepared.Bitmap.Width);
        Assert.AreEqual(75, prepared.Bitmap.Height);
        Assert.AreEqual(1.5, prepared.Scale);
    }

    [TestMethod]
    public void Prepare_NullSource_ThrowsArgumentNullException()
    {
        var processor = new BitmapPreprocessor();
        Assert.ThrowsExactly<ArgumentNullException>(() => processor.Prepare(null!, 1.0));
    }

    [TestMethod]
    public void Prepare_ZeroScale_ThrowsArgumentOutOfRangeException()
    {
        using var source = new Bitmap(10, 10, PixelFormat.Format32bppArgb);
        var processor = new BitmapPreprocessor();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => processor.Prepare(source, 0.0));
    }

    [TestMethod]
    public void Prepare_NoPadding_OffsetIsZero()
    {
        using var source = new Bitmap(200, 200, PixelFormat.Format32bppArgb);
        var processor = new BitmapPreprocessor();

        using PreparedBitmap prepared = processor.Prepare(source, 1.0);

        Assert.AreEqual(0, prepared.OffsetX);
        Assert.AreEqual(0, prepared.OffsetY);
    }
}
