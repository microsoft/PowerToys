// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ContextMenuStackViewModel : ObservableObject
{
    [ObservableProperty]
    public partial ObservableCollection<CommandContextItemViewModel> ContextCommands { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<CommandContextItemViewModel> FilteredItems { get; set; }

    private string _lastSearchText = string.Empty;

    // private Dictionary<KeyChord, CommandContextItemViewModel>? _contextKeybindings;
    public ContextMenuStackViewModel(IEnumerable<CommandContextItemViewModel> commands)
    {
        ContextCommands = [.. commands];
        FilteredItems = [.. ContextCommands];
    }

    public void SetSearchText(string searchText)
    {
        if (searchText == _lastSearchText)
        {
            return;
        }

        _lastSearchText = searchText;

        if (string.IsNullOrEmpty(searchText))
        {
            ListHelpers.InPlaceUpdateList(FilteredItems, ContextCommands);
            return;
        }

        var newResults = ListHelpers.FilterList<CommandContextItemViewModel>(ContextCommands, searchText, ScoreContextCommand);
        ListHelpers.InPlaceUpdateList(FilteredItems, newResults);
    }

    private static int ScoreContextCommand(string query, CommandContextItemViewModel item)
    {
        if (string.IsNullOrEmpty(query) || string.IsNullOrWhiteSpace(query))
        {
            return 1;
        }

        if (string.IsNullOrEmpty(item.Title))
        {
            return 0;
        }

        var nameMatch = StringMatcher.FuzzySearch(query, item.Title);

        var descriptionMatch = StringMatcher.FuzzySearch(query, item.Subtitle);

        return new[] { nameMatch.Score, (descriptionMatch.Score - 4) / 2, 0 }.Max();
    }
}
