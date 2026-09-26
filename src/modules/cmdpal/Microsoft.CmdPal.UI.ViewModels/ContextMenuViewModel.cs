// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels;

public partial class ContextMenuViewModel : ObservableObject
{
    private readonly IFuzzyMatcherProvider _fuzzyMatcherProvider;
    private readonly List<(IContextMenuContext Context, IReadOnlyList<IContextItemViewModel> Commands)> _contextMenuStack = [];

    public IContextMenuContext? SelectedItem { get; private set; }

    public IContextMenuContext? CurrentContext => _contextMenuStack.LastOrDefault().Context;

    private IReadOnlyList<IContextItemViewModel>? CurrentContextMenu => _contextMenuStack.LastOrDefault().Commands;

    public CommandItemViewModel? SecondaryCommand
    {
        get
        {
            var (context, commands) = _contextMenuStack.LastOrDefault();
            var secondary = (context as ICommandBarContext)?.SecondaryCommand;

            // Names can change action roles without replacing the displayed rows.
            // A replacement command is usable only once its row is in this menu.
            return secondary is not null && commands.Any(command => ReferenceEquals(command, secondary)) ? secondary : null;
        }
    }

    [ObservableProperty]
    public partial ObservableCollection<IContextItemViewModel> FilteredItems { get; set; } = [];

    [ObservableProperty]
    public partial bool FilterOnTop { get; set; } = false;

    private string _lastSearchText = string.Empty;

    public ContextMenuViewModel(IFuzzyMatcherProvider fuzzyMatcherProvider)
    {
        _fuzzyMatcherProvider = fuzzyMatcherProvider;
    }

    public void PrepareForOpen(IContextMenuContext context, CommandContextItemViewModel? initialSubmenu = null)
    {
        Close();
        SelectedItem = context;
        PushContextStack(context);
        if (initialSubmenu?.HasSubmenu == true && context.AllCommands.Contains(initialSubmenu))
        {
            PushContextStack(initialSubmenu);
        }

        RefreshCurrentLevel(resetSearch: true);
    }

    public void Close()
    {
        while (_contextMenuStack.Count > 0)
        {
            RemoveLastContext();
        }

        SelectedItem = null;
        _lastSearchText = string.Empty;
        FilteredItems.Clear();
    }

    private void Context_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(IContextMenuContext.AllCommands))
        {
            return;
        }

        var index = _contextMenuStack.FindIndex(level => ReferenceEquals(level.Context, sender));
        if (index < 0)
        {
            return;
        }

        RefreshContextSnapshot(index);

        var stackChanged = false;
        for (var i = 1; i < _contextMenuStack.Count; i++)
        {
            if (_contextMenuStack[i].Context is CommandContextItemViewModel submenu &&
                (!submenu.HasSubmenu || !_contextMenuStack[i - 1].Commands.Contains(submenu)))
            {
                while (_contextMenuStack.Count > i)
                {
                    RemoveLastContext();
                }

                stackChanged = true;
                break;
            }
        }

        if (stackChanged || index == _contextMenuStack.Count - 1)
        {
            RefreshCurrentLevel(resetSearch: stackChanged);
        }
    }

    private void RefreshContextSnapshot(int index)
    {
        var (context, commands) = _contextMenuStack[index];
        var updatedCommands = context.AllCommands;
        if (!ReferenceEquals(commands, updatedCommands))
        {
            ClearDefaultShortcuts(commands);
            _contextMenuStack[index] = (context, updatedCommands);
        }
    }

    public void SetSearchText(string searchText)
    {
        if (searchText == _lastSearchText || SelectedItem is null)
        {
            return;
        }

        _lastSearchText = searchText;
        UpdateFilteredItems();
    }

    private void UpdateFilteredItems()
    {
        if (CurrentContextMenu is null)
        {
            FilteredItems.Clear();
            return;
        }

        if (string.IsNullOrEmpty(_lastSearchText))
        {
            ListHelpers.InPlaceUpdateList(FilteredItems, CurrentContextMenu);
            return;
        }

        var commands = CurrentContextMenu
                            .OfType<CommandContextItemViewModel>()
                            .Where(c => c.ShouldBeVisible);

        var query = _fuzzyMatcherProvider.Current.PrecomputeQuery(_lastSearchText);
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

    public CommandContextItemViewModel? FindKeybinding(KeyChord chord) =>
        IContextMenuContext.FindKeybinding(CurrentContextMenu, chord);

    public bool CanPopContextStack() => _contextMenuStack.Count > 1;

    public void PopContextStack()
    {
        if (CanPopContextStack())
        {
            RemoveLastContext();
            RefreshCurrentLevel(resetSearch: true);
        }
    }

    private void PushContextStack(IContextMenuContext context)
    {
        ClearDefaultShortcuts(CurrentContextMenu);
        _contextMenuStack.Add((context, context.AllCommands));
        context.PropertyChanged += Context_PropertyChanged;
    }

    private void RemoveLastContext()
    {
        var (context, commands) = _contextMenuStack[^1];
        context.PropertyChanged -= Context_PropertyChanged;
        ClearDefaultShortcuts(commands);
        _contextMenuStack.RemoveAt(_contextMenuStack.Count - 1);
    }

    private static void ClearDefaultShortcuts(IReadOnlyList<IContextItemViewModel>? commands)
    {
        if (commands is null)
        {
            return;
        }

        foreach (var command in commands.OfType<CommandContextItemViewModel>())
        {
            command.SetDefaultShortcut(DefaultShortcutRole.None);
        }
    }

    private void RefreshCurrentLevel(bool resetSearch)
    {
        RefreshContextSnapshot(_contextMenuStack.Count - 1);
        var (context, commands) = _contextMenuStack[^1];
        var primary = context switch
        {
            CommandItemViewModel item => item.PrimaryMenuItem,
            ICommandBarContext commandContext => commandContext.PrimaryCommand as CommandContextItemViewModel,
            _ => null,
        };
        var secondary = SecondaryCommand;
        foreach (var command in commands.OfType<CommandContextItemViewModel>())
        {
            var role = ReferenceEquals(command, primary) ? DefaultShortcutRole.Primary :
                ReferenceEquals(command, secondary) ? DefaultShortcutRole.Secondary : DefaultShortcutRole.None;
            command.SetDefaultShortcut(role);
        }

        if (resetSearch)
        {
            _lastSearchText = string.Empty;
        }

        UpdateFilteredItems();
        OnPropertyChanged(resetSearch ? nameof(CurrentContext) : nameof(FilteredItems));
    }

    /// <summary>
    /// Raised after a command is actually invoked (i.e. sent as a <see cref="PerformCommandMessage"/>)
    /// from this context menu. Not raised when the user navigates into a submenu.
    /// </summary>
    public event EventHandler<CommandItemViewModel>? CommandInvoked;

    /// <summary>
    /// Raised immediately before the <see cref="PerformCommandMessage"/> is sent.
    /// Subscribers can decorate the message (for example, to attach an
    /// <see cref="PerformCommandMessage.OnBeforeShowConfirmation"/> callback).
    /// Not raised when the user navigates into a submenu.
    /// </summary>
    public event EventHandler<PerformCommandMessage>? CommandInvoking;

    public ContextKeybindingResult InvokeCommand(CommandItemViewModel? command, bool navigateSubmenus = true)
    {
        if (command is null)
        {
            return ContextKeybindingResult.Unhandled;
        }

        if (navigateSubmenus && command.HasSubmenu)
        {
            PushContextStack(command);
            RefreshCurrentLevel(resetSearch: true);
            return ContextKeybindingResult.KeepOpen;
        }

        var message = new PerformCommandMessage(command.Command.Model, command.Model);
        CommandInvoking?.Invoke(this, message);
        WeakReferenceMessenger.Default.Send(message);
        CommandInvoked?.Invoke(this, command);
        return ContextKeybindingResult.Hide;
    }
}

public enum ContextKeybindingResult
{
    Unhandled,
    Hide,
    KeepOpen,
}
