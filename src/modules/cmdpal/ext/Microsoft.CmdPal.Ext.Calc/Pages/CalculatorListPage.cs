// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using Microsoft.CmdPal.Ext.Calc.Helper;
using Microsoft.CmdPal.Ext.Calc.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Calc.Pages;

// The calculator page is a dynamic list page
// * The first command is where we display the results. Title=result, Subtitle=query
//   - The default command is `SaveCommand`.
//     - When you save, insert into list at spot 1
//     - change SearchText to the result
//   - MoreCommands: a single `CopyCommand` to copy the result to the clipboard
// * The rest of the items are previously saved results
//   - Command is a CopyCommand
//   - Each item also sets the TextToSuggest to the result
public sealed partial class CalculatorListPage : DynamicListPage
{
    private readonly Lock _resultsLock = new();
    private readonly ISettingsInterface _settingsManager;
    private readonly List<ListItem> _items = [];
    private readonly List<ListItem> history = [];
    private readonly ListItem _emptyItem;

    // This is the text that saved when the user click the result.
    // We need to avoid the double calculation. This may cause some wierd behaviors.
    private string skipQuerySearchText = string.Empty;

    public CalculatorListPage(ISettingsInterface settings)
    {
        _settingsManager = settings;
        Icon = Icons.CalculatorIcon;
        Name = Resources.calculator_title;
        PlaceholderText = Resources.calculator_placeholder_text;
        Id = "com.microsoft.cmdpal.calculator";

        _emptyItem = new ListItem(new NoOpCommand())
        {
            Title = Resources.calculator_placeholder_text,
            Icon = Icons.ResultIcon,
        };
        EmptyContent = new CommandItem(new NoOpCommand())
        {
            Icon = Icons.CalculatorIcon,
            Title = Resources.calculator_placeholder_text,
        };

        UpdateSearchText(string.Empty, string.Empty);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (oldSearch == newSearch)
        {
            return;
        }

        if (!string.IsNullOrEmpty(skipQuerySearchText) && newSearch == skipQuerySearchText)
        {
            // only skip once.
            skipQuerySearchText = string.Empty;
            return;
        }

        skipQuerySearchText = string.Empty;

        _emptyItem.Subtitle = newSearch;

        var result = QueryHelper.Query(newSearch, _settingsManager, false, HandleSave);
        UpdateResult(result);
    }

    private void UpdateResult(ListItem result)
    {
        lock (_resultsLock)
        {
            this._items.Clear();

            if (result is not null)
            {
                this._items.Add(result);
            }
            else
            {
                _items.Add(_emptyItem);
            }

            this._items.AddRange(history);
        }

        RaiseItemsChanged(this._items.Count);
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

            history.Insert(0, li);
            _items.Insert(1, li);

            // Why we need to clean the query record? Removed, but if necessary, please move it back.
            // _items[0].Subtitle = string.Empty;

            // this change will call the UpdateSearchText again.
            // We need to avoid it.
            skipQuerySearchText = lastResult;
            SearchText = lastResult;
            this.RaiseItemsChanged(this._items.Count);
        }
    }

    public override IListItem[] GetItems() => _items.ToArray();
}
