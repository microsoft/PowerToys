// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Calc.Helper;
using Microsoft.CmdPal.Ext.Calc.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Calc.Pages;

public sealed partial class FallbackCalculatorItem : FallbackCommandItem
{
    private const string _id = "com.microsoft.cmdpal.builtin.calculator.fallback";

    private readonly NoOpCommand _noOpCommand = new();
    private readonly CalculatorCopyCommand _copyCommand;
    private readonly CalculatorPasteCommand _pasteCommand;
    private readonly ISettingsInterface _settings;
    private readonly CalculatorListPage _calculatorListPage;
    private readonly CommandContextItem _openCalculatorPageContextItem;

    public FallbackCalculatorItem(ISettingsInterface settings, CalculatorListPage calculatorListPage)
        : base(new NoOpCommand(), Resources.calculator_title, _id)
    {
        _copyCommand = new CalculatorCopyCommand(string.Empty, string.Empty, settings, () => settings.SaveFallbackResultsToHistory);
        _pasteCommand = new CalculatorPasteCommand(string.Empty, string.Empty, settings, () => settings.SaveFallbackResultsToHistory);

        Command = _noOpCommand;
        Title = string.Empty;
        Subtitle = Resources.calculator_placeholder_text;
        Icon = Icons.CalculatorIcon;
        _settings = settings;
        _calculatorListPage = calculatorListPage;
        _openCalculatorPageContextItem = new CommandContextItem(_calculatorListPage)
        {
            Title = Resources.calculator_open_in_calculator,
        };
    }

    public override void UpdateQuery(string query)
    {
        var result = QueryHelper.Query(query, _settings, true, out _);

        if (result is null)
        {
            Command = _noOpCommand;
            Title = string.Empty;
            Subtitle = string.Empty;
            MoreCommands = [];
            return;
        }

        var pasteIsPrimary = _settings.PrimaryAction == PrimaryAction.Paste;
        var primaryCommand = pasteIsPrimary ? (IInvokableCommand)_pasteCommand : _copyCommand;
        var secondaryCommand = pasteIsPrimary ? (IInvokableCommand)_copyCommand : _pasteCommand;

        // Update the selected commands with current query/result
        UpdateCommand(primaryCommand, query, result);
        UpdateCommand(secondaryCommand, query, result);

        Command = primaryCommand;
        Title = result.Title;

        // we have to make the subtitle into an equation,
        // so that we will still string match the original query
        // Otherwise, something like 1+2 will have a title of "3" and not match
        Subtitle = query;

        // Set the search text in the calculator list page
        _calculatorListPage.SearchText = query;

        var fallbackCommands = new List<IContextItem>
        {
            _openCalculatorPageContextItem,
            new CommandContextItem(secondaryCommand),
        };

        MoreCommands = [.. fallbackCommands, .. result.MoreCommands];
    }

    private static void UpdateCommand(IInvokableCommand command, string query, ListItem result)
    {
        switch (command)
        {
            case CalculatorPasteCommand pasteCommand:
                pasteCommand.Update(result.Title, query);
                break;
            case CalculatorCopyCommand copyCommand:
                copyCommand.Update(result.Title, query);
                break;
        }
    }
}
