// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class AppIconProtocolProcessorTests
{
    [TestMethod]
    [DataRow(0, false)]
    [DataRow(3, false)]
    [DataRow(-4, false)]
    [DataRow(0, true)]
    [DataRow(3, true)]
    [DataRow(-4, true)]
    public async Task IndexedIconLoadsBeforeTheLaunchPathFallback(int index, bool jumbo)
    {
        var primary = FormattableString.Invariant($"{GetShell32DllPath()},{index}");
        var fallback = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        var attempts = new List<string>();
        var processor = new AppIconProtocolProcessor((candidate, useJumbo) =>
        {
            attempts.Add(candidate);
            return ThumbnailHelper.GetThumbnail(candidate, useJumbo);
        });

        using var result = await processor.PrepareAsync(
            jumbo ? AppIconProtocol.CreateJumbo(primary, fallback) : AppIconProtocol.Create(primary, fallback),
            32,
            ElementTheme.Default);

        CollectionAssert.AreEqual(new[] { primary }, attempts);
        Assert.AreEqual(IconProtocolProcessingResult.ResultKind.BitmapStream, result.Kind);
        var decoder = await BitmapDecoder.CreateAsync(result.BitmapStream!);
        Assert.AreEqual(jumbo ? 256u : 32u, decoder.PixelWidth);
        Assert.AreEqual(decoder.PixelWidth, decoder.PixelHeight);

        // Compare against direct resource extraction, including the absence of overlays.
        using var expected = IconPathConverter.Prepare(primary, null, (int)decoder.PixelWidth);
        using var actual = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        Assert.IsNotNull(expected.SoftwareBitmap);
        var expectedPixels = new byte[actual.PixelWidth * actual.PixelHeight * 4];
        var actualPixels = new byte[expectedPixels.Length];
        expected.SoftwareBitmap.CopyToBuffer(expectedPixels.AsBuffer());
        actual.CopyToBuffer(actualPixels.AsBuffer());
        CollectionAssert.AreEqual(expectedPixels, actualPixels);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MissingIconResourceAllowsFallback(bool jumbo)
    {
        using var result = await ThumbnailHelper.GetThumbnail($"{GetShell32DllPath()},2147483647", jumbo);
        Assert.IsNull(result);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task IndexedIconFileSupportsCommasAndUppercaseExtension(bool jumbo)
    {
        var iconPath = Path.Combine(Path.GetTempPath(), $"CmdPal,icon-{Guid.NewGuid():N}.ICO");
        try
        {
            using (var file = File.Create(iconPath))
            {
                System.Drawing.SystemIcons.Information.Save(file);
            }

            using var result = await ThumbnailHelper.GetThumbnail($"{iconPath},0", jumbo);
            Assert.IsNotNull(result);
            var decoder = await BitmapDecoder.CreateAsync(result);
            Assert.AreEqual(jumbo ? 256u : 32u, decoder.PixelWidth);
            using var prepared = IconPathConverter.Prepare($"{iconPath},0", null, 32);
            Assert.IsNotNull(prepared.SoftwareBitmap);
        }
        finally
        {
            File.Delete(iconPath);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ForwardSlashIconFileLoadsBeforeTheLaunchPathFallback(bool jumbo)
    {
        var iconPath = Path.Combine(Path.GetTempPath(), $"CmdPal-icon-{Guid.NewGuid():N}.ico");
        try
        {
            using (var file = File.Create(iconPath))
            {
                System.Drawing.SystemIcons.Information.Save(file);
            }

            var primary = iconPath.Replace('\\', '/');
            var fallback = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            var processor = new AppIconProtocolProcessor(
                ThumbnailHelper.GetThumbnail,
                ShellItemImageFactoryIconExtractor.Extract);

            using var result = await processor.PrepareAsync(
                jumbo ? AppIconProtocol.CreateJumbo(primary, fallback) : AppIconProtocol.Create(primary, fallback),
                64,
                ElementTheme.Default);

            if (jumbo)
            {
                Assert.AreEqual(IconProtocolProcessingResult.ResultKind.PreparedIcon, result.Kind);
                using var expected = ShellItemImageFactoryIconExtractor.Extract(iconPath, 64);
                using var actual = result.TakePreparedIcon();
                Assert.IsNotNull(expected);
                Assert.IsNotNull(actual);
                Assert.IsNotNull(actual.SoftwareBitmap);
                AssertSamePixels(expected, actual.SoftwareBitmap);
            }
            else
            {
                Assert.AreEqual(IconProtocolProcessingResult.ResultKind.BitmapStream, result.Kind);
                using var expected = await ThumbnailHelper.GetThumbnail(iconPath, false);
                Assert.IsNotNull(expected);
                Assert.IsNotNull(result.BitmapStream);
                var expectedDecoder = await BitmapDecoder.CreateAsync(expected);
                var actualDecoder = await BitmapDecoder.CreateAsync(result.BitmapStream);
                using var expectedBitmap = await expectedDecoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                using var actualBitmap = await actualDecoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                AssertSamePixels(expectedBitmap, actualBitmap);
            }
        }
        finally
        {
            File.Delete(iconPath);
        }
    }

    [TestMethod]
    [DataRow("uplay://launch/42/0")]
    [DataRow("shell:AppsFolder")]
    [DataRow("relative/icons/game.ico")]
    public async Task PreservesNonFilesystemCandidatesForNativeApisAndFallbacks(string candidate)
    {
        foreach (var jumbo in new[] { false, true })
        {
            var thumbnailAttempts = new List<string>();
            var jumboAttempts = new List<string>();
            var processor = new AppIconProtocolProcessor(
                (path, _) =>
                {
                    thumbnailAttempts.Add(path);
                    return Task.FromResult<IRandomAccessStream?>(null);
                },
                (path, _) =>
                {
                    jumboAttempts.Add(path);
                    return null;
                });

            using var result = await processor.PrepareAsync(
                jumbo ? AppIconProtocol.CreateJumbo(candidate) : AppIconProtocol.Create(candidate),
                64,
                ElementTheme.Default);

            CollectionAssert.AreEqual(new[] { candidate }, thumbnailAttempts);
            CollectionAssert.AreEqual(jumbo ? new[] { candidate } : Array.Empty<string>(), jumboAttempts);
            Assert.AreEqual(IconProtocolProcessingResult.ResultKind.FallbackIconStrings, result.Kind);
            CollectionAssert.AreEqual(new[] { candidate }, result.FallbackIconStrings);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task NormalizesIndexedFilesystemCandidatesBeforeNativeApisAndFallbacks(bool jumbo)
    {
        const string primary = "C:/Icons/game.ico,0";
        const string normalized = "C:\\Icons\\game.ico,0";
        const string fallback = "uplay://launch/42/0";
        var thumbnailAttempts = new List<string>();
        var jumboAttempts = new List<string>();
        var processor = new AppIconProtocolProcessor(
            (path, _) =>
            {
                thumbnailAttempts.Add(path);
                return Task.FromResult<IRandomAccessStream?>(null);
            },
            (path, _) =>
            {
                jumboAttempts.Add(path);
                return null;
            });

        using var result = await processor.PrepareAsync(
            jumbo ? AppIconProtocol.CreateJumbo(primary, fallback) : AppIconProtocol.Create(primary, fallback),
            64,
            ElementTheme.Default);

        var expected = new[] { normalized, fallback };
        CollectionAssert.AreEqual(expected, thumbnailAttempts);
        CollectionAssert.AreEqual(jumbo ? expected : Array.Empty<string>(), jumboAttempts);
        Assert.AreEqual(IconProtocolProcessingResult.ResultKind.FallbackIconStrings, result.Kind);
        CollectionAssert.AreEqual(expected, result.FallbackIconStrings);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ExistingFolderEndingInCommaAndNumberIsNotAnIconReference(bool jumbo)
    {
        var path = Path.Combine(Path.GetTempPath(), $"CmdPal-icons-{Guid.NewGuid():N},3");
        Directory.CreateDirectory(path);
        try
        {
            using var result = await ThumbnailHelper.GetThumbnail(path, jumbo);
            Assert.IsNotNull(result);
        }
        finally
        {
            Directory.Delete(path);
        }
    }

    [TestMethod]
    public async Task TriesCandidatesInOrderUntilThumbnailSucceeds()
    {
        const string primary = "C:\\Icons\\missing.ico";
        const string fallback = "steam://run/123|variant";
        var attempts = new List<(string Candidate, bool Jumbo)>();
        var stream = new InMemoryRandomAccessStream();
        var processor = new AppIconProtocolProcessor((candidate, jumbo) =>
        {
            attempts.Add((candidate, jumbo));
            return candidate == primary
                ? Task.FromException<IRandomAccessStream?>(new IOException("Primary failed"))
                : Task.FromResult<IRandomAccessStream?>(stream);
        });

        using var result = await processor.PrepareAsync(
            AppIconProtocol.CreateJumbo(primary, fallback),
            64,
            ElementTheme.Default);

        CollectionAssert.AreEqual(
            new[] { (primary, true), (fallback, true) },
            attempts);
        Assert.AreEqual(IconProtocolProcessingResult.ResultKind.BitmapStream, result.Kind);
        Assert.AreSame(stream, result.BitmapStream);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PreservesFallbackCandidatesAfterEveryThumbnailMisses(bool jumbo)
    {
        const string primary = "C:\\Icons\\primary.ico";
        const string finalFallback = "steam://run/123|variant";
        var fallback = $"{GetShell32DllPath()},1";
        var processor = new AppIconProtocolProcessor(
            static (_, _) => Task.FromResult<IRandomAccessStream?>(null));

        var iconDescription = jumbo
            ? AppIconProtocol.CreateJumbo(primary, fallback, finalFallback)
            : AppIconProtocol.Create(primary, fallback);
        using var result = await processor.PrepareAsync(iconDescription, 20, ElementTheme.Default);

        Assert.AreEqual(IconProtocolProcessingResult.ResultKind.FallbackIconStrings, result.Kind);
        CollectionAssert.AreEqual(
            jumbo ? new[] { primary, fallback, finalFallback } : new[] { primary, fallback },
            result.FallbackIconStrings);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    [Timeout(5_000)]
    public async Task MissingExecutableUsesIndexedFallbackAfterThumbnailMisses(bool jumbo)
    {
        var primary = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.exe");
        var fallback = $"{GetShell32DllPath()},1";
        var processor = new AppIconProtocolProcessor(
            static (_, _) => Task.FromResult<IRandomAccessStream?>(null));

        using var result = await processor.PrepareAsync(
            jumbo ? AppIconProtocol.CreateJumbo(primary, fallback) : AppIconProtocol.Create(primary, fallback),
            32,
            ElementTheme.Default);

        Assert.IsNotNull(result.FallbackIconStrings);
        using var prepared = IconPathConverter.PrepareFirstAvailable(result.FallbackIconStrings, null, 32);

        Assert.AreEqual(IconPathConverter.PreparedIconKind.Binary, prepared.Kind);
        Assert.IsNotNull(prepared.SoftwareBitmap);
        Assert.IsTrue(prepared.SoftwareBitmap.PixelWidth > 0);
        Assert.IsTrue(prepared.SoftwareBitmap.PixelHeight > 0);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    public async Task ShortcutIconMissPreservesThumbnailFallback(bool jumbo, bool throws)
    {
        const string path = @"C:\Apps\Shortcut.LNK";
        var shortcutAttempts = new List<string>();
        var thumbnailAttempts = new List<(string Path, bool Jumbo)>();
        var stream = new InMemoryRandomAccessStream();
        var processor = new AppIconProtocolProcessor(
            (candidate, useJumbo) =>
            {
                thumbnailAttempts.Add((candidate, useJumbo));
                return Task.FromResult<IRandomAccessStream?>(stream);
            },
            getShortcutIcon: (candidate, size) =>
            {
                Assert.AreEqual(32, size);
                shortcutAttempts.Add(candidate);
                if (throws)
                {
                    throw new IOException("Base-icon extraction failed");
                }

                return null;
            });

        using var result = await processor.PrepareAsync(
            jumbo ? AppIconProtocol.CreateJumbo(path) : AppIconProtocol.Create(path),
            32,
            ElementTheme.Default);

        CollectionAssert.AreEqual(jumbo ? Array.Empty<string>() : new[] { path }, shortcutAttempts);
        CollectionAssert.AreEqual(new[] { (path, jumbo) }, thumbnailAttempts);
        Assert.AreEqual(IconProtocolProcessingResult.ResultKind.BitmapStream, result.Kind);
        Assert.AreSame(stream, result.BitmapStream);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task MissingShortcutUsesConfiguredIconFallback(bool jumbo)
    {
        var shortcutPath = Path.Combine(Path.GetTempPath(), $"CmdPal-missing-shortcut-{Guid.NewGuid():N}.lnk");
        var fallback = $"{GetShell32DllPath()},1";
        using var result = await AppIconProtocolProcessor.Instance.PrepareAsync(
            jumbo ? AppIconProtocol.CreateJumbo(shortcutPath, fallback) : AppIconProtocol.Create(shortcutPath, fallback),
            32,
            ElementTheme.Default);

        Assert.AreEqual(IconProtocolProcessingResult.ResultKind.BitmapStream, result.Kind);
        Assert.IsNotNull(result.BitmapStream);
        using var expectedStream = await ThumbnailHelper.GetThumbnail(fallback, jumbo);
        Assert.IsNotNull(expectedStream);
        var expectedDecoder = await BitmapDecoder.CreateAsync(expectedStream);
        var actualDecoder = await BitmapDecoder.CreateAsync(result.BitmapStream);
        using var expected = await expectedDecoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        using var actual = await actualDecoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        AssertSamePixels(expected, actual);
    }

    private static void AssertSamePixels(SoftwareBitmap expected, SoftwareBitmap actual)
    {
        Assert.AreEqual(expected.PixelWidth, actual.PixelWidth);
        Assert.AreEqual(expected.PixelHeight, actual.PixelHeight);
        var expectedPixels = new byte[expected.PixelWidth * expected.PixelHeight * 4];
        var actualPixels = new byte[expectedPixels.Length];
        expected.CopyToBuffer(expectedPixels.AsBuffer());
        actual.CopyToBuffer(actualPixels.AsBuffer());
        CollectionAssert.AreEqual(expectedPixels, actualPixels);
    }

    private static string GetShell32DllPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll");
    }
}
