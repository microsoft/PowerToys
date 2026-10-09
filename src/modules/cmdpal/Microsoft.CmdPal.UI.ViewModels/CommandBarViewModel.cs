// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;
using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed partial class CommandBarViewModel : ObservableObject,
    IRecipient<UpdateCommandBarMessage>
{
    private readonly DispatcherQueueTimer _debounceTimer;

    private volatile ICommandBarContext? _pendingSelectedItem;

    public ICommandBarContext? SelectedItem
    {
        get;
        set
        {
            if (ReferenceEquals(field, value))
            {
                return;
            }

            if (field is not null)
            {
                field.PropertyChanged -= SelectedItemPropertyChanged;
            }

            field = value;

            if (field is not null)
            {
                field.PropertyChanged += SelectedItemPropertyChanged;
            }

            UpdateContextItems();
            OnPropertyChanged();
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPrimaryCommand))]
    public partial CommandItemViewModel? PrimaryCommand { get; set; }

    public bool HasPrimaryCommand => PrimaryCommand is not null && PrimaryCommand.ShouldBeVisible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSecondaryCommand))]
    public partial CommandItemViewModel? SecondaryCommand { get; set; }

    public bool HasSecondaryCommand => SecondaryCommand is not null;

    /// <summary>
    /// Gets or sets whether the command bar shows the More button.
    /// </summary>
    /// <remarks>
    /// The secondary command already has its own button. The selected context reports
    /// <see cref="ICommandBarContext.HasOverflowCommands"/> when other visible commands remain.
    /// The menu must also be openable. Use <see cref="CanOpenContextMenu"/> to decide whether
    /// input can open the menu, independently of this button's visibility.
    /// </remarks>
    [ObservableProperty]
    public partial bool ShouldShowMoreCommandsButton { get; set; } = false;

    /// <summary>
    /// Gets whether the selected context has a visible command and can open its context menu.
    /// </summary>
    /// <remarks>
    /// Used by the menu and keyboard handlers. Ctrl+K can open the menu even when
    /// <see cref="ShouldShowMoreCommandsButton"/> is false.
    /// This value is read from <see cref="SelectedItem"/> on demand and does not raise change notifications.
    /// </remarks>
    public bool CanOpenContextMenu => SelectedItem?.CanOpenContextMenu ?? false;

    [ObservableProperty]
    public partial PageViewModel? CurrentPage { get; set; }

    public CommandBarViewModel()
    {
        var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        if (dispatcherQueue is null)
        {
            throw new InvalidOperationException("DispatcherQueue is not available for the current thread.");
        }

        _debounceTimer = dispatcherQueue.CreateTimer();
        WeakReferenceMessenger.Default.Register<UpdateCommandBarMessage>(this);
    }

    public void Receive(UpdateCommandBarMessage message)
    {
        _pendingSelectedItem = message.ViewModel;

        // immediate: false is intentional — the timer tick always fires on the
        // dispatcher queue thread, which guarantees ApplyPendingSelectedItem
        // runs on the UI thread even if Receive is called from a background
        // thread. Using immediate: true would invoke the delegate synchronously
        // on the calling thread, bypassing the dispatcher.
        _debounceTimer.Debounce(ApplyPendingSelectedItem, TimeSpan.FromMilliseconds(50));
    }

    private void ApplyPendingSelectedItem()
    {
        SelectedItem = _pendingSelectedItem;
    }

    private void SelectedItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SelectedItem.HasOverflowCommands):
            case nameof(SelectedItem.CanOpenContextMenu):
            case nameof(SelectedItem.PrimaryCommand):
            case nameof(SelectedItem.AllCommands):
            case nameof(SelectedItem.SecondaryCommand):
                UpdateContextItems();
                break;
        }
    }

    private void UpdateContextItems()
    {
        if (SelectedItem is null)
        {
            PrimaryCommand = null;
            SecondaryCommand = null;
            ShouldShowMoreCommandsButton = false;
            return;
        }

        PrimaryCommand = SelectedItem.PrimaryCommand;
        SecondaryCommand = SelectedItem.SecondaryCommand;
        ShouldShowMoreCommandsButton = SelectedItem.HasOverflowCommands && SelectedItem.CanOpenContextMenu;

        // The primary command can change visibility without being replaced.
        OnPropertyChanged(nameof(HasPrimaryCommand));
    }

    // InvokeItemCommand is what this will be in Xaml due to source generator
    // this comes in when an item in the list is tapped
    // [RelayCommand]
    public ContextKeybindingResult InvokeItem(CommandContextItemViewModel item) =>
        PerformCommand(item);

    // this comes in when the primary button is tapped
    public void InvokePrimaryCommand()
    {
        PerformCommand(PrimaryCommand);
    }

    // this comes in when the secondary button is tapped
    public void InvokeSecondaryCommand()
    {
        PerformCommand(SecondaryCommand);
    }

    public ContextKeybindingResult CheckKeybinding(bool ctrl, bool alt, bool shift, bool win, VirtualKey key)
    {
        var keybindings = SelectedItem?.Keybindings();
        if (keybindings is not null)
        {
            // Does the pressed key match any of the keybindings?
            var pressedKeyChord = KeyChordHelpers.FromModifiers(ctrl, alt, shift, win, key, 0);
            if (keybindings.TryGetValue(pressedKeyChord, out var matchedItem))
            {
                return matchedItem is not null ? PerformCommand(matchedItem) : ContextKeybindingResult.Unhandled;
            }
        }

        return ContextKeybindingResult.Unhandled;
    }

    private ContextKeybindingResult PerformCommand(CommandItemViewModel? command)
    {
        if (command is null)
        {
            return ContextKeybindingResult.Unhandled;
        }

        WeakReferenceMessenger.Default.Send<PerformCommandMessage>(new(command.Command.Model, command.Model));
        if (command.HasSubmenu)
        {
            return ContextKeybindingResult.KeepOpen;
        }
        else
        {
            return ContextKeybindingResult.Hide;
        }
    }
}

public enum ContextKeybindingResult
{
    Unhandled,
    Hide,
    KeepOpen,
}
