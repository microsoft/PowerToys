// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using AdvancedPaste.Helpers;
using AdvancedPaste.Models;
using AdvancedPaste.Models.KernelQueryCache;
using AdvancedPaste.Settings;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;

namespace AdvancedPaste.Services;

public sealed class CustomActionKernelQueryCacheService : IKernelQueryCacheService
{
    private const string PersistedCacheFileName = "kernelQueryCache.json";

    private readonly SettingsUtils _settingsUtil = new();
    private readonly Dictionary<CacheKey, CacheValue> _memoryCache = [];
    private readonly IUserSettings _userSettings;

    private HashSet<string> _cacheablePrompts = [];

    private static string Version => Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString() ?? string.Empty;

    public CustomActionKernelQueryCacheService(IUserSettings userSettings)
    {
        _userSettings = userSettings;
        _userSettings.Changed += OnUserSettingsChanged;

        RefreshCacheablePrompts();

        _memoryCache = LoadPersistedCacheItems().Where(pair => pair.CacheKey != null)
                                                .GroupBy(pair => pair.CacheKey, pair => pair.CacheValue)
                                                .ToDictionary(group => group.Key, group => group.First());

        RemoveInapplicableCacheKeys();

        Logger.LogDebug($"Kernel query cache initialized with {_memoryCache.Count} items");
    }

    public async Task WriteAsync(CacheKey key, CacheValue value)
    {
        if (_cacheablePrompts.Contains(key.Prompt))
        {
            _memoryCache[key] = value;
            await SaveAsync();
        }
    }

    public CacheValue ReadOrNull(CacheKey key) => _memoryCache.GetValueOrDefault(key);

    private List<PersistedCache.CacheItem> LoadPersistedCacheItems()
    {
        try
        {
            if (!_settingsUtil.SettingsExists(AdvancedPasteSettings.ModuleName, PersistedCacheFileName))
            {
                return [];
            }

            var jsonString = File.ReadAllText(_settingsUtil.GetSettingsFilePath(AdvancedPasteSettings.ModuleName, PersistedCacheFileName));
            var persistedCache = PersistedCache.FromJsonString(jsonString);

            if (persistedCache.Version == Version)
            {
                return persistedCache.Items;
            }
            else
            {
                Logger.LogWarning($"Ignoring persisted kernel query cache; version mismatch - actual: {persistedCache.Version}, expected:{Version}");
                return [];
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to load kernel query cache", ex);
            return [];
        }
    }

    private async void OnUserSettingsChanged(object sender, EventArgs e)
    {
        RefreshCacheablePrompts();

        if (RemoveInapplicableCacheKeys())
        {
            await SaveAsync();
        }
    }

    private void RefreshCacheablePrompts()
    {
        var localizedActionNames = from metadata in PasteFormat.MetadataDict.Values
                                   where !string.IsNullOrEmpty(metadata.ResourceId)
                                   select ResourceLoaderInstance.ResourceLoader.GetString(metadata.ResourceId);

        var customActionPrompts = from customAction in _userSettings.CustomActions
                                  select customAction.Prompt;

        // Only cache queries with these prompts to prevent the cache from getting too large and to avoid potential privacy issues.
        _cacheablePrompts = localizedActionNames.Concat(customActionPrompts)
                                                .ToHashSet(CacheKey.PromptComparer);
    }

    private bool RemoveInapplicableCacheKeys()
    {
        var keysToRemove = _memoryCache.Keys
                                       .Where(key => !_cacheablePrompts.Contains(key.Prompt))
                                       .ToList();

        foreach (var key in keysToRemove)
        {
            _memoryCache.Remove(key);
        }

        return keysToRemove.Count > 0;
    }

    private async Task SaveAsync()
    {
        PersistedCache cache = new()
        {
            Version = Version,
            Items = _memoryCache.Select(pair => new PersistedCache.CacheItem(pair.Key, pair.Value)).ToList(),
        };

        _settingsUtil.SaveSettings(cache.ToJsonString(), AdvancedPasteSettings.ModuleName, PersistedCacheFileName);

        Logger.LogDebug($"Kernel query cache saved with {_memoryCache.Count} items");

        await Task.CompletedTask; // Async placeholder until _settingsUtil.SaveSettings has an async implementation
    }
}
