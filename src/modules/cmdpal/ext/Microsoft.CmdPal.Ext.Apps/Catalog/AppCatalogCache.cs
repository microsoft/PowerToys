// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using MEL = Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.Ext.Apps.Catalog;

internal sealed partial class AppCatalogCache : IAppCatalogCache
{
    internal static readonly TimeSpan MaximumAge = TimeSpan.FromHours(36);

    private readonly string _cachePath;
    private readonly MEL.ILogger<AppCatalogCache> _logger;
    private AppCatalogCacheFile? _lastCache;

    public AppCatalogCache(string cachePath, MEL.ILogger<AppCatalogCache>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cachePath);
        _cachePath = cachePath;
        _logger = logger ?? NullLogger<AppCatalogCache>.Instance;
    }

    public static string DefaultPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        return Path.Combine(directory, "apps.catalog.json");
    }

    public async Task<AppCatalogCacheFile?> LoadAsync(
        AppCatalogCacheContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!File.Exists(_cachePath))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(_cachePath);
            var cache = await JsonSerializer.DeserializeAsync(
                stream,
                JsonSerializationContext.Default.AppCatalogCacheFile,
                cancellationToken).ConfigureAwait(false);

            if (cache?.IsCompatible(context) != true || cache.Sources is null)
            {
                LogIncompatibleCacheIgnored(_logger);
                return null;
            }

            var validCache = new AppCatalogCacheFile { Language = cache.Language };
            var itemCount = 0;
            foreach (var source in cache.Sources)
            {
                if (!IsSourceSnapshotValid(source, context))
                {
                    continue;
                }

                validCache.Sources.Add(source);
                itemCount += source.Items.Count;
            }

            _lastCache = validCache;
            if (validCache.Sources.Count == 0)
            {
                LogCacheWithoutValidSnapshotsIgnored(_logger);
                return null;
            }

            LogCacheLoaded(_logger, itemCount, validCache.Sources.Count);
            return validCache;
        }
        catch (Exception ex) when (
            ex is IOException
                or UnauthorizedAccessException
                or JsonException
                or NotSupportedException
                or ArgumentException)
        {
            LogCacheReadFailed(_logger, ex);
            return null;
        }
    }

    public async Task SaveAsync(
        IReadOnlyDictionary<string, IReadOnlyList<AppCatalogItem>> sourceSnapshots,
        IReadOnlyCollection<string> fullyReconciledSourceIds,
        AppCatalogCacheContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceSnapshots);
        ArgumentNullException.ThrowIfNull(fullyReconciledSourceIds);
        ArgumentNullException.ThrowIfNull(context);

        var directory = Path.GetDirectoryName(_cachePath);
        if (string.IsNullOrEmpty(directory))
        {
            return;
        }

        var previousById = new Dictionary<string, AppCatalogSourceSnapshot>(StringComparer.Ordinal);
        if (_lastCache is not null)
        {
            foreach (var previous in _lastCache.Sources)
            {
                previousById[previous.SourceId] = previous;
            }
        }

        var fullyReconciledSources = new HashSet<string>(fullyReconciledSourceIds, StringComparer.Ordinal);
        var cache = new AppCatalogCacheFile { Language = context.Language };
        var sourceIds = new List<string>(sourceSnapshots.Keys);
        sourceIds.Sort(StringComparer.Ordinal);
        foreach (var sourceId in sourceIds)
        {
            if (!context.SourceKeys.TryGetValue(sourceId, out var sourceKey))
            {
                continue;
            }

            var items = sourceSnapshots[sourceId];
            var validatedAtUtc = context.NowUtc;
            if (previousById.TryGetValue(sourceId, out var previous)
                && string.Equals(previous.SourceKey, sourceKey, StringComparison.Ordinal)
                && !fullyReconciledSources.Contains(sourceId)
                && HasSameItems(previous.Items, items))
            {
                validatedAtUtc = previous.ValidatedAtUtc;
            }

            cache.Sources.Add(new AppCatalogSourceSnapshot
            {
                SourceId = sourceId,
                SourceKey = sourceKey,
                ValidatedAtUtc = validatedAtUtc,
                Items = new List<AppCatalogItem>(items),
            });
        }

        if (_lastCache is not null && HasSameContent(_lastCache, cache))
        {
            return;
        }

        var temporaryPath = $"{_cachePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(directory);
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    cache,
                    JsonSerializationContext.Default.AppCatalogCacheFile,
                    cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, _cachePath, overwrite: true);
            _lastCache = cache;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogCacheWriteFailed(_logger, ex);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogTemporaryCacheRemovalFailed(_logger, ex);
            }
        }
    }

    private static bool IsSourceSnapshotValid(
        AppCatalogSourceSnapshot source,
        AppCatalogCacheContext context)
    {
        if (source is null
            || string.IsNullOrWhiteSpace(source.SourceId)
            || source.Items is null
            || source.Items.Exists(static item => item is null)
            || !context.SourceKeys.TryGetValue(source.SourceId, out var sourceKey)
            || !string.Equals(source.SourceKey, sourceKey, StringComparison.Ordinal))
        {
            return false;
        }

        var age = context.NowUtc - source.ValidatedAtUtc;
        return age >= TimeSpan.Zero && age <= MaximumAge;
    }

    private static bool HasSameContent(AppCatalogCacheFile left, AppCatalogCacheFile right)
    {
        if (!string.Equals(left.Language, right.Language, StringComparison.OrdinalIgnoreCase)
            || left.Sources.Count != right.Sources.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Sources.Count; index++)
        {
            var leftSource = left.Sources[index];
            var rightSource = right.Sources[index];
            if (!string.Equals(leftSource.SourceId, rightSource.SourceId, StringComparison.Ordinal)
                || !string.Equals(leftSource.SourceKey, rightSource.SourceKey, StringComparison.Ordinal)
                || leftSource.ValidatedAtUtc != rightSource.ValidatedAtUtc
                || !HasSameItems(leftSource.Items, rightSource.Items))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasSameItems(
        IReadOnlyList<AppCatalogItem> left,
        IReadOnlyList<AppCatalogItem> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        var leftById = new Dictionary<string, AppCatalogItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in left)
        {
            if (!leftById.TryAdd(item.Identity, item))
            {
                return false;
            }
        }

        foreach (var item in right)
        {
            if (!leftById.TryGetValue(item.Identity, out var leftItem)
                || !leftItem.HasSamePersistedContent(item))
            {
                return false;
            }
        }

        return true;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "Ignored an application catalog cache with an incompatible schema or language.")]
    private static partial void LogIncompatibleCacheIgnored(MEL.ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Ignored an application catalog cache with no valid source snapshots.")]
    private static partial void LogCacheWithoutValidSnapshotsIgnored(MEL.ILogger logger);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "Loaded {ItemCount} applications from {SourceCount} valid catalog source snapshots.")]
    private static partial void LogCacheLoaded(MEL.ILogger logger, int itemCount, int sourceCount);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Failed to read the application catalog cache.")]
    private static partial void LogCacheReadFailed(MEL.ILogger logger, Exception exception);

    [LoggerMessage(EventId = 5, Level = LogLevel.Warning, Message = "Failed to write the application catalog cache.")]
    private static partial void LogCacheWriteFailed(MEL.ILogger logger, Exception exception);

    [LoggerMessage(EventId = 6, Level = LogLevel.Debug, Message = "Failed to remove a temporary application catalog cache file.")]
    private static partial void LogTemporaryCacheRemovalFailed(MEL.ILogger logger, Exception exception);
}
