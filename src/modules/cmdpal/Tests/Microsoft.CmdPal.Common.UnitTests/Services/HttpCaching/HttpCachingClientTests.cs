// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Services.HttpCaching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Common.UnitTests.Services.HttpCaching;

[TestClass]
public class HttpCachingClientTests
{
    private static readonly Uri ResourceUri = new("https://example.test/favicon.ico");
    private static readonly Uri FinalUri = new("https://cdn.example.test/icon.ico");
    private static readonly byte[] ContentBytes = [0x00, 0x00, 0x01, 0x00];
    private readonly string _cacheDirectory = Path.Combine(Path.GetTempPath(), "CmdPal.HttpCachingClientTests", Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_cacheDirectory))
        {
            Directory.Delete(_cacheDirectory, recursive: true);
        }
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("image/*")]
    public async Task GetResourceAsync_ReturnsFileBackedResourceAndOnlySendsConfiguredAccept(string? accept)
    {
        using (var client = new HttpCachingClient(
            _cacheDirectory,
            TimeSpan.FromDays(1),
            TimeSpan.FromSeconds(10),
            userAgent: null,
            new TestHttpMessageHandler(request =>
            {
                Assert.AreEqual(accept ?? string.Empty, request.Headers.Accept.ToString());
                return CreateResponse();
            }),
            NullLogger.Instance,
            accept: accept))
        {
            var result = await client.GetResourceAsync(ResourceUri);

            Assert.IsTrue(File.Exists(result.Resource.ContentPath));
            Assert.IsTrue(result.Resource.ContentUri.IsFile);
            Assert.AreEqual(result.Resource.ContentPath, result.Resource.ContentUri.LocalPath);
            CollectionAssert.AreEqual(ContentBytes, await File.ReadAllBytesAsync(result.Resource.ContentPath));
            Assert.AreEqual(FinalUri, result.Resource.FinalUri);
            Assert.IsFalse(result.Resource.FromCache);
            Assert.IsFalse(result.UsedFallbackCache);
        }

        using var restarted = CreateClient(_ => throw new AssertFailedException("Fresh cache should not use the network"));
        var cached = await restarted.GetResourceAsync(ResourceUri);
        Assert.IsTrue(cached.Resource.FromCache);
        Assert.AreEqual(FinalUri, cached.Resource.FinalUri);
    }

    [TestMethod]
    [DataRow(HttpStatusCode.NotFound)]
    [DataRow(HttpStatusCode.TooManyRequests)]
    public async Task GetResourceAsync_HttpErrorPreservesRedirectWithoutPayloadOrErrorLog(HttpStatusCode statusCode)
    {
        var logger = new Mock<MEL.ILogger>();
        logger.Setup(value => value.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var store = new FileSystemHttpResourceCacheStore(_cacheDirectory, TimeSpan.FromDays(1), logger.Object);
        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(statusCode)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, FinalUri),
        });
        using var client = new HttpCachingClient(store, TimeSpan.FromSeconds(10), null, handler, logger.Object);

        var exception = await Assert.ThrowsExactlyAsync<HttpResourceRequestException>(() => client.GetResourceAsync(ResourceUri));

        Assert.AreEqual(statusCode, exception.StatusCode);
        Assert.AreEqual(FinalUri, exception.FinalUri);
        Assert.IsTrue(exception.FinalUriAlreadyChecked);
        Assert.AreEqual(1, Directory.GetFiles(_cacheDirectory, "metadata.json", SearchOption.AllDirectories).Length);
        Assert.AreEqual(0, Directory.GetFiles(_cacheDirectory, "favicon.ico", SearchOption.AllDirectories).Length);
        Assert.AreEqual(0, logger.Invocations.Count(call => call.Method.Name == "Log" && (LogLevel)call.Arguments[0] >= LogLevel.Warning));
    }

    [TestMethod]
    [DataRow(HttpStatusCode.NotFound)]
    [DataRow(HttpStatusCode.TooManyRequests)]
    public async Task GetResourceAsync_SavedRedirectIsUsedAfterOfflineRestart(HttpStatusCode statusCode)
    {
        using (var client = CreateClient(_ => new HttpResponseMessage(statusCode)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, FinalUri),
        }))
        {
            await Assert.ThrowsExactlyAsync<HttpResourceRequestException>(() => client.GetResourceAsync(ResourceUri));
        }

        using var offline = CreateClient(_ => throw new HttpRequestException("Offline"));
        var exception = await Assert.ThrowsExactlyAsync<HttpResourceRequestException>(() => offline.GetResourceAsync(ResourceUri));

        Assert.AreEqual(FinalUri, exception.FinalUri);
        Assert.IsFalse(exception.FinalUriAlreadyChecked);
        Assert.IsNull(exception.StatusCode);
    }

    [TestMethod]
    [DataRow("not a URI")]
    [DataRow("file:///C:/unexpected")]
    public async Task GetResourceAsync_InvalidSavedRedirectIsIgnoredOffline(string finalUri)
    {
        using (var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, FinalUri),
        }))
        {
            await Assert.ThrowsExactlyAsync<HttpResourceRequestException>(() => client.GetResourceAsync(ResourceUri));
        }

        SetMetadataFinalUri(finalUri);
        using var offline = CreateClient(_ => throw new HttpRequestException("Offline"));

        await Assert.ThrowsExactlyAsync<HttpRequestException>(() => offline.GetResourceAsync(ResourceUri, reuseFailedRedirects: true));
    }

    [TestMethod]
    [DataRow(HttpStatusCode.NotFound, true)]
    [DataRow(HttpStatusCode.Gone, true)]
    [DataRow(HttpStatusCode.Unauthorized, false)]
    [DataRow(HttpStatusCode.Forbidden, false)]
    [DataRow(HttpStatusCode.RequestTimeout, false)]
    [DataRow(HttpStatusCode.TooManyRequests, false)]
    [DataRow(HttpStatusCode.InternalServerError, false)]
    [DataRow(HttpStatusCode.ServiceUnavailable, false)]
    public async Task GetResourceAsync_OnlyReusesMissingResourceRedirects(HttpStatusCode statusCode, bool reuseFailure)
    {
        using (var client = CreateClient(_ => new HttpResponseMessage(statusCode)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, FinalUri),
        }))
        {
            await Assert.ThrowsExactlyAsync<HttpResourceRequestException>(() => client.GetResourceAsync(ResourceUri, reuseFailedRedirects: true));
        }

        if (!reuseFailure)
        {
            using var offline = CreateClient(_ => throw new HttpRequestException("Offline"));
            var exception = await Assert.ThrowsExactlyAsync<HttpResourceRequestException>(() => offline.GetResourceAsync(
                ResourceUri,
                minimumTimeToLive: TimeSpan.FromDays(1),
                reuseFailedRedirects: true));
            Assert.AreEqual(FinalUri, exception.FinalUri);
            Assert.IsFalse(exception.FinalUriAlreadyChecked);
            Assert.IsNull(exception.StatusCode);
        }

        var calls = 0;
        using var recovered = CreateClient(request =>
        {
            calls++;
            Assert.AreEqual(ResourceUri, request.RequestUri);
            Assert.AreEqual(0, request.Headers.IfNoneMatch.Count);
            Assert.IsNull(request.Headers.IfModifiedSince);
            return CreateResponse();
        });
        var fetch = recovered.GetResourceAsync(ResourceUri, minimumTimeToLive: TimeSpan.FromDays(1), reuseFailedRedirects: true);
        if (reuseFailure)
        {
            var exception = await Assert.ThrowsExactlyAsync<HttpResourceRequestException>(() => fetch);
            Assert.AreEqual(statusCode, exception.StatusCode);
            Assert.IsTrue(exception.FinalUriAlreadyChecked);
            Assert.AreEqual(0, calls);
        }
        else
        {
            var result = await fetch;
            CollectionAssert.AreEqual(ContentBytes, await File.ReadAllBytesAsync(result.Resource.ContentPath));
            Assert.AreEqual(1, calls);
        }
    }

    [TestMethod]
    [DataRow(true, false, 0, 0, 0)]
    [DataRow(false, false, 0, 0, 1)]
    [DataRow(true, true, 0, 0, 1)]
    [DataRow(true, false, 1, 0, 1)]
    [DataRow(true, false, 1, 24, 0)]
    public async Task GetResourceAsync_FreshRedirectHonorsRequestOptions(bool reuseFailedRedirects, bool forceRefresh, int ttlHours, int minimumTtlHours, int expectedCalls)
    {
        using (var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, FinalUri),
        }))
        {
            await Assert.ThrowsExactlyAsync<HttpResourceRequestException>(() => client.GetResourceAsync(ResourceUri));
        }

        var metadataPath = Directory.GetFiles(_cacheDirectory, "metadata.json", SearchOption.AllDirectories).Single();
        var metadata = JsonNode.Parse(await File.ReadAllTextAsync(metadataPath));
        Assert.IsNotNull(metadata);
        metadata["LastValidatedUtc"] = DateTimeOffset.UtcNow.AddHours(-12);
        var originalMetadata = metadata.ToJsonString();
        await File.WriteAllTextAsync(metadataPath, originalMetadata);
        var calls = 0;
        using var restarted = CreateClient(request =>
        {
            calls++;
            Assert.AreEqual(ResourceUri, request.RequestUri);
            return CreateResponse();
        });

        var fetch = restarted.GetResourceAsync(
            ResourceUri,
            forceRefresh: forceRefresh,
            timeToLiveOverride: ttlHours == 0 ? null : TimeSpan.FromHours(ttlHours),
            minimumTimeToLive: minimumTtlHours == 0 ? null : TimeSpan.FromHours(minimumTtlHours),
            reuseFailedRedirects: reuseFailedRedirects);
        if (expectedCalls == 0)
        {
            var exception = await Assert.ThrowsExactlyAsync<HttpResourceRequestException>(() => fetch);
            Assert.AreEqual(FinalUri, exception.FinalUri);
            Assert.AreEqual(HttpStatusCode.NotFound, exception.StatusCode);
            Assert.IsTrue(exception.FinalUriAlreadyChecked);
            Assert.AreEqual(originalMetadata, await File.ReadAllTextAsync(metadataPath));
        }
        else
        {
            var result = await fetch;
            Assert.IsFalse(result.Resource.FromCache);
            CollectionAssert.AreEqual(ContentBytes, await File.ReadAllBytesAsync(result.Resource.ContentPath));
        }

        Assert.AreEqual(expectedCalls, calls);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task GetResourceAsync_RefetchesMissingPayloadWithoutValidators(bool reuseFailedRedirects)
    {
        using (var client = CreateClient(_ =>
        {
            var response = CreateResponse();
            response.Content.Headers.LastModified = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
            return response;
        }))
        {
            var original = await client.GetResourceAsync(ResourceUri);
            File.Delete(original.Resource.ContentPath);
        }

        var calls = 0;
        using var restarted = CreateClient(request =>
        {
            calls++;
            Assert.AreEqual(ResourceUri, request.RequestUri);
            Assert.AreEqual(0, request.Headers.IfNoneMatch.Count);
            Assert.IsNull(request.Headers.IfModifiedSince);
            return CreateResponse();
        });

        var fetched = await restarted.GetResourceAsync(ResourceUri, reuseFailedRedirects: reuseFailedRedirects);
        CollectionAssert.AreEqual(ContentBytes, await File.ReadAllBytesAsync(fetched.Resource.ContentPath));
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public async Task GetResourceAsync_RedirectMetadataDoesNotPreventSuccessfulRefetch()
    {
        using (var client = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, FinalUri),
        }))
        {
            await Assert.ThrowsExactlyAsync<HttpResourceRequestException>(() => client.GetResourceAsync(ResourceUri));
        }

        using var recovered = CreateClient(request =>
        {
            Assert.AreEqual(0, request.Headers.IfNoneMatch.Count);
            Assert.IsNull(request.Headers.IfModifiedSince);
            return CreateResponse();
        });
        var result = await recovered.GetResourceAsync(ResourceUri);

        Assert.IsFalse(result.Resource.FromCache);
        CollectionAssert.AreEqual(ContentBytes, await File.ReadAllBytesAsync(result.Resource.ContentPath));
    }

    [TestMethod]
    public async Task GetResourceAsync_FailedRedirectDoesNotOverwriteExistingPayloadMetadata()
    {
        using (var client = CreateClient(_ => CreateResponse(expired: true)))
        {
            await client.GetResourceAsync(ResourceUri);
        }

        var metadataPath = Directory.GetFiles(_cacheDirectory, "metadata.json", SearchOption.AllDirectories).Single();
        var originalMetadata = await File.ReadAllTextAsync(metadataPath);
        using var failing = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, "https://other.example.test/missing"),
        });
        var stale = await failing.GetResourceAsync(ResourceUri);

        Assert.IsTrue(stale.UsedFallbackCache);
        Assert.AreEqual(FinalUri, stale.Resource.FinalUri);
        Assert.AreEqual(originalMetadata, await File.ReadAllTextAsync(metadataPath));
        CollectionAssert.AreEqual(ContentBytes, await File.ReadAllBytesAsync(stale.Resource.ContentPath));
    }

    [TestMethod]
    public async Task GetResourceAsync_OfflineUsesStaleFileWithFinalUri()
    {
        using (var client = CreateClient(_ => CreateResponse(expired: true)))
        {
            await client.GetResourceAsync(ResourceUri);
        }

        using var offline = CreateClient(_ => throw new HttpRequestException("Offline"));
        var result = await offline.GetResourceAsync(ResourceUri);

        Assert.IsTrue(result.UsedFallbackCache);
        Assert.IsTrue(result.Resource.FromCache);
        Assert.AreEqual(FinalUri, result.Resource.FinalUri);
        CollectionAssert.AreEqual(ContentBytes, await File.ReadAllBytesAsync(result.Resource.ContentPath));
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public async Task GetResourceAsync_NotModifiedStoresCurrentFinalUri(bool hasFinalUri)
    {
        var movedFinalUri = new Uri("https://moved.example.test/icon.ico");
        using (var client = CreateClient(_ => CreateResponse(expired: true)))
        {
            await client.GetResourceAsync(ResourceUri);
        }

        if (!hasFinalUri)
        {
            SetMetadataFinalUri(null);
        }

        var expectedFinalUri = hasFinalUri ? movedFinalUri : FinalUri;
        using (var revalidating = CreateClient(request =>
        {
            Assert.AreEqual("\"icon-v1\"", request.Headers.IfNoneMatch.Single().Tag);
            var response = new HttpResponseMessage(HttpStatusCode.NotModified)
            {
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, expectedFinalUri),
            };
            response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.Zero };
            return response;
        }))
        {
            var result = await revalidating.GetResourceAsync(ResourceUri);
            Assert.IsTrue(result.Resource.WasRevalidated);
            Assert.AreEqual(expectedFinalUri, result.Resource.FinalUri);
            Assert.AreEqual("image/x-icon", result.Resource.ContentType);
            CollectionAssert.AreEqual(ContentBytes, await File.ReadAllBytesAsync(result.Resource.ContentPath));
        }

        using var offline = CreateClient(_ => throw new HttpRequestException("Offline"));
        var stale = await offline.GetResourceAsync(ResourceUri);
        Assert.IsTrue(stale.UsedFallbackCache);
        Assert.AreEqual(expectedFinalUri, stale.Resource.FinalUri);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("relative/path")]
    [DataRow("file:///C:/unexpected")]
    public async Task UpdateAfterNotModified_KeepsStoredDetailsWhenResponseOmitsThem(string? responseUri)
    {
        using (var client = CreateClient(_ => CreateResponse(expired: true)))
        {
            await client.GetResourceAsync(ResourceUri);
        }

        var store = new FileSystemHttpResourceCacheStore(_cacheDirectory, TimeSpan.FromDays(1), NullLogger.Instance);
        using var notModified = new HttpResponseMessage(HttpStatusCode.NotModified)
        {
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, responseUri),
        };

        var revalidated = store.UpdateAfterNotModified(store.GetEntry(ResourceUri), notModified);

        Assert.IsNotNull(revalidated);
        Assert.AreEqual(FinalUri, revalidated.FinalUri);
        Assert.AreEqual("image/x-icon", revalidated.ContentType);
        Assert.AreEqual("\"icon-v1\"", store.GetEntry(ResourceUri).Metadata?.ETag);
    }

    [TestMethod]
    public async Task GetResourceAsync_ReplacementDoesNotInheritStoredRepresentationDetails()
    {
        using (var client = CreateClient(_ =>
        {
            var html = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html>Home page</html>", System.Text.Encoding.UTF8, "text/html"),
            };
            html.Content.Headers.LastModified = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
            html.Headers.ETag = new EntityTagHeaderValue("\"home-v1\"");
            html.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.Zero };
            return html;
        }))
        {
            var cachedHtml = await client.GetResourceAsync(ResourceUri);
            Assert.AreEqual("text/html", cachedHtml.Resource.ContentType);
        }

        using (var replacing = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(ContentBytes),
        }))
        {
            var replaced = await replacing.GetResourceAsync(ResourceUri, forceRefresh: true);
            Assert.IsNull(replaced.Resource.ContentType);
            CollectionAssert.AreEqual(ContentBytes, await File.ReadAllBytesAsync(replaced.Resource.ContentPath));
        }

        using var revalidating = CreateClient(request =>
        {
            Assert.AreEqual(0, request.Headers.IfNoneMatch.Count);
            Assert.IsNull(request.Headers.IfModifiedSince);
            return CreateResponse();
        });
        await revalidating.GetResourceAsync(ResourceUri, forceRefresh: true);
    }

    [TestMethod]
    [DataRow("not a URI")]
    [DataRow("file:///C:/unexpected")]
    public async Task GetResourceAsync_IgnoresInvalidCachedFinalUri(string finalUri)
    {
        using (var client = CreateClient(_ => CreateResponse()))
        {
            await client.GetResourceAsync(ResourceUri);
        }

        SetMetadataFinalUri(finalUri);
        using var restarted = CreateClient(_ => throw new AssertFailedException("Fresh cache should not use the network"));
        var result = await restarted.GetResourceAsync(ResourceUri);

        Assert.IsNull(result.Resource.FinalUri);
        CollectionAssert.AreEqual(ContentBytes, await File.ReadAllBytesAsync(result.Resource.ContentPath));
    }

    [TestMethod]
    [DataRow(false, false, 1)]
    [DataRow(true, false, 0)]
    [DataRow(true, true, 1)]
    public async Task GetResourceAsync_MinimumTimeToLiveIsOptInAndForceRefreshStillWorks(bool useMinimum, bool forceRefresh, int expectedCalls)
    {
        using (var client = CreateClient(_ => CreateResponse(expired: true)))
        {
            await client.GetResourceAsync(ResourceUri);
        }

        var calls = 0;
        using var restarted = CreateClient(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.NotModified);
        });
        var result = await restarted.GetResourceAsync(
            ResourceUri,
            forceRefresh: forceRefresh,
            minimumTimeToLive: useMinimum ? TimeSpan.FromDays(1) : null);

        Assert.AreEqual(expectedCalls, calls);
        Assert.AreEqual(expectedCalls > 0, result.Resource.WasRevalidated);
        CollectionAssert.AreEqual(ContentBytes, await File.ReadAllBytesAsync(result.Resource.ContentPath));
    }

    [TestMethod]
    public void GetEntry_DoesNotCreateEntryDirectory()
    {
        var store = new FileSystemHttpResourceCacheStore(_cacheDirectory, TimeSpan.FromDays(1), NullLogger.Instance);

        var entry = store.GetEntry(ResourceUri);

        Assert.IsFalse(Directory.Exists(entry.EntryDirectory));
    }

    [TestMethod]
    public async Task GetResourceAsync_UnreadablePayloadIsCacheStoreFailure()
    {
        string payloadPath;
        using (var client = CreateClient(_ => CreateResponse()))
        {
            payloadPath = (await client.GetResourceAsync(ResourceUri)).Resource.ContentPath;
        }

        using var lockedPayload = new FileStream(payloadPath, FileMode.Open, FileAccess.Read, FileShare.None);
        using var restarted = CreateClient(_ => throw new AssertFailedException("Fresh cache should not use the network"));

        var exception = await Assert.ThrowsExactlyAsync<HttpResourceCacheStoreException>(() => restarted.GetResourceAsync(ResourceUri));

        Assert.IsInstanceOfType<IOException>(exception.InnerException);
    }

    [TestMethod]
    public async Task GetResourceAsync_LateSaveFailureKeepsDiskCauseAndCleansTemporaryPayload()
    {
        var logger = new Mock<MEL.ILogger>();
        logger.Setup(value => value.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var store = new FileSystemHttpResourceCacheStore(_cacheDirectory, TimeSpan.FromDays(1), logger.Object);
        var entry = store.GetEntry(ResourceUri);
        Directory.CreateDirectory(entry.PayloadPath);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var client = new HttpCachingClient(store, TimeSpan.FromSeconds(10), null, new TestHttpMessageHandler(_ => CreateResponse()), logger.Object);
            var exception = await Assert.ThrowsExactlyAsync<HttpResourceCacheStoreException>(() => client.GetResourceAsync(ResourceUri));

            Assert.IsTrue(exception.InnerException is IOException or UnauthorizedAccessException);
            Assert.AreEqual(0, Directory.GetFiles(_cacheDirectory, "*", SearchOption.AllDirectories).Length);
        }

        Assert.AreEqual(1, logger.Invocations.Count(call => call.Method.Name == "Log" && (LogLevel)call.Arguments[0] == LogLevel.Warning));
        Assert.AreEqual(1, logger.Invocations.Count(call => call.Method.Name == "Log" && (LogLevel)call.Arguments[0] == LogLevel.Debug && ((EventId)call.Arguments[1]).Id == 2));
        Assert.AreEqual(0, logger.Invocations.Count(call => call.Method.Name == "Log" && (LogLevel)call.Arguments[0] == LogLevel.Error));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task GetResourceAsync_NetworkReadFailureDoesNotConsumeCacheWarning(bool failOpeningStream)
    {
        var logger = new Mock<MEL.ILogger>();
        logger.Setup(value => value.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var store = new FileSystemHttpResourceCacheStore(_cacheDirectory, TimeSpan.FromDays(1), logger.Object);
        var handler = new TestHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = failOpeningStream ? new FailingResponseContent() : new StreamContent(new FailingReadStream()),
        });
        using (var client = new HttpCachingClient(store, TimeSpan.FromSeconds(10), null, handler, logger.Object))
        {
            var exception = await Assert.ThrowsExactlyAsync<HttpRequestException>(() => client.GetResourceAsync(ResourceUri));
            Assert.IsInstanceOfType<HttpIOException>(exception.InnerException);
        }

        Assert.AreEqual(0, logger.Invocations.Count(call => call.Method.Name == "Log" && (LogLevel)call.Arguments[0] >= LogLevel.Warning));
        Assert.AreEqual(0, Directory.GetFiles(_cacheDirectory, "*", SearchOption.AllDirectories).Length);

        var entry = store.GetEntry(ResourceUri);
        Directory.CreateDirectory(entry.PayloadPath);
        using (var client = new HttpCachingClient(store, TimeSpan.FromSeconds(10), null, new TestHttpMessageHandler(_ => CreateResponse()), logger.Object))
        {
            await Assert.ThrowsExactlyAsync<HttpResourceCacheStoreException>(() => client.GetResourceAsync(ResourceUri));
        }

        Assert.AreEqual(1, logger.Invocations.Count(call => call.Method.Name == "Log" && (LogLevel)call.Arguments[0] == LogLevel.Warning));
    }

    [TestMethod]
    public async Task GetResourceAsync_UnreadableMetadataStillFetches()
    {
        using (var client = CreateClient(_ => CreateResponse()))
        {
            await client.GetResourceAsync(ResourceUri);
        }

        var metadataPath = Directory.GetFiles(_cacheDirectory, "metadata.json", SearchOption.AllDirectories).Single();
        using var lockedMetadata = new FileStream(metadataPath, FileMode.Open, FileAccess.Read, FileShare.None);
        using var restarted = CreateClient(request =>
        {
            Assert.AreEqual(0, request.Headers.IfNoneMatch.Count);
            return CreateResponse();
        });
        var result = await restarted.GetResourceAsync(ResourceUri);

        Assert.IsFalse(result.Resource.FromCache);
        CollectionAssert.AreEqual(ContentBytes, await File.ReadAllBytesAsync(result.Resource.ContentPath));
    }

    [TestMethod]
    [DataRow(HttpStatusCode.OK, false)]
    [DataRow(HttpStatusCode.OK, true)]
    [DataRow(HttpStatusCode.NotFound, false)]
    [DataRow(HttpStatusCode.NotFound, true)]
    public async Task Prune_RetainsDerivedRedirectResourceOnlyWhenRequested(HttpStatusCode statusCode, bool retainRedirectResource)
    {
        var redirectResourceUri = new Uri(FinalUri, "/favicon.ico");
        var obsoleteUri = new Uri("https://obsolete.example.test/favicon.ico");
        using (var client = CreateClient(request => request.RequestUri == ResourceUri
            ? new HttpResponseMessage(statusCode)
            {
                Content = new ByteArrayContent(ContentBytes),
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, FinalUri),
            }
            : CreateResponse()))
        {
            if (statusCode == HttpStatusCode.OK)
            {
                await client.GetResourceAsync(ResourceUri);
            }
            else
            {
                await Assert.ThrowsExactlyAsync<HttpResourceRequestException>(() => client.GetResourceAsync(ResourceUri));
            }

            await client.GetResourceAsync(redirectResourceUri);
            await client.GetResourceAsync(obsoleteUri);
        }

        using var restarted = CreateClient(_ => throw new AssertFailedException("Pruning should not use the network"));
        restarted.Prune([ResourceUri], retainRedirectResource ? uri => new Uri(uri, "/favicon.ico") : null);

        var store = new FileSystemHttpResourceCacheStore(_cacheDirectory, TimeSpan.FromDays(1), NullLogger.Instance);
        Assert.IsTrue(File.Exists(store.GetEntry(ResourceUri).MetadataPath));
        Assert.AreEqual(retainRedirectResource, File.Exists(store.GetEntry(redirectResourceUri).PayloadPath));
        Assert.IsFalse(Directory.Exists(store.GetEntry(obsoleteUri).EntryDirectory));
    }

    private HttpCachingClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var store = new FileSystemHttpResourceCacheStore(_cacheDirectory, TimeSpan.FromDays(1), NullLogger.Instance);
        return new HttpCachingClient(store, TimeSpan.FromSeconds(10), null, new TestHttpMessageHandler(responder), NullLogger.Instance);
    }

    private void SetMetadataFinalUri(string? finalUri)
    {
        var metadataPath = Directory.GetFiles(_cacheDirectory, "metadata.json", SearchOption.AllDirectories).Single();
        var metadata = JsonNode.Parse(File.ReadAllText(metadataPath));
        Assert.IsNotNull(metadata);
        metadata["FinalUri"] = finalUri;
        File.WriteAllText(metadataPath, metadata.ToJsonString());
    }

    private static HttpResponseMessage CreateResponse(bool expired = false)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(ContentBytes),
            RequestMessage = new HttpRequestMessage(HttpMethod.Get, FinalUri),
        };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/x-icon");
        response.Headers.ETag = new EntityTagHeaderValue("\"icon-v1\"");
        if (expired)
        {
            response.Headers.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.Zero };
        }

        return response;
    }

    private sealed class TestHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = responder(request);
            response.RequestMessage ??= request;
            return Task.FromResult(response);
        }
    }

    private sealed class FailingReadStream() : MemoryStream(ContentBytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return Position == 0
                ? base.ReadAsync(buffer[..Math.Min(buffer.Length, 2)], cancellationToken)
                : ValueTask.FromException<int>(new HttpIOException(HttpRequestError.ResponseEnded, "Response body interrupted"));
        }
    }

    private sealed class FailingResponseContent : HttpContent
    {
        protected override Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken)
            => Task.FromException<Stream>(new HttpIOException(HttpRequestError.ResponseEnded, "Response stream unavailable"));

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => throw new NotSupportedException();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
