// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.Settings;

public partial class SettingsCommandProvider : CommandProvider
{
    private readonly SettingsPage settingsPage = new();

    public SettingsCommandProvider()
    {
        DisplayName = $"Settings";
    }

    public override ICommandItem[] TopLevelCommands()
    {
        return [new CommandItem(settingsPage) { Subtitle = "CmdPal settings" }];
    }
}
