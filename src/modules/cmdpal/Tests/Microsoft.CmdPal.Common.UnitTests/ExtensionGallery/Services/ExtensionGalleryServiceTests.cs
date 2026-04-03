// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.ExtensionGallery.Models;
using Microsoft.CmdPal.Common.ExtensionGallery.Services;

namespace Microsoft.CmdPal.Common.UnitTests.ExtensionGallery.Services;

[TestClass]
public class ExtensionGalleryServiceTests
{
    private readonly List<string> _temporaryDirectories = [];

    [TestMethod]
    public async Task RefreshAsync_PreservesCachedExtensions_WhenFreshFetchFails()
    {
        var feedDirectory = CreateTempDirectory("feed");
        var cacheDirectory = CreateTempDirectory("cache");
        WriteGalleryFeed(feedDirectory, "sample-extension", "Sample extension");

        var currentFeedUrl = ToFeedUri(feedDirectory);
        using var service = new ExtensionGalleryService(() => currentFeedUrl, cacheDirectory, httpClient: null);

        var initialResult = await service.FetchExtensionsAsync();

        Assert.IsFalse(initialResult.FromCache);
        Assert.AreEqual(1, initialResult.Extensions.Count);
        Assert.AreEqual("Sample extension", initialResult.Extensions[0].Title);

        currentFeedUrl = "bogus://invalid-feed/";
        var refreshedResult = await service.RefreshAsync();

        Assert.IsTrue(refreshedResult.FromCache);
        Assert.IsFalse(refreshedResult.HasError);
        Assert.AreEqual(1, refreshedResult.Extensions.Count);
        Assert.AreEqual("Sample extension", refreshedResult.Extensions[0].Title);
    }

    [TestMethod]
    public async Task FetchExtensionsAsync_ResolvesManifestAndIcons_RelativeToFeedFileUrl()
    {
        var feedDirectory = CreateTempDirectory("feed");
        var cacheDirectory = CreateTempDirectory("cache");
        WriteGalleryFeed(feedDirectory, "sample-extension", "Sample extension", iconFileName: "icon.png");

        var feedUrl = ToFeedUri(feedDirectory);
        using var service = new ExtensionGalleryService(() => feedUrl, cacheDirectory, httpClient: null);

        var result = await service.FetchExtensionsAsync();

        Assert.IsFalse(result.HasError);
        Assert.IsFalse(result.FromCache);
        Assert.AreEqual(1, result.Extensions.Count);
        Assert.AreEqual("Sample extension", result.Extensions[0].Title);

        var expectedIconUrl = new Uri(Path.Combine(feedDirectory, "extensions", "sample-extension", "icon.png")).AbsoluteUri;
        Assert.AreEqual(expectedIconUrl, service.GetIconUrl("sample-extension", "icon.png"));
    }

    [TestMethod]
    public async Task FetchExtensionsAsync_ParsesWrappedGalleryFormat_WithInlineExtensions()
    {
        var feedDirectory = CreateTempDirectory("feed");
        var cacheDirectory = CreateTempDirectory("cache");

        var wrappedJson = """
            {
                "version": "1.0",
                "extensions": [
                    {
                        "id": "test-extension",
                        "title": "Test Extension",
                        "description": "A test extension",
                        "author": { "name": "Test Author" },
                        "tags": ["test"],
                        "iconUrl": "https://example.com/icon.png",
                        "installSources": []
                    }
                ]
            }
            """;

        File.WriteAllText(Path.Combine(feedDirectory, "index.json"), wrappedJson);

        var feedUrl = ToFeedUri(feedDirectory);
        using var service = new ExtensionGalleryService(() => feedUrl, cacheDirectory, httpClient: null);

        var result = await service.FetchExtensionsAsync();

        Assert.IsFalse(result.HasError);
        Assert.AreEqual(1, result.Extensions.Count);
        Assert.AreEqual("test-extension", result.Extensions[0].Id);
        Assert.AreEqual("Test Extension", result.Extensions[0].Title);
        Assert.AreEqual("A test extension", result.Extensions[0].Description);
        Assert.AreEqual("Test Author", result.Extensions[0].Author.Name);
        Assert.AreEqual("https://example.com/icon.png", result.Extensions[0].Icon);
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var directory in _temporaryDirectories)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private string CreateTempDirectory(string name)
    {
        var directory = Path.Combine(Path.GetTempPath(), "CmdPal.ExtensionGalleryServiceTests", name, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        _temporaryDirectories.Add(directory);
        return directory;
    }

    private static string ToFeedUri(string directory)
    {
        return new Uri(Path.Combine(directory, "index.json")).AbsoluteUri;
    }

    private static void WriteGalleryFeed(string rootDirectory, string extensionId, string title, string? iconFileName = null)
    {
        var extensionDirectory = Path.Combine(rootDirectory, "extensions", extensionId);
        Directory.CreateDirectory(extensionDirectory);

        var indexEntries = new List<GalleryIndexEntry>
        {
            new() { Id = extensionId },
        };

        var manifest = new GalleryExtensionEntry
        {
            Id = extensionId,
            Title = title,
            Description = "Sample description",
            Author = new GalleryAuthor { Name = "Sample author" },
            Icon = iconFileName,
            InstallSources = [],
        };

        File.WriteAllText(
            Path.Combine(rootDirectory, "index.json"),
            JsonSerializer.Serialize(indexEntries, GallerySerializationContext.Default.GalleryIndexEntries));
        File.WriteAllText(
            Path.Combine(extensionDirectory, "manifest.json"),
            JsonSerializer.Serialize(manifest, GallerySerializationContext.Default.GalleryExtensionEntry));

        if (!string.IsNullOrWhiteSpace(iconFileName))
        {
            File.WriteAllText(Path.Combine(extensionDirectory, iconFileName), "icon");
        }
    }
}
