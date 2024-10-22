// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

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
    private HashSet<string> _savedUserPrompts = [];

    private static string Version => Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString() ?? string.Empty;

    public CustomActionKernelQueryCacheService(IUserSettings userSettings)
    {
        _userSettings = userSettings;
        _userSettings.Changed += OnUserSettingsChanged;

        RefreshSavedUserPrompts();

        _memoryCache = LoadPersistedCacheItems().Where(pair => pair.CacheKey != null)
                                                .GroupBy(pair => pair.CacheKey, pair => pair.CacheValue)
                                                .ToDictionary(group => group.Key, group => group.First());

        RemoveInapplicableCacheKeys();

        Logger.LogDebug($"Kernel query cache initialized with {_memoryCache.Count} items");
    }

    public async Task WriteAsync(CacheKey key, CacheValue value)
    {
        if (_savedUserPrompts.Contains(key.Prompt))
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
            var persistedCache = _settingsUtil.GetSettings<PersistedCache>(AdvancedPasteSettings.ModuleName, PersistedCacheFileName);

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
        catch (FileNotFoundException)
        {
            return [];
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to load kernel query cache", ex);
            return [];
        }
    }

    private async void OnUserSettingsChanged(object sender, EventArgs e)
    {
        RefreshSavedUserPrompts();

        if (RemoveInapplicableCacheKeys())
        {
            await SaveAsync();
        }
    }

    private void RefreshSavedUserPrompts()
    {
        _savedUserPrompts = _userSettings.CustomActions
                                         .Select(customAction => customAction.Prompt)
                                         .ToHashSet(CacheKey.PromptComparer);
    }

    private bool RemoveInapplicableCacheKeys()
    {
        var keysToRemove = _memoryCache.Keys
                                       .Where(key => !_savedUserPrompts.Contains(key.Prompt))
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
