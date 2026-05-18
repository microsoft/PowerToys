// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Services.HttpCaching.Abstraction;
using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Common.Services.HttpCaching;

internal sealed partial class HttpCachingClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly IHttpResourceCacheStore _cacheStore;
    private readonly HttpResourceCacheHandler _cacheHandler;

    public HttpCachingClient(
        string cacheDirectory,
        TimeSpan defaultTimeToLive,
        TimeSpan timeout,
        string? userAgent,
        HttpMessageHandler? innerHandler,
        MEL.ILogger logger)
        : this(
            new FileSystemHttpResourceCacheStore(cacheDirectory, defaultTimeToLive, logger),
            timeout,
            userAgent,
            innerHandler,
            logger)
    {
    }

    public HttpCachingClient(
        IHttpResourceCacheStore cacheStore,
        TimeSpan timeout,
        string? userAgent,
        HttpMessageHandler? innerHandler,
        MEL.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(cacheStore);
        ArgumentNullException.ThrowIfNull(logger);

        _cacheStore = cacheStore;
        _cacheHandler = new HttpResourceCacheHandler(cacheStore, innerHandler ?? new HttpClientHandler(), logger);
        _httpClient = new HttpClient(_cacheHandler) { Timeout = timeout };

        if (!string.IsNullOrWhiteSpace(userAgent))
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        }
    }

    public async Task<CachedHttpFetchResult> GetResourceAsync(
        Uri resourceUri,
        string? fileNameHint = null,
        bool forceRefresh = false,
        TimeSpan? timeToLiveOverride = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resourceUri);

        if (!IsSupportedHttpUri(resourceUri))
        {
            throw new InvalidOperationException($"Unsupported HTTP resource URI scheme '{resourceUri.Scheme}'.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, resourceUri);
        HttpResourceCacheHandler.ConfigureRequest(
            request,
            fileNameHint: fileNameHint,
            forceRefresh: forceRefresh,
            timeToLiveOverride: timeToLiveOverride);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var cacheInfo = HttpResourceCacheHandler.GetResponseInfo(response);
        if (cacheInfo.Resource is null)
        {
            throw new InvalidOperationException($"The HTTP cache did not produce a cached resource for '{resourceUri}'.");
        }

        return new CachedHttpFetchResult(cacheInfo.Resource, cacheInfo.UsedFallbackCache);
    }

    public void Prune(IEnumerable<Uri> retainedResourceUris)
    {
        ArgumentNullException.ThrowIfNull(retainedResourceUris);

        List<Uri> retainedUris = [.. retainedResourceUris];
        _cacheHandler.AddInflightResourceUris(retainedUris);
        _cacheStore.Prune(retainedUris);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private static bool IsSupportedHttpUri(Uri resourceUri)
    {
        return resourceUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || resourceUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }
}
