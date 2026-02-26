// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.Logging;
using Windows.System;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ContextMenuViewModel : ObservableObject,
    IRecipient<UpdateCommandBarMessage>
{
    private readonly ILogger _logger;
    private readonly IFuzzyMatcherProvider _fuzzyMatcherProvider;

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

    public ContextMenuViewModel(
        IFuzzyMatcherProvider fuzzyMatcherProvider,
        ILogger<ContextMenuViewModel> logger)
    {
        _logger = logger;
        _fuzzyMatcherProvider = fuzzyMatcherProvider;
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
            if (SelectedItem.PrimaryCommand is not null || SelectedItem.HasMoreCommands)
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
            ListHelpers.InPlaceUpdateList(FilteredItems, CurrentContextMenu);
            return;
        }

        var commands = CurrentContextMenu
                            .OfType<CommandContextItemViewModel>()
                            .Where(c => c.ShouldBeVisible);

        var query = _fuzzyMatcherProvider.Current.PrecomputeQuery(searchText);
        var newResults = InternalListHelpers.FilterList(commands, in query, ScoreFunction);
        ListHelpers.InPlaceUpdateList(FilteredItems, newResults);
    }

    private int ScoreFunction(in FuzzyQuery query, CommandContextItemViewModel item)
    {
        if (string.IsNullOrWhiteSpace(query.Original))
        {
            return 1;
        }

        if (string.IsNullOrEmpty(item.Title))
        {
            return 0;
        }

        var fuzzyMatcher = _fuzzyMatcherProvider.Current;
        var title = item.GetTitleTarget(fuzzyMatcher);
        var subtitle = item.GetSubtitleTarget(fuzzyMatcher);

        var titleScore = fuzzyMatcher.Score(query, title);
        var subtitleScore = (fuzzyMatcher.Score(query, subtitle) - 4) / 2;

        return Max3(titleScore, subtitleScore, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Max3(int a, int b, int c)
    {
        var m = a > b ? a : b;
        return m > c ? m : c;
    }

    /// <summary>
    /// Generates a mapping of key -> command item for this particular item's
    /// MoreCommands. (This won't include the primary Command, but it will
    /// include the secondary one). This map can be used to quickly check if a
    /// shortcut key was pressed. In case there are duplicate keybindings, the first
    /// one is used and the rest are ignored.
    /// </summary>
    /// <returns>a dictionary of KeyChord -> Context commands, for all commands
    /// that have a shortcut key set.</returns>
    private Dictionary<KeyChord, CommandContextItemViewModel> Keybindings()
    {
        var result = new Dictionary<KeyChord, CommandContextItemViewModel>();

        var menu = CurrentContextMenu;
        if (menu is null)
        {
            return result;
        }

        foreach (var item in menu)
        {
            if (item is CommandContextItemViewModel cmd && cmd.HasRequestedShortcut)
            {
                var key = cmd.RequestedShortcut ?? new KeyChord(0, 0, 0);
                var added = result.TryAdd(key, cmd);
                if (!added)
                {
                    Log_DuplicateKeyboardShortcut(KeyChordHelpers.FormatForDebug(key), cmd.Title ?? cmd.Name ?? "(unknown)");
                }
            }
        }

        return result;
    }

    public ContextKeybindingResult? CheckKeybinding(bool ctrl, bool alt, bool shift, bool win, VirtualKey key)
    {
        var keybindings = Keybindings();

        // Does the pressed key match any of the keybindings?
        var pressedKeyChord = KeyChordHelpers.FromModifiers(ctrl, alt, shift, win, key, 0);
        return keybindings.TryGetValue(pressedKeyChord, out var item) ? InvokeCommand(item) : null;
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

        ListHelpers.InPlaceUpdateList(FilteredItems, CurrentContextMenu!);
    }

    private void PushContextStack(IEnumerable<IContextItemViewModel> commands)
    {
        ContextMenuStack.Add(commands.ToList());
        OnPropertyChanging(nameof(CurrentContextMenu));
        OnPropertyChanged(nameof(CurrentContextMenu));

        ListHelpers.InPlaceUpdateList(FilteredItems, CurrentContextMenu!);
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
            ListHelpers.InPlaceUpdateList(FilteredItems, CurrentContextMenu!);
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Ignoring duplicate keyboard shortcut {KeyChord} on command '{CommandName}'")]
    partial void Log_DuplicateKeyboardShortcut(string keyChord, string commandName);
}
