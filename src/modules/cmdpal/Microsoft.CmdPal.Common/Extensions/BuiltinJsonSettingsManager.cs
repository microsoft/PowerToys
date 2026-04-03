// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Common;

/// <summary>
/// Intermediate base class for built-in CmdPal extensions that adds
/// one-time migration from the legacy shared <c>settings.json</c> to
/// per-extension settings files.
/// </summary>
public abstract class BuiltinJsonSettingsManager : JsonSettingsManager
{
    private const string SettingsFolderName = "Microsoft.CmdPal";

    private static readonly JsonSerializerOptions _serializerOptions = new()
    {
        WriteIndented = true,
    };

    private string? _legacyFilePath;

    protected BuiltinJsonSettingsManager()
    {
    }

    protected BuiltinJsonSettingsManager(string settingsNamespace)
    {
        if (string.IsNullOrWhiteSpace(settingsNamespace))
        {
            throw new ArgumentException($"{nameof(settingsNamespace)} cannot be null or whitespace.", nameof(settingsNamespace));
        }

        FilePath = SettingsJsonPath(settingsNamespace);
        EnableMigration(CmdPalLegacySettings.LegacySettingsMigrationSourceJsonPath());
    }

    protected static string SettingsDirectoryPath()
    {
        var directory = Utilities.BaseSettingsPath(SettingsFolderName);
        Directory.CreateDirectory(directory);

        return directory;
    }

    protected static string SettingsJsonPath(string settingsNamespace)
    {
        if (string.IsNullOrWhiteSpace(settingsNamespace))
        {
            throw new ArgumentException($"{nameof(settingsNamespace)} cannot be null or whitespace.", nameof(settingsNamespace));
        }

        return Path.Combine(SettingsDirectoryPath(), $"{settingsNamespace}.settings.json");
    }

    /// <summary>
    /// Registers the legacy shared settings file path so that
    /// <see cref="LoadSettings"/> will automatically attempt a one-time
    /// migration before loading. Call this in the constructor alongside
    /// the <see cref="JsonSettingsManager.FilePath"/> assignment.
    /// </summary>
    protected void EnableMigration(string legacyFilePath)
    {
        _legacyFilePath = legacyFilePath;
    }

    /// <inheritdoc/>
    public override void LoadSettings()
    {
        if (!string.IsNullOrEmpty(_legacyFilePath))
        {
            MigrateFromLegacyFile(_legacyFilePath);
        }

        base.LoadSettings();
    }

    /// <summary>
    /// Migrates settings from a shared legacy file to this extension's own settings file.
    /// Skips if <see cref="JsonSettingsManager.FilePath"/> already exists or the legacy file is missing.
    /// </summary>
    protected void MigrateFromLegacyFile(string legacyFilePath)
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
}
