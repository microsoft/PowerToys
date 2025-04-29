// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Bookmarks.Helpers;

public class SettingsManager : JsonSettingsManager
{
    private static readonly string _namespace = "bookmarks";

    private static string Namespaced(string propertyName) => $"{_namespace}.{propertyName}";

    private readonly ToggleSetting _keepTerminalWindow = new(
        Namespaced(nameof(KeepTerminalWindowOpen)),
        Properties.Resources.bookmarks_settings_keepTerminalWindowOpen,
        Properties.Resources.bookmarks_settings_keepTerminalWindowOpen_descrption,
        true);

    public bool KeepTerminalWindowOpen => _keepTerminalWindow.Value;

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

        Settings.Add(_keepTerminalWindow);

        // Load settings from file upon initialization
        LoadSettings();

        Settings.SettingsChanged += (s, a) => this.SaveSettings();
    }
}
