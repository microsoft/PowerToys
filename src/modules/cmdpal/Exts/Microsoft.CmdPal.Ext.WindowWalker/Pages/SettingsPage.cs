// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.WindowWalker.Helpers;
using Microsoft.CmdPal.Ext.WindowWalker.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowWalker.Pages;

internal sealed partial class SettingsPage : FormPage
{
    private readonly Settings _settings;
    private readonly SettingsManager _settingsManager;

    public override IForm[] Forms()
    {
        var s = _settings.ToForms();
        return s;
    }

    public SettingsPage()
    {
        Name = Resources.windowwalker_settings_name;
        Icon = new IconInfo("\uE713"); // Settings icon
        _settingsManager = SettingsManager.Instance;
        _settings = _settingsManager.Settings;

        _settings.SettingsChanged += SettingsChanged;
    }

    private void SettingsChanged(object sender, Settings args)
    {
        _settingsManager.SaveSettings();
    }
}
