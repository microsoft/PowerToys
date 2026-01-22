// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Calc.Helper;
using Microsoft.CmdPal.Ext.Calc.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Calc.Pages;

public sealed partial class FallbackCalculatorItem : FallbackCommandItem
{
    private const string _id = "com.microsoft.cmdpal.builtin.calculator.fallback";
    private readonly CopyTextCommand _copyCommand = new(string.Empty);
    private readonly ISettingsInterface _settings;

    public FallbackCalculatorItem(ISettingsInterface settings)
        : base(new NoOpCommand(), Resources.calculator_title, _id)
    {
        Command = _copyCommand;
        _copyCommand.Name = string.Empty;
        Title = string.Empty;
        Subtitle = Resources.calculator_placeholder_text;
        Icon = Icons.CalculatorIcon;
        _settings = settings;
    }

    public override void UpdateQuery(string query)
    {
        var result = QueryHelper.Query(query, _settings, true, null);

        if (result is null)
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

        // we have to make the subtitle into an equation,
        // so that we will still string match the original query
        // Otherwise, something like 1+2 will have a title of "3" and not match
        Subtitle = query;

        MoreCommands = result.MoreCommands;
    }
}
