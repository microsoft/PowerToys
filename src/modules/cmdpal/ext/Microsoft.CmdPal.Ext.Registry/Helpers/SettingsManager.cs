// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Registry.Helpers;

public class SettingsManager : JsonSettingsManager, ISettingsInterface
{
    private static readonly string _namespace = "registry";

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    internal static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        return Path.Combine(directory, $"{_namespace}.settings.json");
    }

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        LoadSettings();

        Settings.SettingsChanged += (s, a) => this.SaveSettings();
    }
}
