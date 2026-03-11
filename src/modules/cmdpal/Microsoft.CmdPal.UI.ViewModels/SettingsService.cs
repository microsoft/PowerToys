// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Extensions.Logging;
using Windows.Foundation;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class SettingsService
{
    private const string FileName = "settings.json";

    private readonly ILogger _logger;
    private readonly PersistenceService _persistenceService;
    private readonly string _filePath;
    private const string DeprecatedHotkeyGoesHomeKey = "HotkeyGoesHome";
    private SettingsModel _settingsModel;

    public event TypedEventHandler<SettingsService, SettingsChangedEventArgs>? SettingsChanged;

    public SettingsModel CurrentSettings => _settingsModel;

    public SettingsService(PersistenceService persistenceService, ILogger logger)
    {
        _logger = logger;
        _persistenceService = persistenceService;

        _filePath = _persistenceService.SettingsJsonPath(FileName);
        _settingsModel = LoadSettings();
    }

    private SettingsModel LoadSettings()
    {
        var settings = _persistenceService.LoadObject<SettingsModel>(FileName, JsonSerializationContext.Default.SettingsModel!);

        var migratedAny = false;
        try
        {
            var jsonContent = File.Exists(_filePath) ? File.ReadAllText(_filePath) : "{}";
            if (JsonNode.Parse(jsonContent) is JsonObject root)
            {
                migratedAny |= ApplyMigrations(root, ref settings);
            }
        }
        catch (Exception ex)
        {
            Log_MigrationCheckFailure(ex);
        }

        if (migratedAny)
        {
            SaveSettings(settings, false);
        }

        return settings;
    }

    public void SaveSettings(SettingsModel model, bool hotReload = false)
    {
        _persistenceService.SaveObject(
                        model,
                        FileName,
                        JsonSerializationContext.Default.SettingsModel,
                        JsonSerializationContext.Default.Options,
                        beforeWriteMutation: obj => obj.Remove(DeprecatedHotkeyGoesHomeKey),
                        afterWriteCallback: m => FinalizeSettingsSave(m, hotReload));
    }

    private void FinalizeSettingsSave(SettingsModel model, bool hotReload)
    {
        _settingsModel = model;

        // TODO: Instead of just raising the event here, we should
        // have a file change watcher on the settings file, and
        // reload the settings then
        if (hotReload)
        {
            SettingsChanged?.Invoke(this, new(_settingsModel));
        }
    }

    private bool ApplyMigrations(JsonObject root, ref SettingsModel model)
    {
        var migrated = false;

        migrated |= TryMigrate(
            "Migration #1: HotkeyGoesHome (bool) -> AutoGoHomeInterval (TimeSpan)",
            root,
            ref model,
            nameof(SettingsModel.AutoGoHomeInterval),
            DeprecatedHotkeyGoesHomeKey,
            (settingsModel, goesHome) => settingsModel with { AutoGoHomeInterval = goesHome ? TimeSpan.Zero : Timeout.InfiniteTimeSpan },
            JsonSerializationContext.Default.Boolean);

        return migrated;
    }

    private bool TryMigrate<T>(string migrationName, JsonObject root, ref SettingsModel model, string newKey, string oldKey, Func<SettingsModel, T, SettingsModel> apply, JsonTypeInfo<T> jsonTypeInfo)
    {
        try
        {
            if (root.ContainsKey(newKey) && root[newKey] is not null)
            {
                return false;
            }

            if (root.TryGetPropertyValue(oldKey, out var oldNode) && oldNode is not null)
            {
                var value = oldNode.Deserialize<T>(jsonTypeInfo);
                model = apply(model, value!);
                return true;
            }
        }
        catch (Exception ex)
        {
            Log_MigrationFailure(migrationName, ex);
        }

        return false;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Settings migration '{MigrationName}' failed.")]
    partial void Log_MigrationFailure(string MigrationName, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Settings migration check failed.")]
    partial void Log_MigrationCheckFailure(Exception exception);
}
