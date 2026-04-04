// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Common.Services;

internal sealed class HttpResourceCache
{
    private const string MetadataFileName = "metadata.json";
    private const string DefaultPayloadFileName = "payload.bin";
    private static readonly Action<MEL.ILogger, string, Exception?> LogFailedToCacheHttpResourceMessage = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(0, nameof(LogFailedToCacheHttpResource)),
        "Failed to cache HTTP resource '{ResourceUri}'.");

    private static readonly Action<MEL.ILogger, string, Exception?> LogFailedToEnumerateHttpResourceCacheMessage = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(1, nameof(LogFailedToEnumerateHttpResourceCache)),
        "Failed to enumerate HTTP resource cache '{CacheDirectory}' for pruning.");

    private static readonly Action<MEL.ILogger, string, Exception?> LogFailedToLoadCachedMetadataMessage = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(2, nameof(LogFailedToLoadCachedMetadata)),
        "Failed to load cached metadata from '{MetadataPath}'.");

    private static readonly Action<MEL.ILogger, string, Exception?> LogFailedToSaveCachedMetadataMessage = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(3, nameof(LogFailedToSaveCachedMetadata)),
        "Failed to save cached metadata to '{MetadataPath}'.");

    private static readonly Action<MEL.ILogger, string, Exception?> LogFailedToDeleteCachedHttpResourceDirectoryMessage = LoggerMessage.Define<string>(
        LogLevel.Error,
        new EventId(4, nameof(LogFailedToDeleteCachedHttpResourceDirectory)),
        "Failed to delete cached HTTP resource directory '{EntryDirectory}'.");

    private readonly HttpClient _httpClient;
    private readonly string _cacheDirectory;
    private readonly TimeSpan _defaultTimeToLive;
    private readonly MEL.ILogger _logger;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Task<CachedHttpResourceFetchResult?>> _inflightFetches = new(StringComparer.Ordinal);

    public HttpResourceCache(HttpClient httpClient, string cacheDirectory, TimeSpan defaultTimeToLive, MEL.ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _cacheDirectory = cacheDirectory;
        _defaultTimeToLive = defaultTimeToLive;
        _logger = logger;

        Directory.CreateDirectory(_cacheDirectory);
    }

    public Task<CachedHttpResource?> GetOrFetchAsync(
        Uri resourceUri,
        string? fileNameHint = null,
        CancellationToken cancellationToken = default)
    {
        return GetOrFetchAsync(resourceUri, fileNameHint, forceRefresh: false, timeToLiveOverride: null, cancellationToken);
    }

    public async Task<CachedHttpResource?> GetOrFetchAsync(
        Uri resourceUri,
        string? fileNameHint,
        bool forceRefresh,
        TimeSpan? timeToLiveOverride,
        CancellationToken cancellationToken = default)
    {
        var fetchResult = await GetOrFetchWithStatusAsync(resourceUri, fileNameHint, forceRefresh, timeToLiveOverride, cancellationToken);
        return fetchResult?.Resource;
    }

    public Task<CachedHttpResourceFetchResult?> GetOrFetchWithStatusAsync(
        Uri resourceUri,
        string? fileNameHint = null,
        bool forceRefresh = false,
        TimeSpan? timeToLiveOverride = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resourceUri);

        if (!IsSupportedHttpUri(resourceUri))
        {
            return Task.FromResult<CachedHttpResourceFetchResult?>(null);
        }

        var inflightKey = resourceUri.AbsoluteUri;

        lock (_lock)
        {
            if (_inflightFetches.TryGetValue(inflightKey, out var existingTask))
            {
                return existingTask;
            }

            var fetchTask = GetOrFetchCoreAsync(resourceUri, fileNameHint, forceRefresh, timeToLiveOverride, cancellationToken);
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

    public void Prune(IEnumerable<Uri> retainedResourceUris)
    {
        ArgumentNullException.ThrowIfNull(retainedResourceUris);

        HashSet<string> retainedEntryDirectories = new(StringComparer.OrdinalIgnoreCase);
        foreach (var retainedResourceUri in retainedResourceUris)
        {
            if (!IsSupportedHttpUri(retainedResourceUri))
            {
                continue;
            }

            retainedEntryDirectories.Add(Path.GetFullPath(GetEntryDirectory(retainedResourceUri)));
        }

        lock (_lock)
        {
            foreach (var inflightKey in _inflightFetches.Keys)
            {
                if (!Uri.TryCreate(inflightKey, UriKind.Absolute, out var inflightUri)
                    || !IsSupportedHttpUri(inflightUri))
                {
                    continue;
                }

                retainedEntryDirectories.Add(Path.GetFullPath(GetEntryDirectory(inflightUri)));
            }
        }

        try
        {
            foreach (var entryDirectory in Directory.EnumerateDirectories(_cacheDirectory))
            {
                var fullEntryDirectory = Path.GetFullPath(entryDirectory);
                if (retainedEntryDirectories.Contains(fullEntryDirectory))
                {
                    continue;
                }

                TryDeleteEntryDirectory(fullEntryDirectory);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            LogFailedToEnumerateHttpResourceCache(_logger, _cacheDirectory, ex);
        }
    }

    private async Task<CachedHttpResourceFetchResult?> GetOrFetchCoreAsync(
        Uri resourceUri,
        string? fileNameHint,
        bool forceRefresh,
        TimeSpan? timeToLiveOverride,
        CancellationToken cancellationToken)
    {
        var entryDirectory = GetEntryDirectory(resourceUri);
        Directory.CreateDirectory(entryDirectory);

        var metadataPath = Path.Combine(entryDirectory, MetadataFileName);
        var metadata = TryLoadMetadata(metadataPath);
        var payloadFileName = ResolvePayloadFileName(resourceUri, fileNameHint, metadata);
        var payloadPath = Path.Combine(entryDirectory, payloadFileName);

        if (!forceRefresh && File.Exists(payloadPath) && IsFresh(metadata, timeToLiveOverride))
        {
            return CreateFetchResult(payloadPath, metadata, fromCache: true, wasRevalidated: false, usedFallbackCache: false);
        }

        try
        {
            using var request = CreateRequest(resourceUri, metadata);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotModified && File.Exists(payloadPath))
            {
                var refreshedMetadata = UpdateMetadata(metadata, resourceUri, response, payloadFileName, DateTimeOffset.UtcNow);
                TrySaveMetadata(metadataPath, refreshedMetadata);
                return CreateFetchResult(payloadPath, refreshedMetadata, fromCache: true, wasRevalidated: true, usedFallbackCache: false);
            }

            response.EnsureSuccessStatusCode();

            var tempPath = Path.Combine(entryDirectory, $"{payloadFileName}.tmp");
            try
            {
                await using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using (var destinationStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
                {
                    await sourceStream.CopyToAsync(destinationStream, cancellationToken);
                }

                File.Move(tempPath, payloadPath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }

            var updatedMetadata = UpdateMetadata(metadata, resourceUri, response, payloadFileName, DateTimeOffset.UtcNow);
            TrySaveMetadata(metadataPath, updatedMetadata);
            return CreateFetchResult(payloadPath, updatedMetadata, fromCache: false, wasRevalidated: metadata is not null, usedFallbackCache: false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            LogFailedToCacheHttpResource(_logger, resourceUri.AbsoluteUri, ex);

            if (File.Exists(payloadPath))
            {
                return CreateFetchResult(payloadPath, metadata, fromCache: true, wasRevalidated: false, usedFallbackCache: true);
            }

            return null;
        }
    }

    private HttpRequestMessage CreateRequest(Uri resourceUri, HttpResourceCacheMetadata? metadata)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, resourceUri);
        if (!string.IsNullOrWhiteSpace(metadata?.ETag))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", metadata.ETag);
        }

        if (metadata?.LastModifiedUtc is { } lastModifiedUtc)
        {
            request.Headers.IfModifiedSince = lastModifiedUtc;
        }

        return request;
    }

    private bool IsFresh(HttpResourceCacheMetadata? metadata, TimeSpan? timeToLiveOverride)
    {
        if (metadata is null)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        if (metadata.ExpiresUtc is { } expiresUtc)
        {
            return expiresUtc > now;
        }

        var effectiveTimeToLive = timeToLiveOverride ?? _defaultTimeToLive;
        return metadata.LastValidatedUtc + effectiveTimeToLive > now;
    }

    private static CachedHttpResource CreateCachedResource(
        string payloadPath,
        HttpResourceCacheMetadata? metadata,
        bool fromCache,
        bool wasRevalidated)
    {
        return new CachedHttpResource(
            payloadPath,
            metadata?.ContentType,
            fromCache,
            wasRevalidated);
    }

    private static CachedHttpResourceFetchResult CreateFetchResult(
        string payloadPath,
        HttpResourceCacheMetadata? metadata,
        bool fromCache,
        bool wasRevalidated,
        bool usedFallbackCache)
    {
        return new CachedHttpResourceFetchResult(
            CreateCachedResource(payloadPath, metadata, fromCache, wasRevalidated),
            usedFallbackCache);
    }

    private static HttpResourceCacheMetadata UpdateMetadata(
        HttpResourceCacheMetadata? metadata,
        Uri resourceUri,
        HttpResponseMessage response,
        string payloadFileName,
        DateTimeOffset now)
    {
        return new HttpResourceCacheMetadata
        {
            ContentType = response.Content?.Headers.ContentType?.MediaType ?? metadata?.ContentType,
            ETag = response.Headers.ETag?.ToString() ?? metadata?.ETag,
            ExpiresUtc = GetExpirationUtc(response, now),
            FileName = payloadFileName,
            LastModifiedUtc = response.Content?.Headers.LastModified ?? metadata?.LastModifiedUtc,
            LastValidatedUtc = now,
            SourceUri = resourceUri.AbsoluteUri,
        };
    }

    private static DateTimeOffset? GetExpirationUtc(HttpResponseMessage response, DateTimeOffset now)
    {
        if (response.Headers.CacheControl?.MaxAge is { } maxAge)
        {
            return now + maxAge;
        }

        return response.Content?.Headers.Expires;
    }

    private string GetEntryDirectory(Uri resourceUri)
    {
        var normalizedResourceName = BuildEntryName(resourceUri);
        if (normalizedResourceName.Length > 48)
        {
            normalizedResourceName = normalizedResourceName[..48];
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(resourceUri.AbsoluteUri)));
        return Path.Combine(_cacheDirectory, $"{normalizedResourceName}_{hash}");
    }

    private static string BuildEntryName(Uri resourceUri)
    {
        var host = SanitizeFileName(resourceUri.Host);
        var fileName = SanitizeFileName(Path.GetFileName(Uri.UnescapeDataString(resourceUri.AbsolutePath)));

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = DefaultPayloadFileName;
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            return fileName;
        }

        return $"{host}_{fileName}";
    }

    private static string ResolvePayloadFileName(Uri resourceUri, string? fileNameHint, HttpResourceCacheMetadata? metadata)
    {
        var candidate = metadata?.FileName;
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            return candidate;
        }

        candidate = fileNameHint;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = Path.GetFileName(Uri.UnescapeDataString(resourceUri.AbsolutePath));
        }

        candidate = SanitizeFileName(candidate);
        return string.IsNullOrWhiteSpace(candidate) ? DefaultPayloadFileName : candidate;
    }

    private static string SanitizeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        StringBuilder builder = new(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            builder.Append(Path.GetInvalidFileNameChars().Contains(current) ? '_' : current);
        }

        return builder
            .ToString()
            .Trim()
            .Trim('.', ' ');
    }

    private static bool IsSupportedHttpUri(Uri resourceUri)
    {
        return resourceUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || resourceUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private HttpResourceCacheMetadata? TryLoadMetadata(string metadataPath)
    {
        try
        {
            if (!File.Exists(metadataPath))
            {
                return null;
            }

            var json = File.ReadAllText(metadataPath);
            return JsonSerializer.Deserialize(json, HttpResourceCacheJsonContext.Default.HttpResourceCacheMetadata) as HttpResourceCacheMetadata;
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            LogFailedToLoadCachedMetadata(_logger, metadataPath, ex);
            return null;
        }
    }

    private void TrySaveMetadata(string metadataPath, HttpResourceCacheMetadata metadata)
    {
        try
        {
            var json = JsonSerializer.Serialize(metadata, HttpResourceCacheJsonContext.Default.HttpResourceCacheMetadata);
            File.WriteAllText(metadataPath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogFailedToSaveCachedMetadata(_logger, metadataPath, ex);
        }
    }

    private void TryDeleteEntryDirectory(string entryDirectory)
    {
        try
        {
            Directory.Delete(entryDirectory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
        {
            LogFailedToDeleteCachedHttpResourceDirectory(_logger, entryDirectory, ex);
        }
    }

    private static void LogFailedToCacheHttpResource(MEL.ILogger logger, string resourceUri, Exception exception)
    {
        LogFailedToCacheHttpResourceMessage(logger, resourceUri, exception);
    }

    private static void LogFailedToEnumerateHttpResourceCache(MEL.ILogger logger, string cacheDirectory, Exception exception)
    {
        LogFailedToEnumerateHttpResourceCacheMessage(logger, cacheDirectory, exception);
    }

    private static void LogFailedToLoadCachedMetadata(MEL.ILogger logger, string metadataPath, Exception exception)
    {
        LogFailedToLoadCachedMetadataMessage(logger, metadataPath, exception);
    }

    private static void LogFailedToSaveCachedMetadata(MEL.ILogger logger, string metadataPath, Exception exception)
    {
        LogFailedToSaveCachedMetadataMessage(logger, metadataPath, exception);
    }

    private static void LogFailedToDeleteCachedHttpResourceDirectory(MEL.ILogger logger, string entryDirectory, Exception exception)
    {
        LogFailedToDeleteCachedHttpResourceDirectoryMessage(logger, entryDirectory, exception);
    }

    internal sealed class CachedHttpResource
    {
        public CachedHttpResource(string contentPath, string? contentType, bool fromCache, bool wasRevalidated)
        {
            ContentPath = Path.GetFullPath(contentPath);
            ContentType = contentType;
            FromCache = fromCache;
            WasRevalidated = wasRevalidated;
        }

        public string ContentPath { get; }

        public Uri ContentUri => new(ContentPath);

        public string? ContentType { get; }

        public bool FromCache { get; }

        public bool WasRevalidated { get; }
    }

    internal sealed class CachedHttpResourceFetchResult(CachedHttpResource resource, bool usedFallbackCache)
    {
        public CachedHttpResource Resource { get; } = resource;

        public bool UsedFallbackCache { get; } = usedFallbackCache;
    }
}
