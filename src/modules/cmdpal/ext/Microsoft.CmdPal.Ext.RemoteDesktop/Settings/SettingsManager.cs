// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.CmdPal.Ext.RemoteDesktop.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.RemoteDesktop.Settings;

public class SettingsManager : JsonSettingsManager, ISettingsInterface
{
    private static readonly string _namespace = "remotedesktop";

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    private readonly TextSetting _predefinedConnections = new(
        Namespaced(nameof(PredefinedConnections)),
        Resources.remotedesktop_settings_predefined_connections_title,
        Resources.remotedesktop_settings_predefined_connections_description,
        string.Empty);

    public string PredefinedConnections => _predefinedConnections.Value ?? string.Empty;

    public bool RunAsAdministrator { get; set; }

    internal static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        // now, the state is just next to the exe
        return Path.Combine(directory, "settings.json");
    }

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        _predefinedConnections.Multiline = true;
        Settings.Add(_predefinedConnections);

        // Load settings from file upon initialization
        LoadSettings();

        Settings.SettingsChanged += (s, a) =>
        {
            this.SaveSettings();
            this.LoadSettings();
        };
    }
}
