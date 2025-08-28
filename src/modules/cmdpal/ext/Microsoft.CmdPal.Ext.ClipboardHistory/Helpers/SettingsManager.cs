// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using Microsoft.CmdPal.Ext.ClipboardHistory.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;

internal sealed class SettingsManager : JsonSettingsManager, ISettingOptions
{
    private const string Namespace = "clipboardHistory";

    private static string Namespaced(string propertyName) => $"{Namespace}.{propertyName}";

    private readonly ToggleSetting _keepAfterPaste = new(
        Namespaced(nameof(KeepAfterPaste)),
        Resources.settings_keep_after_paste_title!,
        Resources.settings_keep_after_paste_description!,
        false);

    public bool KeepAfterPaste => _keepAfterPaste.Value;

    private static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);

        // now, the state is just next to the exe
        return Path.Combine(directory, "settings.json");
    }

    public SettingsManager()
    {
        FilePath = SettingsJsonPath();

        Settings.Add(_keepAfterPaste);

        // Load settings from file upon initialization
        LoadSettings();

        Settings.SettingsChanged += (_, _) => SaveSettings();
    }
}
