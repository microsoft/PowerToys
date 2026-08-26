// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
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

public sealed partial class ContextMenu : UserControl
{
    public static readonly DependencyProperty ShowFilterBoxProperty =
        DependencyProperty.Register(nameof(ShowFilterBox), typeof(bool), typeof(ContextMenu), new PropertyMetadata(true));

    private static readonly CompositeFormat _contextMenuOpenedFormat =
        CompositeFormat.Parse(ResourceLoaderInstance.GetString("ScreenReader_Announcement_ContextMenuOpened"));

    /// <summary>
    /// True while the context menu is transitioning from PrepareForOpen to AnnounceOpened.
    /// Prevents ViewModel_PropertyChanged from triggering UIA-visible selection changes.
    /// </summary>
    private bool _isOpening;

    public bool ShowFilterBox
    {
        get => (bool)GetValue(ShowFilterBoxProperty);
        set => SetValue(ShowFilterBoxProperty, value);
    }

    public ContextMenuViewModel ViewModel { get; }

    public event EventHandler? CloseRequested;

    public event EventHandler? FocusSearchRequested;

    public ContextMenu()
    {
        this.InitializeComponent();

        ViewModel = new ContextMenuViewModel(App.Current.Services.GetRequiredService<IFuzzyMatcherProvider>());
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    public void SetCommandContext(ICommandBarContext? context)
    {
        ViewModel.SetCommandContext(context);
        UpdateUiForStackChange();
    }

    internal void PrepareForOpen(ContextMenuFilterLocation filterLocation)
    {
        _isOpening = true;

        ViewModel.FilterOnTop = filterLocation == ContextMenuFilterLocation.Top;
        ViewModel.ResetContextMenu();

        UpdateUiForStackChange();
    }

    /// <summary>
    /// Fires a single consolidated Narrator announcement.
    /// Call this after the flyout is opened and focus has been set.
    /// </summary>
    internal void AnnounceOpened()
    {
        // Defer the announcement to the next dispatcher cycle. This ensures
        // any pending FilteredItems updates have completed and the flyout
        // content is fully materialized in the UIA tree.
        DispatcherQueue.TryEnqueue(() =>
        {
            _isOpening = false;

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
        });
    }

    public ContextKeybindingResult TryCommandKeybinding(bool ctrl, bool alt, bool shift, bool win, VirtualKey key)
    {
        var result = ViewModel.CheckKeybinding(ctrl, alt, shift, win, key) ?? ContextKeybindingResult.Unhandled;

        if (result == ContextKeybindingResult.Hide)
        {
            RequestClose();
            UpdateUiForStackChange();
        }
        else if (result == ContextKeybindingResult.KeepOpen)
        {
            UpdateUiForStackChange();
        }

        return result;
    }

    private void CommandsDropdown_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CommandContextItemViewModel item)
        {
            if (InvokeCommand(item) == ContextKeybindingResult.Hide)
            {
                RequestClose();
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
            RequestClose();
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
            RequestClose();

            // Find the parent CommandBar and set focus to MoreCommandsButton
            var parent = this.FindParent<CommandBar>();
            parent?.FocusMoreCommandsButton();

            e.Handled = true;
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var prop = e.PropertyName;

        if (prop == nameof(ContextMenuViewModel.FilteredItems) && !_isOpening)
        {
            UpdateUiForStackChange();
        }
    }

    private void ContextFilterBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ViewModel?.SetSearchText(ContextFilterBox.Text);

        if (CommandsDropdown.SelectedIndex == -1)
        {
            CommandsDropdown.SelectedIndex = 0;
        }
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
                    RequestClose();
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
                RequestClose(focusSearch: true);
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
            AnnounceSelectedItem();

            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Down)
        {
            NavigateDown();
            AnnounceSelectedItem();

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
        if (CommandsDropdown.SelectedItem is not CommandContextItemViewModel selected)
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
        CommandsDropdown.SelectedIndex = 0;
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

    private void RequestClose(bool focusSearch = false)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
        if (focusSearch)
        {
            FocusSearchRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
