// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Graphics.Imaging;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class ShellItemIconLocatorTests
{
    [TestMethod]
    public void LocateRestoresCallingThreadErrorMode()
    {
        var modeBefore = ShellThreadErrorModeScope.CurrentMode;
        var request = new ShellItemIconRequest(
            $"C:\\CmdPalIdentityTest\\{Guid.NewGuid():N}\\missing.txt",
            jumbo: false);

        Assert.IsTrue(ShellItemIconLocator.Instance.TryLocate(request, out _));
        Assert.AreEqual(modeBefore, ShellThreadErrorModeScope.CurrentMode);
    }

    [TestMethod]
    public void ExistingItemsOfSameRegisteredTypeUseSameSystemImageListIdentity()
    {
        var firstPath = Path.GetTempFileName();
        var secondPath = Path.GetTempFileName();
        try
        {
            var firstRequest = new ShellItemIconRequest(firstPath, jumbo: false);
            var secondRequest = new ShellItemIconRequest(secondPath, jumbo: false);

            Assert.IsTrue(ShellItemIconLocator.Instance.TryLocate(firstRequest, out var first));
            Assert.IsTrue(ShellItemIconLocator.Instance.TryLocate(secondRequest, out var second));
            Assert.AreEqual(ShellIconIdentityKind.SystemImageList, first.Identity.Kind);
            Assert.AreEqual(first.Identity, second.Identity);
        }
        finally
        {
            File.Delete(firstPath);
            File.Delete(secondPath);
        }
    }

    [TestMethod]
    public void SystemImageListIdentityExtractsPremultipliedSoftwareBitmapDirectly()
    {
        var path = Path.GetTempFileName();
        try
        {
            var request = new ShellItemIconRequest(path, jumbo: false);
            Assert.IsTrue(ShellItemIconLocator.Instance.TryLocate(request, out var locatedIcon));
            Assert.AreEqual(ShellIconIdentityKind.SystemImageList, locatedIcon.Identity.Kind);

            using var extraction = ShellSystemImageListIconExtractor.Extract(
                locatedIcon.Identity.SystemImageListIndex,
                jumbo: false,
                requestedPixelSize: 20);

            Assert.IsTrue(extraction.HasContent);
            Assert.AreEqual(ShellImageListSize.Large, extraction.ImageListSize);
            Assert.AreEqual(20, extraction.RequestedPixelSize);
            Assert.IsTrue(extraction.SourceWidth > 0);
            Assert.IsTrue(extraction.SourceHeight > 0);
            Assert.IsNull(extraction.BitmapStream);
            Assert.IsNotNull(extraction.SoftwareBitmap);
            Assert.AreEqual(BitmapPixelFormat.Bgra8, extraction.SoftwareBitmap.BitmapPixelFormat);
            Assert.AreEqual(BitmapAlphaMode.Premultiplied, extraction.SoftwareBitmap.BitmapAlphaMode);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void MissingItemsOfSameRegisteredTypeUseSharedSyntheticIdentity()
    {
        var uniqueSegment = Guid.NewGuid().ToString("N");
        var firstRequest = new ShellItemIconRequest(
            $"C:\\CmdPalIdentityTest\\{uniqueSegment}\\first.txt",
            jumbo: false);
        var secondRequest = new ShellItemIconRequest(
            $"C:\\CmdPalIdentityTest\\{uniqueSegment}\\second.txt",
            jumbo: false);

        Assert.IsTrue(ShellItemIconLocator.Instance.TryLocate(firstRequest, out var first));
        Assert.IsTrue(ShellItemIconLocator.Instance.TryLocate(secondRequest, out var second));
        Assert.AreEqual(ShellIconIdentityKind.SystemImageList, first.Identity.Kind);
        Assert.AreEqual(first.Identity, second.Identity);
        Assert.IsFalse(first.CacheRawRequestAlias);
        Assert.IsFalse(second.CacheRawRequestAlias);

        using var extraction = ShellSystemImageListIconExtractor.Extract(
            first.Identity.SystemImageListIndex,
            jumbo: false,
            requestedPixelSize: 20);
        Assert.IsTrue(extraction.HasContent);
    }

    [TestMethod]
    public void ImageThumbnailsRemainPathSpecific()
    {
        var uniqueSegment = Guid.NewGuid().ToString("N");
        var firstRequest = new ShellItemIconRequest(
            $"C:\\CmdPalIdentityTest\\{uniqueSegment}\\first.png",
            jumbo: false);
        var secondRequest = new ShellItemIconRequest(
            $"C:\\CmdPalIdentityTest\\{uniqueSegment}\\second.png",
            jumbo: false);

        Assert.IsTrue(ShellItemIconLocator.Instance.TryLocate(firstRequest, out var first));
        Assert.IsTrue(ShellItemIconLocator.Instance.TryLocate(secondRequest, out var second));
        Assert.AreEqual(ShellIconIdentityKind.ItemThumbnail, first.Identity.Kind);
        Assert.AreNotEqual(first.Identity, second.Identity);
    }

    [TestMethod]
    public void CaseDistinctRawAliasesCanConvergeToSameShellIdentity()
    {
        var path = Path.GetTempFileName();
        try
        {
            var differentlyCasedPath = path.ToUpperInvariant();
            var firstRequest = new ShellItemIconRequest(path, jumbo: false);
            var secondRequest = new ShellItemIconRequest(differentlyCasedPath, jumbo: false);

            Assert.AreNotEqual(firstRequest, secondRequest);
            Assert.IsTrue(ShellItemIconLocator.Instance.TryLocate(firstRequest, out var first));
            Assert.IsTrue(ShellItemIconLocator.Instance.TryLocate(secondRequest, out var second));
            Assert.AreEqual(ShellIconIdentityKind.SystemImageList, first.Identity.Kind);
            Assert.AreEqual(first.Identity, second.Identity);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
