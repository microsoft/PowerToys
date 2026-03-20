// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using ManagedCommon;
using Microsoft.CmdPal.Common.Services;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
/// Default implementation of <see cref="ISettingsService"/>.
/// Handles loading, saving, migration, and change notification for <see cref="SettingsModel"/>.
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private const string DeprecatedHotkeyGoesHomeKey = "HotkeyGoesHome";

    private readonly IPersistenceService _persistence;
    private readonly IApplicationInfoService _appInfoService;
    private readonly string _filePath;

    public SettingsService(IPersistenceService persistence, IApplicationInfoService appInfoService)
    {
        _persistence = persistence;
        _appInfoService = appInfoService;
        _filePath = SettingsJsonPath();
        Settings = _persistence.Load(_filePath, JsonSerializationContext.Default.SettingsModel);
        ApplyMigrations();
    }

    /// <inheritdoc/>
    public SettingsModel Settings { get; private set; }

    /// <inheritdoc/>
    public event TypedEventHandler<ISettingsService, SettingsModel>? SettingsChanged;

    /// <inheritdoc/>
    public void Save(bool hotReload = true)
    {
        _persistence.Save(
            Settings,
            _filePath,
            JsonSerializationContext.Default.SettingsModel);

        if (hotReload)
        {
            SettingsChanged?.Invoke(this, Settings);
        }
    }

    private string SettingsJsonPath()
    {
        var directory = _appInfoService.ConfigDirectory;
        return Path.Combine(directory, "settings.json");
    }

    private void ApplyMigrations()
    {
        var migratedAny = false;

        try
        {
            var jsonContent = File.Exists(_filePath) ? File.ReadAllText(_filePath) : null;
            if (jsonContent is not null && JsonNode.Parse(jsonContent) is JsonObject root)
            {
                migratedAny |= TryMigrate(
                    "Migration #1: HotkeyGoesHome (bool) -> AutoGoHomeInterval (TimeSpan)",
                    root,
                    Settings,
                    nameof(SettingsModel.AutoGoHomeInterval),
                    DeprecatedHotkeyGoesHomeKey,
                    (model, goesHome) => model.AutoGoHomeInterval = goesHome ? TimeSpan.Zero : Timeout.InfiniteTimeSpan,
                    JsonSerializationContext.Default.Boolean);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Migration check failed: {ex}");
        }

        if (migratedAny)
        {
            Save(hotReload: false);
        }
    }

    private static bool TryMigrate<T>(
        string migrationName,
        JsonObject root,
        SettingsModel model,
        string newKey,
        string oldKey,
        Action<SettingsModel, T> apply,
        JsonTypeInfo<T> jsonTypeInfo)
    {
        try
        {
            if (root.ContainsKey(newKey) && root[newKey] is not null)
            {
                return false;
            }

            if (root.TryGetPropertyValue(oldKey, out var oldNode) && oldNode is not null)
            {
                var value = oldNode.Deserialize(jsonTypeInfo);
                apply(model, value!);
                return true;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error during migration {migrationName}.", ex);
        }

        return false;
    }
}
