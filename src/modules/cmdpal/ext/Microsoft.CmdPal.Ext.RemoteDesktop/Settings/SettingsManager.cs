// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CmdPal.Ext.RemoteDesktop.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.Settings;

internal class SettingsManager : JsonSettingsManager, ISettingsInterface
{
    // Line break character used in WinUI3 TextBox and TextBlock.
    private const char TEXTBOXNEWLINE = '\r';

    private static readonly string _namespace = "com.microsoft.cmdpal.builtin.remotedesktop";

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
    };

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    private readonly TextSetting _predefinedConnections = new(
        Namespaced(nameof(PredefinedConnections)),
        Resources.remotedesktop_settings_predefined_connections_title,
        Resources.remotedesktop_settings_predefined_connections_description,
        string.Empty)
    {
        Multiline = true,
        Placeholder = $"server1.domain.com{TEXTBOXNEWLINE}server2.domain.com{TEXTBOXNEWLINE}192.168.1.1",
    };

    public IReadOnlyCollection<string> PredefinedConnections => _predefinedConnections.Value?.Split(TEXTBOXNEWLINE).ToList() ?? [];

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

        Settings.Add(_predefinedConnections);

        MigrateFromLegacyFile(LegacySettingsJsonPath());
        LoadSettings();

        Settings.SettingsChanged += (s, a) => this.SaveSettings();
    }
}
