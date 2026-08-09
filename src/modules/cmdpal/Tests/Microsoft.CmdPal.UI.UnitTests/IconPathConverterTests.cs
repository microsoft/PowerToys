// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Graphics.Imaging;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class IconPathConverterTests
{
    [TestMethod]
    [Timeout(5_000)]
    public void IndexedShellIconIsPreparedAsSoftwareBitmap()
    {
        var shell32Path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "shell32.dll");

        using var prepared = IconPathConverter.Prepare($"{shell32Path},0", null, 32);

        Assert.AreEqual(IconPathConverter.PreparedIconKind.Binary, prepared.Kind);
        Assert.IsNotNull(prepared.SoftwareBitmap);
        var bitmap = prepared.SoftwareBitmap;
        Assert.IsTrue(bitmap.PixelWidth > 0);
        Assert.IsTrue(bitmap.PixelHeight > 0);
        Assert.AreEqual(BitmapPixelFormat.Bgra8, bitmap.BitmapPixelFormat);
        Assert.AreEqual(BitmapAlphaMode.Premultiplied, bitmap.BitmapAlphaMode);
    }

    [TestMethod]
    public void PreparedBinaryIconTransfersSoftwareBitmapOwnership()
    {
        using var bitmap = new SoftwareBitmap(
            BitmapPixelFormat.Bgra8,
            1,
            1,
            BitmapAlphaMode.Premultiplied);
        using var prepared = IconPathConverter.PreparedIcon.FromBinary(bitmap);

        var transferred = prepared.TakeSoftwareBitmap();
        prepared.Dispose();

        Assert.IsNotNull(transferred);
        Assert.AreSame(bitmap, transferred);
        Assert.AreEqual(1, transferred.PixelWidth);
    }

    [TestMethod]
    public void UriAndInvalidTextPreparationPreserveConverterFallbacks()
    {
        using var svg = IconPathConverter.Prepare("ms-appx:///Assets/icon.svg", null, 20);
        Assert.AreEqual(IconPathConverter.PreparedIconKind.SvgUri, svg.Kind);
        Assert.AreEqual(20, svg.TargetSize);

        using var invalidText = IconPathConverter.Prepare("not a glyph", "Custom Font", 24);
        Assert.AreEqual(IconPathConverter.PreparedIconKind.Glyph, invalidText.Kind);
        Assert.AreEqual("\u25CC", invalidText.Glyph);
        Assert.AreEqual("Segoe UI", invalidText.FontFamily);

        using var relativeText = IconPathConverter.Prepare("not-a-glyph", null, 24);
        Assert.AreEqual(IconPathConverter.PreparedIconKind.Glyph, relativeText.Kind);
        Assert.AreEqual("\u25CC", relativeText.Glyph);
    }
}
