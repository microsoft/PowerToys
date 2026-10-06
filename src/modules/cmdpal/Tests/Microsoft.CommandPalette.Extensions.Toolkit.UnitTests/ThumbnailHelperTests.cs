// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class ThumbnailHelperTests
{
    [TestMethod]
    public void ShellDialogSuppressionPreservesModesAcrossNestedScopes()
    {
        var modeBefore = ShellThreadErrorModeScope.CurrentMode;

        using (ShellThreadErrorModeScope.SuppressShellDialogs())
        {
            var outerMode = ShellThreadErrorModeScope.CurrentMode;
            Assert.AreEqual(
                ShellThreadErrorModeScope.SuppressedModes,
                outerMode & ShellThreadErrorModeScope.SuppressedModes);
            Assert.AreEqual(
                modeBefore & ~ShellThreadErrorModeScope.SuppressedModes,
                outerMode & ~ShellThreadErrorModeScope.SuppressedModes);

            using (ShellThreadErrorModeScope.SuppressShellDialogs())
            {
                Assert.AreEqual(outerMode, ShellThreadErrorModeScope.CurrentMode);
            }

            Assert.AreEqual(outerMode, ShellThreadErrorModeScope.CurrentMode);
        }

        Assert.AreEqual(modeBefore, ShellThreadErrorModeScope.CurrentMode);
    }

    [DataTestMethod]
    [DataRow(@"C:\Files\image.jfif")]
    [DataRow(@"C:\Files\image.dib")]
    [DataRow(@"C:\Files\image.avif")]
    [DataRow(@"C:\Files\image.HEIC")]
    [DataRow(@"C:\Files\image.heif")]
    [DataRow(@"C:\Files\image.jxr")]
    [DataRow(@"C:\Files\image.svg")]
    [DataRow(@"C:\Files\image.tif")]
    [DataRow(@"C:\Files\image.webp")]
    public void RecognizesEveryNewImageExtension(string path)
    {
        Assert.IsTrue(ThumbnailHelper.IsImagePath(path));
    }

    [TestMethod]
    public void ImagePathClassificationDoesNotAllocateAnExtensionString()
    {
        const string ImagePath = @"C:\Files\image.HEIC";
        for (var i = 0; i < 32; i++)
        {
            _ = ThumbnailHelper.IsImagePath(ImagePath);
        }

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var allRecognized = true;
        for (var i = 0; i < 1_000; i++)
        {
            allRecognized &= ThumbnailHelper.IsImagePath(ImagePath);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.IsTrue(allRecognized);
        Assert.IsTrue(
            allocated < 1_000,
            $"Image-path classification allocated {allocated} bytes across 1,000 calls.");
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(@"C:\Files\document.txt")]
    [DataRow(@"C:\Files\README")]
    public void RejectsNonImagePaths(string? path)
    {
        Assert.IsFalse(ThumbnailHelper.IsImagePath(path));
    }
}
