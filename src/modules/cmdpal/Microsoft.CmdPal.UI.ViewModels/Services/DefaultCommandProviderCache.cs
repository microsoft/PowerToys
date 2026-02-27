// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

public sealed partial class DefaultCommandProviderCache : ICommandProviderCache, IDisposable
{
    private const string CacheFileName = "commandProviderCache.json";

    private readonly Dictionary<string, CommandProviderCacheItem> _cache = new(StringComparer.Ordinal);

    private readonly Lock _sync = new();

    private readonly SupersedingAsyncGate _saveGate;

    private readonly ILogger<DefaultCommandProviderCache> _logger;

    public DefaultCommandProviderCache(ILogger<DefaultCommandProviderCache> logger)
    {
        _logger = logger;
        _saveGate = new SupersedingAsyncGate(async _ => await TrySaveAsync().ConfigureAwait(false));
        TryLoad();
    }

    public void Memorize(string providerId, CommandProviderCacheItem item)
    {
        ArgumentNullException.ThrowIfNull(providerId);

        lock (_sync)
        {
            _cache[providerId] = item;
        }

        _ = _saveGate.ExecuteAsync();
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
            var path = GetCacheFilePath();
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
            Log_FailedLoadingCommandProviderCache(ex);
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

            var path = GetCacheFilePath();
            var json = JsonSerializer.Serialize(container, CommandProviderCacheSerializationContext.Default.CommandProviderCacheContainer!);
            await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log_FailedSavingCommandProviderCache(ex);
        }
    }

    public void Dispose()
    {
        _saveGate.Dispose();
        GC.SuppressFinalize(this);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to save command provider cache")]
    partial void Log_FailedSavingCommandProviderCache(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load command provider cache")]
    partial void Log_FailedLoadingCommandProviderCache(Exception ex);
}
