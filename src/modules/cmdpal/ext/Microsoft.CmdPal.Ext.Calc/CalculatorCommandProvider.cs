// Copyright (c) Microsoft Corporation
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
    private readonly ISettingsInterface _settings = new SettingsManager();
    private readonly ListItem _listItem;
    private readonly FallbackCalculatorItem _fallback;

    public CalculatorCommandProvider()
    {
        Id = "com.microsoft.cmdpal.builtin.calculator";
        DisplayName = Resources.calculator_display_name;
        Icon = Icons.CalculatorIcon;
        Settings = ((SettingsManager)_settings).Settings;

        var calculatorListPage = new CalculatorListPage(_settings);
        _listItem = new ListItem(calculatorListPage)
        {
            MoreCommands = [new CommandContextItem(((SettingsManager)_settings).Settings.SettingsPage)],
        };
        _fallback = new(_settings, calculatorListPage);
    }

    public override ICommandItem[] TopLevelCommands() => [_listItem];

    public override IFallbackCommandItem[] FallbackCommands() => [_fallback];
}
