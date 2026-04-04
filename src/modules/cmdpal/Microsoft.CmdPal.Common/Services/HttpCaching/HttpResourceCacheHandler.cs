// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.CmdPal.Common.Services.HttpCaching.Abstraction;
using Microsoft.Extensions.Logging;
using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Common.Services.HttpCaching;

internal sealed partial class HttpResourceCacheHandler : DelegatingHandler
{
    private static readonly HttpRequestOptionsKey<HttpResourceCacheRequestOptions> RequestOptionsKey = new("CmdPal.HttpResourceCache.RequestOptions");
    private static readonly HttpRequestOptionsKey<CachedHttpResponseInfo> ResponseInfoKey = new("CmdPal.HttpResourceCache.ResponseInfo");
    private static readonly Action<MEL.ILogger, string, Exception?> LogFailedToCacheHttpResourceMessage = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(0, nameof(LogFailedToCacheHttpResource)),
        "Failed to cache HTTP resource '{ResourceUri}'.");

    private readonly IHttpResourceCacheStore _cacheStore;
    private readonly MEL.ILogger _logger;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Task<CachedHttpResponseInfo?>> _inflightFetches = new(StringComparer.Ordinal);

    public HttpResourceCacheHandler(IHttpResourceCacheStore cacheStore, HttpMessageHandler innerHandler, MEL.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(cacheStore);
        ArgumentNullException.ThrowIfNull(innerHandler);
        ArgumentNullException.ThrowIfNull(logger);

        _cacheStore = cacheStore;
        _logger = logger;
        InnerHandler = innerHandler;
    }

    public static void ConfigureRequest(
        HttpRequestMessage request,
        string? fileNameHint = null,
        bool forceRefresh = false,
        TimeSpan? timeToLiveOverride = null)
    {
        ArgumentNullException.ThrowIfNull(request);

        request.Options.Set(
            RequestOptionsKey,
            new HttpResourceCacheRequestOptions(fileNameHint, forceRefresh, timeToLiveOverride));
    }

    public static CachedHttpResponseInfo GetResponseInfo(HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return response.RequestMessage?.Options.TryGetValue(ResponseInfoKey, out var responseInfo) == true
            ? responseInfo
            : CachedHttpResponseInfo.None;
    }

    public static bool TryGetResponseInfo(HttpResponseMessage response, [NotNullWhen(true)] out CachedHttpResponseInfo? responseInfo)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response.RequestMessage?.Options.TryGetValue(ResponseInfoKey, out responseInfo) == true)
        {
            return true;
        }

        responseInfo = null;
        return false;
    }

    internal void AddInflightResourceUris(ICollection<Uri> retainedResourceUris)
    {
        ArgumentNullException.ThrowIfNull(retainedResourceUris);

        lock (_lock)
        {
            foreach (var inflightKey in _inflightFetches.Keys)
            {
                if (!Uri.TryCreate(inflightKey, UriKind.Absolute, out var inflightUri))
                {
                    continue;
                }

                retainedResourceUris.Add(inflightUri);
            }
        }
    }

    protected async override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!CanCache(request))
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var options = request.Options.TryGetValue(RequestOptionsKey, out var requestOptions)
            ? requestOptions
            : HttpResourceCacheRequestOptions.Default;
        var fetchResult = await GetOrFetchAsync(request, options, cancellationToken);
        if (fetchResult?.Resource is null)
        {
            throw new HttpRequestException($"Could not reach HTTP resource '{request.RequestUri}'.");
        }

        return CreateResponse(request, fetchResult);
    }

    private Task<CachedHttpResponseInfo?> GetOrFetchAsync(
        HttpRequestMessage request,
        HttpResourceCacheRequestOptions options,
        CancellationToken cancellationToken)
    {
        var inflightKey = request.RequestUri!.AbsoluteUri;

        lock (_lock)
        {
            if (_inflightFetches.TryGetValue(inflightKey, out var existingTask))
            {
                return existingTask;
            }

            var fetchTask = GetOrFetchCoreAsync(request, options, cancellationToken);
            _inflightFetches[inflightKey] = fetchTask;

            _ = fetchTask.ContinueWith(
                _ =>
                {
                    lock (_lock)
                    {
                        _inflightFetches.Remove(inflightKey);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.None,
                TaskScheduler.Default);

            return fetchTask;
        }
    }

    private async Task<CachedHttpResponseInfo?> GetOrFetchCoreAsync(
        HttpRequestMessage request,
        HttpResourceCacheRequestOptions options,
        CancellationToken cancellationToken)
    {
        var entry = _cacheStore.GetEntry(request.RequestUri!, options.FileNameHint);
        if (!options.ForceRefresh && _cacheStore.TryGetFresh(entry, options.TimeToLiveOverride) is { } freshResource)
        {
            return new CachedHttpResponseInfo(freshResource, usedFallbackCache: false);
        }

        try
        {
            using var networkRequest = CloneRequest(request, entry.Metadata);
            using var response = await base.SendAsync(networkRequest, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                var revalidatedResource = _cacheStore.UpdateAfterNotModified(entry, response);
                return revalidatedResource is null
                    ? null
                    : new CachedHttpResponseInfo(revalidatedResource, usedFallbackCache: false);
            }

            response.EnsureSuccessStatusCode();
            var cachedResource = await _cacheStore.SaveResponseAsync(entry, response, cancellationToken);
            return new CachedHttpResponseInfo(cachedResource, usedFallbackCache: false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            LogFailedToCacheHttpResource(_logger, request.RequestUri!.AbsoluteUri, ex);

            var cachedResource = _cacheStore.TryGetCached(entry, fromCache: true, wasRevalidated: false);
            if (cachedResource is not null)
            {
                return new CachedHttpResponseInfo(cachedResource, usedFallbackCache: true);
            }

            throw;
        }
        catch (IOException ex)
        {
            LogFailedToCacheHttpResource(_logger, request.RequestUri!.AbsoluteUri, ex);

            var cachedResource = _cacheStore.TryGetCached(entry, fromCache: true, wasRevalidated: false);
            if (cachedResource is not null)
            {
                return new CachedHttpResponseInfo(cachedResource, usedFallbackCache: true);
            }

            throw new HttpRequestException($"Could not reach HTTP resource '{request.RequestUri}'.", ex);
        }
    }

    private static HttpResponseMessage CreateResponse(HttpRequestMessage request, CachedHttpResponseInfo responseInfo)
    {
        var contentStream = new FileStream(
            responseInfo.Resource!.ContentPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(contentStream),
            RequestMessage = request,
        };

        if (!string.IsNullOrWhiteSpace(responseInfo.Resource.ContentType))
        {
            response.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(responseInfo.Resource.ContentType);
        }

        request.Options.Set(ResponseInfoKey, responseInfo);
        return response;
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request, HttpResourceCacheMetadata? metadata)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
        };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (!string.IsNullOrWhiteSpace(metadata?.ETag) && !clone.Headers.Contains("If-None-Match"))
        {
            clone.Headers.TryAddWithoutValidation("If-None-Match", metadata.ETag);
        }

        if (metadata?.LastModifiedUtc is { } lastModifiedUtc && clone.Headers.IfModifiedSince is null)
        {
            clone.Headers.IfModifiedSince = lastModifiedUtc;
        }

        return clone;
    }

    private static bool CanCache(HttpRequestMessage request)
    {
        return request.Method == HttpMethod.Get
            && request.RequestUri is { } requestUri
            && IsSupportedHttpUri(requestUri);
    }

    private static bool IsSupportedHttpUri(Uri resourceUri)
    {
        return resourceUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || resourceUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static void LogFailedToCacheHttpResource(MEL.ILogger logger, string resourceUri, Exception exception)
    {
        LogFailedToCacheHttpResourceMessage(logger, resourceUri, exception);
    }

    private sealed record HttpResourceCacheRequestOptions(string? FileNameHint, bool ForceRefresh, TimeSpan? TimeToLiveOverride)
    {
        public static HttpResourceCacheRequestOptions Default { get; } = new(FileNameHint: null, ForceRefresh: false, TimeToLiveOverride: null);
    }

    internal sealed class CachedHttpResponseInfo(CachedHttpResource? resource, bool usedFallbackCache)
    {
        public static CachedHttpResponseInfo None { get; } = new(resource: null, usedFallbackCache: false);

        public CachedHttpResource? Resource { get; } = resource;

        public bool UsedFallbackCache { get; } = usedFallbackCache;
    }
}
