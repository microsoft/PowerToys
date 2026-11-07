// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
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
    public async Task GetResourceAsync_RefetchesMissingPayloadWithoutValidators()
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

        var fetched = await restarted.GetResourceAsync(ResourceUri);
        CollectionAssert.AreEqual(ContentBytes, await File.ReadAllBytesAsync(fetched.Resource.ContentPath));
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public async Task UpdateAfterNotModified_KeepsStoredDetailsWhenResponseOmitsThem()
    {
        using (var client = CreateClient(_ => CreateResponse(expired: true)))
        {
            await client.GetResourceAsync(ResourceUri);
        }

        var store = new FileSystemHttpResourceCacheStore(_cacheDirectory, TimeSpan.FromDays(1), NullLogger.Instance);
        using var notModified = new HttpResponseMessage(HttpStatusCode.NotModified);

        var revalidated = store.UpdateAfterNotModified(store.GetEntry(ResourceUri), notModified);

        Assert.IsNotNull(revalidated);
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

    private HttpCachingClient CreateClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var store = new FileSystemHttpResourceCacheStore(_cacheDirectory, TimeSpan.FromDays(1), NullLogger.Instance);
        return new HttpCachingClient(store, TimeSpan.FromSeconds(10), null, new TestHttpMessageHandler(responder), NullLogger.Instance);
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
