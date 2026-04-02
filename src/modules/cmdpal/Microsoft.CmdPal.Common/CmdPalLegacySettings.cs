// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Common;

/// <summary>
/// Provides the shared CmdPal settings paths needed to preserve the original legacy settings payload during migration.
/// </summary>
public static class CmdPalLegacySettings
{
    private const string SettingsFolderName = "Microsoft.CmdPal";
    private const string SettingsFileName = "settings.json";
    private const string LegacySettingsBackupFileName = "settings.legacy.bak";

    /// <summary>
    /// Gets the live shared CmdPal settings path for the provided configuration directory.
    /// </summary>
    /// <param name="configDirectory">The CmdPal configuration directory.</param>
    /// <returns>The shared settings path.</returns>
    public static string SettingsJsonPath(string configDirectory)
    {
        if (string.IsNullOrWhiteSpace(configDirectory))
        {
            throw new ArgumentException($"{nameof(configDirectory)} cannot be null or whitespace.", nameof(configDirectory));
        }

        return Path.Combine(configDirectory, SettingsFileName);
    }

    /// <summary>
    /// Gets the preserved legacy settings backup path for the provided configuration directory.
    /// </summary>
    /// <param name="configDirectory">The CmdPal configuration directory.</param>
    /// <returns>The preserved legacy settings backup path.</returns>
    public static string LegacySettingsBackupJsonPath(string configDirectory)
    {
        if (string.IsNullOrWhiteSpace(configDirectory))
        {
            throw new ArgumentException($"{nameof(configDirectory)} cannot be null or whitespace.", nameof(configDirectory));
        }

        return Path.Combine(configDirectory, LegacySettingsBackupFileName);
    }

    /// <summary>
    /// Gets the preserved legacy settings backup path for the current CmdPal settings directory.
    /// </summary>
    /// <returns>The preserved legacy settings backup path.</returns>
    public static string LegacySettingsBackupJsonPath()
    {
        return LegacySettingsBackupJsonPath(Utilities.BaseSettingsPath(SettingsFolderName));
    }

    /// <summary>
    /// Gets the settings file extensions should use as a migration source for the provided configuration directory.
    /// </summary>
    /// <param name="configDirectory">The CmdPal configuration directory.</param>
    /// <returns>The backup path when available; otherwise the live shared settings path.</returns>
    public static string LegacySettingsMigrationSourceJsonPath(string configDirectory)
    {
        var backupPath = LegacySettingsBackupJsonPath(configDirectory);
        return File.Exists(backupPath) ? backupPath : SettingsJsonPath(configDirectory);
    }

    /// <summary>
    /// Gets the settings file extensions should use as a migration source for the current CmdPal settings directory.
    /// </summary>
    /// <returns>The backup path when available; otherwise the live shared settings path.</returns>
    public static string LegacySettingsMigrationSourceJsonPath()
    {
        return LegacySettingsMigrationSourceJsonPath(Utilities.BaseSettingsPath(SettingsFolderName));
    }

    /// <summary>
    /// Creates the preserved legacy settings backup if the shared settings file exists and no backup has been created yet.
    /// </summary>
    /// <param name="configDirectory">The CmdPal configuration directory.</param>
    public static void EnsureLegacySettingsBackup(string configDirectory)
    {
        var settingsPath = SettingsJsonPath(configDirectory);
        if (!File.Exists(settingsPath))
        {
            return;
        }

        var backupPath = LegacySettingsBackupJsonPath(configDirectory);
        if (File.Exists(backupPath))
        {
            return;
        }

        Directory.CreateDirectory(configDirectory);
        File.Copy(settingsPath, backupPath);
    }
}
