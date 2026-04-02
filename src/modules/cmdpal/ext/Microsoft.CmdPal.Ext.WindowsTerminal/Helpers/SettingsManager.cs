// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Ext.WindowsTerminal.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

#nullable enable

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;

public class SettingsManager : JsonSettingsManager
{
    private static readonly string _namespace = "wt";

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
    };

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    private readonly ToggleSetting _showHiddenProfiles = new(
        Namespaced(nameof(ShowHiddenProfiles)),
        Resources.show_hidden_profiles,
        Resources.show_hidden_profiles,
        false);

    private readonly ToggleSetting _openNewTab = new(
        Namespaced(nameof(OpenNewTab)),
        Resources.open_new_tab,
        Resources.open_new_tab,
        false);

    private readonly ToggleSetting _openQuake = new(
        Namespaced(nameof(OpenQuake)),
        Resources.open_quake,
        Resources.open_quake_description,
        false);

    private readonly ToggleSetting _saveLastSelectedChannel = new(
        Namespaced(nameof(SaveLastSelectedChannel)),
        Resources.save_last_selected_channel!,
        Resources.save_last_selected_channel_description!,
        false);

    private readonly ChoiceSetSetting _profileSortOrder = new(
        Namespaced(nameof(ProfileSortOrder)),
        Resources.profile_sort_order!,
        Resources.profile_sort_order_description!,
        [
            new ChoiceSetSetting.Choice(Resources.profile_sort_order_item_default!, ProfileSortOrder.Default.ToString("G")),
            new ChoiceSetSetting.Choice(Resources.profile_sort_order_item_alphabetical!, ProfileSortOrder.Alphabetical.ToString("G")),
            new ChoiceSetSetting.Choice(Resources.profile_sort_order_item_mru!, ProfileSortOrder.MostRecentlyUsed.ToString("G")),
        ]);

    public bool ShowHiddenProfiles => _showHiddenProfiles.Value;

    public bool OpenNewTab => _openNewTab.Value;

    public bool OpenQuake => _openQuake.Value;

    public bool SaveLastSelectedChannel => _saveLastSelectedChannel.Value;

    public ProfileSortOrder ProfileSortOrder => System.Enum.TryParse<ProfileSortOrder>(_profileSortOrder.Value, out var result) ? result : ProfileSortOrder.Default;

    private static string SettingsJsonPath()
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

        Settings.Add(_showHiddenProfiles);
        Settings.Add(_openNewTab);
        Settings.Add(_openQuake);
        Settings.Add(_saveLastSelectedChannel);
        Settings.Add(_profileSortOrder);

        MigrateFromLegacyFile(LegacySettingsJsonPath());
        LoadSettings();

        Settings.SettingsChanged += (s, a) => this.SaveSettings();
    }
}
