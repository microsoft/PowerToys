// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
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
        using var service = new ExtensionGalleryService(() => currentFeedUrl, applicationInfoService: null, cacheDirectory, httpClient: null);

        var initialResult = await service.FetchExtensionsAsync();

        Assert.IsFalse(initialResult.FromCache);
        Assert.AreEqual(1, initialResult.Extensions.Count);
        Assert.AreEqual("Sample extension", initialResult.Extensions[0].Title);

        currentFeedUrl = "bogus://invalid-feed/";
        var refreshedResult = await service.RefreshAsync();

        Assert.IsTrue(refreshedResult.FromCache);
        Assert.IsTrue(refreshedResult.UsedFallbackCache);
        Assert.IsFalse(refreshedResult.HasError);
        Assert.AreEqual(1, refreshedResult.Extensions.Count);
        Assert.AreEqual("Sample extension", refreshedResult.Extensions[0].Title);
    }

    [TestMethod]
    public async Task FetchExtensionsAsync_UsesFreshCacheWithoutFallbackWarning()
    {
        var feedDirectory = CreateTempDirectory("feed");
        var cacheDirectory = CreateTempDirectory("cache");
        WriteGalleryFeed(feedDirectory, "sample-extension", "Sample extension");

        var feedUrl = ToFeedUri(feedDirectory);
        using var service = new ExtensionGalleryService(() => feedUrl, applicationInfoService: null, cacheDirectory, httpClient: null);

        var initialResult = await service.FetchExtensionsAsync();
        var cachedResult = await service.FetchExtensionsAsync();

        Assert.IsFalse(initialResult.FromCache);
        Assert.IsFalse(initialResult.UsedFallbackCache);
        Assert.IsTrue(cachedResult.FromCache);
        Assert.IsFalse(cachedResult.UsedFallbackCache);
        Assert.IsFalse(cachedResult.HasError);
        Assert.AreEqual(1, cachedResult.Extensions.Count);
    }

    [TestMethod]
    public async Task FetchExtensionsAsync_PreservesAbsoluteIconUrlFromManifest()
    {
        var feedDirectory = CreateTempDirectory("feed");
        var cacheDirectory = CreateTempDirectory("cache");
        const string iconUrl = "https://example.com/icon.png";
        WriteGalleryFeed(feedDirectory, "sample-extension", "Sample extension", iconUrl: iconUrl);

        var feedUrl = ToFeedUri(feedDirectory);
        using var service = new ExtensionGalleryService(() => feedUrl, applicationInfoService: null, cacheDirectory, httpClient: null);

        var result = await service.FetchExtensionsAsync();

        Assert.IsFalse(result.HasError);
        Assert.IsFalse(result.FromCache);
        Assert.IsFalse(result.UsedFallbackCache);
        Assert.AreEqual(1, result.Extensions.Count);
        Assert.AreEqual("Sample extension", result.Extensions[0].Title);
        Assert.AreEqual(iconUrl, result.Extensions[0].IconUrl);
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
        using var service = new ExtensionGalleryService(() => feedUrl, applicationInfoService: null, cacheDirectory, httpClient: null);

        var result = await service.FetchExtensionsAsync();

        Assert.IsFalse(result.HasError);
        Assert.IsFalse(result.UsedFallbackCache);
        Assert.AreEqual(1, result.Extensions.Count);
        Assert.AreEqual("test-extension", result.Extensions[0].Id);
        Assert.AreEqual("Test Extension", result.Extensions[0].Title);
        Assert.AreEqual("A test extension", result.Extensions[0].Description);
        Assert.AreEqual("Test Author", result.Extensions[0].Author.Name);
        Assert.AreEqual("https://example.com/icon.png", result.Extensions[0].IconUrl);
    }

    [TestMethod]
    public async Task GetCachedIconUriAsync_ReusesFreshHttpIconCache_WithoutAnotherNetworkCall()
    {
        var cacheDirectory = CreateTempDirectory("cache");
        var handler = new TestHttpMessageHandler(request =>
        {
            return CreateHttpResponse(
                HttpStatusCode.OK,
                [0x01, 0x02, 0x03],
                "image/png",
                "\"icon-v1\"",
                maxAge: TimeSpan.FromHours(1));
        });

        using var httpClient = new HttpClient(handler);
        using var service = new ExtensionGalleryService(() => null, applicationInfoService: null, cacheDirectory, httpClient);
        var iconUri = new Uri("https://example.com/icons/sample.png");

        var firstCachedIconUri = await service.GetCachedIconUriAsync("sample-extension", iconUri);
        var secondCachedIconUri = await service.GetCachedIconUriAsync("sample-extension", iconUri);

        Assert.IsNotNull(firstCachedIconUri);
        Assert.IsNotNull(secondCachedIconUri);
        Assert.AreEqual(firstCachedIconUri, secondCachedIconUri);
        Assert.AreEqual(1, handler.CallCount);
        Assert.IsTrue(firstCachedIconUri!.IsFile);
        Assert.IsTrue(File.Exists(firstCachedIconUri.LocalPath));
    }

    [TestMethod]
    public async Task GetCachedIconUriAsync_RevalidatesStaleHttpIconCache_WithEtag()
    {
        var cacheDirectory = CreateTempDirectory("cache");
        var requestCount = 0;
        var handler = new TestHttpMessageHandler(request =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                return CreateHttpResponse(
                    HttpStatusCode.OK,
                    [0x01, 0x02, 0x03],
                    "image/png",
                    "\"icon-v1\"",
                    maxAge: TimeSpan.Zero);
            }

            Assert.AreEqual("\"icon-v1\"", request.Headers.IfNoneMatch.FirstOrDefault()?.Tag);
            return CreateHttpResponse(HttpStatusCode.NotModified, content: null, contentType: null, etag: "\"icon-v1\"", maxAge: TimeSpan.Zero);
        });

        using var httpClient = new HttpClient(handler);
        using var service = new ExtensionGalleryService(() => null, applicationInfoService: null, cacheDirectory, httpClient);
        var iconUri = new Uri("https://example.com/icons/sample.png");

        var firstCachedIconUri = await service.GetCachedIconUriAsync("sample-extension", iconUri);
        var secondCachedIconUri = await service.GetCachedIconUriAsync("sample-extension", iconUri);

        Assert.IsNotNull(firstCachedIconUri);
        Assert.IsNotNull(secondCachedIconUri);
        Assert.AreEqual(firstCachedIconUri, secondCachedIconUri);
        Assert.AreEqual(2, handler.CallCount);
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

    private static void WriteGalleryFeed(string rootDirectory, string extensionId, string title, string? iconUrl = null)
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
            IconUrl = iconUrl,
            InstallSources = [],
        };

        File.WriteAllText(
            Path.Combine(rootDirectory, "index.json"),
            JsonSerializer.Serialize(indexEntries, GallerySerializationContext.Default.GalleryIndexEntries));
        File.WriteAllText(
            Path.Combine(extensionDirectory, "manifest.json"),
            JsonSerializer.Serialize(manifest, GallerySerializationContext.Default.GalleryExtensionEntry));
    }

    private static HttpResponseMessage CreateHttpResponse(
        HttpStatusCode statusCode,
        byte[]? content,
        string? contentType,
        string? etag,
        TimeSpan? maxAge)
    {
        var response = new HttpResponseMessage(statusCode);
        if (content is not null)
        {
            response.Content = new ByteArrayContent(content);
            if (!string.IsNullOrWhiteSpace(contentType))
            {
                response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            }
        }

        if (!string.IsNullOrWhiteSpace(etag))
        {
            response.Headers.ETag = new EntityTagHeaderValue(etag);
        }

        if (maxAge is TimeSpan timeToLive)
        {
            response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = timeToLive };
        }

        return response;
    }

    private sealed class TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(responder(request));
        }
    }
}
