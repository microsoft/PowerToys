// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class BackgroundImagePathResolverTests
{
    [TestMethod]
    public void TryGetLocalFolderPath_WithFolderPath_ReturnsTrue()
    {
        var directory = CreateTempDirectory();

        try
        {
            var ok = BackgroundImagePathResolver.TryGetLocalFolderPath(directory, out var resolved);

            Assert.IsTrue(ok);
            Assert.AreEqual(Path.GetFullPath(directory), resolved);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void TryGetLocalFolderPath_WithFileUriFolder_ReturnsTrue()
    {
        var directory = CreateTempDirectory();

        try
        {
            var uri = new Uri(directory).AbsoluteUri;
            var ok = BackgroundImagePathResolver.TryGetLocalFolderPath(uri, out var resolved);

            Assert.IsTrue(ok);
            Assert.AreEqual(Path.GetFullPath(directory), resolved);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void GetSupportedImageFiles_FiltersAndSortsImageFiles()
    {
        var directory = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(directory, "b.jpg"), "b");
            File.WriteAllText(Path.Combine(directory, "a.png"), "a");
            File.WriteAllText(Path.Combine(directory, "note.txt"), "x");

            var files = BackgroundImagePathResolver.GetSupportedImageFiles(directory);

            Assert.AreEqual(2, files.Count);
            Assert.IsTrue(files[0].EndsWith("a.png", StringComparison.OrdinalIgnoreCase));
            Assert.IsTrue(files[1].EndsWith("b.jpg", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void ResolvePreviewImagePath_WithFolderPath_ReturnsFirstSupportedImage()
    {
        var directory = CreateTempDirectory();

        try
        {
            var first = Path.Combine(directory, "a.png");
            var second = Path.Combine(directory, "z.jpg");
            File.WriteAllText(second, "z");
            File.WriteAllText(first, "a");

            var resolved = BackgroundImagePathResolver.ResolvePreviewImagePath(directory);

            Assert.AreEqual(first, resolved, true, CultureInfo.InvariantCulture);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void ResolvePreviewImagePath_WithFolderWithoutImages_ReturnsNull()
    {
        var directory = CreateTempDirectory();

        try
        {
            File.WriteAllText(Path.Combine(directory, "notes.txt"), "x");

            var resolved = BackgroundImagePathResolver.ResolvePreviewImagePath(directory);

            Assert.IsNull(resolved);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void TryGetLocalFolderPath_WithNull_ReturnsFalse()
    {
        var ok = BackgroundImagePathResolver.TryGetLocalFolderPath(null, out var resolved);

        Assert.IsFalse(ok);
        Assert.AreEqual(string.Empty, resolved);
    }

    [TestMethod]
    public void TryGetLocalFolderPath_WithEmptyString_ReturnsFalse()
    {
        var ok = BackgroundImagePathResolver.TryGetLocalFolderPath("   ", out var resolved);

        Assert.IsFalse(ok);
        Assert.AreEqual(string.Empty, resolved);
    }

    [TestMethod]
    public void TryGetLocalFolderPath_WithFilePath_ReturnsFalse()
    {
        var directory = CreateTempDirectory();

        try
        {
            var filePath = Path.Combine(directory, "image.png");
            File.WriteAllText(filePath, "x");

            var ok = BackgroundImagePathResolver.TryGetLocalFolderPath(filePath, out var resolved);

            Assert.IsFalse(ok);
            Assert.AreEqual(string.Empty, resolved);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void TryGetLocalFolderPath_WithHttpUri_ReturnsFalse()
    {
        var ok = BackgroundImagePathResolver.TryGetLocalFolderPath("https://example.com/images", out var resolved);

        Assert.IsFalse(ok);
        Assert.AreEqual(string.Empty, resolved);
    }

    [TestMethod]
    public void TryGetLocalFolderPath_WithNonExistentPath_ReturnsFalse()
    {
        var nonExistentPath = Path.Combine(Path.GetTempPath(), $"CmdPalBackgroundResolverTests_{Guid.NewGuid():N}");

        var ok = BackgroundImagePathResolver.TryGetLocalFolderPath(nonExistentPath, out var resolved);

        Assert.IsFalse(ok);
        Assert.AreEqual(string.Empty, resolved);
    }

    [TestMethod]
    public void ResolvePreviewImagePath_WithNull_ReturnsNull()
    {
        var resolved = BackgroundImagePathResolver.ResolvePreviewImagePath(null);

        Assert.IsNull(resolved);
    }

    [TestMethod]
    public void ResolvePreviewImagePath_WithFilePath_ReturnsPathAsIs()
    {
        var directory = CreateTempDirectory();

        try
        {
            var filePath = Path.Combine(directory, "image.png");
            File.WriteAllText(filePath, "x");

            var resolved = BackgroundImagePathResolver.ResolvePreviewImagePath($"  {filePath}  ");

            Assert.AreEqual(filePath, resolved, true, CultureInfo.InvariantCulture);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    public void GetSupportedImageFiles_WithEmptyFolder_ReturnsEmpty()
    {
        var directory = CreateTempDirectory();

        try
        {
            var files = BackgroundImagePathResolver.GetSupportedImageFiles(directory);

            Assert.AreEqual(0, files.Count);
        }
        finally
        {
            DeleteDirectory(directory);
        }
    }

    [TestMethod]
    [DataRow("C:\\invalid<>path")]
    [DataRow("C:\\path\0with_null")]
    [DataRow(":::")]
    public void TryGetLocalFolderPath_WithMalformedPath_ReturnsFalse(string malformedPath)
    {
        var ok = BackgroundImagePathResolver.TryGetLocalFolderPath(malformedPath, out var resolved);

        Assert.IsFalse(ok);
        Assert.AreEqual(string.Empty, resolved);
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"CmdPalBackgroundResolverTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
