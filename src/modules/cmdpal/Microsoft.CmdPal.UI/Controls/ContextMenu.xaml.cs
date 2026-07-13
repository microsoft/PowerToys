// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.WinUI;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class ContextMenu : UserControl,
    IRecipient<UpdateCommandBarMessage>,
    IRecipient<TryCommandKeybindingMessage>
{
    public static readonly DependencyProperty ShowFilterBoxProperty =
        DependencyProperty.Register(nameof(ShowFilterBox), typeof(bool), typeof(ContextMenu), new PropertyMetadata(true));

    public static readonly DependencyProperty SubscribeToCommandBarProperty =
        DependencyProperty.Register(nameof(SubscribeToCommandBar), typeof(bool), typeof(ContextMenu), new PropertyMetadata(true, OnSubscribeToCommandBarChanged));

    public bool ShowFilterBox
    {
        get => (bool)GetValue(ShowFilterBoxProperty);
        set => SetValue(ShowFilterBoxProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether this control listens to the command bar's
    /// selection and keybinding messages. Set to false for standalone usage (e.g. dock)
    /// where the caller manages selection and opening directly.
    /// </summary>
    public bool SubscribeToCommandBar
    {
        get => (bool)GetValue(SubscribeToCommandBarProperty);
        set => SetValue(SubscribeToCommandBarProperty, value);
    }

    public ContextMenuViewModel ViewModel { get; }

    private bool _suppressProgrammaticUpdates;

    private bool _isMenuOpen;

    private (int Index, string Title) _lastAnnouncedSelectionKey = (-1, string.Empty);

    public ContextMenu()
    {
        this.InitializeComponent();

        ViewModel = new ContextMenuViewModel(App.Current.Services.GetRequiredService<IFuzzyMatcherProvider>());
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;

        if (SubscribeToCommandBar)
        {
            HookCommandBar();
        }
    }

    private static void OnSubscribeToCommandBarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ContextMenu control)
        {
            if (e.NewValue is true)
            {
                control.HookCommandBar();
            }
            else
            {
                control.UnhookCommandBar();
            }
        }
    }

    private void HookCommandBar()
    {
        var messenger = WeakReferenceMessenger.Default;

        if (!messenger.IsRegistered<UpdateCommandBarMessage>(this))
        {
            messenger.Register<UpdateCommandBarMessage>(this);
        }

        if (!messenger.IsRegistered<TryCommandKeybindingMessage>(this))
        {
            messenger.Register<TryCommandKeybindingMessage>(this);
        }

        ViewModel.HookCommandBar();
    }

    private void UnhookCommandBar()
    {
        var messenger = WeakReferenceMessenger.Default;

        messenger.Unregister<UpdateCommandBarMessage>(this);
        messenger.Unregister<TryCommandKeybindingMessage>(this);

        ViewModel.UnhookCommandBar();
    }

    internal void PrepareForOpen(ContextMenuFilterLocation filterLocation)
    {
        _isMenuOpen = true;
        _suppressProgrammaticUpdates = true;
        _lastAnnouncedSelectionKey = (-1, string.Empty);

        ViewModel.FilterOnTop = filterLocation == ContextMenuFilterLocation.Top;
        ViewModel.ResetContextMenu();

        UpdateUiForStackChange();
    }

    /// <summary>
    /// Called after the flyout is in the visual tree. Focuses the filter box and
    /// raises a single consolidated Narrator announcement for the opened menu.
    /// </summary>
    internal void CompleteMenuOpen()
    {
        _suppressProgrammaticUpdates = true;

        if (CommandsDropdown.Items.Count > 0 && CommandsDropdown.SelectedIndex < 0)
        {
            CommandsDropdown.SelectedIndex = 0;
        }

        _ = DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Normal, () =>
        {
            AnnounceMenuOpened();

            _ = DispatcherQueue.TryEnqueue(() =>
            {
                FocusSearchBox();
                _suppressProgrammaticUpdates = false;
            });
        });
    }

    internal void NotifyMenuClosed()
    {
        _isMenuOpen = false;
        _suppressProgrammaticUpdates = false;
        _lastAnnouncedSelectionKey = (-1, string.Empty);
    }

    public void Receive(UpdateCommandBarMessage message)
    {
        UpdateUiForStackChange();
    }

    public void Receive(TryCommandKeybindingMessage msg)
    {
        var result = ViewModel?.CheckKeybinding(msg.Ctrl, msg.Alt, msg.Shift, msg.Win, msg.Key);

        if (result == ContextKeybindingResult.Hide)
        {
            msg.Handled = true;
            WeakReferenceMessenger.Default.Send<CloseContextMenuMessage>();
            UpdateUiForStackChange();
        }
        else if (result == ContextKeybindingResult.KeepOpen)
        {
            UpdateUiForStackChange();
            msg.Handled = true;
        }
        else if (result == ContextKeybindingResult.Unhandled)
        {
            msg.Handled = false;
        }
    }

    private void CommandsDropdown_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CommandContextItemViewModel item)
        {
            if (InvokeCommand(item) == ContextKeybindingResult.Hide)
            {
                WeakReferenceMessenger.Default.Send<CloseContextMenuMessage>();
            }

            UpdateUiForStackChange();
        }
    }

    private void CommandsDropdown_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

        var mods = KeyModifiers.GetCurrent();

        var result = ViewModel?.CheckKeybinding(mods.Ctrl, mods.Alt, mods.Shift, mods.Win, e.Key);

        if (result == ContextKeybindingResult.Hide)
        {
            e.Handled = true;
            WeakReferenceMessenger.Default.Send<CloseContextMenuMessage>();
            UpdateUiForStackChange();
        }
        else if (result == ContextKeybindingResult.KeepOpen)
        {
            e.Handled = true;
        }
        else if (result == ContextKeybindingResult.Unhandled)
        {
            e.Handled = false;
        }
    }

    /// <summary>
    /// Handles Escape to close the context menu and return focus to the "More" button.
    /// </summary>
    private void UserControl_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            // Close the context menu (if not already handled)
            WeakReferenceMessenger.Default.Send(new CloseContextMenuMessage());

            // Find the parent CommandBar and set focus to MoreCommandsButton
            var parent = this.FindParent<CommandBar>();
            parent?.FocusMoreCommandsButton();

            e.Handled = true;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var prop = e.PropertyName;

        if (prop == nameof(ContextMenuViewModel.FilteredItems))
        {
            UpdateUiForStackChange();
        }
    }

    private void ContextFilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressProgrammaticUpdates)
        {
            return;
        }

        _suppressProgrammaticUpdates = true;

        ViewModel?.SetSearchText(ContextFilterBox.Text);

        if (CommandsDropdown.Items.Count > 0)
        {
            CommandsDropdown.SelectedIndex = 0;
        }

        _lastAnnouncedSelectionKey = (-1, string.Empty);
        _suppressProgrammaticUpdates = false;
    }

    private void ContextFilterBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var modifiers = KeyModifiers.GetCurrent();

        if (e.Key == VirtualKey.Enter)
        {
            if (CommandsDropdown.SelectedItem is CommandContextItemViewModel item)
            {
                if (InvokeCommand(item) == ContextKeybindingResult.Hide)
                {
                    WeakReferenceMessenger.Default.Send<CloseContextMenuMessage>();
                }

                UpdateUiForStackChange();

                e.Handled = true;
            }
        }
        else if (e.Key == VirtualKey.Escape ||
            (e.Key == VirtualKey.Left && modifiers.Alt))
        {
            if (ViewModel.CanPopContextStack())
            {
                ViewModel.PopContextStack();
                UpdateUiForStackChange();
            }
            else
            {
                WeakReferenceMessenger.Default.Send<CloseContextMenuMessage>();
                WeakReferenceMessenger.Default.Send<FocusSearchBoxMessage>();
                UpdateUiForStackChange();
            }

            e.Handled = true;
        }
    }

    private void ContextFilterBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Up)
        {
            NavigateUp();

            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Down)
        {
            NavigateDown();

            e.Handled = true;
        }

        CommandsDropdown_PreviewKeyDown(sender, e);
    }

    private void NavigateUp()
    {
        var newIndex = CommandsDropdown.SelectedIndex;

        if (CommandsDropdown.SelectedIndex > 0)
        {
            newIndex--;

            while (
                newIndex >= 0 &&
                IsSeparator(CommandsDropdown.Items[newIndex]) &&
                newIndex != CommandsDropdown.SelectedIndex)
            {
                newIndex--;
            }

            if (newIndex < 0)
            {
                newIndex = CommandsDropdown.Items.Count - 1;

                while (
                    newIndex >= 0 &&
                    IsSeparator(CommandsDropdown.Items[newIndex]) &&
                    newIndex != CommandsDropdown.SelectedIndex)
                {
                    newIndex--;
                }
            }
        }
        else
        {
            newIndex = CommandsDropdown.Items.Count - 1;
        }

        CommandsDropdown.SelectedIndex = newIndex;
        AnnounceSelectedCommand();
    }

    private void NavigateDown()
    {
        var newIndex = CommandsDropdown.SelectedIndex;

        if (CommandsDropdown.SelectedIndex == CommandsDropdown.Items.Count - 1)
        {
            newIndex = 0;
        }
        else
        {
            newIndex++;

            while (
                newIndex < CommandsDropdown.Items.Count &&
                IsSeparator(CommandsDropdown.Items[newIndex]) &&
                newIndex != CommandsDropdown.SelectedIndex)
            {
                newIndex++;
            }

            if (newIndex >= CommandsDropdown.Items.Count)
            {
                newIndex = 0;

                while (
                    newIndex < CommandsDropdown.Items.Count &&
                    IsSeparator(CommandsDropdown.Items[newIndex]) &&
                    newIndex != CommandsDropdown.SelectedIndex)
                {
                    newIndex++;
                }
            }
        }

        CommandsDropdown.SelectedIndex = newIndex;
        AnnounceSelectedCommand();
    }

    private bool IsSeparator(object item)
    {
        return item is SeparatorViewModel;
    }

    private void UpdateUiForStackChange()
    {
        var previousSuppress = _suppressProgrammaticUpdates;
        _suppressProgrammaticUpdates = true;

        ContextFilterBox.Text = string.Empty;
        ViewModel?.SetSearchText(string.Empty);

        if (CommandsDropdown.Items.Count > 0)
        {
            CommandsDropdown.SelectedIndex = 0;
        }
        else
        {
            CommandsDropdown.SelectedIndex = -1;
        }

        _lastAnnouncedSelectionKey = (-1, string.Empty);
        _suppressProgrammaticUpdates = previousSuppress || _isMenuOpen;
    }

    internal void AnnounceSelectedCommand()
    {
        if (_suppressProgrammaticUpdates || FrameworkElementAutomationPeer.FromElement(CommandsDropdown) is null)
        {
            return;
        }

        var index = CommandsDropdown.SelectedIndex;
        if (index < 0 || index >= CommandsDropdown.Items.Count)
        {
            return;
        }

        if (CommandsDropdown.Items[index] is not CommandContextItemViewModel item || IsSeparator(item))
        {
            return;
        }

        var title = item.Title ?? string.Empty;
        var selectionKey = (index, title);
        if (selectionKey == _lastAnnouncedSelectionKey)
        {
            return;
        }

        _lastAnnouncedSelectionKey = selectionKey;

        var announcement = BuildCommandAnnouncementWithPosition(item, index);
        UIHelper.AnnounceActionForAccessibility(
            ContextMenuGrid,
            announcement,
            "CommandPaletteContextMenuSelectionChanged");
    }

    private void AnnounceMenuOpened()
    {
        var openedText = ResourceLoaderInstance.GetString("ContextMenu_OpenedAnnouncement");
        var count = CountVisibleCommands();
        var selectionText = BuildOpenSelectionAnnouncement();

        string announcement;
        if (count == 0)
        {
            announcement = $"{openedText}, no items";
        }
        else if (string.IsNullOrEmpty(selectionText))
        {
            announcement = $"{openedText}, {count} items";
        }
        else
        {
            announcement = $"{openedText}, {count} items, {selectionText}";
        }

        if (CommandsDropdown.SelectedIndex is var index and >= 0 &&
            CommandsDropdown.Items[index] is CommandContextItemViewModel item &&
            !IsSeparator(item))
        {
            _lastAnnouncedSelectionKey = (index, item.Title ?? string.Empty);
        }

        UIHelper.AnnounceActionForAccessibility(
            ContextMenuGrid,
            announcement,
            "CommandPaletteContextMenuOpened");
    }

    private string BuildOpenSelectionAnnouncement()
    {
        var index = CommandsDropdown.SelectedIndex;
        if (index < 0 || index >= CommandsDropdown.Items.Count ||
            CommandsDropdown.Items[index] is not CommandContextItemViewModel item ||
            IsSeparator(item))
        {
            return string.Empty;
        }

        return BuildCommandAnnouncementWithPosition(item, index);
    }

    private string BuildCommandAnnouncementWithPosition(CommandContextItemViewModel item, int listIndex)
    {
        var text = UIHelper.BuildContextCommandAnnouncement(item);
        var count = CountVisibleCommands();
        if (count <= 1)
        {
            return text;
        }

        var position = GetVisibleCommandPosition(listIndex);
        return $"{text}, {position} of {count}";
    }

    private int CountVisibleCommands()
    {
        var count = 0;
        foreach (var item in CommandsDropdown.Items)
        {
            if (!IsSeparator(item))
            {
                count++;
            }
        }

        return count;
    }

    private int GetVisibleCommandPosition(int listIndex)
    {
        if (listIndex < 0)
        {
            return 0;
        }

        var position = 0;
        for (var i = 0; i <= listIndex && i < CommandsDropdown.Items.Count; i++)
        {
            if (!IsSeparator(CommandsDropdown.Items[i]))
            {
                position++;
            }
        }

        return position;
    }

    /// <summary>
    /// Manually focuses our search box. This needs to be called after we're actually
    /// In the UI tree - if we're in a Flyout, that's not until Opened()
    /// </summary>
    internal void FocusSearchBox()
    {
        ContextFilterBox.Focus(FocusState.Programmatic);
    }

    private ContextKeybindingResult InvokeCommand(CommandItemViewModel command) => ViewModel.InvokeCommand(command);
}
