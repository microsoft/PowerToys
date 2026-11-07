// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Bookmarks.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

[TestClass]
public class FaviconLoaderTests
{
    private static readonly byte[] IconBytes = [0x00, 0x00, 0x01, 0x00];
    private readonly string _cacheDirectory = Path.Combine(Path.GetTempPath(), "CmdPal.FaviconLoaderTests", Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_cacheDirectory))
        {
            Directory.Delete(_cacheDirectory, recursive: true);
        }
    }

    [TestMethod]
    [DataRow("https://example.test/bookmark?query=value", "https://example.test/favicon.ico")]
    [DataRow("http://example.test:8080/bookmark", "http://example.test:8080/favicon.ico")]
    public async Task TryGetFaviconAsync_ReusesFreshCacheForSameAuthority(string siteUrl, string faviconUrl)
    {
        var handler = new TestHttpHandlerFactory((request, _) =>
        {
            Assert.AreEqual(faviconUrl, request.RequestUri.AbsoluteUri);
            Assert.AreEqual("image/*", request.Headers.Accept.Single().MediaType);
            StringAssert.Contains(request.Headers.UserAgent.ToString(), "WindowsCommandPalette/1.0");
            return Task.FromResult(CreateImageResponse());
        });
        using var loader = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance);

        using var first = await loader.TryGetFaviconAsync(new Uri(siteUrl));
        using var second = await loader.TryGetFaviconAsync(new Uri(new Uri(siteUrl), "/another-bookmark"));

        await AssertIconAsync(first);
        await AssertIconAsync(second);
        Assert.AreEqual(1, handler.CallCount);
    }

    [TestMethod]
    public async Task TryGetFaviconAsync_ReusesDiskCacheAcrossLoaders()
    {
        await PopulateCacheAsync();
        var handler = new TestHttpHandlerFactory((_, _) => throw new HttpRequestException("Offline"));
        using var loader = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance);

        using var icon = await loader.TryGetFaviconAsync(new Uri("https://example.test/another-bookmark"));

        await AssertIconAsync(icon);
        Assert.AreEqual(0, handler.CallCount);
    }

    [TestMethod]
    public async Task TryGetFaviconAsync_RevalidatesExpiredCache()
    {
        await PopulateCacheAsync(expired: true);
        var handler = new TestHttpHandlerFactory((request, _) =>
        {
            Assert.AreEqual("\"favicon-v1\"", request.Headers.IfNoneMatch.Single().Tag);
            Assert.AreEqual(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), request.Headers.IfModifiedSince);
            var response = new HttpResponseMessage(HttpStatusCode.NotModified);
            response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromHours(1) };
            return Task.FromResult(response);
        });
        using var loader = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance);

        using var icon = await loader.TryGetFaviconAsync(new Uri("https://example.test/bookmark"));

        await AssertIconAsync(icon);
        Assert.AreEqual(1, handler.CallCount);
    }

    [TestMethod]
    public async Task TryGetFaviconAsync_UsesExpiredCacheWhenOffline()
    {
        await PopulateCacheAsync(expired: true);
        var handler = new TestHttpHandlerFactory((_, _) => throw new HttpRequestException("Offline"));
        using var loader = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance);

        using var icon = await loader.TryGetFaviconAsync(new Uri("https://example.test/bookmark"));

        await AssertIconAsync(icon);
        Assert.AreEqual(1, handler.CallCount);
    }

    [TestMethod]
    public async Task TryGetFaviconAsync_RetriesFaviconOnFinalAuthorityAfterHtmlRedirect()
    {
        var handler = new TestHttpHandlerFactory((request, _) =>
        {
            if (request.RequestUri.AbsoluteUri == "https://www.example.test:8443/favicon.ico")
            {
                return Task.FromResult(CreateImageResponse(expired: true));
            }

            Assert.AreEqual("https://example.test/favicon.ico", request.RequestUri.AbsoluteUri);
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html>Redirected to the home page</html>", System.Text.Encoding.UTF8, "text/html"),
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://www.example.test:8443/"),
            };
            response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.Zero };
            return Task.FromResult(response);
        });
        using var loader = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance);

        using var icon = await loader.TryGetFaviconAsync(new Uri("https://example.test/bookmark"));

        await AssertIconAsync(icon);
        Assert.AreEqual(2, handler.CallCount);

        var offline = new TestHttpHandlerFactory((_, _) => throw new HttpRequestException("Offline"));
        using (var restarted = new FaviconLoader(_cacheDirectory, offline.CreateHandler, NullLogger<FaviconLoader>.Instance))
        {
            using var cachedIcon = await restarted.TryGetFaviconAsync(new Uri("https://example.test/bookmark"));
            await AssertIconAsync(cachedIcon);
            Assert.AreEqual(0, offline.CallCount);
        }

        ExpireCache();
        using (var restarted = new FaviconLoader(_cacheDirectory, offline.CreateHandler, NullLogger<FaviconLoader>.Instance))
        {
            using var staleIcon = await restarted.TryGetFaviconAsync(new Uri("https://example.test/bookmark"));
            await AssertIconAsync(staleIcon);
            Assert.AreEqual(2, offline.CallCount);
        }
    }

    [TestMethod]
    public async Task TryGetFaviconAsync_RejectsNonImageContent()
    {
        var handler = new TestHttpHandlerFactory((request, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>No icon</html>", System.Text.Encoding.UTF8, "text/html"),
            RequestMessage = request,
        }));
        using var loader = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance);

        using var icon = await loader.TryGetFaviconAsync(new Uri("https://example.test/bookmark"));

        Assert.IsNull(icon);
        Assert.AreEqual(1, handler.CallCount);
    }

    [TestMethod]
    public async Task TryGetFaviconAsync_CachesHtmlWithZeroMaxAgeForOneDay()
    {
        var handler = new TestHttpHandlerFactory((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html>No icon</html>", System.Text.Encoding.UTF8, "text/html"),
            };
            response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.Zero };
            return Task.FromResult(response);
        });
        using var loader = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance);

        Assert.IsNull(await loader.TryGetFaviconAsync(new Uri("https://example.test/bookmark")));
        Assert.IsNull(await loader.TryGetFaviconAsync(new Uri("https://example.test/another-bookmark")));

        Assert.AreEqual(1, handler.CallCount);
        var payloadPath = Directory.GetFiles(_cacheDirectory, "favicon.ico", SearchOption.AllDirectories).Single();
        Assert.AreEqual("<html>No icon</html>", await File.ReadAllTextAsync(payloadPath));
    }

    [TestMethod]
    public async Task TryGetFaviconAsync_AcceptsReplacementIconWithoutContentTypeAfterCachedHtml()
    {
        var htmlHandler = new TestHttpHandlerFactory((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>No icon</html>", System.Text.Encoding.UTF8, "text/html"),
        }));
        using (var loader = new FaviconLoader(_cacheDirectory, htmlHandler.CreateHandler, NullLogger<FaviconLoader>.Instance))
        {
            Assert.IsNull(await loader.TryGetFaviconAsync(new Uri("https://example.test/bookmark")));
        }

        ExpireCache();
        var iconHandler = new TestHttpHandlerFactory((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(IconBytes),
        }));
        using var restarted = new FaviconLoader(_cacheDirectory, iconHandler.CreateHandler, NullLogger<FaviconLoader>.Instance);

        using var icon = await restarted.TryGetFaviconAsync(new Uri("https://example.test/bookmark"));

        await AssertIconAsync(icon);
        Assert.AreEqual(1, iconHandler.CallCount);
    }

    [TestMethod]
    public async Task TryGetFaviconAsync_NotFoundDoesNotCreateCacheEntry()
    {
        var handler = new TestHttpHandlerFactory((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        using var loader = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance);

        Assert.IsNull(await loader.TryGetFaviconAsync(new Uri("https://example.test/bookmark")));

        Assert.AreEqual(1, handler.CallCount);
        Assert.AreEqual(0, Directory.GetFileSystemEntries(_cacheDirectory).Length);
    }

    [TestMethod]
    public async Task TryGetFaviconAsync_MissingPayloadDoesNotSendValidators()
    {
        await PopulateCacheAsync();
        File.Delete(Directory.GetFiles(_cacheDirectory, "favicon.ico", SearchOption.AllDirectories).Single());
        var handler = new TestHttpHandlerFactory((request, _) =>
        {
            Assert.AreEqual(0, request.Headers.IfNoneMatch.Count);
            Assert.IsNull(request.Headers.IfModifiedSince);
            return Task.FromResult(CreateImageResponse());
        });
        using var loader = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance);

        using var icon = await loader.TryGetFaviconAsync(new Uri("https://example.test/bookmark"));

        await AssertIconAsync(icon);
        Assert.AreEqual(1, handler.CallCount);
        Assert.AreEqual(1, Directory.GetFiles(_cacheDirectory, "favicon.ico", SearchOption.AllDirectories).Length);
    }

    [TestMethod]
    public async Task TryGetFaviconAsync_CacheCreationFailureUsesDirectClientAndDisposesHandlers()
    {
        Directory.CreateDirectory(_cacheDirectory);
        var blockingFile = Path.Combine(_cacheDirectory, "file");
        await File.WriteAllTextAsync(blockingFile, "Blocks cache creation");
        var handler = new TestHttpHandlerFactory((_, _) => Task.FromResult(CreateImageResponse()));

        using (var loader = new FaviconLoader(Path.Combine(blockingFile, "cache"), handler.CreateHandler, NullLogger<FaviconLoader>.Instance))
        {
            loader.Prune([new Uri("https://example.test/bookmark")]);
            using var first = await loader.TryGetFaviconAsync(new Uri("https://example.test/bookmark"));
            using var second = await loader.TryGetFaviconAsync(new Uri("https://example.test/another-bookmark"));
            await AssertIconAsync(first);
            await AssertIconAsync(second);
            Assert.AreEqual(2, handler.CallCount);
            Assert.AreEqual(2, handler.CreatedCount);
            Assert.AreEqual(1, handler.DisposedCount);
        }

        Assert.AreEqual(2, handler.DisposedCount);
    }

    [TestMethod]
    public async Task TryGetFaviconAsync_LateCacheWriteFailureRetriesCompleteBodyAndKeepsCacheEnabled()
    {
        await PopulateCacheAsync();
        var payloadPath = Directory.GetFiles(_cacheDirectory, "favicon.ico", SearchOption.AllDirectories).Single();
        File.Delete(payloadPath);
        Directory.CreateDirectory(payloadPath);
        var handler = new TestHttpHandlerFactory((_, _) => Task.FromResult(CreateImageResponse()));
        using var loader = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance);

        using var first = await loader.TryGetFaviconAsync(new Uri("https://example.test/bookmark"));
        await AssertIconAsync(first);
        Assert.AreEqual(2, handler.CallCount);
        Assert.IsFalse(File.Exists(payloadPath));
        Assert.AreEqual(0, Directory.GetFiles(_cacheDirectory, "*.tmp", SearchOption.AllDirectories).Length);

        Directory.Delete(payloadPath);
        using var second = await loader.TryGetFaviconAsync(new Uri("https://example.test/another-bookmark"));
        using var third = await loader.TryGetFaviconAsync(new Uri("https://example.test/third-bookmark"));
        await AssertIconAsync(second);
        await AssertIconAsync(third);
        Assert.AreEqual(3, handler.CallCount);
        Assert.AreEqual(2, handler.CreatedCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task TryGetFaviconAsync_NetworkIOExceptionDoesNotDisableCache(bool duringBodyRead)
    {
        await PopulateCacheAsync();
        var handler = new TestHttpHandlerFactory((request, _) =>
        {
            if (request.RequestUri.Host == "broken.test")
            {
                if (!duringBodyRead)
                {
                    throw new HttpRequestException("Connection reset", new IOException("Transport failure"));
                }

                var response = CreateImageResponse();
                response.Content.Dispose();
                response.Content = new StreamContent(new FailingReadStream());
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/x-icon");
                return Task.FromResult(response);
            }

            return Task.FromResult(CreateImageResponse());
        });
        using var loader = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance);

        Assert.IsNull(await loader.TryGetFaviconAsync(new Uri("https://broken.test/bookmark")));
        Assert.AreEqual(1, handler.CallCount);
        using var existing = await loader.TryGetFaviconAsync(new Uri("https://example.test/bookmark"));
        await AssertIconAsync(existing);
        Assert.AreEqual(1, handler.CallCount);

        using var downloaded = await loader.TryGetFaviconAsync(new Uri("https://new.test/bookmark"));
        using var cached = await loader.TryGetFaviconAsync(new Uri("https://new.test/another-bookmark"));
        await AssertIconAsync(downloaded);
        await AssertIconAsync(cached);
        Assert.AreEqual(2, handler.CallCount);
        Assert.AreEqual(1, handler.CreatedCount);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.NotFound)]
    [DataRow(HttpStatusCode.Gone)]
    public async Task TryGetFaviconAsync_ReusesFreshRedirectAndSurvivesOfflineRestart(HttpStatusCode statusCode)
    {
        var handler = new TestHttpHandlerFactory((request, _) =>
        {
            if (request.RequestUri.AbsoluteUri == "https://www.example.test:8443/favicon.ico")
            {
                return Task.FromResult(CreateImageResponse());
            }

            Assert.AreEqual("https://example.test/favicon.ico", request.RequestUri.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("Error body must not be cached"),
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://www.example.test:8443/en-US/favicon.ico"),
            });
        });
        using var loader = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance);

        using var icon = await loader.TryGetFaviconAsync(new Uri("https://example.test/bookmark"));

        await AssertIconAsync(icon);
        Assert.AreEqual(2, handler.CallCount);
        Assert.AreEqual(2, Directory.GetFiles(_cacheDirectory, "metadata.json", SearchOption.AllDirectories).Length);
        Assert.AreEqual(1, Directory.GetFiles(_cacheDirectory, "favicon.ico", SearchOption.AllDirectories).Length);

        var metadataPaths = Directory.GetFiles(_cacheDirectory, "metadata.json", SearchOption.AllDirectories);
        var originalMetadata = metadataPaths.Select(File.ReadAllText).ToArray();
        using var secondIcon = await loader.TryGetFaviconAsync(new Uri("https://example.test/another-bookmark"));
        await AssertIconAsync(secondIcon);
        Assert.AreEqual(2, handler.CallCount);

        loader.Dispose();
        using (var fresh = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance))
        {
            using var freshIcon = await fresh.TryGetFaviconAsync(new Uri("https://example.test/bookmark"));
            await AssertIconAsync(freshIcon);
            Assert.AreEqual(2, handler.CallCount);
        }

        CollectionAssert.AreEqual(originalMetadata, metadataPaths.Select(File.ReadAllText).ToArray());
        ExpireCache();
        var offline = new TestHttpHandlerFactory((_, _) => throw new HttpRequestException("Offline"));
        using var restarted = new FaviconLoader(_cacheDirectory, offline.CreateHandler, NullLogger<FaviconLoader>.Instance);
        using var cachedIcon = await restarted.TryGetFaviconAsync(new Uri("https://example.test/bookmark"));

        await AssertIconAsync(cachedIcon);
        Assert.AreEqual(2, offline.CallCount);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.Unauthorized)]
    [DataRow(HttpStatusCode.Forbidden)]
    [DataRow(HttpStatusCode.RequestTimeout)]
    [DataRow(HttpStatusCode.TooManyRequests)]
    [DataRow(HttpStatusCode.ServiceUnavailable)]
    public async Task TryGetFaviconAsync_RetriesTemporaryRedirectFailureBeforeCacheExpires(HttpStatusCode statusCode)
    {
        var failed = new TestHttpHandlerFactory((request, _) =>
        {
            Assert.AreEqual("https://example.test/favicon.ico", request.RequestUri.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://www.example.test/favicon.ico"),
            });
        });
        using (var loader = new FaviconLoader(_cacheDirectory, failed.CreateHandler, NullLogger<FaviconLoader>.Instance))
        {
            Assert.IsNull(await loader.TryGetFaviconAsync(new Uri("https://example.test/bookmark")));
            Assert.AreEqual(1, failed.CallCount);
        }

        var recovered = new TestHttpHandlerFactory((request, _) =>
        {
            Assert.AreEqual("https://example.test/favicon.ico", request.RequestUri.AbsoluteUri);
            var response = CreateImageResponse();
            response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://www.example.test/favicon.ico");
            return Task.FromResult(response);
        });
        using var restarted = new FaviconLoader(_cacheDirectory, recovered.CreateHandler, NullLogger<FaviconLoader>.Instance);

        using var icon = await restarted.TryGetFaviconAsync(new Uri("https://example.test/bookmark"));

        await AssertIconAsync(icon);
        Assert.AreEqual(1, recovered.CallCount);
    }

    [TestMethod]
    public async Task TryGetFaviconAsync_RefreshesExpiredRedirect()
    {
        var initial = new TestHttpHandlerFactory((request, _) => Task.FromResult(
            request.RequestUri.Host == "example.test"
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://old.example.test/missing"),
                }
                : CreateImageResponse()));
        using (var loader = new FaviconLoader(_cacheDirectory, initial.CreateHandler, NullLogger<FaviconLoader>.Instance))
        {
            using var icon = await loader.TryGetFaviconAsync(new Uri("https://example.test/bookmark"));
            await AssertIconAsync(icon);
        }

        ExpireCache();
        var refreshed = new TestHttpHandlerFactory((request, _) =>
        {
            if (request.RequestUri.Host == "example.test")
            {
                Assert.AreEqual(0, request.Headers.IfNoneMatch.Count);
                Assert.IsNull(request.Headers.IfModifiedSince);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://new.example.test/missing"),
                });
            }

            Assert.AreEqual("https://new.example.test/favicon.ico", request.RequestUri.AbsoluteUri);
            return Task.FromResult(CreateImageResponse());
        });
        using var restarted = new FaviconLoader(_cacheDirectory, refreshed.CreateHandler, NullLogger<FaviconLoader>.Instance);

        using var updated = await restarted.TryGetFaviconAsync(new Uri("https://example.test/bookmark"));

        await AssertIconAsync(updated);
        Assert.AreEqual(2, refreshed.CallCount);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.NotFound, true)]
    [DataRow(HttpStatusCode.NotFound, false)]
    [DataRow(HttpStatusCode.OK, true)]
    [DataRow(HttpStatusCode.OK, false)]
    public async Task TryGetFaviconAsync_DoesNotRepeatFinalFaviconUrl(HttpStatusCode statusCode, bool cacheAvailable)
    {
        var cacheDirectory = _cacheDirectory;
        if (!cacheAvailable)
        {
            Directory.CreateDirectory(_cacheDirectory);
            var blockingFile = Path.Combine(_cacheDirectory, "file");
            await File.WriteAllTextAsync(blockingFile, "Blocks cache creation");
            cacheDirectory = Path.Combine(blockingFile, "cache");
        }

        var finalHostCalls = 0;
        var handler = new TestHttpHandlerFactory((request, _) =>
        {
            if (request.RequestUri.AbsoluteUri == "https://www.example.test:8443/favicon.ico")
            {
                finalHostCalls++;
            }
            else
            {
                Assert.AreEqual("https://example.test/favicon.ico", request.RequestUri.AbsoluteUri);
            }

            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent("<html>No icon</html>", System.Text.Encoding.UTF8, "text/html"),
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://www.example.test:8443/favicon.ico"),
            });
        });
        using var loader = new FaviconLoader(cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance);

        Assert.IsNull(await loader.TryGetFaviconAsync(new Uri("https://example.test/bookmark")));
        Assert.AreEqual(1, handler.CallCount);
        Assert.AreEqual(0, finalHostCalls);

        loader.Dispose();
        using var restarted = new FaviconLoader(cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance);
        Assert.IsNull(await restarted.TryGetFaviconAsync(new Uri("https://example.test/another-bookmark")));
        Assert.AreEqual(cacheAvailable ? 1 : 2, handler.CallCount);
        Assert.AreEqual(0, finalHostCalls);
    }

    [TestMethod]
    public async Task TryGetFaviconAsync_FreshRedirectStillPropagatesCancellation()
    {
        var handler = new TestHttpHandlerFactory((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://www.example.test/favicon.ico"),
        }));
        using var loader = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance);
        var siteUri = new Uri("https://example.test/bookmark");
        Assert.IsNull(await loader.TryGetFaviconAsync(siteUri));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => loader.TryGetFaviconAsync(siteUri, cancellation.Token));
        Assert.AreEqual(1, handler.CallCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task TryGetFaviconAsync_FreshRedirectFailureTakesPrecedenceOverOlderFinalIcon(bool expired)
    {
        var handler = new TestHttpHandlerFactory((request, _) =>
        {
            if (request.RequestUri.Host == "www.example.test")
            {
                return Task.FromResult(CreateImageResponse());
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://www.example.test/favicon.ico"),
            });
        });
        using (var loader = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance))
        {
            using var icon = await loader.TryGetFaviconAsync(new Uri("https://www.example.test/bookmark"));
            await AssertIconAsync(icon);
            ExpireCache();
            Assert.IsNull(await loader.TryGetFaviconAsync(new Uri("https://example.test/bookmark")));
            Assert.AreEqual(2, handler.CallCount);
        }

        using (var repeated = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance))
        {
            Assert.IsNull(await repeated.TryGetFaviconAsync(new Uri("https://example.test/bookmark")));
            Assert.AreEqual(2, handler.CallCount);
        }

        if (expired)
        {
            ExpireCache();
        }

        var offline = new TestHttpHandlerFactory((_, _) => throw new HttpRequestException("Offline"));
        using var restarted = new FaviconLoader(_cacheDirectory, offline.CreateHandler, NullLogger<FaviconLoader>.Instance);
        using var cached = await restarted.TryGetFaviconAsync(new Uri("https://example.test/bookmark"));

        if (expired)
        {
            await AssertIconAsync(cached);
        }
        else
        {
            Assert.IsNull(cached);
        }

        Assert.AreEqual(expired ? 2 : 0, offline.CallCount);
    }

    [TestMethod]
    public async Task TryGetFaviconAsync_UnreadableCachedFileRetriesDirectly()
    {
        await PopulateCacheAsync();
        var payloadPath = Directory.GetFiles(_cacheDirectory, "favicon.ico", SearchOption.AllDirectories).Single();
        var handler = new TestHttpHandlerFactory((_, _) => Task.FromResult(CreateImageResponse()));
        using var loader = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance);

        using (var lockedPayload = new FileStream(payloadPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            using var icon = await loader.TryGetFaviconAsync(new Uri("https://example.test/bookmark"));
            await AssertIconAsync(icon);
        }

        using var cached = await loader.TryGetFaviconAsync(new Uri("https://example.test/another-bookmark"));
        await AssertIconAsync(cached);
        Assert.AreEqual(1, handler.CallCount);
        Assert.AreEqual(2, handler.CreatedCount);
    }

    [TestMethod]
    public async Task TryGetFaviconAsync_ConcurrentLookupsReadIndependentStreams()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new TestHttpHandlerFactory(async (_, ct) =>
        {
            started.SetResult();
            await release.Task.WaitAsync(ct);
            return CreateImageResponse();
        });
        using var loader = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance);

        var firstFetch = loader.TryGetFaviconAsync(new Uri("https://example.test/bookmark"));
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var secondFetch = loader.TryGetFaviconAsync(new Uri("https://example.test/another-bookmark"));
        release.SetResult();
        using var first = await firstFetch;
        using var second = await secondFetch;

        Assert.AreNotSame(first, second);
        await AssertIconAsync(first);
        first.Dispose();
        await AssertIconAsync(second);
        Assert.AreEqual(1, handler.CallCount);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task TryGetFaviconAsync_TimeoutReturnsNoIconEvenWithStaleCache(bool hasCache)
    {
        if (hasCache)
        {
            await PopulateCacheAsync(expired: true);
        }

        var handler = new TestHttpHandlerFactory((_, _) => throw new TaskCanceledException("HTTP timeout"));
        using var loader = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance);

        Assert.IsNull(await loader.TryGetFaviconAsync(new Uri("https://example.test/bookmark")));
        Assert.AreEqual(1, handler.CallCount);
    }

    [TestMethod]
    [DataRow("file:///C:/bookmark.txt")]
    [DataRow("mailto:person@example.test")]
    public async Task TryGetFaviconAsync_IgnoresNonHttpUrls(string siteUrl)
    {
        var handler = new TestHttpHandlerFactory((_, _) => throw new InvalidOperationException("No request expected"));
        using var loader = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance);

        using var icon = await loader.TryGetFaviconAsync(new Uri(siteUrl));

        Assert.IsNull(icon);
        Assert.AreEqual(0, handler.CallCount);
        Assert.IsFalse(Directory.Exists(_cacheDirectory));
    }

    [TestMethod]
    public async Task TryGetFaviconAsync_PropagatesCancellation()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new TestHttpHandlerFactory(async (_, cancellationToken) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return CreateImageResponse();
        });
        using var loader = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance);
        using var cancellation = new CancellationTokenSource();

        var fetch = loader.TryGetFaviconAsync(new Uri("https://example.test/bookmark"), cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => fetch);
        Assert.AreEqual(1, handler.CallCount);
    }

    [TestMethod]
    public async Task TryGetFaviconAsync_ThrowsAfterDisposalBeforeFirstFetch()
    {
        var handler = new TestHttpHandlerFactory((_, _) => throw new InvalidOperationException("No request expected"));
        var loader = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance);
        loader.Dispose();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => loader.TryGetFaviconAsync(new Uri("https://example.test/bookmark")));
        Assert.AreEqual(0, handler.CallCount);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.OK)]
    [DataRow(HttpStatusCode.NotFound)]
    [DataRow(HttpStatusCode.ServiceUnavailable)]
    public async Task Prune_AfterRestartKeepsRedirectedOfflineIconAndRemovesUnusedSites(HttpStatusCode statusCode)
    {
        var bookmarkUri = new Uri("https://example.test/bookmark");
        var handler = new TestHttpHandlerFactory((request, _) => Task.FromResult(
            request.RequestUri!.Host == "example.test"
                ? new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent("<html>Home page</html>", System.Text.Encoding.UTF8, "text/html"),
                    RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://www.example.test:8443/"),
                }
                : CreateImageResponse()));
        using (var loader = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance))
        {
            using var icon = await loader.TryGetFaviconAsync(bookmarkUri);
            await AssertIconAsync(icon);
            using var obsolete = await loader.TryGetFaviconAsync(new Uri("https://obsolete.example.test/bookmark"));
            await AssertIconAsync(obsolete);
        }

        ExpireCache();
        var offline = new TestHttpHandlerFactory((_, _) => throw new HttpRequestException("Offline"));
        using var restarted = new FaviconLoader(_cacheDirectory, offline.CreateHandler, NullLogger<FaviconLoader>.Instance);
        restarted.Prune([bookmarkUri, new Uri("https://example.test/another"), new Uri("file:///C:/unused"), new Uri("relative", UriKind.Relative)]);

        Assert.AreEqual(0, offline.CallCount);
        Assert.AreEqual(2, Directory.GetDirectories(_cacheDirectory).Length);
        using (var cachedIcon = await restarted.TryGetFaviconAsync(bookmarkUri))
        {
            await AssertIconAsync(cachedIcon);
        }

        Assert.AreEqual(2, offline.CallCount);
        restarted.Prune([]);
        Assert.AreEqual(0, Directory.GetDirectories(_cacheDirectory).Length);
    }

    [TestMethod]
    public async Task Prune_PreservesInflightResourcesUntilTheNextPass()
    {
        await PopulateCacheAsync(expired: true);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new TestHttpHandlerFactory(async (_, _) =>
        {
            started.TrySetResult();
            await release.Task;
            return CreateImageResponse();
        });
        using var loader = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance);
        var fetch = loader.TryGetFaviconAsync(new Uri("https://example.test/bookmark"));
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            loader.Prune([]);
            Assert.AreEqual(1, Directory.GetFiles(_cacheDirectory, "favicon.ico", SearchOption.AllDirectories).Length);
        }
        finally
        {
            release.TrySetResult();
        }

        using var icon = await fetch;
        await AssertIconAsync(icon);
        loader.Prune([]);
        Assert.AreEqual(0, Directory.GetDirectories(_cacheDirectory).Length);
    }

    private async Task PopulateCacheAsync(bool expired = false)
    {
        var handler = new TestHttpHandlerFactory((_, _) => Task.FromResult(CreateImageResponse(expired)));
        using var loader = new FaviconLoader(_cacheDirectory, handler.CreateHandler, NullLogger<FaviconLoader>.Instance);
        using var icon = await loader.TryGetFaviconAsync(new Uri("https://example.test/bookmark"));
        await AssertIconAsync(icon);
        if (expired)
        {
            ExpireCache();
        }
    }

    private void ExpireCache()
    {
        foreach (var metadataPath in Directory.GetFiles(_cacheDirectory, "metadata.json", SearchOption.AllDirectories))
        {
            var metadata = JsonNode.Parse(File.ReadAllText(metadataPath));
            metadata["LastValidatedUtc"] = DateTimeOffset.UtcNow.AddDays(-2);
            metadata["ExpiresUtc"] = DateTimeOffset.UtcNow.AddDays(-1);
            File.WriteAllText(metadataPath, metadata.ToJsonString());
        }
    }

    private static HttpResponseMessage CreateImageResponse(bool expired = false)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(IconBytes),
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/x-icon");
        response.Content.Headers.LastModified = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        response.Headers.ETag = new EntityTagHeaderValue("\"favicon-v1\"");
        if (expired)
        {
            response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.Zero };
        }

        return response;
    }

    private static async Task AssertIconAsync(IRandomAccessStream stream)
    {
        Assert.IsNotNull(stream);
        Assert.AreEqual(0UL, stream.Position);
        Assert.AreEqual((ulong)IconBytes.Length, stream.Size);
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)IconBytes.Length);
        var bytes = new byte[IconBytes.Length];
        reader.ReadBytes(bytes);
        CollectionAssert.AreEqual(IconBytes, bytes);
    }

    private sealed class TestHttpHandlerFactory(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
    {
        public int CallCount { get; private set; }

        public int CreatedCount { get; private set; }

        public int DisposedCount { get; private set; }

        private Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Responder { get; } = responder;

        public HttpMessageHandler CreateHandler()
        {
            CreatedCount++;
            return new Handler(this);
        }

        private sealed class Handler(TestHttpHandlerFactory factory) : HttpMessageHandler
        {
            private bool _disposed;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                factory.CallCount++;
                var response = await factory.Responder(request, cancellationToken);
                response.RequestMessage ??= request;
                return response;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && !_disposed)
                {
                    _disposed = true;
                    factory.DisposedCount++;
                }

                base.Dispose(disposing);
            }
        }
    }

    private sealed class FailingReadStream() : MemoryStream(IconBytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return Position == 0
                ? base.ReadAsync(buffer[..Math.Min(buffer.Length, 2)], cancellationToken)
                : ValueTask.FromException<int>(new HttpIOException(HttpRequestError.ResponseEnded, "Response body interrupted"));
        }
    }
}
