// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace Microsoft.CmdPal.Core.ViewModels;

public partial class ContextMenuViewModel : ObservableObject,
    IRecipient<UpdateCommandBarMessage>
{
    public ICommandBarContext? SelectedItem
    {
        get => field;
        set
        {
            field = value;
            UpdateContextItems();
        }
    }

    [ObservableProperty]
    private partial ObservableCollection<List<IContextItemViewModel>> ContextMenuStack { get; set; } = [];

    private List<IContextItemViewModel>? CurrentContextMenu => ContextMenuStack.LastOrDefault();

    [ObservableProperty]
    public partial ObservableCollection<IContextItemViewModel> FilteredItems { get; set; } = [];

    [ObservableProperty]
    public partial bool FilterOnTop { get; set; } = false;

    private string _lastSearchText = string.Empty;

    public ContextMenuViewModel()
    {
        WeakReferenceMessenger.Default.Register<UpdateCommandBarMessage>(this);
    }

    public void Receive(UpdateCommandBarMessage message)
    {
        SelectedItem = message.ViewModel;
    }

    public void UpdateContextItems()
    {
        if (SelectedItem is not null)
        {
            if (SelectedItem.MoreCommands.Count() > 1)
            {
                ContextMenuStack.Clear();
                PushContextStack(SelectedItem.AllCommands);
            }
        }
    }

    public void SetSearchText(string searchText)
    {
        if (searchText == _lastSearchText)
        {
            return;
        }

        if (SelectedItem is null)
        {
            return;
        }

        _lastSearchText = searchText;

        if (CurrentContextMenu is null)
        {
            ListHelpers.InPlaceUpdateList(FilteredItems, []);
            return;
        }

        if (string.IsNullOrEmpty(searchText))
        {
            ListHelpers.InPlaceUpdateList(FilteredItems, [.. CurrentContextMenu]);
            return;
        }

        var commands = CurrentContextMenu
                            .OfType<CommandContextItemViewModel>()
                            .Where(c => c.ShouldBeVisible);

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

    /// <summary>
    /// Generates a mapping of key -> command item for this particular item's
    /// MoreCommands. (This won't include the primary Command, but it will
    /// include the secondary one). This map can be used to quickly check if a
    /// shortcut key was pressed
    /// </summary>
    /// <returns>a dictionary of KeyChord -> Context commands, for all commands
    /// that have a shortcut key set.</returns>
    public Dictionary<KeyChord, CommandContextItemViewModel> Keybindings()
    {
        if (CurrentContextMenu is null)
        {
            return [];
        }

        return CurrentContextMenu
            .OfType<CommandContextItemViewModel>()
            .Where(c => c.HasRequestedShortcut)
            .ToDictionary(
            c => c.RequestedShortcut ?? new KeyChord(0, 0, 0),
            c => c);
    }

    public ContextKeybindingResult? CheckKeybinding(bool ctrl, bool alt, bool shift, bool win, VirtualKey key)
    {
        var keybindings = Keybindings();
        if (keybindings is not null)
        {
            // Does the pressed key match any of the keybindings?
            var pressedKeyChord = KeyChordHelpers.FromModifiers(ctrl, alt, shift, win, key, 0);
            if (keybindings.TryGetValue(pressedKeyChord, out var item))
            {
                return InvokeCommand(item);
            }
        }

        return null;
    }

    public bool CanPopContextStack()
    {
        return ContextMenuStack.Count > 1;
    }

    public void PopContextStack()
    {
        if (ContextMenuStack.Count > 1)
        {
            ContextMenuStack.RemoveAt(ContextMenuStack.Count - 1);
        }

        OnPropertyChanging(nameof(CurrentContextMenu));
        OnPropertyChanged(nameof(CurrentContextMenu));

        ListHelpers.InPlaceUpdateList(FilteredItems, [.. CurrentContextMenu!]);
    }

    private void PushContextStack(IEnumerable<IContextItemViewModel> commands)
    {
        ContextMenuStack.Add(commands.ToList());
        OnPropertyChanging(nameof(CurrentContextMenu));
        OnPropertyChanged(nameof(CurrentContextMenu));

        ListHelpers.InPlaceUpdateList(FilteredItems, [.. CurrentContextMenu!]);
    }

    public void ResetContextMenu()
    {
        while (ContextMenuStack.Count > 1)
        {
            ContextMenuStack.RemoveAt(ContextMenuStack.Count - 1);
        }

        OnPropertyChanging(nameof(CurrentContextMenu));
        OnPropertyChanged(nameof(CurrentContextMenu));

        if (CurrentContextMenu is not null)
        {
            ListHelpers.InPlaceUpdateList(FilteredItems, [.. CurrentContextMenu!]);
        }
    }

    public ContextKeybindingResult InvokeCommand(CommandItemViewModel? command)
    {
        if (command is null)
        {
            return ContextKeybindingResult.Unhandled;
        }

        if (command.HasMoreCommands)
        {
            // Display the commands child commands
            PushContextStack(command.AllCommands);
            OnPropertyChanging(nameof(FilteredItems));
            OnPropertyChanged(nameof(FilteredItems));
            return ContextKeybindingResult.KeepOpen;
        }
        else
        {
            WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(command.Command.Model, command.Model));
            UpdateContextItems();
            return ContextKeybindingResult.Hide;
        }
    }
}
