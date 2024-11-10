// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO.Abstractions;
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

/// <summary>
/// Implements <see cref="IKernelQueryCacheService"/> by only caching queries with prompts
/// that correspond to the user's custom actions or to the localized names of bundled actions.
/// This avoids potential privacy issues and prevents the cache from getting too large.
/// </summary>
public sealed class CustomActionKernelQueryCacheService : IKernelQueryCacheService
{
    private const string PersistedCacheFileName = "kernelQueryCache.json";

    private readonly HashSet<string> _cacheablePrompts = new(CacheKey.PromptComparer);
    private readonly Dictionary<CacheKey, CacheValue> _memoryCache = [];

    private readonly IUserSettings _userSettings;
    private readonly IFileSystem _fileSystem;
    private readonly SettingsUtils _settingsUtil;

    private static string Version => Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString() ?? string.Empty;

    public CustomActionKernelQueryCacheService(IUserSettings userSettings, IFileSystem fileSystem)
    {
        _userSettings = userSettings;
        _fileSystem = fileSystem;
        _settingsUtil = new SettingsUtils(fileSystem);

        _userSettings.Changed += OnUserSettingsChanged;

        UpdateCacheablePrompts();

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

            var jsonString = _fileSystem.File.ReadAllText(_settingsUtil.GetSettingsFilePath(AdvancedPasteSettings.ModuleName, PersistedCacheFileName));
            var persistedCache = PersistedCache.FromJsonString(jsonString);

            if (persistedCache.Version == Version)
            {
                return persistedCache.Items;
            }
            else
            {
                Logger.LogWarning($"Ignoring persisted kernel query cache; version mismatch - actual: {persistedCache.Version}, expected: {Version}");
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
        UpdateCacheablePrompts();

        if (RemoveInapplicableCacheKeys())
        {
            await SaveAsync();
        }
    }

    private void UpdateCacheablePrompts()
    {
        var localizedActionNames = from pair in PasteFormat.MetadataDict
                                   let format = pair.Key
                                   let metadata = pair.Value
                                   where !string.IsNullOrEmpty(metadata.ResourceId)
                                   where metadata.IsCoreAction || _userSettings.AdditionalActions.Contains(format)
                                   select ResourceLoaderInstance.ResourceLoader.GetString(metadata.ResourceId);

        var customActionPrompts = from customAction in _userSettings.CustomActions
                                  select customAction.Prompt;

        _cacheablePrompts.Clear();
        _cacheablePrompts.UnionWith(localizedActionNames.Concat(customActionPrompts));
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

        Logger.LogDebug($"Kernel query cache saved with {_memoryCache.Count} item(s)");

        await Task.CompletedTask; // Async placeholder until _settingsUtil.SaveSettings has an async implementation
    }
}
