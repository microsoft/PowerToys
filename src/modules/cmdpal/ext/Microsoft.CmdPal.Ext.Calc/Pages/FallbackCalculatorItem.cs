﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Calc.Helper;
using Microsoft.CmdPal.Ext.Calc.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Calc.Pages;

public sealed partial class FallbackCalculatorItem : FallbackCommandItem
{
    private readonly CopyTextCommand _copyCommand = new(string.Empty);
    private readonly SettingsManager _settings;

    public FallbackCalculatorItem(SettingsManager settings)
        : base(new NoOpCommand(), Resources.calculator_title)
    {
        Command = _copyCommand;
        _copyCommand.Name = string.Empty;
        Title = string.Empty;
        Subtitle = Resources.calculator_placeholder_text;
        Icon = CalculatorIcons.ProviderIcon;
        _settings = settings;
    }

    public override void UpdateQuery(string query)
    {
        var result = QueryHelper.Query(query, _settings, true, null);

        if (result == null)
        {
            _copyCommand.Text = string.Empty;
            _copyCommand.Name = string.Empty;
            Title = string.Empty;
            Subtitle = string.Empty;
            MoreCommands = [];
            return;
        }

        _copyCommand.Text = result.Title;
        _copyCommand.Name = string.IsNullOrWhiteSpace(query) ? string.Empty : Resources.calculator_copy_command_name;
        Title = result.Title;

        // we have to make the subtitle the equation,
        // so that we will still string match the original query
        // Otherwise, something like 1+2 will have a title of "3" and not match
        Subtitle = query;

        MoreCommands = result.MoreCommands;
    }
}
