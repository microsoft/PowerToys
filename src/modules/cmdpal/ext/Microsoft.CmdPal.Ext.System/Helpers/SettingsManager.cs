// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.System.Helpers;

public class SettingsManager : JsonSettingsManager, ISettingsInterface
{
    private static readonly string _namespace = "system";

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
    };

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    private readonly ToggleSetting _showDialogToConfirmCommand = new(
        Namespaced(nameof(ShowDialogToConfirmCommand)),
        Resources.confirm_system_commands,
        Resources.confirm_system_commands,
        false); // TODO -- double check default value

    private readonly ToggleSetting _showSuccessMessageAfterEmptyingRecycleBin = new(
        Namespaced(nameof(ShowSuccessMessageAfterEmptyingRecycleBin)),
        Resources.Microsoft_plugin_sys_RecycleBin_ShowEmptySuccessMessage,
        Resources.Microsoft_plugin_sys_RecycleBin_ShowEmptySuccessMessage,
        false); // TODO -- double check default value

    private readonly ToggleSetting _hideEmptyRecycleBin = new(
        Namespaced(nameof(HideEmptyRecycleBin)),
        Resources.Microsoft_plugin_sys_RecycleBin_HideEmpty,
        Resources.Microsoft_plugin_sys_RecycleBin_HideEmpty,
        false);

    private readonly ToggleSetting _hideDisconnectedNetworkInfo = new(
        Namespaced(nameof(HideDisconnectedNetworkInfo)),
        Resources.Microsoft_plugin_ext_settings_hideDisconnectedNetworkInfo,
        Resources.Microsoft_plugin_ext_settings_hideDisconnectedNetworkInfo,
        false);

    internal static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        return Path.Combine(directory, $"{_namespace}.settings.json");
    }

    private static string LegacySettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        return Path.Combine(directory, "settings.json");
    }

    public bool ShowDialogToConfirmCommand() => _showDialogToConfirmCommand.Value;

    public bool ShowSuccessMessageAfterEmptyingRecycleBin() => _showSuccessMessageAfterEmptyingRecycleBin.Value;

    public bool HideEmptyRecycleBin() => _hideEmptyRecycleBin.Value;

    public bool HideDisconnectedNetworkInfo() => _hideDisconnectedNetworkInfo.Value;

    public FirmwareType GetSystemFirmwareType() => Win32Helpers.GetSystemFirmwareType();

    /// <summary>
    /// Migrates settings from a shared legacy file to this extension's own settings file.
    /// Call after registering all settings with <see cref="Settings"/> and before <see cref="LoadSettings"/>.
    /// Skips if <see cref="FilePath"/> already exists or <paramref name="legacyFilePath"/> is missing.
    /// </summary>
    private void MigrateFromLegacyFile(string legacyFilePath)
    {
        if (string.IsNullOrEmpty(FilePath) || string.IsNullOrEmpty(legacyFilePath))
        {
            return;
        }

        // Already migrated — per-extension file exists.
        if (File.Exists(FilePath))
        {
            return;
        }

        if (!File.Exists(legacyFilePath))
        {
            return;
        }

        try
        {
            var legacyContent = File.ReadAllText(legacyFilePath);
            if (JsonNode.Parse(legacyContent) is not JsonObject)
            {
                return;
            }

            // Extract only the keys this extension owns.
            Settings.Update(legacyContent);
            var settingsJson = Settings.ToJson();

            if (JsonNode.Parse(settingsJson) is JsonObject extracted && extracted.Count > 0)
            {
                var directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(FilePath, extracted.ToJsonString(_serializerOptions));
            }
        }
        catch (Exception ex)
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = $"Settings migration failed from '{legacyFilePath}' to '{FilePath}': {ex}" });
        }
    }

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(_showDialogToConfirmCommand);
        Settings.Add(_showSuccessMessageAfterEmptyingRecycleBin);
        Settings.Add(_hideEmptyRecycleBin);
        Settings.Add(_hideDisconnectedNetworkInfo);

        MigrateFromLegacyFile(LegacySettingsJsonPath());
        LoadSettings();

        Settings.SettingsChanged += (s, a) => this.SaveSettings();
    }
}
