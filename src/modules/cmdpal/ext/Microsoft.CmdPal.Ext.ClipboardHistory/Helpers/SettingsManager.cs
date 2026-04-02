// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Ext.ClipboardHistory.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;

internal sealed class SettingsManager : JsonSettingsManager, ISettingOptions
{
    private const string Namespace = "clipboardHistory";

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
    };

    private static string Namespaced(string propertyName) => $"{Namespace}.{propertyName}";

    private readonly ToggleSetting _keepAfterPaste = new(
        Namespaced(nameof(KeepAfterPaste)),
        Resources.settings_keep_after_paste_title!,
        Resources.settings_keep_after_paste_description!,
        false);

    private readonly ToggleSetting _confirmDelete = new(
        Namespaced(nameof(DeleteFromHistoryRequiresConfirmation)),
        Resources.settings_confirm_delete_title!,
        Resources.settings_confirm_delete_description!,
        true);

    private readonly ChoiceSetSetting _primaryAction = new(
        Namespaced(nameof(PrimaryAction)),
        Resources.settings_primary_action_title!,
        Resources.settings_primary_action_description!,
        [
            new ChoiceSetSetting.Choice(Resources.settings_primary_action_default!, PrimaryAction.Default.ToString("G")),
            new ChoiceSetSetting.Choice(Resources.settings_primary_action_paste!, PrimaryAction.Paste.ToString("G")),
            new ChoiceSetSetting.Choice(Resources.settings_primary_action_copy!, PrimaryAction.Copy.ToString("G"))
        ]);

    public bool KeepAfterPaste => _keepAfterPaste.Value;

    public bool DeleteFromHistoryRequiresConfirmation => _confirmDelete.Value;

    public PrimaryAction PrimaryAction => Enum.TryParse<PrimaryAction>(_primaryAction.Value, out var action) ? action : PrimaryAction.Default;

    private static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        return Path.Combine(directory, $"{Namespace}.settings.json");
    }

    private static string LegacySettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        return Path.Combine(directory, "settings.json");
    }

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

        Settings.Add(_keepAfterPaste);
        Settings.Add(_confirmDelete);
        Settings.Add(_primaryAction);

        MigrateFromLegacyFile(LegacySettingsJsonPath());
        LoadSettings();

        Settings.SettingsChanged += (_, _) => SaveSettings();
    }
}
