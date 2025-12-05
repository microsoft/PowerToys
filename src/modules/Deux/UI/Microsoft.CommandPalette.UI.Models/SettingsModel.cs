// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Windows.Foundation;

namespace Microsoft.CommandPalette.UI.Models;

public partial class SettingsModel : ObservableObject
{
    private const string DeprecatedHotkeyGoesHomeKey = "HotkeyGoesHome";

    [JsonIgnore]
    private static readonly string _filePath;

    public event TypedEventHandler<SettingsModel, object?>? SettingsChanged;

    ///////////////////////////////////////////////////////////////////////////
    // SETTINGS HERE
    public static HotkeySettings DefaultActivationShortcut { get; } = new HotkeySettings(true, false, true, false, 0x20); // win+alt+space

    public HotkeySettings? Hotkey { get; set; } = DefaultActivationShortcut;

    public bool UseLowLevelGlobalHotkey { get; set; }

    public bool ShowAppDetails { get; set; }

    public bool BackspaceGoesBack { get; set; }

    public bool SingleClickActivates { get; set; }

    public bool HighlightSearchOnActivate { get; set; } = true;

    public bool ShowSystemTrayIcon { get; set; } = true;

    public bool IgnoreShortcutWhenFullscreen { get; set; }

    public bool AllowExternalReload { get; set; }

    public MonitorBehavior SummonOn { get; set; } = MonitorBehavior.ToMouse;

    public bool DisableAnimations { get; set; } = true;

    public bool EnableAnimations { get; set; }

    public WindowPosition? LastWindowPosition { get; set; }

    public TimeSpan AutoGoHomeInterval { get; set; } = Timeout.InfiniteTimeSpan;

    // END SETTINGS
    ///////////////////////////////////////////////////////////////////////////

    static SettingsModel()
    {
        _filePath = PersistenceService.SettingsJsonPath("settings.json");
    }

    private static bool ApplyMigrations(JsonObject root, SettingsModel model, ILogger logger)
    {
        var migrated = false;

        migrated |= TryMigrate(
            "Migration #1: HotkeyGoesHome (bool) -> AutoGoHomeInterval (TimeSpan)",
            root,
            model,
            nameof(AutoGoHomeInterval),
            DeprecatedHotkeyGoesHomeKey,
            (settingsModel, goesHome) => settingsModel.AutoGoHomeInterval = goesHome ? TimeSpan.Zero : Timeout.InfiniteTimeSpan,
            JsonSerializationContext.Default.Boolean,
            logger);

        return migrated;
    }

    private static bool TryMigrate<T>(string migrationName, JsonObject root, SettingsModel model, string newKey, string oldKey, Action<SettingsModel, T> apply, JsonTypeInfo<T> jsonTypeInfo, ILogger logger)
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
            Log_MigrationFailure(logger, migrationName, ex);
        }

        return false;
    }

    public static SettingsModel LoadSettings(ILogger logger)
    {
        var settings = PersistenceService.LoadObject<SettingsModel>(_filePath, JsonSerializationContext.Default.SettingsModel!, logger);

        var migratedAny = false;
        try
        {
            var jsonContent = File.Exists(_filePath) ? File.ReadAllText(_filePath) : "{}";
            if (JsonNode.Parse(jsonContent) is JsonObject root)
            {
                migratedAny |= ApplyMigrations(root, settings, logger);
            }
        }
        catch (Exception ex)
        {
            Log_MigrationCheckFailure(logger, ex);
        }

        if (migratedAny)
        {
            SaveSettings(settings, logger);
        }

        return settings;
    }

    public static void SaveSettings(SettingsModel model, ILogger logger)
    {
        PersistenceService.SaveObject(
                        model,
                        _filePath,
                        JsonSerializationContext.Default.SettingsModel,
                        JsonSerializationContext.Default.Options,
                        beforeWriteMutation: obj => obj.Remove(DeprecatedHotkeyGoesHomeKey),
                        afterWriteCallback: m => m.SettingsChanged?.Invoke(m, null),
                        logger);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Settings migration '{MigrationName}' failed.")]
    static partial void Log_MigrationFailure(ILogger logger, string MigrationName, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Settings migration check failed.")]
    static partial void Log_MigrationCheckFailure(ILogger logger, Exception exception);
}
