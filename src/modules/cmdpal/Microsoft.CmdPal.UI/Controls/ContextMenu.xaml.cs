// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
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

    private static readonly CompositeFormat _contextMenuBackFormat =
        CompositeFormat.Parse(ResourceLoaderInstance.GetString("ContextMenu_Back"));

    public event EventHandler? BackRequested;

    /// <summary>
    /// Suppresses intermediate UI updates while synchronously replacing the menu context.
    /// </summary>
    private bool _isPreparing;
    private bool _hasAnnouncedOpen;
    private int _openVersion;
    private Func<bool>? _isFlyoutOpen;

    /// <summary>
    /// Like Windows menus, a newly shown menu level keeps its selection unhighlighted until the user changes it.
    /// The list has no selection meanwhile; <see cref="_hiddenSelection"/> is the item Enter still invokes.
    /// </summary>
    private bool _isSelectionHidden;
    private IContextItemViewModel? _hiddenSelection;

    public bool ShowFilterBox
    {
        get => (bool)GetValue(ShowFilterBoxProperty);
        set => SetValue(ShowFilterBoxProperty, value);
    }

    public ContextMenuViewModel ViewModel { get; }

    public event EventHandler? CloseRequested;

    private IContextItemViewModel? SelectedMenuItem =>
        _isSelectionHidden ? _hiddenSelection : CommandsDropdown.SelectedItem as IContextItemViewModel;

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

            var menuItems = ViewModel.FilteredItems.Where(item => item is not SeparatorViewModel).ToList();
            var itemCount = menuItems.Count;
            var selectedItem = SelectedMenuItem;
            var selectedName = GetItemTitle(selectedItem);
            var selectedIndex = selectedItem is not null ? menuItems.IndexOf(selectedItem) + 1 : 0;

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

    private void CommandsDropdown_ItemClick(object sender, ItemClickEventArgs e) => InvokeItem(e.ClickedItem);

    private void InvokeItem(object? item)
    {
        if (item is ContextMenuBackItemViewModel)
        {
            NavigateBackOrClose();
        }
        else if (item is CommandContextItemViewModel command)
        {
            InvokeCommand(command);
        }
    }

    private void HandleShortcut(KeyRoutedEventArgs e)
    {
        if (e.Handled)
        {
            return;
        }

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
            e.Handled = true;
            if (ViewModel.IsSubmenu)
            {
                NavigateBackOrClose();
            }
            else
            {
                // Let the flyout restore the previously focused element, even when More is hidden.
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }

            return;
        }

        if (e.Key == VirtualKey.Left && KeyModifiers.GetCurrent().OnlyAlt)
        {
            e.Handled = true;
            NavigateBackOrClose();
            return;
        }

        // Tab focus enters the list at its selected row, which must be the row Enter invokes.
        if (e.Key == VirtualKey.Tab)
        {
            ShowSelection();
        }

        // Until the list shows a selection, move from the hidden one rather than from the list's focused row.
        if ((_isSelectionHidden || ContextFilterBox.FocusState != FocusState.Unfocused) && e.Key is VirtualKey.Up or VirtualKey.Down)
        {
            e.Handled = true;
            MoveSelection(e.Key == VirtualKey.Down);
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
        else if (!e.Handled && e.Key == VirtualKey.Enter)
        {
            e.Handled = true;
            InvokeItem(SelectedMenuItem);
        }
    }

    private void MoveSelection(bool forward)
    {
        var index = SelectedMenuItem is { } current ? ViewModel.FilteredItems.IndexOf(current) : -1;
        _isSelectionHidden = false;
        _hiddenSelection = null;
        CommandsDropdown.SelectedIndex = ViewModel.GetNextItemIndex(index, forward);
        if (CommandsDropdown.SelectedItem is { } selected && CommandsDropdown.ContainerFromItem(selected) is Control container)
        {
            container.StartBringIntoView();

            // A focused list handles later keys natively from its focused row.
            if (ContextFilterBox.FocusState == FocusState.Unfocused)
            {
                container.Focus(FocusState.Keyboard);
            }
        }

        AnnounceSelectedItem();
    }

    private void CommandsDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // A selection made by the list itself, such as Home or End, is highlighted and becomes the Enter target.
        if (_isSelectionHidden && CommandsDropdown.SelectedItem is not null)
        {
            _isSelectionHidden = false;
            _hiddenSelection = null;
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

        // Filtering changes which item Enter invokes, so show it.
        if (!string.IsNullOrEmpty(ContextFilterBox.Text))
        {
            ShowSelection();
        }
    }

    private void NavigateBackOrClose()
    {
        if (ViewModel.CanPopContextStack())
        {
            ViewModel.PopContextStack();
            FocusSearchBox();
        }
        else
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private string GetItemTitle(IContextItemViewModel? item) => item switch
    {
        CommandContextItemViewModel command => command.Title,
        ContextMenuBackItemViewModel => string.Format(CultureInfo.CurrentCulture, _contextMenuBackFormat, ViewModel.CurrentMenuTitle),
        _ => string.Empty,
    };

    private void AnnounceSelectedItem()
    {
        if (_isPreparing || !_hasAnnouncedOpen || _isFlyoutOpen?.Invoke() != true || SelectedMenuItem is not { } selected)
        {
            return;
        }

        var menuItems = ViewModel.FilteredItems.Where(item => item is not SeparatorViewModel).ToList();
        var position = menuItems.IndexOf(selected) + 1;
        var total = menuItems.Count;
        var announcement = $"{GetItemTitle(selected)}, {position} of {total}";

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
        if (!force && SelectedMenuItem is { } current && ViewModel.FilteredItems.Contains(current))
        {
            return;
        }

        var item = (IContextItemViewModel?)ViewModel.FilteredItems.OfType<CommandContextItemViewModel>().FirstOrDefault()
            ?? ViewModel.FilteredItems.OfType<ContextMenuBackItemViewModel>().FirstOrDefault();
        if (force)
        {
            _isSelectionHidden = true;
            CommandsDropdown.SelectedItem = null;
        }

        if (_isSelectionHidden)
        {
            _hiddenSelection = item;
        }
        else
        {
            CommandsDropdown.SelectedItem = item;
        }
    }

    private void ShowSelection()
    {
        if (_isSelectionHidden)
        {
            _isSelectionHidden = false;
            CommandsDropdown.SelectedItem = _hiddenSelection;
            _hiddenSelection = null;
        }
    }

    /// <summary>
    /// Manually focuses our search box. This needs to be called after we're actually
    /// In the UI tree - if we're in a Flyout, that's not until Opened()
    /// </summary>
    internal void FocusSearchBox()
    {
        if (!ContextFilterBox.Focus(FocusState.Programmatic))
        {
            CommandsDropdown.Focus(FocusState.Programmatic);
        }
    }

    private void InvokeCommand(CommandItemViewModel command, bool navigateSubmenus = true)
    {
        var result = ViewModel.InvokeCommand(command, navigateSubmenus);
        if (result == ContextKeybindingResult.Hide)
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
