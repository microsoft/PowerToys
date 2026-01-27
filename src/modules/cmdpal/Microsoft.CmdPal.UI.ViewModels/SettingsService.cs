// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Microsoft.CmdPal.Common;
using Microsoft.Extensions.Logging;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class SettingsService
{
    private readonly ILogger _logger;
    private readonly string _filePath;
    private const string DeprecatedHotkeyGoesHomeKey = "HotkeyGoesHome";
    private SettingsModel _settingsModel;

    public event TypedEventHandler<SettingsModel, object?>? SettingsChanged;

    public SettingsModel CurrentSettings => _settingsModel;

    public SettingsService(ILogger logger)
    {
        this._logger = logger;
        _filePath = PersistenceService.SettingsJsonPath("settings.json");
        _settingsModel = LoadSettings();
    }

    private SettingsModel LoadSettings()
    {
        var settings = PersistenceService.LoadObject<SettingsModel>(_filePath, JsonSerializationContext.Default.SettingsModel!, _logger);

        var migratedAny = false;
        try
        {
            var jsonContent = File.Exists(_filePath) ? File.ReadAllText(_filePath) : "{}";
            if (JsonNode.Parse(jsonContent) is JsonObject root)
            {
                migratedAny |= ApplyMigrations(root, settings);
            }
        }
        catch (Exception ex)
        {
            Log_MigrationCheckFailure(ex);
        }

        if (migratedAny)
        {
            SaveSettings(settings);
        }

        return settings;
    }

    public void SaveSettings(SettingsModel model)
    {
        PersistenceService.SaveObject(
                        model,
                        _filePath,
                        JsonSerializationContext.Default.SettingsModel,
                        JsonSerializationContext.Default.Options,
                        beforeWriteMutation: obj => obj.Remove(DeprecatedHotkeyGoesHomeKey),
                        afterWriteCallback: m => FinalizeSettingsSave(m),
                        _logger);
    }

    private void FinalizeSettingsSave(SettingsModel model)
    {
        _settingsModel = model;
        SettingsChanged?.Invoke(model, null);
    }

    private bool ApplyMigrations(JsonObject root, SettingsModel model)
    {
        var migrated = false;

        migrated |= TryMigrate(
            "Migration #1: HotkeyGoesHome (bool) -> AutoGoHomeInterval (TimeSpan)",
            root,
            model,
            nameof(SettingsModel.AutoGoHomeInterval),
            DeprecatedHotkeyGoesHomeKey,
            (settingsModel, goesHome) => settingsModel.AutoGoHomeInterval = goesHome ? TimeSpan.Zero : Timeout.InfiniteTimeSpan,
            JsonSerializationContext.Default.Boolean);

        return migrated;
    }

    private bool TryMigrate<T>(string migrationName, JsonObject root, SettingsModel model, string newKey, string oldKey, Action<SettingsModel, T> apply, JsonTypeInfo<T> jsonTypeInfo)
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
                apply(model, value!);
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
