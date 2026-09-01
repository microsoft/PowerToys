// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.UI.Dock;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.Taskbar;

public sealed partial class TaskbarBandControl : UserControl,
    IRecipient<EnterEditModeMessage>,
    IRecipient<ExitEditModeMessage>,
    IRecipient<CrossMonitorBandDropMessage>,
    IDisposable
{
    private sealed record PendingOverflowPageRequest(
        PerformCommandMessage Message,
        FrameworkElement Anchor,
        Point Position);

    private readonly DockViewModel _viewModel;
    private readonly DockPageFlyoutController _pageFlyoutController;
    private readonly Func<DockSide> _taskbarSide;
    private bool _isEditMode;
    private bool _showFlyout;
    private DockBandViewModel? _editModeContextBand;
    private DockBandViewModel? _draggedBand;
    private Point? _bandContextMenuPalettePos;
    private FrameworkElement? _bandContextMenuTarget;
    private DockItemViewModel? _bandContextMenuItem;
    private PendingOverflowPageRequest? _pendingOverflowPageRequest;
    private bool _disposed;

    /// <summary>
    /// Gets a value indicating whether minimum width (in DIPs) the band area should occupy during edit
    /// mode, snapshotted when editing starts.
    /// </summary>
    internal bool IsEditMode => _isEditMode;

    internal DockViewModel ViewModel => _viewModel;

    /// <summary>
    /// Gets or sets the HWND of the parent <see cref="TaskbarWindow"/> that owns this control.
    /// Used to target palette-show messages to the correct window.
    /// </summary>
    internal IntPtr OwnerHwnd { get; set; }

    internal TaskbarBandControl(DockViewModel viewModel, Func<DockSide> taskbarSide)
    {
        _viewModel = viewModel;
        _taskbarSide = taskbarSide;
        InitializeComponent();

        var services = App.Current.Services;
        _pageFlyoutController = new(
            TaskbarPageFlyout,
            DispatcherQueue,
            TaskScheduler.FromCurrentSynchronizationContext(),
            services.GetRequiredService<IPageViewModelFactoryService>(),
            services.GetRequiredService<IAppHostService>(),
            () => OwnerHwnd,
            taskbarSide,
            () => !_isEditMode,
            () => Focus(FocusState.Programmatic));
        _pageFlyoutController.Activate();

        BandsListView.ItemsSource = _viewModel.TaskbarItems;

        ContextControl.CloseRequested += ContextControl_CloseRequested;
        ContextControl.ViewModel.CommandInvoked += ContextMenu_CommandInvoked;
        ContextControl.ViewModel.CommandInvoking += ContextMenu_CommandInvoking;
        MoreButton.Flyout.Closed += MoreButtonFlyout_Closed;
        WeakReferenceMessenger.Default.Register<EnterEditModeMessage>(this);
        WeakReferenceMessenger.Default.Register<ExitEditModeMessage>(this);
        WeakReferenceMessenger.Default.Register<CrossMonitorBandDropMessage>(this);

        UpdateEditMode(false);
    }

    public void SetMaxAvailableWidth(double availableSpace)
    {
        if (availableSpace <= 0 && !_isEditMode)
        {
            MoreButton.Visibility = Visibility.Collapsed;
            BandsListView.Visibility = Visibility.Collapsed;
            return;
        }

        BandsListView.Visibility = Visibility.Visible;

        // In edit mode, show all bands — no overflow.
        if (_isEditMode)
        {
            MoreButton.Visibility = Visibility.Collapsed;
            OverflowListView.ItemsSource = null;
            return;
        }

        // Measure each band's width
        var items = _viewModel.TaskbarItems;
        var bandWidths = new List<(DockBandViewModel Band, double Width)>();
        foreach (var band in items)
        {
            if (BandsListView.ContainerFromItem(band) is FrameworkElement container)
            {
                container.Measure(new Size(availableSpace, ActualHeight));
                bandWidths.Add((band, container.DesiredSize.Width));
            }
        }

        // First pass: check if everything fits
        var totalNeeded = 0.0;
        foreach (var (_, w) in bandWidths)
        {
            totalNeeded += w;
        }

        if (totalNeeded <= availableSpace)
        {
            MoreButton.Visibility = Visibility.Collapsed;
            OverflowListView.ItemsSource = null;
            return;
        }

        // Second pass: some bands overflow. Reserve space for the
        // MoreButton, then pack bands right-to-left until full.
        const double moreButtonWidth = 40;
        var budget = availableSpace - moreButtonWidth;
        var overflowBands = new List<DockBandViewModel>();
        double takenSpace = 0;
        var cutoffIndex = bandWidths.Count;

        for (var i = 0; i < bandWidths.Count; i++)
        {
            if (takenSpace + bandWidths[i].Width <= budget)
            {
                takenSpace += bandWidths[i].Width;
            }
            else
            {
                cutoffIndex = i;
                break;
            }
        }

        for (var i = cutoffIndex; i < bandWidths.Count; i++)
        {
            overflowBands.Add(bandWidths[i].Band);
        }

        MoreButton.Visibility = Visibility.Visible;
        OverflowListView.ItemsSource = overflowBands;
    }

    internal void EnterEditMode(bool showFlyout = true)
    {
        _showFlyout = showFlyout;
        _viewModel.SnapshotBandOrder();
        _isEditMode = true;
        UpdateEditMode(true, showFlyout);
    }

    /// <summary>
    /// Sets the preferred placement of the edit mode teaching tip.
    /// Should be the opposite of the taskbar edge (e.g. Top for a
    /// bottom taskbar, Bottom for a top taskbar).
    /// </summary>
    internal void SetTeachingTipPlacement(Microsoft.UI.Xaml.Controls.TeachingTipPlacementMode placement)
    {
        EditButtonsTeachingTip.PreferredPlacement = placement;
    }

    /// <summary>
    /// Switches the band layout between horizontal (for top/bottom
    /// taskbars) and vertical (for left/right taskbars).  Swaps the
    /// ListView ItemsPanel, item container style, and each band's
    /// inner ItemsRepeater layout so every level stacks correctly.
    /// </summary>
    internal void SetOrientation(Orientation orientation)
    {
        var isVertical = orientation == Orientation.Vertical;

        RootPanel.Orientation = orientation;

        // Swap the BandsListView items panel template and container style.
        BandsListView.ItemsPanel = (ItemsPanelTemplate)Resources[
            isVertical ? "VerticalBandsPanel" : "HorizontalBandsPanel"];
        BandsListView.ItemContainerStyle = (Style)Resources[
            isVertical ? "VerticalBandListViewItemStyle" : "HorizontalBandListViewItemStyle"];

        // Swap the Layout on every band's inner ItemsRepeater.
        var layout = (Microsoft.UI.Xaml.Controls.Layout)Resources[
            isVertical ? "VerticalItemsLayout" : "HorizontalItemsLayout"];
        foreach (var item in _viewModel.TaskbarItems)
        {
            if (BandsListView.ContainerFromItem(item) is ListViewItem container)
            {
                var repeater = FindDescendant<ItemsRepeater>(container);
                if (repeater != null)
                {
                    repeater.Layout = layout;
                }
            }
        }

        if (isVertical)
        {
            RootPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            RootPanel.VerticalAlignment = VerticalAlignment.Bottom;
            BandsListView.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
        else
        {
            RootPanel.HorizontalAlignment = HorizontalAlignment.Right;
            RootPanel.VerticalAlignment = VerticalAlignment.Stretch;
            BandsListView.HorizontalAlignment = HorizontalAlignment.Stretch;
        }

        // Reapply compact mode after orientation change since containers
        // may have been re-realized.
        ApplyCompactModeToAllItems();
    }

    private static T? FindDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                return match;
            }

            var result = FindDescendant<T>(child);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private static void FindAllDescendants<T>(DependencyObject parent, List<T> results)
        where T : DependencyObject
    {
        var count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                results.Add(match);
            }

            FindAllDescendants(child, results);
        }
    }

    private bool _isCompact;
    private bool _hideText;

    /// <summary>
    /// Sets compact mode on all realized DockItemControl instances.
    /// Stores the state so it can be reapplied when items are re-realized.
    /// </summary>
    internal void SetCompactMode(bool isCompact, bool hideText)
    {
        _isCompact = isCompact;
        _hideText = hideText;
        ApplyCompactModeToAllItems();
    }

    private void ApplyCompactModeToAllItems()
    {
        var items = new List<DockItemControl>();
        foreach (var band in _viewModel.TaskbarItems)
        {
            if (BandsListView.ContainerFromItem(band) is ListViewItem container)
            {
                FindAllDescendants(container, items);
            }
        }

        foreach (var item in items)
        {
            item.IsCompact = _isCompact;
            if (_hideText)
            {
                item.TextVisibility = Visibility.Collapsed;
            }
            else
            {
                // Clear the forced override so the item determines text
                // visibility from its own Title/Subtitle content. Setting
                // Visible here would override the "TextHidden" visual
                // state for items that have no text at all.
                item.ClearValue(DockItemControl.TextVisibilityProperty);
            }
        }

        // Swap to tighter spacing between items when compact.
        var isVertical = RootPanel.Orientation == Orientation.Vertical;
        string layoutKey;
        if (_isCompact)
        {
            layoutKey = isVertical ? "VerticalCompactItemsLayout" : "HorizontalCompactItemsLayout";
        }
        else
        {
            layoutKey = isVertical ? "VerticalItemsLayout" : "HorizontalItemsLayout";
        }

        var layout = (Microsoft.UI.Xaml.Controls.Layout)Resources[layoutKey];
        foreach (var band in _viewModel.TaskbarItems)
        {
            if (BandsListView.ContainerFromItem(band) is ListViewItem container)
            {
                var repeater = FindDescendant<ItemsRepeater>(container);
                if (repeater != null)
                {
                    repeater.Layout = layout;
                }
            }
        }
    }

    internal void ExitEditMode()
    {
        _isEditMode = false;
        UpdateEditMode(false);
        _viewModel.SaveBandOrder();
    }

    internal void DiscardEditMode()
    {
        _isEditMode = false;
        UpdateEditMode(false);
        _viewModel.RestoreBandOrder();
    }

    private void UpdateEditMode(bool isEditMode, bool showFlyout = true)
    {
        BandsListView.CanDragItems = isEditMode;
        BandsListView.CanReorderItems = isEditMode;
        BandsListView.AllowDrop = isEditMode;

        AddBandButton.Visibility = isEditMode ? Visibility.Visible : Visibility.Collapsed;
        EditButtonsTeachingTip.IsOpen = isEditMode && showFlyout;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Parent (TaskbarWindow) will manage available width via SetMaxAvailableWidth
    }

    private void BandItem_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (_isEditMode)
        {
            return;
        }

        if (sender is DockItemControl dockItem && dockItem.DataContext is DockBandViewModel band && dockItem.Tag is DockItemViewModel item)
        {
            var anchor = GetInvocationAnchor(dockItem);
            var anchorCenter = GetElementCenter(anchor);

            InvokeItem(item, anchor, anchorCenter);
            e.Handled = true;
        }
    }

    private void BandItem_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        if (sender is DockItemControl dockItem && dockItem.DataContext is DockBandViewModel band && dockItem.Tag is DockItemViewModel item)
        {
            if (_isEditMode)
            {
                _editModeContextBand = band;
                ShowTitlesMenuItem.IsChecked = _editModeContextBand.ShowTitles;
                ShowSubtitlesMenuItem.IsChecked = _editModeContextBand.ShowSubtitles;

                EditModeContextMenu.ShowAt(
                    dockItem,
                    new FlyoutShowOptions()
                    {
                        ShowMode = FlyoutShowMode.Standard,
                        Placement = FlyoutPlacementMode.TopEdgeAlignedRight,
                    });
                e.Handled = true;
                return;
            }

            if (item.HasMoreCommands)
            {
                var anchor = GetInvocationAnchor(dockItem);
                _bandContextMenuPalettePos = GetElementCenter(anchor);
                _bandContextMenuTarget = anchor;
                _bandContextMenuItem = item;

                ContextControl.SetCommandContext(item);
                ContextControl.ShowFilterBox = true;
                ContextControl.PrepareForOpen(GetContextMenuFilterLocation());
                ContextMenuFlyout.ShowAt(
                    dockItem,
                    new FlyoutShowOptions()
                    {
                        ShowMode = FlyoutShowMode.Standard,
                        Placement = FlyoutPlacementMode.TopEdgeAlignedRight,
                    });
                e.Handled = true;
            }
        }
    }

    private void ShowTitlesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_editModeContextBand != null)
        {
            _editModeContextBand.ShowTitles = ShowTitlesMenuItem.IsChecked;
        }
    }

    private void ShowSubtitlesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_editModeContextBand != null)
        {
            _editModeContextBand.ShowSubtitles = ShowSubtitlesMenuItem.IsChecked;
        }
    }

    private void UnpinBandMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (_editModeContextBand != null)
        {
            _viewModel.UnpinBand(_editModeContextBand);
            _editModeContextBand = null;
        }
    }

    private void InvokeItem(DockItemViewModel item, FrameworkElement anchor, Point pos)
    {
        var command = item.Command;
        var hwnd = OwnerHwnd;
        try
        {
            PerformCommandMessage message = new(command.Model, item.Model)
            {
                WithAnimation = false,
                TransientPage = true,
            };
            AddSourceContext(message, item);

            if (command.Model.Unsafe is IPage)
            {
                if (!DeferPageUntilOverflowCloses(message, anchor, pos))
                {
                    StartPageRequest(message, anchor, pos);
                }
            }
            else
            {
                message.OnBeforeShowConfirmation = () =>
                    WeakReferenceMessenger.Default.Send<RequestShowPaletteAtMessage>(new(pos, hwnd));
                WeakReferenceMessenger.Default.Send(message);
            }
        }
        catch (COMException e)
        {
            Logger.LogError("Error invoking taskbar command", e);
        }
    }

    private void ContextMenuFlyout_Opened(object sender, object e)
    {
        ContextControl.FocusSearchBox();
        ContextControl.AnnounceOpened();
    }

    private ContextMenuFilterLocation GetContextMenuFilterLocation()
    {
        return _taskbarSide() == DockSide.Bottom
            ? ContextMenuFilterLocation.Bottom
            : ContextMenuFilterLocation.Top;
    }

    private FrameworkElement GetInvocationAnchor(DockItemControl dockItem)
    {
        DependencyObject? parent = dockItem;
        while (parent is not null)
        {
            if (ReferenceEquals(parent, OverflowListView))
            {
                return MoreButton;
            }

            parent = VisualTreeHelper.GetParent(parent);
        }

        return dockItem;
    }

    private static Point GetElementCenter(FrameworkElement element)
    {
        var position = element.TransformToVisual(null).TransformPoint(new Point(0, 0));
        return new Point(
            position.X + (element.ActualWidth / 2),
            position.Y + (element.ActualHeight / 2));
    }

    private bool DeferPageUntilOverflowCloses(
        PerformCommandMessage message,
        FrameworkElement anchor,
        Point position)
    {
        if (ReferenceEquals(anchor, MoreButton) && MoreButton.Flyout?.IsOpen == true)
        {
            _pendingOverflowPageRequest = new(message, anchor, position);
            MoreButton.Flyout.Hide();
            return true;
        }

        return false;
    }

    private void MoreButtonFlyout_Closed(object? sender, object e)
    {
        var pending = _pendingOverflowPageRequest;
        _pendingOverflowPageRequest = null;
        if (!_disposed && pending is not null)
        {
            StartPageRequest(pending.Message, pending.Anchor, pending.Position);
        }
    }

    private void StartPageRequest(
        PerformCommandMessage message,
        FrameworkElement anchor,
        Point position)
    {
        var result = _pageFlyoutController.Open(message, anchor, position);
        if (result == DockPageFlyoutController.RequestResult.Started)
        {
            WeakReferenceMessenger.Default.Send(message);
        }
        else if (result == DockPageFlyoutController.RequestResult.Failed)
        {
            _pageFlyoutController.PreparePaletteFallback(message, position);
            WeakReferenceMessenger.Default.Send(message);
        }
    }

    private void ContextMenu_CommandInvoked(object? sender, CommandItemViewModel command)
    {
        ClearBandContextMenuInvocation();
    }

    private void ClearBandContextMenuInvocation()
    {
        _bandContextMenuPalettePos = null;
        _bandContextMenuTarget = null;
        _bandContextMenuItem = null;
    }

    private void ContextMenu_CommandInvoking(object? sender, PerformCommandMessage message)
    {
        var pos = _bandContextMenuPalettePos;
        var target = _bandContextMenuTarget;
        var item = _bandContextMenuItem;
        if (pos is null || target is null || item is null)
        {
            return;
        }

        AddSourceContext(message, item);
        if (message.Command.Unsafe is IPage)
        {
            if (ContextMenuFlyout.IsOpen)
            {
                ContextMenuFlyout.Hide();
            }

            if (DeferPageUntilOverflowCloses(message, target, pos.Value))
            {
                message.CancelSend();
                ClearBandContextMenuInvocation();
                return;
            }

            var result = _pageFlyoutController.Open(message, target, pos.Value);
            if (result == DockPageFlyoutController.RequestResult.Deferred)
            {
                message.CancelSend();
                ClearBandContextMenuInvocation();
            }
            else if (result == DockPageFlyoutController.RequestResult.Failed)
            {
                _pageFlyoutController.PreparePaletteFallback(message, pos.Value);
            }

            return;
        }

        var hwnd = OwnerHwnd;
        var capturedPos = pos.Value;
        message.OnBeforeShowConfirmation = () =>
            WeakReferenceMessenger.Default.Send<RequestShowPaletteAtMessage>(new(capturedPos, hwnd));
    }

    private static void AddSourceContext(PerformCommandMessage message, DockItemViewModel item)
    {
        if (!item.PageContext.TryGetTarget(out var pageContext))
        {
            return;
        }

        message.SourceProviderContext = pageContext.ProviderContext;
        if (pageContext.ProviderContext is CommandProviderWrapper provider)
        {
            message.SourceExtensionHost = provider.ExtensionHost;
        }
    }

    private void ContextControl_CloseRequested(object? sender, EventArgs e)
    {
        if (ContextMenuFlyout.IsOpen)
        {
            ContextMenuFlyout.Hide();
        }
    }

    public void Receive(EnterEditModeMessage message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            EnterEditMode(showFlyout: message.Origin == EditModeOrigin.Taskbar);
        });
    }

    public void Receive(ExitEditModeMessage message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (message.Save)
            {
                ExitEditMode();
            }
            else
            {
                DiscardEditMode();
            }
        });
    }

    public void Receive(CrossMonitorBandDropMessage message)
    {
        // Only react when the band was dragged out of the taskbar onto a dock.
        // Real monitor device IDs are handled by the dock controls themselves.
        if (!string.Equals(message.SourceMonitorDeviceId, CrossMonitorBandDropMessage.TaskbarSourceId, StringComparison.Ordinal))
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() => _viewModel.RemoveBandById(message.BandId));
    }

    private void RootPanel_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        if (_isEditMode)
        {
            return;
        }

        var pos = e.GetPosition(null);
        var item = _viewModel.GetContextMenuForTaskbar();
        if (item.HasMoreCommands)
        {
            ClearBandContextMenuInvocation();
            ContextControl.SetCommandContext(item);
            ContextControl.ShowFilterBox = false;
            ContextControl.PrepareForOpen(GetContextMenuFilterLocation());
            ContextMenuFlyout.ShowAt(
                (FrameworkElement)sender,
                new FlyoutShowOptions()
                {
                    ShowMode = FlyoutShowMode.Standard,
                    Placement = FlyoutPlacementMode.TopEdgeAlignedRight,
                    Position = e.GetPosition((UIElement)sender),
                });
            e.Handled = true;
        }
    }

    private void DoneEditingButton_Click(object sender, RoutedEventArgs e)
    {
        // Tell both dock and taskbar to exit edit mode
        WeakReferenceMessenger.Default.Send(new ExitEditModeMessage(Save: true));
    }

    private void DiscardEditingButton_Click(object sender, RoutedEventArgs e)
    {
        // Tell both dock and taskbar to discard edit mode
        WeakReferenceMessenger.Default.Send(new ExitEditModeMessage(Save: false));
    }

    private void AddBandButton_Click(object sender, RoutedEventArgs e)
    {
        var availableBands = _viewModel.GetAvailableBandsToAdd().ToList();
        AddBandListView.ItemsSource = availableBands;

        var hasAvailableBands = availableBands.Count > 0;
        NoAvailableBandsText.Visibility = hasAvailableBands ? Visibility.Collapsed : Visibility.Visible;
        AddBandListView.Visibility = hasAvailableBands ? Visibility.Visible : Visibility.Collapsed;

        AddBandFlyout.ShowAt((Button)sender);
    }

    private void AddBandListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is TopLevelViewModel topLevel)
        {
            _viewModel.AddBandToSection(topLevel, DockPinSide.Taskbar);
            AddBandFlyout.Hide();
        }
    }

    // Drag and drop handlers
    private void BandsListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (e.Items.Count > 0 && e.Items[0] is DockBandViewModel band)
        {
            _draggedBand = band;
            _viewModel.DraggedBand = band;
            e.Data.RequestedOperation = DataPackageOperation.Move;

            // Advertise cross-window drag data so a dock on any monitor can
            // accept this band. The taskbar has no monitor ID, so use the
            // taskbar sentinel as the source identifier.
            e.Data.Properties["DockBandId"] = band.Id;
            e.Data.Properties["SourceMonitorDeviceId"] = CrossMonitorBandDropMessage.TaskbarSourceId;
        }
    }

    private void BandsListView_DragOver(object sender, DragEventArgs e)
    {
        // Accept drops from this window (_draggedBand), from a same-VM drag
        // (shared _viewModel.DraggedBand), or a cross-window drag from a dock
        // on another monitor (advertised via the DockBandId data property).
        if (_draggedBand != null
            || _viewModel.DraggedBand != null
            || e.DataView.Properties.ContainsKey("DockBandId"))
        {
            e.AcceptedOperation = DataPackageOperation.Move;
        }
    }

    private void BandsListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        if (args.DropResult == DataPackageOperation.Move && _draggedBand != null)
        {
            var newIndex = _viewModel.TaskbarItems.IndexOf(_draggedBand);
            if (newIndex >= 0)
            {
                _viewModel.SyncBandPosition(_draggedBand, DockPinSide.Taskbar, newIndex);
            }
        }

        _draggedBand = null;
        _viewModel.DraggedBand = null;
    }

    private void BandsListView_Drop(object sender, DragEventArgs e)
    {
        // Use local _draggedBand for same-window drags, fall back to shared
        // _viewModel.DraggedBand for cross-window drags (e.g. from dock)
        var draggedBand = _draggedBand ?? _viewModel.DraggedBand;
        if (draggedBand == null)
        {
            // Cross-window drag from a dock that uses a different DockViewModel
            // instance (per-monitor). The dragged band isn't shared in-process,
            // so recreate it from the advertised drag data instead.
            HandleCrossWindowDrop(e);
            ResetListViewState(sender);
            return;
        }

        // Only handle cross-section drops; same-list reorders are handled in DragItemsCompleted
        if (!_viewModel.TaskbarItems.Contains(draggedBand))
        {
            var dropIndex = GetDropIndex(BandsListView, e, _viewModel.TaskbarItems.Count);
            _viewModel.MoveBandWithoutSaving(draggedBand, DockPinSide.Taskbar, dropIndex);
            e.Handled = true;
        }

        ResetListViewState(sender);
    }

    private void HandleCrossWindowDrop(DragEventArgs e)
    {
        if (e.DataView.Properties.TryGetValue("DockBandId", out var bandIdObj) &&
            e.DataView.Properties.TryGetValue("SourceMonitorDeviceId", out var sourceObj) &&
            bandIdObj is string bandId &&
            sourceObj is string sourceMonitorDeviceId)
        {
            // Drags that started in the taskbar itself are handled by the local
            // path above; ignore them here.
            if (string.Equals(sourceMonitorDeviceId, CrossMonitorBandDropMessage.TaskbarSourceId, StringComparison.Ordinal))
            {
                return;
            }

            var dropIndex = GetDropIndex(BandsListView, e, _viewModel.TaskbarItems.Count);
            _viewModel.AcceptBandFromMonitor(bandId, DockPinSide.Taskbar, dropIndex);

            // Tell the source dock to remove the band from its own list.
            WeakReferenceMessenger.Default.Send(new CrossMonitorBandDropMessage(bandId, sourceMonitorDeviceId));
            e.Handled = true;
        }
    }

    private int GetDropIndex(ListView listView, DragEventArgs e, int itemCount)
    {
        var position = e.GetPosition(listView);

        for (var i = 0; i < itemCount; i++)
        {
            if (listView.ContainerFromIndex(i) is ListViewItem container)
            {
                var itemBounds = container.TransformToVisual(listView).TransformBounds(
                    new Rect(0, 0, container.ActualWidth, container.ActualHeight));

                // Horizontal layout: check X position
                if (position.X < itemBounds.X + (itemBounds.Width / 2))
                {
                    return i;
                }
            }
        }

        return itemCount;
    }

    private void BandsListView_DragEnter(object sender, DragEventArgs e)
    {
        if (sender is ListView view)
        {
            view.Background = Application.Current.Resources["ControlAltFillColorQuarternaryBrush"] as Microsoft.UI.Xaml.Media.SolidColorBrush;
            e.DragUIOverride.IsGlyphVisible = false;
            e.DragUIOverride.IsCaptionVisible = false;
        }
    }

    private void BandsListView_DragLeave(object sender, DragEventArgs e)
    {
        ResetListViewState(sender);
    }

    private void ResetListViewState(object sender)
    {
        if (sender is ListView view)
        {
            view.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        ContextControl.CloseRequested -= ContextControl_CloseRequested;
        ContextControl.ViewModel.CommandInvoked -= ContextMenu_CommandInvoked;
        ContextControl.ViewModel.CommandInvoking -= ContextMenu_CommandInvoking;
        MoreButton.Flyout.Closed -= MoreButtonFlyout_Closed;
        _pendingOverflowPageRequest = null;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _pageFlyoutController.Dispose();
        GC.SuppressFinalize(this);
    }
}
