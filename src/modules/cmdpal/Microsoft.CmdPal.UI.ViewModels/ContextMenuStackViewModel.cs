// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ContextMenuStackViewModel : ObservableObject
{
    [ObservableProperty]
    public partial ObservableCollection<CommandContextItemViewModel> FilteredItems { get; set; }

    private readonly IContextMenuContext _context;
    private string _lastSearchText = string.Empty;

    // private Dictionary<KeyChord, CommandContextItemViewModel>? _contextKeybindings;
    public ContextMenuStackViewModel(IContextMenuContext context)
    {
        _context = context;
        FilteredItems = [.. context.AllCommands];
    }

    public void SetSearchText(string searchText)
    {
        if (searchText == _lastSearchText)
        {
            return;
        }

        _lastSearchText = searchText;

        var commands = _context.AllCommands.Where(c => c.ShouldBeVisible);
        if (string.IsNullOrEmpty(searchText))
        {
            ListHelpers.InPlaceUpdateList(FilteredItems, commands);
            return;
        }

        var newResults = ListHelpers.FilterList<CommandContextItemViewModel>(commands, searchText, ScoreContextCommand);
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

    public CommandContextItemViewModel? CheckKeybinding(bool ctrl, bool alt, bool shift, bool win, VirtualKey key)
    {
        var keybindings = _context.Keybindings();
        if (keybindings != null)
        {
            // Does the pressed key match any of the keybindings?
            var pressedKeyChord = KeyChordHelpers.FromModifiers(ctrl, alt, shift, win, key, 0);
            if (keybindings.TryGetValue(pressedKeyChord, out var item))
            {
                return item;
            }
        }

        return null;
    }
}
