// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CmdPal.Common.Commands;
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
    private readonly Lock _historyLock = new();
    private readonly ISettingsInterface _settingsManager;
    private readonly List<ListItem> _items = [];
    private readonly ListItem _emptyItem;
    private List<ListItem> _historyItems = [];

    // This is the text that saved when the user click the result.
    // We need to avoid the double calculation. This may cause some wierd behaviors.
    private string _skipQuerySearchText = string.Empty;

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

        _settingsManager.HistoryChanged += SettingsManagerOnHistoryChanged;
        _settingsManager.SettingsChanged += SettingsManagerOnSettingsChanged;

        UpdateHistory();
        AppendResult(null);
        UpdateSearchText(string.Empty, string.Empty);
    }

    private void SettingsManagerOnHistoryChanged(object sender, EventArgs e)
    {
        UpdateHistory();
        AppendResult(GetCurrentResultItem());
    }

    private void SettingsManagerOnSettingsChanged(object sender, EventArgs e)
    {
        UpdateHistory();
        AppendResult(RequeryCurrentResult());
    }

    private void HandleReplaceQuery(object sender, object args)
    {
        var lastResult = _items[0].Title;
        if (!string.IsNullOrEmpty(lastResult))
        {
            _skipQuerySearchText = lastResult;
            SearchText = lastResult;
            OnPropertyChanged(nameof(SearchText));
        }
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        if (oldSearch == newSearch)
        {
            return;
        }

        if (!string.IsNullOrEmpty(_skipQuerySearchText) && newSearch == _skipQuerySearchText)
        {
            // only skip once.
            _skipQuerySearchText = string.Empty;
            return;
        }

        var copyResultToSearchText = false;
        if (_settingsManager.CopyResultToSearchBarIfQueryEndsWithEqualSign && newSearch.EndsWith('='))
        {
            newSearch = newSearch.TrimEnd('=').TrimEnd();
            copyResultToSearchText = true;
        }

        _skipQuerySearchText = string.Empty;

        _emptyItem.Subtitle = newSearch;

        var result = QueryHelper.Query(newSearch, _settingsManager, isFallbackSearch: false, out var displayQuery, HandleReplaceQuery);

        AppendResult(result);

        if (copyResultToSearchText && result is not null)
        {
            _skipQuerySearchText = result.Title;
            SearchText = result.Title;

            // LOAD BEARING: The SearchText setter does not raise a PropertyChanged notification,
            // so we must raise it explicitly to ensure the UI updates correctly.
            OnPropertyChanged(nameof(SearchText));
        }
    }

    private void AppendResult(ListItem result)
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

            lock (_historyLock)
            {
                if (_historyItems.Count > 0)
                {
                    this._items.Add(CreateSectionHeader(Resources.calculator_history_header));
                    this._items.AddRange(_historyItems);
                }
            }
        }

        RaiseItemsChanged(this._items.Count);
    }

    private void UpdateHistory()
    {
        List<ListItem> history = [];

        var items = _settingsManager.HistoryItems;
        for (var index = items.Count - 1; index >= 0; index--)
        {
            var historyItem = items[index];
            history.Add(CreateHistoryItem(historyItem));
        }

        lock (_historyLock)
        {
            _historyItems = history;
        }
    }

    private ListItem CreateHistoryItem(HistoryItem historyItem)
    {
        var copyCommand = new CalculatorCopyCommand(historyItem.Result, historyItem.Query, _settingsManager, canStoreHistory: false);
        var pasteCommand = new CalculatorPasteCommand(historyItem.Result, historyItem.Query, _settingsManager, canStoreHistory: false);
        var primaryCommand = _settingsManager.PrimaryAction == PrimaryAction.Paste ? (ICommand)pasteCommand : copyCommand;
        var secondaryCommand = _settingsManager.PrimaryAction == PrimaryAction.Paste ? (ICommand)copyCommand : pasteCommand;

        var deleteConfirmationCommand = new ConfirmableCommand
        {
            Command = new DeleteHistoryItemCommand(_settingsManager, historyItem.Id),
            ConfirmationTitle = Resources.calculator_delete_confirmation_title,
            ConfirmationMessage = Resources.calculator_delete_confirmation_message,
            IsConfirmationRequired = () => _settingsManager.DeleteHistoryRequiresConfirmation,
        };

        var deleteAllConfirmationCommand = new ConfirmableCommand
        {
            Command = new ClearHistoryCommand(_settingsManager),
            ConfirmationTitle = Resources.calculator_delete_all_confirmation_title,
            ConfirmationMessage = Resources.calculator_delete_all_confirmation_message,
            IsConfirmationRequired = () => _settingsManager.DeleteHistoryRequiresConfirmation,
        };

        return new ListItem(primaryCommand)
        {
            Icon = Icons.HistoryIcon,
            Title = historyItem.Result,
            Subtitle = historyItem.Query,
            TextToSuggest = historyItem.Result,
            MoreCommands =
            [
                new CommandContextItem(secondaryCommand),
                new Separator(),
                new CommandContextItem(deleteConfirmationCommand) { IsCritical = true, RequestedShortcut = KeyChords.DeleteItemFromHistory, },
                new CommandContextItem(deleteAllConfirmationCommand) { IsCritical = true, RequestedShortcut = KeyChords.ClearHistory, },
            ],
        };
    }

    private ListItem GetCurrentResultItem()
    {
        lock (_resultsLock)
        {
            return _items.Count > 0 ? _items[0] : _emptyItem;
        }
    }

    private ListItem RequeryCurrentResult()
    {
        var searchText = SearchText ?? string.Empty;
        if (string.IsNullOrEmpty(searchText))
        {
            return null;
        }

        return QueryHelper.Query(searchText, _settingsManager, isFallbackSearch: false, out _, HandleReplaceQuery);
    }

    public override IListItem[] GetItems() => _items.ToArray();

    private static ListItem CreateSectionHeader(string title)
    {
        return new ListItem(new NoOpCommand())
        {
            Title = title,
            Section = title,
            Command = null!,
        };
    }
}
