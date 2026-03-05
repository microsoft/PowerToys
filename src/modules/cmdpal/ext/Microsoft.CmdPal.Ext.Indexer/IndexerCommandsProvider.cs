// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.CmdPal.Ext.Indexer.Data;
using Microsoft.CmdPal.Ext.Indexer.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation.Metadata;

namespace Microsoft.CmdPal.Ext.Indexer;

public partial class IndexerCommandsProvider : CommandProvider
{
    private readonly FallbackOpenFileItem _fallbackFileItem = new();
    private readonly string _pinCachePath;
    private readonly object _diskCacheLock = new();

    public IndexerCommandsProvider()
    {
        Id = "Files";
        DisplayName = Resources.IndexerCommandsProvider_DisplayName;
        Icon = Icons.FileExplorerIcon;

        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);
        _pinCachePath = Path.Combine(directory, "indexer_pin_cache.json");

        if (IndexerListItem.IsActionsFeatureEnabled && ApiInformation.IsApiContractPresent("Windows.AI.Actions.ActionsContract", 4))
        {
            _ = ActionRuntimeManager.InstanceAsync;
        }
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return [
            new CommandItem(new IndexerPage())
            {
                Title = Resources.Indexer_Title,
            }
        ];
    }

    public override IFallbackCommandItem[] FallbackCommands() =>
        [
            _fallbackFileItem
        ];

    public void SuppressFallbackWhen(Func<string, bool> callback)
    {
        _fallbackFileItem.SuppressFallbackWhen(callback);
    }

    public override ICommandItem[] GetDockBands()
    {
        // Return everything we have in our disk cache as WrappedDockItems.
        var diskCache = LoadDiskCache();
        var dockItems = new List<CommandItem>();
        foreach (var kvp in diskCache)
        {
            var id = kvp.Key;
            var path = kvp.Value;

            if (Path.Exists(path))
            {
                var indexerItem = new IndexerItem(path);
                var listItem = new IndexerListItem(indexerItem, browseByDefault: IncludeBrowseCommand.AsDefault);
                var dockItem = new WrappedDockItem(new[] { listItem }, id, listItem.Title);
                dockItems.Add(dockItem);
            }
            else
            {
                // File no longer exists — prune from disk cache
                RemoveFromDiskCache(id);
            }
        }

        return dockItems.ToArray();
    }

    public override ICommandItem GetCommandItem(string id)
    {
        // 1. Try in-memory cache first (covers items from current search session)
        if (IndexerListItem.IdToPathCache.TryGetValue(id, out var path))
        {
            return ResolveItemFromPath(id, path);
        }

        // 2. Fall back to disk cache (covers app restarts)
        var diskCache = LoadDiskCache();
        if (diskCache.TryGetValue(id, out var diskPath))
        {
            return ResolveItemFromPath(id, diskPath);
        }

        return null;
    }

    private ICommandItem ResolveItemFromPath(string id, string fullPath)
    {
        if (!Path.Exists(fullPath))
        {
            // File no longer exists — prune from disk cache
            RemoveFromDiskCache(id);
            return null;
        }

        // Persist to disk cache so it survives restarts
        PersistToDiskCache(id, fullPath);

        var indexerItem = new IndexerItem(fullPath);
        var listItem = new IndexerListItem(indexerItem, browseByDefault: IncludeBrowseCommand.Include);
        return listItem;
    }

    private Dictionary<string, string> LoadDiskCache()
    {
        lock (_diskCacheLock)
        {
            try
            {
                if (!File.Exists(_pinCachePath))
                {
                    return new Dictionary<string, string>();
                }

                var json = File.ReadAllText(_pinCachePath);
                return JsonSerializer.Deserialize(json, IndexerPinCacheContext.Default.DictionaryStringString)
                       ?? new Dictionary<string, string>();
            }
            catch (Exception)
            {
                return new Dictionary<string, string>();
            }
        }
    }

    private void PersistToDiskCache(string id, string fullPath)
    {
        lock (_diskCacheLock)
        {
            try
            {
                var cache = LoadDiskCacheUnsafe();
                cache[id] = fullPath;
                WriteDiskCache(cache);
            }
            catch (Exception)
            {
            }
        }
    }

    private void RemoveFromDiskCache(string id)
    {
        lock (_diskCacheLock)
        {
            try
            {
                var cache = LoadDiskCacheUnsafe();
                if (cache.Remove(id))
                {
                    WriteDiskCache(cache);
                }
            }
            catch (Exception)
            {
            }
        }
    }

    /// <summary>Must be called under _diskCacheLock.</summary>
    private Dictionary<string, string> LoadDiskCacheUnsafe()
    {
        if (!File.Exists(_pinCachePath))
        {
            return new Dictionary<string, string>();
        }

        var json = File.ReadAllText(_pinCachePath);
        return JsonSerializer.Deserialize(json, IndexerPinCacheContext.Default.DictionaryStringString)
               ?? new Dictionary<string, string>();
    }

    /// <summary>Must be called under _diskCacheLock.</summary>
    private void WriteDiskCache(Dictionary<string, string> cache)
    {
        var json = JsonSerializer.Serialize(cache, IndexerPinCacheContext.Default.DictionaryStringString);
        File.WriteAllText(_pinCachePath, json);
    }
}
