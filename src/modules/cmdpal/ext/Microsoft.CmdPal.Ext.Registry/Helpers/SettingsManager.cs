// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.CmdPal.Common;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Registry.Helpers;

public class SettingsManager : BuiltinJsonSettingsManager, ISettingsInterface
{
    private static readonly string _namespace = "registry";

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    internal static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        return Path.Combine(directory, $"{_namespace}.settings.json");
    }

    private static string LegacySettingsJsonPath()
    {
        return CmdPalLegacySettings.LegacySettingsMigrationSourceJsonPath();
    }

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();
        EnableMigration(LegacySettingsJsonPath());

        // Add settings here when needed
        // Settings.Add(setting);
        LoadSettings();

        Settings.SettingsChanged += (s, a) => this.SaveSettings();
    }
}
