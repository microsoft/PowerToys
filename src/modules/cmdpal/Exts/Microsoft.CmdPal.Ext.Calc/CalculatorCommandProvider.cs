// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.CmdPal.Ext.Calc.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.Calc;

public partial class CalculatorCommandProvider : CommandProvider
{
    private readonly ListItem _listItem = new(new CalculatorListPage()) { Subtitle = Resources.calculator_top_level_subtitle };
    private readonly FallbackCalculatorItem _fallback = new();

    public CalculatorCommandProvider()
    {
        Id = "Calculator";
        DisplayName = Resources.calculator_display_name;
        Icon = IconHelpers.FromRelativePath("Assets\\Calculator.svg");
    }

    public override ICommandItem[] TopLevelCommands() => [_listItem];

    public override IFallbackCommandItem[] FallbackCommands() => [_fallback];
}

// The calculator page is a dynamic list page
// * The first command is where we display the results. Title=result, Subtitle=query
//   - The default command is `SaveCommand`.
//     - When you save, insert into list at spot 1
//     - change SearchText to the result
//   - MoreCommands: a single `CopyCommand` to copy the result to the clipboard
// * The rest of the items are previously saved results
//   - Command is a CopyCommand
//   - Each item also sets the TextToSuggest to the result
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "This is sample code")]
public sealed partial class CalculatorListPage : DynamicListPage
{
    private readonly List<ListItem> _items = [];
    private readonly SaveCommand _saveCommand = new();
    private readonly CopyTextCommand _copyContextCommand;
    private readonly CommandContextItem _copyContextMenuItem;
    private static readonly CompositeFormat ErrorMessage = System.Text.CompositeFormat.Parse(Properties.Resources.calculator_error);

    public CalculatorListPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\Calculator.svg");
        Name = Resources.calculator_title;
        PlaceholderText = Resources.calculator_placeholder_text;
        Id = "com.microsoft.cmdpal.calculator";

        _copyContextCommand = new CopyTextCommand(string.Empty);
        _copyContextMenuItem = new CommandContextItem(_copyContextCommand);

        _items.Add(new(_saveCommand) { Icon = new IconInfo("\uE94E") });

        UpdateSearchText(string.Empty, string.Empty);

        _saveCommand.SaveRequested += HandleSave;
    }

    private void HandleSave(object sender, object args)
    {
        var lastResult = _items[0].Title;
        if (!string.IsNullOrEmpty(lastResult))
        {
            var li = new ListItem(new CopyTextCommand(lastResult))
            {
                Title = _items[0].Title,
                Subtitle = _items[0].Subtitle,
                TextToSuggest = lastResult,
            };
            _items.Insert(1, li);
            _items[0].Subtitle = string.Empty;
            SearchText = lastResult;
            this.RaiseItemsChanged(this._items.Count);
        }
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        var firstItem = _items[0];
        if (string.IsNullOrEmpty(newSearch))
        {
            firstItem.Title = Resources.calculator_placeholder_text;
            firstItem.Subtitle = string.Empty;
            firstItem.MoreCommands = [];
        }
        else
        {
            _copyContextCommand.Text = ParseQuery(newSearch, out var result) ? result : string.Empty;
            firstItem.Title = result;
            firstItem.Subtitle = newSearch;
            firstItem.MoreCommands = [_copyContextMenuItem];
        }
    }

    internal static bool ParseQuery(string equation, out string result)
    {
        try
        {
            var resultNumber = new DataTable().Compute(equation, null);
            result = resultNumber.ToString() ?? string.Empty;
            return true;
        }
        catch (Exception e)
        {
            result = string.Format(CultureInfo.CurrentCulture, ErrorMessage, e.Message);
            return false;
        }
    }

    public override IListItem[] GetItems() => _items.ToArray();
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "This is sample code")]
public sealed partial class SaveCommand : InvokableCommand
{
    public event TypedEventHandler<object, object> SaveRequested;

    public SaveCommand()
    {
        Name = Resources.calculator_save_command_name;
    }

    public override ICommandResult Invoke()
    {
        SaveRequested?.Invoke(this, this);
        return CommandResult.KeepOpen();
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "This is sample code")]
internal sealed partial class FallbackCalculatorItem : FallbackCommandItem
{
    private readonly CopyTextCommand _copyCommand = new(string.Empty);
    private static readonly IconInfo _cachedIcon = IconHelpers.FromRelativePath("Assets\\Calculator.svg");

    public FallbackCalculatorItem()
        : base(new NoOpCommand(), Resources.calculator_title)
    {
        Command = _copyCommand;
        _copyCommand.Name = string.Empty;
        Title = string.Empty;
        Subtitle = Resources.calculator_placeholder_text;
        Icon = _cachedIcon;
    }

    public override void UpdateQuery(string query)
    {
        if (CalculatorListPage.ParseQuery(query, out var result))
        {
            _copyCommand.Text = result;
            _copyCommand.Name = string.IsNullOrWhiteSpace(query) ? string.Empty : Resources.calculator_copy_command_name;
            Title = result;

            // we have to make the subtitle the equation,
            // so that we will still string match the original query
            // Otherwise, something like 1+2 will have a title of "3" and not match
            Subtitle = query;
        }
        else
        {
            _copyCommand.Text = string.Empty;
            _copyCommand.Name = string.Empty;
            Title = string.Empty;
            Subtitle = string.Empty;
        }
    }
}
