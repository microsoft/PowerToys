// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Calc.Helper;
using Microsoft.CmdPal.Ext.Calc.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Calc.Pages;

public sealed partial class FallbackCalculatorItem : FallbackCommandItem
{
    private readonly CopyTextCommand _copyCommand = new(string.Empty);
    private static readonly IconInfo _cachedIcon = IconHelpers.FromRelativePath("Assets\\Calculator.svg");
    private SettingsManager _settings;

    public FallbackCalculatorItem(SettingsManager settings)
        : base(new NoOpCommand(), Resources.calculator_title)
    {
        Command = _copyCommand;
        _copyCommand.Name = string.Empty;
        Title = string.Empty;
        Subtitle = Resources.calculator_placeholder_text;
        Icon = _cachedIcon;
        _settings = settings;
    }

    public override void UpdateQuery(string query)
    {
        var results = QueryHelper.Query(query, _settings, true);

        if (results.Count == 0)
        {
            _copyCommand.Text = string.Empty;
            _copyCommand.Name = string.Empty;
            Title = string.Empty;
            Subtitle = string.Empty;

            return;
        }

        _copyCommand.Text = results[0].Title;
        _copyCommand.Name = string.IsNullOrWhiteSpace(query) ? string.Empty : Resources.calculator_copy_command_name;
        Title = results[0].Title;

        // we have to make the subtitle the equation,
        // so that we will still string match the original query
        // Otherwise, something like 1+2 will have a title of "3" and not match
        Subtitle = query;
    }
}
