// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class ImagePreviewDecodeTests
{
    [TestMethod]
    [DataRow(3840, 2160, 480.0, 200.0, 356, 200)]
    [DataRow(2160, 3840, 600.0, 250.0, 141, 250)]
    [DataRow(3840, 240, 480.75, 250.0, 480, 30)]
    public async Task OversizedImageDecodesToFittedPixelSize(
        int width, int height, double maxWidth, double maxHeight, int decodeWidth, int decodeHeight)
    {
        using var stream = new InMemoryRandomAccessStream();
        await EncodeImageAsync(stream, width, height);

        var bounds = new Size(maxWidth, maxHeight);
        var result = await new ImagePreviewDecode(bounds).GetSizeAsync(stream, bounds);

        Assert.AreEqual(new Size(decodeWidth, decodeHeight), result);
        Assert.AreEqual(0UL, stream.Position);
        var original = await BitmapDecoder.CreateAsync(stream);
        Assert.AreEqual((uint)width, original.PixelWidth);
        Assert.AreEqual((uint)height, original.PixelHeight);
    }

    [TestMethod]
    [DataRow(6)]
    [DataRow(8)]
    public async Task RotatedJpegFitsItsOrientedDimensions(int orientation)
    {
        using var stream = new InMemoryRandomAccessStream();
        await EncodeImageAsync(stream, 4032, 3024, jpegOrientation: (ushort)orientation);

        var bounds = new Size(320, 200);
        var result = await new ImagePreviewDecode(bounds).GetSizeAsync(stream, bounds);

        Assert.AreEqual(new Size(150, 200), result);
    }

    [TestMethod]
    public async Task SmallImageKeepsItsOriginalDecodeSize()
    {
        using var stream = new InMemoryRandomAccessStream();
        await EncodeImageAsync(stream, 120, 80);

        var bounds = new Size(480, 200);
        var result = await new ImagePreviewDecode(bounds).GetSizeAsync(stream, bounds);

        Assert.IsTrue(result.IsEmpty);
    }

    [TestMethod]
    public async Task WideningDetailsPreviewIncreasesDecodeSize()
    {
        using var stream = new InMemoryRandomAccessStream();
        await EncodeImageAsync(stream, 1920, 1080);
        var maxSize = new Size(1920, 200);
        var narrow = ImagePreviewDecode.GetBounds(new Size(280, double.PositiveInfinity), maxSize, new Size(800, 600));
        var wide = ImagePreviewDecode.GetBounds(new Size(440, double.PositiveInfinity), maxSize, new Size(1200, 600));

        var decode = new ImagePreviewDecode(narrow);
        Assert.AreEqual(new Size(280, 158), await decode.GetSizeAsync(stream, narrow));
        Assert.IsFalse(decode.NeedsRefresh(wide, 1));
        decode.Complete(loadSucceeded: true);
        Assert.IsTrue(decode.NeedsRefresh(wide, 1));
        Assert.AreEqual(new Size(356, 200), await new ImagePreviewDecode(wide).GetSizeAsync(stream, wide));
    }

    [TestMethod]
    [DataRow(0, 300)]
    [DataRow(280, 0)]
    public void ZeroLayoutDefersDecodeUntilBoundsAreUsable(int width, int height)
    {
        var viewport = new Size(800, 600);
        var maxSize = new Size(1920, 200);
        var empty = ImagePreviewDecode.GetBounds(new Size(width, height), maxSize, viewport);
        var ready = ImagePreviewDecode.GetBounds(new Size(280, 300), maxSize, viewport);

        Assert.IsTrue(empty.IsEmpty);
        var decode = new ImagePreviewDecode(empty);
        decode.Complete(loadSucceeded: false);
        Assert.IsFalse(decode.NeedsRefresh(empty, 1));
        Assert.IsTrue(decode.NeedsRefresh(ready, 1));
    }

    [TestMethod]
    [DataRow(560.0, 200.0, false)]
    [DataRow(560.0, 190.0, false)]
    [DataRow(355.5, 260.0, false)]
    [DataRow(800.0, 200.4, false)]
    [DataRow(800.0, 200.5, true)]
    public async Task HeightLimitedPreviewRefreshesOnlyForLargerDecode(double width, double height, bool expected)
    {
        using var stream = new InMemoryRandomAccessStream();
        await EncodeImageAsync(stream, 1920, 1080);
        var decode = new ImagePreviewDecode(new Size(440, 200));
        Assert.AreEqual(new Size(711, 400), await decode.GetSizeAsync(stream, new Size(880, 400)));
        decode.Complete(loadSucceeded: true);

        Assert.AreEqual(expected, decode.NeedsRefresh(new Size(width, height), 2));
    }

    [TestMethod]
    public async Task NativeSizePreviewReusesBitmapWhenBothBoundsGrow()
    {
        using var stream = new InMemoryRandomAccessStream();
        await EncodeImageAsync(stream, 90, 72);
        var bounds = new Size(440, 200);
        var decode = new ImagePreviewDecode(bounds);
        Assert.IsTrue((await decode.GetSizeAsync(stream, bounds)).IsEmpty);
        decode.Complete(loadSucceeded: true);

        Assert.IsFalse(decode.NeedsRefresh(new Size(640, 300), 1));
    }

    private static async Task EncodeImageAsync(IRandomAccessStream stream, int width, int height, ushort? jpegOrientation = null)
    {
        var encoderId = jpegOrientation.HasValue ? BitmapEncoder.JpegEncoderId : BitmapEncoder.PngEncoderId;
        var encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Ignore,
            (uint)width,
            (uint)height,
            96,
            96,
            new byte[width * height * 4]);
        if (jpegOrientation is { } orientation)
        {
            await encoder.BitmapProperties.SetPropertiesAsync(new BitmapPropertySet
            {
                ["/app1/ifd/{ushort=274}"] = new BitmapTypedValue(orientation, PropertyType.UInt16),
            });
        }

        await encoder.FlushAsync();
        stream.Seek(0);
    }
}
