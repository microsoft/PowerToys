// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.WebSearch.Helpers;
using Microsoft.CmdPal.Ext.WebSearch.Properties;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.WebSearch.Pages;

internal sealed partial class SettingsPage : FormPage
{
    private readonly Microsoft.CmdPal.Extensions.Helpers.Settings _settings;
    private readonly SettingsManager _settingsManager;

    public override IForm[] Forms()
    {
        var s = _settings.ToForms();
        return s;
    }

    public SettingsPage(SettingsManager settingsManager)
    {
        Name = Resources.settings_page_name;
        Icon = new("\uE713"); // Settings icon
        _settings = settingsManager.GetSettings();
        _settingsManager = settingsManager;

        _settings.SettingsChanged += SettingsChanged;
    }

    private void SettingsChanged(object sender, Microsoft.CmdPal.Extensions.Helpers.Settings args) => _settingsManager.SaveSettings();
}
