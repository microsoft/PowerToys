// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
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
    // private readonly SaveCommand _saveCommand = new();
    private readonly Lock _resultsLock = new();
    private SettingsManager _settingsManager;
    private IList<ListItem> _items = [];

    public CalculatorListPage(SettingsManager settings)
    {
        _settingsManager = settings;
        Icon = IconHelpers.FromRelativePath("Assets\\Calculator.svg");
        Name = Resources.calculator_title;
        PlaceholderText = Resources.calculator_placeholder_text;
        Id = "com.microsoft.cmdpal.calculator";

        UpdateSearchText(string.Empty, string.Empty);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (newSearch == oldSearch)
        {
            return;
        }

        var results = QueryHelper.Query(newSearch, _settingsManager, false);
        UpdateResult(results);
    }

    private void UpdateResult(IList<ListItem> result)
    {
        lock (_resultsLock)
        {
            this._items = result;
        }

        RaiseItemsChanged(this._items.Count);
    }

    public override IListItem[] GetItems() => _items.ToArray();
}
