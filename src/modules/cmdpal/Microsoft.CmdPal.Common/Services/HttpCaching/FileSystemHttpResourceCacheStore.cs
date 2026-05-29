// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.CmdPal.Common.Services.HttpCaching.Abstraction;
using Microsoft.Extensions.Logging;
using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Common.Services.HttpCaching;

internal sealed class FileSystemHttpResourceCacheStore : IHttpResourceCacheStore
{
    private const string MetadataFileName = "metadata.json";
    private const string DefaultPayloadFileName = "payload.bin";
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

    private readonly string _cacheDirectory;
    private readonly TimeSpan _defaultTimeToLive;
    private readonly MEL.ILogger _logger;

    public FileSystemHttpResourceCacheStore(string cacheDirectory, TimeSpan defaultTimeToLive, MEL.ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cacheDirectory);
        ArgumentNullException.ThrowIfNull(logger);

        _cacheDirectory = cacheDirectory;
        _defaultTimeToLive = defaultTimeToLive;
        _logger = logger;

        Directory.CreateDirectory(_cacheDirectory);
    }

    public CachedHttpResourceEntry GetEntry(Uri resourceUri, string? fileNameHint = null)
    {
        ArgumentNullException.ThrowIfNull(resourceUri);

        var entryDirectory = GetEntryDirectory(resourceUri);
        Directory.CreateDirectory(entryDirectory);

        var metadataPath = Path.Combine(entryDirectory, MetadataFileName);
        var metadata = TryLoadMetadata(metadataPath);
        var payloadFileName = ResolvePayloadFileName(resourceUri, fileNameHint, metadata);
        var payloadPath = Path.Combine(entryDirectory, payloadFileName);

        return new CachedHttpResourceEntry(
            resourceUri,
            entryDirectory,
            metadataPath,
            payloadPath,
            payloadFileName,
            metadata);
    }

    public CachedHttpResource? TryGetFresh(CachedHttpResourceEntry entry, TimeSpan? timeToLiveOverride)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (!File.Exists(entry.PayloadPath) || !IsFresh(entry.Metadata, timeToLiveOverride))
        {
            return null;
        }

        return CreateCachedResource(entry.PayloadPath, entry.Metadata, fromCache: true, wasRevalidated: false);
    }

    public CachedHttpResource? TryGetCached(CachedHttpResourceEntry entry, bool fromCache, bool wasRevalidated)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (!File.Exists(entry.PayloadPath))
        {
            return null;
        }

        return CreateCachedResource(entry.PayloadPath, entry.Metadata, fromCache, wasRevalidated);
    }

    public CachedHttpResource? UpdateAfterNotModified(CachedHttpResourceEntry entry, HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(response);

        if (!File.Exists(entry.PayloadPath))
        {
            return null;
        }

        var refreshedMetadata = UpdateMetadata(entry.Metadata, entry.ResourceUri, response, entry.PayloadFileName, DateTimeOffset.UtcNow);
        TrySaveMetadata(entry.MetadataPath, refreshedMetadata);
        return CreateCachedResource(entry.PayloadPath, refreshedMetadata, fromCache: true, wasRevalidated: true);
    }

    public async Task<CachedHttpResource> SaveResponseAsync(CachedHttpResourceEntry entry, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(response.Content);

        var tempPath = Path.Combine(entry.EntryDirectory, $"{entry.PayloadFileName}.tmp");
        try
        {
            await using var sourceStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using (var destinationStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            {
                await sourceStream.CopyToAsync(destinationStream, cancellationToken);
            }

            File.Move(tempPath, entry.PayloadPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        var updatedMetadata = UpdateMetadata(entry.Metadata, entry.ResourceUri, response, entry.PayloadFileName, DateTimeOffset.UtcNow);
        TrySaveMetadata(entry.MetadataPath, updatedMetadata);
        return CreateCachedResource(entry.PayloadPath, updatedMetadata, fromCache: false, wasRevalidated: entry.Metadata is not null);
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
}
