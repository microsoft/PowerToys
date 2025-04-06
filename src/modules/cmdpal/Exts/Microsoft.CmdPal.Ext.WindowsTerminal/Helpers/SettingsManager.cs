// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.CmdPal.Ext.WindowsTerminal.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

#nullable enable

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;

public class SettingsManager : JsonSettingsManager
{
    private static readonly string _namespace = "wt";

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

    public bool ShowHiddenProfiles => _showHiddenProfiles.Value;

    public bool OpenNewTab => _openNewTab.Value;

    public bool OpenQuake => _openQuake.Value;

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

        Settings.Add(_showHiddenProfiles);
        Settings.Add(_openNewTab);
        Settings.Add(_openQuake);

        // Load settings from file upon initialization
        LoadSettings();

        Settings.SettingsChanged += (s, a) => this.SaveSettings();
    }
}
