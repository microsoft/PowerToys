// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
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
        _settings = _persistence.Load(_filePath, JsonSerializationContext.Default.SettingsModel);
        ApplyMigrations();
    }

    private SettingsModel _settings;

    /// <inheritdoc/>
    public SettingsModel Settings => _settings;

    /// <inheritdoc/>
    public event TypedEventHandler<ISettingsService, SettingsModel>? SettingsChanged;

    /// <inheritdoc/>
    public void Save(bool hotReload = true) => UpdateSettings(s => s, hotReload);

    /// <inheritdoc/>
    public void UpdateSettings(Func<SettingsModel, SettingsModel> transform, bool hotReload = true)
    {
        var newSettings = transform(_settings);
        Interlocked.Exchange(ref _settings, newSettings);
        _persistence.Save(newSettings, _filePath, JsonSerializationContext.Default.SettingsModel);
        if (hotReload)
        {
            SettingsChanged?.Invoke(this, newSettings);
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
                    ref _settings,
                    nameof(SettingsModel.AutoGoHomeInterval),
                    DeprecatedHotkeyGoesHomeKey,
                    (ref SettingsModel model, bool goesHome) => model = model with { AutoGoHomeInterval = goesHome ? TimeSpan.Zero : Timeout.InfiniteTimeSpan },
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

    private delegate void MigrationApply<T>(ref SettingsModel model, T value);

    private static bool TryMigrate<T>(
        string migrationName,
        JsonObject root,
        ref SettingsModel model,
        string newKey,
        string oldKey,
        MigrationApply<T> apply,
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
                apply(ref model, value!);
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
