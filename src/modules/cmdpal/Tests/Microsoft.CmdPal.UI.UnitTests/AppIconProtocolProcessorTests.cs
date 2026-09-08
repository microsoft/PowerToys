// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class AppIconProtocolProcessorTests
{
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

    private static string GetShell32DllPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "shell32.dll");
    }
}
