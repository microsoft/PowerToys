// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.
namespace Microsoft.CmdPal.Ext.Settings;

internal sealed class SettingsPage : FormPage
{
    private readonly SettingsForm _settings = new();

    public override IForm[] Forms() => [_settings];

    public SettingsPage()
    {
        Icon = new("\uE713");
        Name = "Settings";
    }
}
