// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Microsoft.CmdPal.UI.Controls;

public sealed partial class ContextMenu : UserControl
{
    public static readonly DependencyProperty ShowFilterBoxProperty =
        DependencyProperty.Register(nameof(ShowFilterBox), typeof(bool), typeof(ContextMenu), new PropertyMetadata(true));

    private static readonly CompositeFormat _contextMenuOpenedFormat =
        CompositeFormat.Parse(ResourceLoaderInstance.GetString("ScreenReader_Announcement_ContextMenuOpened"));

    /// <summary>
    /// Suppresses intermediate UI updates while synchronously replacing the menu context.
    /// </summary>
    private bool _isPreparing;
    private bool _hasAnnouncedOpen;
    private int _openVersion;
    private Func<bool>? _isFlyoutOpen;

    public bool ShowFilterBox
    {
        get => (bool)GetValue(ShowFilterBoxProperty);
        set => SetValue(ShowFilterBoxProperty, value);
    }

    public ContextMenuViewModel ViewModel { get; }

    public ContextMenu()
    {
        this.InitializeComponent();

        ViewModel = new ContextMenuViewModel(App.Current.Services.GetRequiredService<IFuzzyMatcherProvider>());
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    internal void PrepareForOpen(IContextMenuContext context, ContextMenuFilterLocation filterLocation, CommandContextItemViewModel? initialSubmenu = null)
    {
        _openVersion++;
        _hasAnnouncedOpen = false;
        _isPreparing = true;
        try
        {
            ViewModel.FilterOnTop = filterLocation == ContextMenuFilterLocation.Top;
            ViewModel.PrepareForOpen(context, initialSubmenu);

            UpdateUiForStackChange();
        }
        finally
        {
            _isPreparing = false;
        }
    }

    /// <summary>
    /// Fires a single consolidated Narrator announcement.
    /// Call this after the flyout is opened and focus has been set.
    /// </summary>
    internal void AnnounceOpened(Func<bool> isFlyoutOpen)
    {
        _isFlyoutOpen = isFlyoutOpen;

        // Defer the announcement to the next dispatcher cycle. This ensures
        // any pending FilteredItems updates have completed and the flyout
        // content is fully materialized in the UIA tree.
        var openVersion = _openVersion;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (openVersion != _openVersion || !isFlyoutOpen() || ViewModel.SelectedItem is null)
            {
                return;
            }

            var commandItems = ViewModel.FilteredItems.OfType<CommandContextItemViewModel>().ToList();
            var itemCount = commandItems.Count;
            var selectedItem = CommandsDropdown.SelectedItem as CommandContextItemViewModel;
            var selectedName = selectedItem?.Title ?? string.Empty;
            var selectedIndex = selectedItem is not null ? commandItems.IndexOf(selectedItem) + 1 : 0;

            var announcement = string.Format(
                CultureInfo.CurrentCulture,
                _contextMenuOpenedFormat,
                itemCount,
                selectedName,
                selectedIndex);

            RaiseNarratorNotification(
                AutomationNotificationKind.ActionCompleted,
                announcement,
                "ContextMenuOpened");

            _hasAnnouncedOpen = true;
        });
    }

    private void CommandsDropdown_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CommandContextItemViewModel item)
        {
            InvokeCommand(item);
        }
    }

    private void HandleShortcut(KeyRoutedEventArgs e)
    {
        var mods = KeyModifiers.GetCurrent();
        var chord = KeyChordHelpers.FromModifiers(mods.Ctrl, mods.Alt, mods.Shift, mods.Win, e.Key, 0);
        var item = ViewModel.FindKeybinding(chord);
        if (item is null)
        {
            return;
        }

        e.Handled = true;
        InvokeCommand(item);
    }

    /// <summary>
    /// Handles menu shortcuts before the focused filter or list processes the key.
    /// </summary>
    private void UserControl_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape)
        {
            // TODO: Use NavigateBackOrClose() so Escape pops a submenu before closing at the root.
            // Let the flyout restore the previously focused element, even when More is hidden.
            e.Handled = true;
            WeakReferenceMessenger.Default.Send(new CloseContextMenuMessage());
            return;
        }

        if (ContextFilterBox.FocusState != FocusState.Unfocused && e.Key is VirtualKey.Up or VirtualKey.Down)
        {
            e.Handled = true;
            if (e.Key == VirtualKey.Up)
            {
                NavigateUp();
            }
            else
            {
                NavigateDown();
            }

            AnnounceSelectedItem();
            return;
        }

        HandleShortcut(e);
        if (!e.Handled && e.Key == VirtualKey.Enter && KeyModifiers.GetCurrent().OnlyCtrl)
        {
            e.Handled = true;
            if (ViewModel.SecondaryCommand is { } secondary)
            {
                // Match the command bar: activate the secondary action even when it has a submenu.
                InvokeCommand(secondary, navigateSubmenus: false);
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_isPreparing)
        {
            return;
        }

        if (e.PropertyName == nameof(ContextMenuViewModel.CurrentContext))
        {
            UpdateUiForStackChange();
        }
        else if (e.PropertyName == nameof(ContextMenuViewModel.FilteredItems))
        {
            ResetSelection();
        }
    }

    private void ContextFilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel?.SetSearchText(ContextFilterBox.Text);

        ResetSelection();
    }

    private void ContextFilterBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var modifiers = KeyModifiers.GetCurrent();

        if (e.Key == VirtualKey.Enter)
        {
            if (CommandsDropdown.SelectedItem is CommandContextItemViewModel item)
            {
                e.Handled = true;
                InvokeCommand(item);
            }
        }
        else if (e.Key == VirtualKey.Left && modifiers.Alt)
        {
            e.Handled = true;
            NavigateBackOrClose();
        }
    }

    private void NavigateBackOrClose()
    {
        if (ViewModel.CanPopContextStack())
        {
            ViewModel.PopContextStack();
        }
        else
        {
            WeakReferenceMessenger.Default.Send<CloseContextMenuMessage>();
            WeakReferenceMessenger.Default.Send<FocusSearchBoxMessage>();
        }
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
    }

    private bool IsSeparator(object item)
    {
        return item is SeparatorViewModel;
    }

    private void AnnounceSelectedItem()
    {
        if (_isPreparing || !_hasAnnouncedOpen || _isFlyoutOpen?.Invoke() != true || CommandsDropdown.SelectedItem is not CommandContextItemViewModel selected)
        {
            return;
        }

        var commandItems = ViewModel.FilteredItems.OfType<CommandContextItemViewModel>().ToList();
        var position = commandItems.IndexOf(selected) + 1;
        var total = commandItems.Count;
        var announcement = $"{selected.Title}, {position} of {total}";

        RaiseNarratorNotification(
            AutomationNotificationKind.ItemAdded,
            announcement,
            "ContextMenuSelectionChanged");
    }

    /// <summary>
    /// Raises a UIA notification via the dedicated NarratorAnnouncer element.
    /// Ensures the element has a peer (forcing layout if needed on first use).
    /// </summary>
    private void RaiseNarratorNotification(AutomationNotificationKind kind, string announcement, string activityId)
    {
        // On first flyout open the announcer may not have a peer yet.
        // UpdateLayout ensures the element is materialized in the UIA tree.
        var peer = FrameworkElementAutomationPeer.FromElement(NarratorAnnouncer);
        if (peer is null)
        {
            NarratorAnnouncer.UpdateLayout();
            peer = FrameworkElementAutomationPeer.CreatePeerForElement(NarratorAnnouncer);
        }

        peer?.RaiseNotificationEvent(
            kind,
            AutomationNotificationProcessing.ImportantMostRecent,
            announcement,
            activityId);
    }

    private void UpdateUiForStackChange()
    {
        ContextFilterBox.Text = string.Empty;
        ViewModel?.SetSearchText(string.Empty);
        ResetSelection(force: true);
    }

    private void ResetSelection(bool force = false)
    {
        // Selection must stay current independently of deferred Narrator announcements.
        if (force || CommandsDropdown.SelectedIndex == -1)
        {
            CommandsDropdown.SelectedIndex = 0;
        }
    }

    /// <summary>
    /// Manually focuses our search box. This needs to be called after we're actually
    /// In the UI tree - if we're in a Flyout, that's not until Opened()
    /// </summary>
    internal void FocusSearchBox()
    {
        ContextFilterBox.Focus(FocusState.Programmatic);
    }

    private void InvokeCommand(CommandItemViewModel command, bool navigateSubmenus = true)
    {
        var result = ViewModel.InvokeCommand(command, navigateSubmenus);
        if (result == ContextKeybindingResult.Hide)
        {
            WeakReferenceMessenger.Default.Send<CloseContextMenuMessage>();
        }
    }
}
