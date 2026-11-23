// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using ManagedCommon;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

public sealed partial class DefaultCommandProviderCache : ICommandProviderCache, IDisposable, IAsyncDisposable
{
    private const string CacheFileName = "commandProviderCache.json";

    private readonly Dictionary<string, CommandProviderCacheItem> _cache = new(StringComparer.Ordinal);
    private readonly Lock _sync = new();
    private readonly string? _cacheFilePath;

    private readonly CoalescingAsyncRunner _saveRunner;

    public DefaultCommandProviderCache()
        : this(null)
    {
    }

    internal DefaultCommandProviderCache(string? cacheFilePath)
    {
        _cacheFilePath = cacheFilePath;
        _saveRunner = new CoalescingAsyncRunner(TrySaveAsync);
        TryLoad();
    }

    public void Memorize(string providerId, CommandProviderCacheItem item)
    {
        ArgumentNullException.ThrowIfNull(providerId);

        lock (_sync)
        {
            _cache[providerId] = item;
        }

        _ = _saveRunner.RequestAsync();
    }

    public CommandProviderCacheItem? Recall(string providerId)
    {
        ArgumentNullException.ThrowIfNull(providerId);

        lock (_sync)
        {
            _cache.TryGetValue(providerId, out var item);
            return item;
        }
    }

    private static string GetCacheFilePath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, CacheFileName);
    }

    private void TryLoad()
    {
        try
        {
            var path = _cacheFilePath ?? GetCacheFilePath();
            if (!File.Exists(path))
            {
                return;
            }

            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            var loaded = JsonSerializer.Deserialize(
                json,
                CommandProviderCacheSerializationContext.Default.CommandProviderCacheContainer!);
            if (loaded?.Cache is null)
            {
                return;
            }

            _cache.Clear();
            foreach (var kvp in loaded.Cache)
            {
                if (!string.IsNullOrEmpty(kvp.Key) && kvp.Value is not null)
                {
                    _cache[kvp.Key] = kvp.Value;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to load command provider cache: ", ex);
        }
    }

    private async Task TrySaveAsync()
    {
        try
        {
            Dictionary<string, CommandProviderCacheItem> snapshot;
            lock (_sync)
            {
                snapshot = new Dictionary<string, CommandProviderCacheItem>(_cache, StringComparer.Ordinal);
            }

            var container = new CommandProviderCacheContainer
            {
                Cache = snapshot,
            };

            var path = _cacheFilePath ?? GetCacheFilePath();
            var json = JsonSerializer.Serialize(container, CommandProviderCacheSerializationContext.Default.CommandProviderCacheContainer!);
            await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to save command provider cache: ", ex);
        }
    }

    internal Task WhenIdleAsync()
    {
        return _saveRunner.WhenIdleAsync();
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        return _saveRunner.DisposeAsync();
    }

    public void Dispose()
    {
        _saveRunner.Dispose();
        GC.SuppressFinalize(this);
    }
}
