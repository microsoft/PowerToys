// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ManagedCommon;
using Microsoft.CmdPal.Ext.WindowsPalettes.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowsPalettes;

internal sealed class WindowsPalettesSettings : JsonSettingsManager, IWindowsPalettesSettings
{
    private const string Namespace = "windowsPalettes";

    private readonly ToggleSetting _showInstalledPalettes = new(
        Namespaced(nameof(ShowInstalledPalettes)),
        Resources.ShowInstalledPalettesSetting,
        Resources.ShowInstalledPalettesSettingDescription,
        false);

    public event EventHandler? SettingsChanged;

    public bool ShowInstalledPalettes => _showInstalledPalettes.Value;

    public WindowsPalettesSettings()
        : this(SettingsJsonPath())
    {
    }

    internal WindowsPalettesSettings(string filePath)
    {
        FilePath = filePath;
        Settings.Add(_showInstalledPalettes);
        LoadSettings();
        Settings.SettingsChanged += Settings_SettingsChanged;
    }

    private static string Namespaced(string propertyName) => $"{Namespace}.{propertyName}";

    private static string SettingsJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{Namespace}.settings.json");
    }

    private void Settings_SettingsChanged(object sender, Settings args)
    {
        SaveSettings();
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
}
