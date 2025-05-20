﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Calc.Helper;
using Microsoft.CmdPal.Ext.Calc.Pages;
using Microsoft.CmdPal.Ext.Calc.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Calc;

public partial class CalculatorCommandProvider : CommandProvider
{
    private readonly ListItem _listItem = new(new CalculatorListPage(settings))
    {
        Subtitle = Resources.calculator_top_level_subtitle,
        MoreCommands = [new CommandContextItem(settings.Settings.SettingsPage)],
    };

    private readonly FallbackCalculatorItem _fallback = new(settings);
    private static SettingsManager settings = new();

    public CalculatorCommandProvider()
    {
        Id = "Calculator";
        DisplayName = Resources.calculator_display_name;
        Icon = CalculatorIcons.ProviderIcon;
        Settings = settings.Settings;
    }

    public override ICommandItem[] TopLevelCommands() => [_listItem];

    public override IFallbackCommandItem[] FallbackCommands() => [_fallback];
}
