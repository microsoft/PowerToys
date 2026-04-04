// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.ExtensionGallery.Models;
using Microsoft.CmdPal.Common.ExtensionGallery.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.CmdPal.Common.UnitTests.ExtensionGallery.Services;

[TestClass]
public class ExtensionGalleryServiceTests
{
    private readonly List<string> _temporaryDirectories = [];

    [TestMethod]
    public async Task RefreshAsync_PreservesCachedExtensions_WhenFreshFetchFails()
    {
        var cacheDirectory = CreateTempDirectory("cache");
        var feedUrl = "https://example.com/extensions.json";
        var requestCount = 0;
        var handler = new TestHttpMessageHandler(_ =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                return CreateHttpResponse(
                    HttpStatusCode.OK,
                    System.Text.Encoding.UTF8.GetBytes(CreateGalleryFeedJson("sample-extension", "Sample extension")),
                    "application/json",
                    "\"feed-v1\"",
                    maxAge: TimeSpan.FromHours(1));
            }

            throw new HttpRequestException("Could not reach an extension gallery.");
        });

        using var serviceHandle = CreateService(() => feedUrl, cacheDirectory, handler);

        var initialResult = await serviceHandle.Service.FetchExtensionsAsync();

        Assert.IsFalse(initialResult.FromCache);
        Assert.AreEqual(1, initialResult.Extensions.Count);
        Assert.AreEqual("Sample extension", initialResult.Extensions[0].Title);
        var refreshedResult = await serviceHandle.Service.RefreshAsync();

        Assert.IsTrue(refreshedResult.FromCache);
        Assert.IsTrue(refreshedResult.UsedFallbackCache);
        Assert.IsFalse(refreshedResult.HasError);
        Assert.AreEqual(1, refreshedResult.Extensions.Count);
        Assert.AreEqual("Sample extension", refreshedResult.Extensions[0].Title);
        Assert.AreEqual(2, handler.CallCount);
    }

    [TestMethod]
    public async Task FetchExtensionsAsync_UsesFreshCacheWithoutFallbackWarning()
    {
        var cacheDirectory = CreateTempDirectory("cache");
        var feedUrl = "https://example.com/extensions.json";
        var handler = new TestHttpMessageHandler(_ =>
            CreateHttpResponse(
                HttpStatusCode.OK,
                System.Text.Encoding.UTF8.GetBytes(CreateGalleryFeedJson("sample-extension", "Sample extension")),
                "application/json",
                "\"feed-v1\"",
                maxAge: TimeSpan.FromHours(1)));

        using var serviceHandle = CreateService(() => feedUrl, cacheDirectory, handler);

        var initialResult = await serviceHandle.Service.FetchExtensionsAsync();
        var cachedResult = await serviceHandle.Service.FetchExtensionsAsync();

        Assert.IsFalse(initialResult.FromCache);
        Assert.IsFalse(initialResult.UsedFallbackCache);
        Assert.IsTrue(cachedResult.FromCache);
        Assert.IsFalse(cachedResult.UsedFallbackCache);
        Assert.IsFalse(cachedResult.HasError);
        Assert.AreEqual(1, cachedResult.Extensions.Count);
        Assert.AreEqual(1, handler.CallCount);
    }

    [TestMethod]
    public async Task FetchExtensionsAsync_UsesGalleryCacheSubdirectory()
    {
        var appCacheDirectory = CreateTempDirectory("app-cache");
        var feedUrl = "https://example.com/extensions.json";
        var handler = new TestHttpMessageHandler(_ =>
            CreateHttpResponse(
                HttpStatusCode.OK,
                System.Text.Encoding.UTF8.GetBytes(CreateGalleryFeedJson("sample-extension", "Sample extension")),
                "application/json",
                "\"feed-v1\"",
                maxAge: TimeSpan.FromHours(1)));

        using var galleryHttpClient = CreateGalleryHttpClient(
            Path.Combine(appCacheDirectory, ExtensionGalleryHttpClient.CacheDirectoryName),
            handler);
        var service = new ExtensionGalleryService(galleryHttpClient, NullLogger<ExtensionGalleryService>.Instance, new GalleryFeedUrlProvider(() => feedUrl));

        var result = await service.FetchExtensionsAsync();

        Assert.IsFalse(result.HasError);
        Assert.AreEqual(1, result.Extensions.Count);
        var cachedFeedFiles = Directory.GetFiles(Path.Combine(appCacheDirectory, "GalleryCache"), "extensions.json", SearchOption.AllDirectories);
        Assert.AreEqual(1, cachedFeedFiles.Length);
    }

    [TestMethod]
    public async Task FetchExtensionsAsync_ReturnsRateLimitedError_WhenGalleryRespondsWithTooManyRequestsAndNoCacheIsAvailable()
    {
        var cacheDirectory = CreateTempDirectory("cache");
        var feedUrl = "https://example.com/extensions.json";
        var handler = new TestHttpMessageHandler(_ => CreateHttpResponse(HttpStatusCode.TooManyRequests, content: null, contentType: null, etag: null, maxAge: null));

        using var serviceHandle = CreateService(() => feedUrl, cacheDirectory, handler);

        var result = await serviceHandle.Service.FetchExtensionsAsync();

        Assert.IsTrue(result.HasError);
        Assert.IsTrue(result.IsRateLimited);
        Assert.IsNull(result.ErrorMessage);
        Assert.AreEqual(0, result.Extensions.Count);
    }

    [TestMethod]
    public async Task FetchExtensionsAsync_AcceptsDirectoryOverride_ForLocalCompoundFeed()
    {
        var feedDirectory = CreateTempDirectory("feed");
        var cacheDirectory = CreateTempDirectory("cache");
        WriteGalleryFeed(feedDirectory, "sample-extension", "Sample extension");

        using var serviceHandle = CreateService(() => feedDirectory, cacheDirectory, innerHandler: null);

        var result = await serviceHandle.Service.FetchExtensionsAsync();

        Assert.IsFalse(result.HasError);
        Assert.IsFalse(result.FromCache);
        Assert.IsFalse(result.UsedFallbackCache);
        Assert.AreEqual(1, result.Extensions.Count);
        Assert.AreEqual("sample-extension", result.Extensions[0].Id);
    }

    [TestMethod]
    public async Task FetchExtensionsAsync_LocalizesAbsoluteIconUrlFromWrappedFeed()
    {
        var feedDirectory = CreateTempDirectory("feed");
        var cacheDirectory = CreateTempDirectory("cache");
        const string iconUrl = "https://example.com/icon.png";
        WriteGalleryFeed(feedDirectory, "sample-extension", "Sample extension", iconUrl: iconUrl);

        var feedUrl = ToFeedUri(feedDirectory);
        var handler = new TestHttpMessageHandler(request =>
        {
            Assert.AreEqual(iconUrl, request.RequestUri!.AbsoluteUri);
            return CreateHttpResponse(
                HttpStatusCode.OK,
                [0x01, 0x02, 0x03],
                "image/png",
                "\"icon-v1\"",
                maxAge: TimeSpan.FromHours(1));
        });

        using var serviceHandle = CreateService(() => feedUrl, cacheDirectory, handler);

        var result = await serviceHandle.Service.FetchExtensionsAsync();

        Assert.IsFalse(result.HasError);
        Assert.IsFalse(result.FromCache);
        Assert.IsFalse(result.UsedFallbackCache);
        Assert.AreEqual(1, result.Extensions.Count);
        Assert.AreEqual("Sample extension", result.Extensions[0].Title);
        var localizedIconUri = new Uri(result.Extensions[0].IconUrl!);
        Assert.IsTrue(localizedIconUri.IsFile);
        Assert.IsTrue(File.Exists(localizedIconUri.LocalPath));
        Assert.AreEqual(1, handler.CallCount);
    }

    [TestMethod]
    public async Task FetchExtensionsAsync_ResolvesRelativeIconUrlFromWrappedFileFeed()
    {
        var feedDirectory = CreateTempDirectory("feed");
        var cacheDirectory = CreateTempDirectory("cache");
        var expectedIconPath = Path.Combine(feedDirectory, "extensions", "sample-extension", "icon.png");
        WriteGalleryFeed(
            feedDirectory,
            "sample-extension",
            "Sample extension",
            iconUrl: "extensions/sample-extension/icon.png",
            createLocalIcon: true);

        var feedUrl = ToFeedUri(feedDirectory);
        using var serviceHandle = CreateService(() => feedUrl, cacheDirectory, innerHandler: null);

        var result = await serviceHandle.Service.FetchExtensionsAsync();

        Assert.IsFalse(result.HasError);
        Assert.AreEqual(1, result.Extensions.Count);
        Assert.AreEqual(new Uri(expectedIconPath).AbsoluteUri, result.Extensions[0].IconUrl);
    }

    [TestMethod]
    public async Task FetchExtensionsAsync_ParsesWrappedGalleryFormat_WithInlineExtensions()
    {
        var feedDirectory = CreateTempDirectory("feed");
        var cacheDirectory = CreateTempDirectory("cache");
        var iconDirectory = Path.Combine(feedDirectory, "extensions", "test-extension");
        Directory.CreateDirectory(iconDirectory);
        var expectedIconPath = Path.Combine(iconDirectory, "icon.png");
        File.WriteAllBytes(expectedIconPath, [0x01, 0x02, 0x03]);

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
                        "iconUrl": "extensions/test-extension/icon.png",
                        "installSources": []
                    }
                ]
            }
            """;

        File.WriteAllText(Path.Combine(feedDirectory, "extensions.json"), wrappedJson);

        var feedUrl = ToFeedUri(feedDirectory);
        using var serviceHandle = CreateService(() => feedUrl, cacheDirectory, innerHandler: null);

        var result = await serviceHandle.Service.FetchExtensionsAsync();

        Assert.IsFalse(result.HasError);
        Assert.IsFalse(result.UsedFallbackCache);
        Assert.AreEqual(1, result.Extensions.Count);
        Assert.AreEqual("test-extension", result.Extensions[0].Id);
        Assert.AreEqual("Test Extension", result.Extensions[0].Title);
        Assert.AreEqual("A test extension", result.Extensions[0].Description);
        Assert.AreEqual("Test Author", result.Extensions[0].Author.Name);
        Assert.AreEqual(new Uri(expectedIconPath).AbsoluteUri, result.Extensions[0].IconUrl);
    }

    [TestMethod]
    public async Task FetchExtensionsAsync_ReusesFreshHttpIconCache_WithoutAnotherNetworkCall()
    {
        var cacheDirectory = CreateTempDirectory("cache");
        var feedUrl = "https://example.com/extensions.json";
        var iconUrl = "https://example.com/icons/sample.png";
        var handler = new TestHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri.Equals(feedUrl, StringComparison.OrdinalIgnoreCase))
            {
                return CreateHttpResponse(
                    HttpStatusCode.OK,
                    System.Text.Encoding.UTF8.GetBytes(CreateGalleryFeedJson("sample-extension", "Sample extension", iconUrl)),
                    "application/json",
                    "\"feed-v1\"",
                    maxAge: TimeSpan.FromHours(1));
            }

            return CreateHttpResponse(
                HttpStatusCode.OK,
                [0x01, 0x02, 0x03],
                "image/png",
                "\"icon-v1\"",
                maxAge: TimeSpan.FromHours(1));
        });

        using var serviceHandle = CreateService(() => feedUrl, cacheDirectory, handler);

        var firstResult = await serviceHandle.Service.FetchExtensionsAsync();
        await Task.Delay(50);
        var secondResult = await serviceHandle.Service.FetchExtensionsAsync();

        var firstCachedIconUri = new Uri(firstResult.Extensions[0].IconUrl!);
        var secondCachedIconUri = new Uri(secondResult.Extensions[0].IconUrl!);
        Assert.AreEqual(firstCachedIconUri, secondCachedIconUri);
        Assert.AreEqual(2, handler.CallCount);
        Assert.IsTrue(firstCachedIconUri.IsFile);
        Assert.IsTrue(File.Exists(firstCachedIconUri.LocalPath));
    }

    [TestMethod]
    public async Task FetchExtensionsAsync_RevalidatesStaleHttpIconCache_WithEtag()
    {
        var cacheDirectory = CreateTempDirectory("cache");
        var feedUrl = "https://example.com/extensions.json";
        var iconUrl = "https://example.com/icons/sample.png";
        var iconRequestCount = 0;
        var handler = new TestHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri.Equals(feedUrl, StringComparison.OrdinalIgnoreCase))
            {
                return CreateHttpResponse(
                    HttpStatusCode.OK,
                    System.Text.Encoding.UTF8.GetBytes(CreateGalleryFeedJson("sample-extension", "Sample extension", iconUrl)),
                    "application/json",
                    "\"feed-v1\"",
                    maxAge: TimeSpan.FromHours(1));
            }

            iconRequestCount++;
            if (iconRequestCount == 1)
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

        using var serviceHandle = CreateService(() => feedUrl, cacheDirectory, handler);

        var firstResult = await serviceHandle.Service.FetchExtensionsAsync();
        await Task.Delay(50);
        var secondResult = await serviceHandle.Service.FetchExtensionsAsync();

        var firstCachedIconUri = new Uri(firstResult.Extensions[0].IconUrl!);
        var secondCachedIconUri = new Uri(secondResult.Extensions[0].IconUrl!);
        Assert.AreEqual(firstCachedIconUri, secondCachedIconUri);
        Assert.AreEqual(3, handler.CallCount);
    }

    [TestMethod]
    public async Task RefreshAsync_PrunesObsoleteCachedIcons_AfterSuccessfulRefresh()
    {
        var cacheDirectory = CreateTempDirectory("cache");
        var feedUrl = "https://example.com/extensions.json";
        var retainedIconUrl = "https://example.com/icons/current.png";
        var obsoleteIconUrl = "https://example.com/icons/obsolete.png";
        var currentFeedJson = CreateGalleryFeedJson(
            new GalleryExtensionEntry
            {
                Id = "current-extension",
                Title = "Current extension",
                Description = "Current extension",
                Author = new GalleryAuthor { Name = "Sample author" },
                IconUrl = retainedIconUrl,
                InstallSources = [],
            },
            new GalleryExtensionEntry
            {
                Id = "obsolete-extension",
                Title = "Obsolete extension",
                Description = "Obsolete extension",
                Author = new GalleryAuthor { Name = "Sample author" },
                IconUrl = obsoleteIconUrl,
                InstallSources = [],
            });

        var handler = new TestHttpMessageHandler(request =>
        {
            var requestUri = request.RequestUri!.AbsoluteUri;
            if (requestUri.Equals(feedUrl, StringComparison.OrdinalIgnoreCase))
            {
                return CreateHttpResponse(
                    HttpStatusCode.OK,
                    System.Text.Encoding.UTF8.GetBytes(currentFeedJson),
                    "application/json",
                    "\"feed-v1\"",
                    maxAge: TimeSpan.FromHours(1));
            }

            if (requestUri.Equals(retainedIconUrl, StringComparison.OrdinalIgnoreCase)
                || requestUri.Equals(obsoleteIconUrl, StringComparison.OrdinalIgnoreCase))
            {
                return CreateHttpResponse(
                    HttpStatusCode.OK,
                    [0x01, 0x02, 0x03],
                    "image/png",
                    "\"icon-v1\"",
                    maxAge: TimeSpan.FromHours(1));
            }

            throw new InvalidOperationException($"Unexpected request URI '{requestUri}'.");
        });

        using var serviceHandle = CreateService(() => feedUrl, cacheDirectory, handler);

        var initialResult = await serviceHandle.Service.FetchExtensionsAsync();
        Assert.IsFalse(initialResult.HasError);

        var retainedCachedIconUri = new Uri(initialResult.Extensions.Single(entry => entry.Id == "current-extension").IconUrl!);
        var obsoleteCachedIconUri = new Uri(initialResult.Extensions.Single(entry => entry.Id == "obsolete-extension").IconUrl!);
        Assert.IsTrue(File.Exists(retainedCachedIconUri.LocalPath));
        Assert.IsTrue(File.Exists(obsoleteCachedIconUri.LocalPath));

        currentFeedJson = CreateGalleryFeedJson(
            new GalleryExtensionEntry
            {
                Id = "current-extension",
                Title = "Current extension",
                Description = "Current extension",
                Author = new GalleryAuthor { Name = "Sample author" },
                IconUrl = retainedIconUrl,
                InstallSources = [],
            });

        var refreshedResult = await serviceHandle.Service.RefreshAsync();

        Assert.IsFalse(refreshedResult.HasError);
        Assert.IsFalse(refreshedResult.UsedFallbackCache);
        Assert.AreEqual(1, refreshedResult.Extensions.Count);
        Assert.IsTrue(File.Exists(retainedCachedIconUri.LocalPath));
        Assert.IsFalse(File.Exists(obsoleteCachedIconUri.LocalPath));
        Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(obsoleteCachedIconUri.LocalPath)!));
    }

    [TestMethod]
    public async Task RefreshAsync_DoesNotPruneCachedIcons_WhenRefreshFallsBackToCache()
    {
        var cacheDirectory = CreateTempDirectory("cache");
        var feedUrl = "https://example.com/extensions.json";
        var iconUrl = "https://example.com/icons/sample.png";
        var requestCount = 0;
        var handler = new TestHttpMessageHandler(request =>
        {
            var requestUri = request.RequestUri!.AbsoluteUri;
            if (requestUri.Equals(feedUrl, StringComparison.OrdinalIgnoreCase))
            {
                requestCount++;
                if (requestCount == 1)
                {
                    return CreateHttpResponse(
                        HttpStatusCode.OK,
                        System.Text.Encoding.UTF8.GetBytes(CreateGalleryFeedJson("sample-extension", "Sample extension", iconUrl)),
                        "application/json",
                        "\"feed-v1\"",
                        maxAge: TimeSpan.FromHours(1));
                }

                throw new HttpRequestException("Could not reach an extension gallery.");
            }

            if (requestUri.Equals(iconUrl, StringComparison.OrdinalIgnoreCase))
            {
                return CreateHttpResponse(
                    HttpStatusCode.OK,
                    [0x01, 0x02, 0x03],
                    "image/png",
                    "\"icon-v1\"",
                    maxAge: TimeSpan.FromHours(1));
            }

            throw new InvalidOperationException($"Unexpected request URI '{requestUri}'.");
        });

        using var serviceHandle = CreateService(() => feedUrl, cacheDirectory, handler);

        var initialResult = await serviceHandle.Service.FetchExtensionsAsync();
        Assert.IsFalse(initialResult.HasError);

        var cachedIconUri = new Uri(initialResult.Extensions[0].IconUrl!);
        Assert.IsTrue(File.Exists(cachedIconUri.LocalPath));

        var refreshedResult = await serviceHandle.Service.RefreshAsync();

        Assert.IsFalse(refreshedResult.HasError);
        Assert.IsTrue(refreshedResult.UsedFallbackCache);
        Assert.IsTrue(File.Exists(cachedIconUri.LocalPath));
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
        return new Uri(Path.Combine(directory, "extensions.json")).AbsoluteUri;
    }

    private static TestServiceHandle CreateService(GalleryFeedUrlProvider feedUrlProvider, string cacheDirectory, HttpMessageHandler? innerHandler)
    {
        var galleryHttpClient = CreateGalleryHttpClient(cacheDirectory, innerHandler);
        var service = new ExtensionGalleryService(galleryHttpClient, NullLogger<ExtensionGalleryService>.Instance, feedUrlProvider);
        return new TestServiceHandle(service, galleryHttpClient);
    }

    private static ExtensionGalleryHttpClient CreateGalleryHttpClient(string cacheDirectory, HttpMessageHandler? innerHandler)
    {
        return new ExtensionGalleryHttpClient(cacheDirectory, innerHandler, NullLogger<ExtensionGalleryHttpClient>.Instance);
    }

    private static void WriteGalleryFeed(
        string rootDirectory,
        string extensionId,
        string title,
        string? iconUrl = null,
        bool createLocalIcon = false)
    {
        var extensionDirectory = Path.Combine(rootDirectory, "extensions", extensionId);
        Directory.CreateDirectory(extensionDirectory);

        if (createLocalIcon)
        {
            File.WriteAllBytes(Path.Combine(extensionDirectory, "icon.png"), [0x01, 0x02, 0x03]);
        }

        var entry = new GalleryExtensionEntry
        {
            Id = extensionId,
            Title = title,
            Description = "Sample description",
            Author = new GalleryAuthor { Name = "Sample author" },
            IconUrl = iconUrl,
            InstallSources = [],
        };

        File.WriteAllText(
            Path.Combine(rootDirectory, "extensions.json"),
            JsonSerializer.Serialize(
                new GalleryRemoteIndex
                {
                    Extensions =
                    [
                        entry,
                    ],
                },
                GallerySerializationContext.Default.GalleryRemoteIndex));
    }

    private static string CreateGalleryFeedJson(string extensionId, string title, string? iconUrl = null)
    {
        return CreateGalleryFeedJson(
            new GalleryExtensionEntry
            {
                Id = extensionId,
                Title = title,
                Description = "Sample description",
                Author = new GalleryAuthor { Name = "Sample author" },
                IconUrl = iconUrl,
                InstallSources = [],
            });
    }

    private static string CreateGalleryFeedJson(params GalleryExtensionEntry[] entries)
    {
        return JsonSerializer.Serialize(
            new GalleryRemoteIndex
            {
                Extensions = [.. entries],
            },
            GallerySerializationContext.Default.GalleryRemoteIndex);
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

    private sealed class TestServiceHandle(ExtensionGalleryService service, ExtensionGalleryHttpClient galleryHttpClient) : IDisposable
    {
        public ExtensionGalleryService Service { get; } = service;

        public void Dispose()
        {
            galleryHttpClient.Dispose();
        }
    }
}
