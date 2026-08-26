// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Ext.Bookmarks;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Win32;
using Windows.Win32.Foundation;

using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI.Dock;

public sealed partial class DockControl : UserControl, IRecipient<EnterDockEditModeMessage>, IRecipient<ExitDockEditModeMessage>, IRecipient<CrossMonitorBandDropMessage>, IRecipient<PerformCommandMessage>, IRecipient<HandleCommandResultMessage>, IDisposable
{
    private readonly DockViewModel _viewModel;
    private readonly TaskScheduler _uiScheduler;
    private DockPageNavigationViewModel? _pageNavigation;
    private DockPageControl? _pageControl;
    private DockCommandRoute? _activePageRoute;
    private PendingDockPageRequest? _pendingPageRequest;
    private Point? _pagePalettePosition;
    private bool _isUnloaded;

    internal DockViewModel ViewModel => _viewModel;

    /// <summary>
    /// Gets or sets the HWND of the parent DockWindow that owns this control.
    /// Used to target palette-show messages to the correct DockWindow in multi-monitor setups.
    /// </summary>
    internal IntPtr OwnerHwnd { get; set; }

    internal bool HasOpenTransientUi =>
        ContextMenuFlyout.IsOpen ||
        AddBandFlyout.IsOpen ||
        EditModeContextMenu.IsOpen ||
        EditButtonsTeachingTip.IsOpen ||
        DockPageFlyout.IsOpen ||
        (_pageControl?.HasOpenTransientUi ?? false);

    internal bool IsDragOperationActive => _draggedBand is not null;

    public static readonly DependencyProperty ItemsOrientationProperty =
        DependencyProperty.Register(nameof(ItemsOrientation), typeof(Orientation), typeof(DockControl), new PropertyMetadata(Orientation.Horizontal));

    public Orientation ItemsOrientation
    {
        get => (Orientation)GetValue(ItemsOrientationProperty);
        set => SetValue(ItemsOrientationProperty, value);
    }

    public static readonly DependencyProperty DockSideProperty =
        DependencyProperty.Register(nameof(DockSide), typeof(DockSide), typeof(DockControl), new PropertyMetadata(DockSide.Top));

    public DockSide DockSide
    {
        get => (DockSide)GetValue(DockSideProperty);
        set => SetValue(DockSideProperty, value);
    }

    public static readonly DependencyProperty DockSizeProperty =
        DependencyProperty.Register(nameof(DockSize), typeof(DockSize), typeof(DockControl), new PropertyMetadata(DockSize.Default));

    public DockSize DockSize
    {
        get => (DockSize)GetValue(DockSizeProperty);
        set => SetValue(DockSizeProperty, value);
    }

    public static readonly DependencyProperty IsEditModeProperty =
        DependencyProperty.Register(nameof(IsEditMode), typeof(bool), typeof(DockControl), new PropertyMetadata(false, OnIsEditModeChanged));

    public bool IsEditMode
    {
        get => (bool)GetValue(IsEditModeProperty);
        set => SetValue(IsEditModeProperty, value);
    }

    private static void OnIsEditModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DockControl control && e.NewValue is bool isEditMode)
        {
            control.UpdateEditMode(isEditMode);
        }
    }

    internal sealed record PendingDockPageRequest(
        PerformCommandMessage Message,
        FrameworkElement Anchor,
        Point Position,
        DockCommandRoute Route);

    private enum DockPageRequestResult
    {
        Started,
        Deferred,
        Failed,
    }

    internal DockControl(DockViewModel viewModel)
    {
        _viewModel = viewModel;
        _uiScheduler = TaskScheduler.FromCurrentSynchronizationContext();
        InitializeComponent();
        ContextControl.CloseRequested += ContextControl_CloseRequested;
        Loaded += DockControl_Loaded;
        Unloaded += DockControl_Unloaded;

        // Start with edit mode disabled - normal click behavior
        UpdateEditMode(false);
    }

    private void DockControl_Loaded(object sender, RoutedEventArgs e)
    {
        _isUnloaded = false;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        WeakReferenceMessenger.Default.Register<EnterDockEditModeMessage>(this);
        WeakReferenceMessenger.Default.Register<ExitDockEditModeMessage>(this);
        WeakReferenceMessenger.Default.Register<CrossMonitorBandDropMessage>(this);
        WeakReferenceMessenger.Default.Register<PerformCommandMessage>(this);
        WeakReferenceMessenger.Default.Register<HandleCommandResultMessage>(this);

        ContextControl.ViewModel.CommandInvoked -= ContextMenu_CommandInvoked;
        ContextControl.ViewModel.CommandInvoked += ContextMenu_CommandInvoked;
        ContextControl.ViewModel.CommandInvoking -= ContextMenu_CommandInvoking;
        ContextControl.ViewModel.CommandInvoking += ContextMenu_CommandInvoking;

        ViewModel.CenterItems.CollectionChanged -= CenterItems_CollectionChanged;
        ViewModel.CenterItems.CollectionChanged += CenterItems_CollectionChanged;

        UpdateEditModeTeachingTip();
    }

    private void DockControl_Unloaded(object sender, RoutedEventArgs e)
    {
        _isUnloaded = true;
        WeakReferenceMessenger.Default.UnregisterAll(this);

        ContextControl.ViewModel.CommandInvoked -= ContextMenu_CommandInvoked;
        ContextControl.ViewModel.CommandInvoking -= ContextMenu_CommandInvoking;

        ViewModel.CenterItems.CollectionChanged -= CenterItems_CollectionChanged;

        if (EditButtonsTeachingTip.IsOpen)
        {
            EditButtonsTeachingTip.IsOpen = false;
        }

        if (ContextMenuFlyout.IsOpen)
        {
            ContextMenuFlyout.Hide();
        }

        if (AddBandFlyout.IsOpen)
        {
            AddBandFlyout.Hide();
        }

        if (EditModeContextMenu.IsOpen)
        {
            EditModeContextMenu.Hide();
        }

        _pendingPageRequest = null;
        if (DockPageFlyout.IsOpen)
        {
            DockPageFlyout.Hide();
        }

        CleanupDockPage();
    }

    private void CenterItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateCenterVisibility();
    }

    private void UpdateCenterVisibility()
    {
        ContentGrid.IsCenterVisible = IsEditMode || ViewModel.CenterItems.Count > 0;
    }

    public void Receive(EnterDockEditModeMessage message)
    {
        // Message may arrive from a background thread, dispatch to UI thread
        DispatcherQueue.TryEnqueue(() =>
        {
            EnterEditMode();
        });
    }

    public void Receive(ExitDockEditModeMessage message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (message.Discard)
            {
                DiscardEditMode();
            }
            else
            {
                ExitEditMode();
            }
        });
    }

    private void UpdateEditMode(bool isEditMode)
    {
        // Update center visibility based on edit mode and center items
        UpdateCenterVisibility();

        // Enable/disable drag-and-drop based on edit mode
        StartListView.CanDragItems = isEditMode;
        StartListView.CanReorderItems = isEditMode;
        StartListView.AllowDrop = isEditMode;

        CenterListView.CanDragItems = isEditMode;
        CenterListView.CanReorderItems = isEditMode;
        CenterListView.AllowDrop = isEditMode;

        EndListView.CanDragItems = isEditMode;
        EndListView.CanReorderItems = isEditMode;
        EndListView.AllowDrop = isEditMode;

        if (isEditMode)
        {
            EditButtonsTeachingTip.PreferredPlacement = DockSide switch
            {
                DockSide.Left => TeachingTipPlacementMode.Right,
                DockSide.Right => TeachingTipPlacementMode.Left,
                DockSide.Top => TeachingTipPlacementMode.Bottom,
                DockSide.Bottom => TeachingTipPlacementMode.Top,
                _ => TeachingTipPlacementMode.Auto,
            };
        }

        UpdateEditModeTeachingTip();
    }

    private void UpdateEditModeTeachingTip()
    {
        if (XamlRoot is null || ContentGrid.XamlRoot is null || EditButtonsTeachingTip.Parent is null)
        {
            return;
        }

        if (!IsEditMode)
        {
            if (EditButtonsTeachingTip.IsOpen)
            {
                EditButtonsTeachingTip.IsOpen = false;
            }

            return;
        }

        if (!EditButtonsTeachingTip.IsOpen)
        {
            EditButtonsTeachingTip.IsOpen = true;
        }
    }

    private static void PreparePopupForShow(FlyoutBase popup, FrameworkElement placementTarget)
    {
        if (placementTarget.XamlRoot is not null && popup.XamlRoot != placementTarget.XamlRoot)
        {
            popup.XamlRoot = placementTarget.XamlRoot;
        }
    }

    internal void EnterEditMode()
    {
        // Snapshot current state so we can restore on discard
        ViewModel.SnapshotBandOrder();
        IsEditMode = true;
    }

    internal void ExitEditMode()
    {
        IsEditMode = false;

        // Save all changes when exiting edit mode
        ViewModel.SaveBandOrder();
    }

    internal void DiscardEditMode()
    {
        IsEditMode = false;

        // Restore the original band order from snapshot
        ViewModel.RestoreBandOrder();
    }

    private void DoneEditingButton_Click(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new ExitDockEditModeMessage(Discard: false));
    }

    private void DiscardEditingButton_Click(object sender, RoutedEventArgs e)
    {
        WeakReferenceMessenger.Default.Send(new ExitDockEditModeMessage(Discard: true));
    }

    internal void UpdateSettings(DockSettings settings, DockSide? effectiveSide = null)
    {
        var side = effectiveSide ?? settings.Side;
        DockSide = side;

        // Compact mode is only supported for Top/Bottom positions
        var isHorizontal = side == DockSide.Top || side == DockSide.Bottom;
        var effectiveSize = isHorizontal ? settings.DockSize : DockSize.Default;
        DockSize = effectiveSize;

        ItemsOrientation = isHorizontal ? Orientation.Horizontal : Orientation.Vertical;

        if (settings.Backdrop == DockBackdrop.Transparent)
        {
            RootGrid.BorderBrush = new SolidColorBrush(Colors.Transparent);
        }
    }

    private void BandItem_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        // Ignore clicks when in edit mode - allow drag behavior instead
        if (IsEditMode)
        {
            return;
        }

        if (sender is DockItemControl dockItem && dockItem.DataContext is DockBandViewModel band && dockItem.Tag is DockItemViewModel item)
        {
            // Use the center of the border as the point to open at
            var borderCenter = GetDockItemCenter(dockItem);

            InvokeItem(item, dockItem, borderCenter);
            e.Handled = true;
        }
    }

    private ContextMenuFilterLocation GetDockContextMenuFilterLocation()
    {
        return DockSide == DockSide.Bottom
            ? ContextMenuFilterLocation.Bottom
            : ContextMenuFilterLocation.Top;
    }

    // Stores the band that was right-clicked for edit mode context menu
    private DockBandViewModel? _editModeContextBand;

    // Position (in window coords) of the dock item whose context menu is currently
    // open, used to anchor the cmdpal palette when a Page command is invoked from
    // the context menu. Null when the open context menu is not anchored to a band.
    private Point? _bandContextMenuPalettePos;

    private FrameworkElement? _bandContextMenuTarget;

    private DockItemViewModel? _bandContextMenuItem;

    private void BandItem_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        if (sender is DockItemControl dockItem && dockItem.DataContext is DockBandViewModel band && dockItem.Tag is DockItemViewModel item)
        {
            // In edit mode, show the edit mode context menu (show/hide labels)
            if (IsEditMode)
            {
                // Find the parent DockBandViewModel for this item
                _editModeContextBand = band;
                if (_editModeContextBand != null)
                {
                    // Update toggle menu item checked state based on current settings
                    ShowTitlesMenuItem.IsChecked = _editModeContextBand.ShowTitles;
                    ShowSubtitlesMenuItem.IsChecked = _editModeContextBand.ShowSubtitles;

                    // Hide subtitle toggle in compact mode — no subtitle in the template
                    ShowSubtitlesMenuItem.Visibility = DockSize == DockSize.Compact
                        ? Visibility.Collapsed
                        : Visibility.Visible;

                    PreparePopupForShow(EditModeContextMenu, dockItem);
                    EditModeContextMenu.ShowAt(
                        dockItem,
                        new FlyoutShowOptions()
                        {
                            ShowMode = FlyoutShowMode.Standard,
                            Placement = FlyoutPlacementMode.TopEdgeAlignedRight,
                        });
                    e.Handled = true;
                }

                return;
            }

            // Normal mode - show the command context menu
            if (item.CanOpenContextMenu)
            {
                // Remember where to anchor the palette if the user picks a Page
                // command from the context menu.
                _bandContextMenuPalettePos = GetDockItemCenter(dockItem);
                _bandContextMenuTarget = dockItem;
                _bandContextMenuItem = item;

                ContextControl.SetCommandContext(item);
                ContextControl.ShowFilterBox = true;
                ContextControl.PrepareForOpen(GetDockContextMenuFilterLocation());
                PreparePopupForShow(ContextMenuFlyout, dockItem);
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
            ViewModel.UnpinBand(_editModeContextBand);
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
                var result = PrepareDockPageRequest(message, anchor, pos);
                if (result == DockPageRequestResult.Started)
                {
                    WeakReferenceMessenger.Default.Send(message);
                }
                else if (result == DockPageRequestResult.Failed)
                {
                    PreparePageFallback(message, pos);
                    WeakReferenceMessenger.Default.Send(message);
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
            Logger.LogError("Error invoking dock command", e);
        }
    }

    private static Point GetDockItemCenter(FrameworkElement dockItem)
    {
        var borderPos = dockItem.TransformToVisual(null).TransformPoint(new Point(0, 0));
        return new Point(
            borderPos.X + (dockItem.ActualWidth / 2),
            borderPos.Y + (dockItem.ActualHeight / 2));
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
        // The context menu is about to dispatch a command. If it was opened
        // from a dock band, attach a callback so that an invokable command
        // whose result is a Confirm surfaces the cmdpal window anchored at the
        // dock item before the confirmation dialog appears.
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

            var result = PrepareDockPageRequest(message, target, pos.Value);
            if (result == DockPageRequestResult.Deferred)
            {
                message.CancelSend();
                ClearBandContextMenuInvocation();
            }
            else if (result == DockPageRequestResult.Failed)
            {
                PreparePageFallback(message, pos.Value);
            }

            return;
        }

        var hwnd = OwnerHwnd;
        var capturedPos = pos.Value;
        message.OnBeforeShowConfirmation = () =>
            WeakReferenceMessenger.Default.Send<RequestShowPaletteAtMessage>(new(capturedPos, hwnd));
    }

    private void ContextMenuFlyout_Opened(object sender, object e)
    {
        // Focus the filter box so the flyout captures keyboard input,
        // then fire a single consolidated Narrator announcement.
        ContextControl.FocusSearchBox();
        ContextControl.AnnounceOpened();
    }

    public void Receive(PerformCommandMessage message)
    {
        var route = message.DockRoute;
        if (route is null ||
            route.Value.OwnerHwnd != OwnerHwnd)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            var navigation = _pageNavigation;
            if (message.DockRoute != _activePageRoute || navigation is null)
            {
                return;
            }

            if (message.Command.Unsafe is IPage)
            {
                _ = ObserveNavigationAsync(navigation.NavigateAsync(message));
            }
            else if (message.Command.Unsafe is IInvokableCommand)
            {
                var sourcePage = message.SourcePage ?? navigation.CurrentPage;
                if (sourcePage is null || !navigation.OwnsSourcePage(sourcePage))
                {
                    return;
                }

                var forwarded = message with
                {
                    DockRoute = null,
                    SourcePage = sourcePage,
                    SourceExtensionHost = message.SourceExtensionHost ?? navigation.CurrentPage?.ExtensionHost,
                    SourceProviderContext = message.SourceProviderContext ?? navigation.CurrentPage?.ProviderContext,
                };
                var existingCallback = forwarded.OnBeforeShowConfirmation;
                var capturedPosition = _pagePalettePosition;
                var hwnd = OwnerHwnd;
                forwarded.OnBeforeShowConfirmation = () =>
                {
                    existingCallback?.Invoke();
                    if (capturedPosition is Point position)
                    {
                        WeakReferenceMessenger.Default.Send<RequestShowPaletteAtMessage>(new(position, hwnd));
                    }
                };

                var existingHandler = forwarded.ResultHandler;
                var commandRoute = message.DockRoute!.Value;
                forwarded.ResultHandler = result =>
                {
                    if (existingHandler?.Invoke(result) == true)
                    {
                        return true;
                    }

                    return HandleDockCommandResult(navigation, commandRoute, sourcePage, result);
                };
                WeakReferenceMessenger.Default.Send(forwarded);
            }
        });
    }

    public void Receive(HandleCommandResultMessage message)
    {
        var route = message.DockRoute;
        if (route is null ||
            route.Value.OwnerHwnd != OwnerHwnd)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            var navigation = _pageNavigation;
            var sourcePage = message.SourcePage;
            if (message.DockRoute != _activePageRoute ||
                navigation is null ||
                sourcePage is null ||
                !navigation.OwnsSourcePage(sourcePage))
            {
                return;
            }

            var forwarded = message with { DockRoute = null };
            var existingCallback = forwarded.OnBeforeShowConfirmation;
            var capturedPosition = _pagePalettePosition;
            var hwnd = OwnerHwnd;
            forwarded.OnBeforeShowConfirmation = () =>
            {
                existingCallback?.Invoke();
                if (capturedPosition is Point position)
                {
                    WeakReferenceMessenger.Default.Send<RequestShowPaletteAtMessage>(new(position, hwnd));
                }
            };

            var existingHandler = forwarded.ResultHandler;
            forwarded.ResultHandler = result =>
            {
                if (existingHandler?.Invoke(result) == true)
                {
                    return true;
                }

                return HandleDockCommandResult(navigation, route.Value, sourcePage, result);
            };
            WeakReferenceMessenger.Default.Send(forwarded);
        });
    }

    private bool HandleDockCommandResult(
        DockPageNavigationViewModel navigation,
        DockCommandRoute route,
        PageViewModel sourcePage,
        ICommandResult result)
    {
        if (!ReferenceEquals(Volatile.Read(ref _pageNavigation), navigation) ||
            navigation.Route != route ||
            !navigation.OwnsSourcePage(sourcePage))
        {
            return true;
        }

        if (result.Kind is CommandResultKind.ShowToast or CommandResultKind.Confirm)
        {
            return false;
        }

        if (DispatcherQueue.HasThreadAccess)
        {
            ApplyDockCommandResult(navigation, route, sourcePage, result.Kind);
        }
        else
        {
            DispatcherQueue.TryEnqueue(
                () => ApplyDockCommandResult(navigation, route, sourcePage, result.Kind));
        }

        return true;
    }

    private void ApplyDockCommandResult(
        DockPageNavigationViewModel navigation,
        DockCommandRoute route,
        PageViewModel sourcePage,
        CommandResultKind kind)
    {
        if (!ReferenceEquals(_pageNavigation, navigation) ||
            navigation.Route != route ||
            !navigation.OwnsSourcePage(sourcePage))
        {
            return;
        }

        switch (kind)
        {
            case CommandResultKind.Dismiss:
            case CommandResultKind.Hide:
                CloseDockPageFlyout();
                break;
            case CommandResultKind.GoHome:
                _ = ObserveNavigationAsync(navigation.GoHomeAsync());
                break;
            case CommandResultKind.GoBack:
                if (navigation.CanGoBack)
                {
                    _ = ObserveNavigationAsync(navigation.GoBackAsync());
                }
                else
                {
                    CloseDockPageFlyout();
                }

                break;
        }
    }

    private void CloseDockPageFlyout()
    {
        _pendingPageRequest = null;
        if (DockPageFlyout.IsOpen)
        {
            DockPageFlyout.Hide();
        }
        else
        {
            CleanupDockPage();
        }
    }

    private static async Task ObserveNavigationAsync(Task<bool> navigationTask)
    {
        try
        {
            await navigationTask;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to open a dock page.", ex);
        }
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

    private DockPageRequestResult PrepareDockPageRequest(PerformCommandMessage message, FrameworkElement anchor, Point position)
    {
        var route = new DockCommandRoute(OwnerHwnd, Guid.NewGuid());
        message.DockRoute = route;
        var request = new PendingDockPageRequest(message, anchor, position, route);

        if (DockPageFlyout.IsOpen)
        {
            _pendingPageRequest = request;
            DockPageFlyout.Hide();
            return DockPageRequestResult.Deferred;
        }

        CleanupDockPage();
        if (StartDockPageRequest(request))
        {
            return DockPageRequestResult.Started;
        }

        message.DockRoute = null;
        return DockPageRequestResult.Failed;
    }

    private void PreparePageFallback(PerformCommandMessage message, Point position)
    {
        message.DockRoute = null;
        WeakReferenceMessenger.Default.Send<RequestShowPaletteAtMessage>(new(position, OwnerHwnd));
    }

    private bool StartDockPageRequest(PendingDockPageRequest request)
    {
        if (request.Anchor.XamlRoot is null || request.Route.OwnerHwnd != OwnerHwnd)
        {
            return false;
        }

        try
        {
            var services = App.Current.Services;
            _activePageRoute = request.Route;
            _pagePalettePosition = request.Position;
            _pageNavigation = new DockPageNavigationViewModel(
                request.Route,
                _uiScheduler,
                services.GetRequiredService<IPageViewModelFactoryService>(),
                services.GetRequiredService<IAppHostService>());
            _pageControl = new DockPageControl(_pageNavigation);
            _pageControl.CloseRequested += DockPageControl_CloseRequested;
            DockPageFlyout.Content = _pageControl;

            // A windowed popup only receives pointer input when its owner is active.
            var ownerHwnd = new HWND(OwnerHwnd);
            PInvoke.SetForegroundWindow(ownerHwnd);
            PInvoke.SetActiveWindow(ownerHwnd);

            PreparePopupForShow(DockPageFlyout, request.Anchor);
            DockPageFlyout.ShowAt(
                request.Anchor,
                new FlyoutShowOptions
                {
                    ShowMode = FlyoutShowMode.Standard,
                    Placement = GetDockPagePlacement(),
                });
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to show a dock page.", ex);
            CleanupDockPage();
            return false;
        }
    }

    private FlyoutPlacementMode GetDockPagePlacement()
    {
        return DockSide switch
        {
            DockSide.Top => FlyoutPlacementMode.Bottom,
            DockSide.Bottom => FlyoutPlacementMode.Top,
            DockSide.Left => FlyoutPlacementMode.RightEdgeAlignedTop,
            DockSide.Right => FlyoutPlacementMode.LeftEdgeAlignedTop,
            _ => FlyoutPlacementMode.Bottom,
        };
    }

    private void DockPageFlyout_Opened(object sender, object e) =>
        DispatcherQueue.TryEnqueue(
            Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
            () => _pageControl?.FocusSearch());

    private void DockPageFlyout_Closed(object sender, object e)
    {
        CleanupDockPage();
        if (!_isUnloaded && !IsEditMode)
        {
            Focus(FocusState.Programmatic);
        }

        var pending = _pendingPageRequest;
        _pendingPageRequest = null;
        if (pending is not null)
        {
            if (!StartDockPageRequest(pending))
            {
                PreparePageFallback(pending.Message, pending.Position);
            }

            WeakReferenceMessenger.Default.Send(pending.Message);
        }
    }

    private void DockPageControl_CloseRequested(object? sender, EventArgs e)
    {
        if (DockPageFlyout.IsOpen)
        {
            DockPageFlyout.Hide();
        }
    }

    private void CleanupDockPage()
    {
        _activePageRoute = null;
        _pagePalettePosition = null;
        DockPageFlyout.Content = null;

        if (_pageControl is not null)
        {
            _pageControl.CloseRequested -= DockPageControl_CloseRequested;
            _pageControl.Dispose();
            _pageControl = null;
            _pageNavigation = null;
        }
        else
        {
            _pageNavigation?.Dispose();
            _pageNavigation = null;
        }
    }

    private void ContextControl_CloseRequested(object? sender, EventArgs e)
    {
        if (ContextMenuFlyout.IsOpen)
        {
            ContextMenuFlyout.Hide();
        }
    }

    private void RootGrid_RightTapped(object sender, Microsoft.UI.Xaml.Input.RightTappedRoutedEventArgs e)
    {
        // Don't show the dock context menu while in edit mode
        if (IsEditMode)
        {
            return;
        }

        // This context menu is for the dock itself (not a band), so the palette
        // should not be opened on invocation.
        _bandContextMenuPalettePos = null;
        _bandContextMenuTarget = null;
        _bandContextMenuItem = null;

        var pos = e.GetPosition(null);
        var item = this.ViewModel.GetContextMenuForDock();
        if (item.HasMoreCommands)
        {
            ContextControl.SetCommandContext(item);
            ContextControl.ShowFilterBox = false;
            ContextControl.PrepareForOpen(GetDockContextMenuFilterLocation());
            PreparePopupForShow(ContextMenuFlyout, RootGrid);
            ContextMenuFlyout.ShowAt(
            this.RootGrid,
            new FlyoutShowOptions()
            {
                ShowMode = FlyoutShowMode.Standard,
                Placement = FlyoutPlacementMode.TopEdgeAlignedRight,
                Position = pos,
            });
            e.Handled = true;
        }
    }

    private DockBandViewModel? _draggedBand;

    private void BandListView_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
    {
        if (e.Items.Count > 0 && e.Items[0] is DockBandViewModel band)
        {
            _draggedBand = band;
            e.Data.RequestedOperation = DataPackageOperation.Move;

            // Only advertise cross-monitor data when we have a real monitor ID.
            // Without one (single-monitor / global dock) the cross-monitor path
            // cannot safely distinguish source from target.
            if (ViewModel.MonitorDeviceId is not null)
            {
                e.Data.Properties["DockBandId"] = band.Id;
                e.Data.Properties["SourceMonitorDeviceId"] = ViewModel.MonitorDeviceId;
            }
        }
    }

    private void BandListView_DragOver(object sender, DragEventArgs e)
    {
        if (_draggedBand != null || e.DataView.Properties.ContainsKey("DockBandId"))
        {
            e.AcceptedOperation = DataPackageOperation.Move;
        }
    }

    private void BandListView_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
    {
        // Reordering within the same list is handled automatically by ListView
        // We just need to sync the ViewModel order without saving
        if (args.DropResult == DataPackageOperation.Move && _draggedBand != null)
        {
            DockPinSide targetSide;
            ObservableCollection<DockBandViewModel> targetCollection;

            if (sender == StartListView)
            {
                targetSide = DockPinSide.Start;
                targetCollection = ViewModel.StartItems;
            }
            else if (sender == CenterListView)
            {
                targetSide = DockPinSide.Center;
                targetCollection = ViewModel.CenterItems;
            }
            else
            {
                targetSide = DockPinSide.End;
                targetCollection = ViewModel.EndItems;
            }

            // Find the new index and sync ViewModel (without saving)
            var newIndex = targetCollection.IndexOf(_draggedBand);
            if (newIndex >= 0)
            {
                ViewModel.SyncBandPosition(_draggedBand, targetSide, newIndex);
            }
        }

        _draggedBand = null;
    }

    private void StartListView_Drop(object sender, DragEventArgs e)
    {
        HandleCrossListDrop(DockPinSide.Start, e);
        ResetListViewState(sender);
    }

    private void CenterListView_Drop(object sender, DragEventArgs e)
    {
        HandleCrossListDrop(DockPinSide.Center, e);
        ResetListViewState(sender);
    }

    private void EndListView_Drop(object sender, DragEventArgs e)
    {
        HandleCrossListDrop(DockPinSide.End, e);
        ResetListViewState(sender);
    }

    private void HandleCrossListDrop(DockPinSide targetSide, DragEventArgs e)
    {
        if (_draggedBand != null)
        {
            HandleLocalCrossListDrop(targetSide, e);
            return;
        }

        // Cross-monitor drag from another DockControl
        if (e.DataView.Properties.TryGetValue("DockBandId", out var bandIdObj) &&
            e.DataView.Properties.TryGetValue("SourceMonitorDeviceId", out var sourceMonitorObj) &&
            bandIdObj is string bandId &&
            sourceMonitorObj is string sourceMonitorDeviceId)
        {
            HandleCrossMonitorDrop(bandId, sourceMonitorDeviceId, targetSide, e);
        }
    }

    private void HandleLocalCrossListDrop(DockPinSide targetSide, DragEventArgs e)
    {
        // Check which list the band is currently in
        var isInStart = ViewModel.StartItems.Contains(_draggedBand!);
        var isInCenter = ViewModel.CenterItems.Contains(_draggedBand!);

        DockPinSide sourceSide;
        if (isInStart)
        {
            sourceSide = DockPinSide.Start;
        }
        else if (isInCenter)
        {
            sourceSide = DockPinSide.Center;
        }
        else
        {
            sourceSide = DockPinSide.End;
        }

        // Only handle cross-list drops here; same-list reorders are handled in DragItemsCompleted
        if (sourceSide != targetSide)
        {
            var targetListView = targetSide switch
            {
                DockPinSide.Start => StartListView,
                DockPinSide.Center => CenterListView,
                _ => EndListView,
            };
            var targetCollection = targetSide switch
            {
                DockPinSide.Start => ViewModel.StartItems,
                DockPinSide.Center => ViewModel.CenterItems,
                _ => ViewModel.EndItems,
            };

            var dropIndex = GetDropIndex(targetListView, e, targetCollection.Count);

            // Move the band to the new side (without saving - save happens on Done)
            ViewModel.MoveBandWithoutSaving(_draggedBand!, targetSide, dropIndex);
            e.Handled = true;
        }
    }

    private void HandleCrossMonitorDrop(string bandId, string sourceMonitorDeviceId, DockPinSide targetSide, DragEventArgs e)
    {
        var targetListView = targetSide switch
        {
            DockPinSide.Start => StartListView,
            DockPinSide.Center => CenterListView,
            _ => EndListView,
        };
        var targetCollection = targetSide switch
        {
            DockPinSide.Start => ViewModel.StartItems,
            DockPinSide.Center => ViewModel.CenterItems,
            _ => ViewModel.EndItems,
        };

        var dropIndex = GetDropIndex(targetListView, e, targetCollection.Count);

        ViewModel.AcceptBandFromMonitor(bandId, targetSide, dropIndex);

        if (!string.IsNullOrEmpty(sourceMonitorDeviceId))
        {
            WeakReferenceMessenger.Default.Send(new CrossMonitorBandDropMessage(bandId, sourceMonitorDeviceId));
        }

        e.Handled = true;
    }

    private int GetDropIndex(ListView listView, DragEventArgs e, int itemCount)
    {
        var position = e.GetPosition(listView);

        // Find the item at the drop position
        for (var i = 0; i < itemCount; i++)
        {
            if (listView.ContainerFromIndex(i) is ListViewItem container)
            {
                var itemBounds = container.TransformToVisual(listView).TransformBounds(
                    new Rect(0, 0, container.ActualWidth, container.ActualHeight));

                if (ItemsOrientation == Orientation.Horizontal)
                {
                    // For horizontal layout, check X position
                    if (position.X < itemBounds.X + (itemBounds.Width / 2))
                    {
                        return i;
                    }
                }
                else
                {
                    // For vertical layout, check Y position
                    if (position.Y < itemBounds.Y + (itemBounds.Height / 2))
                    {
                        return i;
                    }
                }
            }
        }

        // If we're past all items, insert at the end
        return itemCount;
    }

    // Tracks which section (Start/Center/End) the add button was clicked for
    private DockPinSide _addBandTargetSide;

    private void AddBandButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string sideTag)
        {
            _addBandTargetSide = sideTag switch
            {
                "Start" => DockPinSide.Start,
                "Center" => DockPinSide.Center,
                "End" => DockPinSide.End,
                _ => DockPinSide.Center,
            };

            // Populate the list with available bands (not already in the dock)
            var availableBands = ViewModel.GetAvailableBandsToAdd().ToList();
            AddBandListView.ItemsSource = availableBands;

            // Show/hide empty state text based on whether there are bands to add
            var hasAvailableBands = availableBands.Count > 0;
            NoAvailableBandsText.Visibility = hasAvailableBands ? Visibility.Collapsed : Visibility.Visible;
            AddBandListView.Visibility = hasAvailableBands ? Visibility.Visible : Visibility.Collapsed;

            // Show the flyout
            PreparePopupForShow(AddBandFlyout, button);
            AddBandFlyout.ShowAt(button);
        }
    }

    private void AddBandListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is TopLevelViewModel topLevel)
        {
            // Add the band to the target section
            ViewModel.AddBandToSection(topLevel, _addBandTargetSide);

            // Close the flyout
            AddBandFlyout.Hide();
        }
    }

    private void BandListView_DragEnter(object sender, DragEventArgs e)
    {
        if (sender is ListView view && (_draggedBand != null || e.DataView.Properties.ContainsKey("DockBandId")))
        {
            view.Background = Application.Current.Resources["ControlAltFillColorQuarternaryBrush"] as SolidColorBrush;
            e.DragUIOverride.IsGlyphVisible = false;
            e.DragUIOverride.IsCaptionVisible = false;
        }
    }

    private void BandListView_DragLeave(object sender, DragEventArgs e)
    {
        ResetListViewState(sender);
    }

    private void ResetListViewState(object sender)
    {
        if (sender is ListView listView)
        {
            listView.Background = new SolidColorBrush(Colors.Transparent);
        }
    }

    private void RootGrid_DragOver(object sender, DragEventArgs e)
    {
        // Don't intercept internal band drag-drop during edit mode
        if (_draggedBand != null)
        {
            return;
        }

        if (e.DataView.Contains(StandardDataFormats.StorageItems) ||
            e.DataView.Contains(StandardDataFormats.Uri))
        {
            e.AcceptedOperation = DataPackageOperation.Link;
            e.DragUIOverride.Caption = RS_.GetString("Dock_DropFile_Caption");
            e.DragUIOverride.IsGlyphVisible = true;
            e.DragUIOverride.IsCaptionVisible = true;

            // DON'T mark the event as handled - if you do, we won't get the Drop event.
        }
    }

    private async void RootGrid_Drop(object sender, DragEventArgs e)
    {
        // Don't intercept internal band drag-drop during edit mode
        if (_draggedBand != null)
        {
            Logger.LogDebug("[DockDrop] RootGrid_Drop: ignoring (internal band drag in progress)");
            return;
        }

        var hasStorageItems = e.DataView.Contains(StandardDataFormats.StorageItems);
        var hasUri = e.DataView.Contains(StandardDataFormats.Uri);

        if (!hasStorageItems && !hasUri)
        {
            return;
        }

        e.Handled = true;

        try
        {
            var bookmarksManager = App.Current.Services.GetService<IBookmarksManager>();
            if (bookmarksManager == null)
            {
                Logger.LogWarning("[DockDrop] IBookmarksManager service is not registered; cannot pin dropped item");
                return;
            }

            var foundItem = false;
            if (hasStorageItems)
            {
                var items = await e.DataView.GetStorageItemsAsync();
                foreach (var item in items)
                {
                    var path = item.Path;
                    if (string.IsNullOrEmpty(path))
                    {
                        continue;
                    }

                    var name = Path.GetFileNameWithoutExtension(path);
                    AddBookmarkAndPinToDock(bookmarksManager, name, path);
                    foundItem = true;
                }
            }

            if (foundItem)
            {
                return;
            }

            if (hasUri)
            {
                var uri = await e.DataView.GetUriAsync();
                var url = uri.AbsoluteUri;
                var name = uri.Host;
                AddBookmarkAndPinToDock(bookmarksManager, name, url);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("[DockDrop] Error handling file drop on dock", ex);
        }
    }

    private static void AddBookmarkAndPinToDock(IBookmarksManager bookmarksManager, string name, string bookmarkValue)
    {
        var bookmark = bookmarksManager.Add(name, bookmarkValue);

        // Make the command ID exactly the same as the ID it would have in the
        // top-level list, so that pinning to the dock from the top-level is seamless.
        var commandId = Ext.Bookmarks.Helpers.CommandIds.GetLaunchBookmarkItemId(bookmark.Id);
        Logger.LogDebug($"[DockDrop] Pinning dropped item '{name}' as bookmark id={bookmark.Id} (commandId='{commandId}')");
        WeakReferenceMessenger.Default.Send(new PinToDockMessage("Bookmarks", commandId, true, WithReload: false));
    }

    public void Receive(CrossMonitorBandDropMessage message)
    {
        // Only match if this dock has a real monitor ID that matches the source.
        if (ViewModel.MonitorDeviceId is null)
        {
            return;
        }

        if (!string.Equals(ViewModel.MonitorDeviceId, message.SourceMonitorDeviceId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            ViewModel.RemoveBandById(message.BandId);
        });
    }

    public void Dispose()
    {
        Loaded -= DockControl_Loaded;
        Unloaded -= DockControl_Unloaded;
        ContextControl.CloseRequested -= ContextControl_CloseRequested;
        ContextControl.ViewModel.CommandInvoked -= ContextMenu_CommandInvoked;
        ContextControl.ViewModel.CommandInvoking -= ContextMenu_CommandInvoking;
        ViewModel.CenterItems.CollectionChanged -= CenterItems_CollectionChanged;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _pendingPageRequest = null;
        CleanupDockPage();
        GC.SuppressFinalize(this);
    }
}
