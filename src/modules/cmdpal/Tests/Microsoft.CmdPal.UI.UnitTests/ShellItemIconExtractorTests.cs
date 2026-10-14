// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Graphics.Imaging;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class ShellItemIconExtractorTests
{
    [TestMethod]
    public void TakingSoftwareBitmapTransfersDisposalOwnership()
    {
        var bitmap = new SoftwareBitmap(
            BitmapPixelFormat.Bgra8,
            1,
            1,
            BitmapAlphaMode.Premultiplied);
        using var extraction = ShellIconExtractionResult.FromSoftwareBitmap(
            bitmap,
            ShellImageListSize.Large,
            requestedPixelSize: 20,
            sourceWidth: 1,
            sourceHeight: 1,
            hIconConversionTicks: 0);

        var transferred = extraction.TakeSoftwareBitmap();
        extraction.Dispose();

        Assert.AreSame(bitmap, transferred);
        Assert.IsNotNull(transferred);
        Assert.AreEqual(1, transferred.PixelWidth);
        transferred.Dispose();
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task SystemImageListIdentityUsesDirectSoftwareBitmapRoute()
    {
        var path = Path.GetTempFileName();
        try
        {
            var request = new ShellItemIconRequest(path, jumbo: false);
            Assert.IsTrue(ShellItemIconLocator.Instance.TryLocate(request, out var locatedIcon));
            Assert.AreEqual(ShellIconIdentityKind.SystemImageList, locatedIcon.Identity.Kind);

            using var extraction = await ShellItemIconExtractor.Instance.ExtractAsync(
                locatedIcon,
                targetPixelSize: 20);

            Assert.IsNotNull(extraction.SoftwareBitmap);
            Assert.IsNull(extraction.BitmapStream);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    [Timeout(5_000)]
    public async Task ItemPathIdentityUsesBitmapStreamRoute()
    {
        var path = Path.GetTempFileName();
        try
        {
            var request = new ShellItemIconRequest(path, jumbo: false);
            var locatedIcon = new LocatedShellIcon(
                request,
                ShellIconIdentity.FromItemPath(path, jumbo: false));

            using var extraction = await ShellItemIconExtractor.Instance.ExtractAsync(
                locatedIcon,
                targetPixelSize: 20);

            Assert.IsNull(extraction.SoftwareBitmap);
            Assert.IsNotNull(extraction.BitmapStream);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
