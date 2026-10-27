// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Dashboard;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.CommandPalette.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;
using WinUIEx;

namespace Microsoft.CmdPal.UI.Dashboard;

/// <summary>
/// The dashboard: a transient, full-screen overlay on the primary display that shows the
/// pages the user pinned to it. Each tile works like a dock flyout whose root is the pinned
/// page. Commands from a tile carry that tile's <see cref="DockCommandRoute"/>, so this window
/// navigates its own pages and forwards everything else to the palette.
/// </summary>
public sealed partial class DashboardWindow : WindowEx,
    IRecipient<PerformCommandMessage>,
    IRecipient<HandleCommandResultMessage>,
    IRecipient<ShowWindowMessage>,
    IRecipient<ReloadFinishedMessage>,
    IRecipient<ProviderEnabledStateChangedMessage>,
    IDisposable
{
    // The space between neighboring tiles, in DIPs.
    private const double TileGap = 12;

    private static readonly Microsoft.Windows.ApplicationModel.Resources.ResourceLoader ResourceLoader = new();

    private readonly ISettingsService _settingsService;
    private readonly TopLevelCommandManager _commandManager;
    private readonly TaskScheduler _uiScheduler;
    private readonly IPageViewModelFactoryService _pageFactory;
    private readonly IAppHostService _appHostService;
    private readonly HiddenOwnerWindowBehavior _hiddenOwnerWindowBehavior = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly HWND _hwnd;

    private List<DashboardTileControl> _tiles = [];
    private DashboardSettings _settings;
    private DashboardBackdrop? _appliedBackdrop;
    private int _columns = DashboardLayout.MinColumnSpan;
    private int _rows = DashboardLayout.MinRowSpan;
    private bool _isShown;
    private bool _isEditing;
    private bool _isDisposed;

    // The tile being moved or resized, and where it was when the gesture started.
    private DashboardTileControl? _editTile;
    private DashboardTileRect _editStartRect;
    private DashboardTileRect _editTargetRect;

    internal DashboardWindow(DashboardSettings settings)
    {
        var services = App.Current.Services;
        _settingsService = services.GetRequiredService<ISettingsService>();
        _commandManager = services.GetRequiredService<TopLevelCommandManager>();
        _uiScheduler = services.GetRequiredService<TaskScheduler>();
        _pageFactory = services.GetRequiredService<IPageViewModelFactoryService>();
        _appHostService = services.GetRequiredService<IAppHostService>();
        _settings = settings;

        InitializeComponent();
        Title = ResourceLoader.GetString("Dashboard_WindowTitle");

        _hiddenOwnerWindowBehavior.ShowInTaskbar(this, false);
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(false, false);
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
        }

        _hwnd = new HWND(WindowNative.GetWindowHandle(this));
        IsAlwaysOnTop = true;
        UpdateWindowFrame();

        Activated += DashboardWindow_Activated;
        Closed += DashboardWindow_Closed;
        Root.KeyDown += Root_KeyDown;
        FadeOutStoryboard.Completed += FadeOutStoryboard_Completed;

        WeakReferenceMessenger.Default.Register<PerformCommandMessage>(this);
        WeakReferenceMessenger.Default.Register<HandleCommandResultMessage>(this);
        WeakReferenceMessenger.Default.Register<ShowWindowMessage>(this);
        WeakReferenceMessenger.Default.Register<ReloadFinishedMessage>(this);
        WeakReferenceMessenger.Default.Register<ProviderEnabledStateChangedMessage>(this);

        ApplySettings(settings);
    }

    internal bool IsShown => _isShown;

    internal IntPtr Hwnd => _hwnd;

    /// <summary>
    /// Fades the dashboard in over the primary display.
    /// </summary>
    internal void ShowDashboard()
    {
        if (_isDisposed)
        {
            return;
        }

        if (_isShown)
        {
            PInvoke.SetForegroundWindow(_hwnd);
            return;
        }

        _isShown = true;
        FadeOutStoryboard.Stop();
        Root.Opacity = 0;
        UpdateBounds();

        PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_SHOW);
        Activate();
        PInvoke.SetForegroundWindow(_hwnd);
        PInvoke.SetActiveWindow(_hwnd);

        // Moving between displays with different DPIs can resize the window after the first
        // move. Settle on the right bounds once the DPI change has been handled.
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, UpdateBounds);

        FadeInStoryboard.Begin();
        RetryUnavailableTiles();
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, FocusFirstTile);
    }

    /// <summary>
    /// Fades the dashboard out and returns every tile to its pinned page.
    /// </summary>
    internal void HideDashboard()
    {
        if (_isDisposed || !_isShown)
        {
            return;
        }

        _isShown = false;
        SetEditing(false);

        // Stopping the fade-in reverts Root to its base opacity (0), so carry the visible
        // value over for the fade-out to start from.
        var currentOpacity = Root.Opacity;
        FadeInStoryboard.Stop();
        Root.Opacity = currentOpacity;
        FadeOutStoryboard.Begin();
    }

    internal void ToggleDashboard()
    {
        if (_isShown)
        {
            HideDashboard();
        }
        else
        {
            ShowDashboard();
        }
    }

    /// <summary>
    /// Applies new dashboard settings: the backdrop, the set of tiles, and their layout.
    /// </summary>
    internal void ApplySettings(DashboardSettings settings)
    {
        if (_isDisposed)
        {
            return;
        }

        _settings = settings;
        UpdateBackdrop();
        ReconcileTiles();
        Relayout();
    }

    /// <summary>
    /// Covers the primary display. Called when the display configuration changes.
    /// </summary>
    internal void UpdateBounds()
    {
        if (_isDisposed)
        {
            return;
        }

        var bounds = DisplayArea.Primary.OuterBounds;
        var originalMinWidth = MinWidth;
        var originalMinHeight = MinHeight;
        try
        {
            MinWidth = 0;
            MinHeight = 0;
            AppWindow.MoveAndResize(bounds);
        }
        finally
        {
            MinWidth = originalMinWidth;
            MinHeight = originalMinHeight;
        }
    }

    private void UpdateWindowFrame()
    {
        HwndExtensions.ToggleWindowStyle(_hwnd, false, WindowStyle.TiledWindow);
        unsafe
        {
            var corner = DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND;
            PInvoke.DwmSetWindowAttribute(_hwnd, DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, &corner, sizeof(DWM_WINDOW_CORNER_PREFERENCE));

            // DWMWA_COLOR_NONE: no accent border.
            uint borderColorNone = 0xFFFFFFFE;
            PInvoke.DwmSetWindowAttribute(_hwnd, DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR, &borderColorNone, sizeof(uint));

            // We fade ourselves; skip the system's show/hide animation.
            BOOL disableTransitions = true;
            PInvoke.DwmSetWindowAttribute(_hwnd, DWMWINDOWATTRIBUTE.DWMWA_TRANSITIONS_FORCEDISABLED, &disableTransitions, (uint)sizeof(BOOL));
        }
    }

    private void UpdateBackdrop()
    {
        Scrim.Opacity = Math.Clamp(_settings.BackgroundOpacity, DashboardSettings.MinBackgroundOpacity, DashboardSettings.MaxBackgroundOpacity) / 100.0;

        if (_appliedBackdrop == _settings.Backdrop)
        {
            return;
        }

        _appliedBackdrop = _settings.Backdrop;
        SystemBackdrop = _settings.Backdrop switch
        {
            DashboardBackdrop.Acrylic => new DesktopAcrylicBackdrop(),
            _ => new TransparentTintBackdrop(),
        };
    }

    // Tiles
    private void ReconcileTiles()
    {
        var next = new List<DashboardTileControl>(_settings.Tiles.Count);
        foreach (var tileSettings in _settings.Tiles)
        {
            var tile = _tiles.FirstOrDefault(t => t.Matches(tileSettings)) ?? CreateTile(tileSettings);
            next.Add(tile);
        }

        foreach (var removed in _tiles.Where(t => !next.Contains(t)))
        {
            DestroyTile(removed);
        }

        _tiles = next;
        EmptyState.Visibility = _tiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        EditButton.IsEnabled = _tiles.Count > 0;
        if (_tiles.Count == 0)
        {
            SetEditing(false);
        }
    }

    private DashboardTileControl CreateTile(DashboardTileSettings settings)
    {
        var tile = new DashboardTileControl(settings) { IsEditing = _isEditing };
        tile.DismissRequested += Tile_DismissRequested;
        tile.UnpinRequested += Tile_UnpinRequested;
        tile.EditStarted += Tile_EditStarted;
        tile.EditDelta += Tile_EditDelta;
        tile.EditCompleted += Tile_EditCompleted;
        TileCanvas.Children.Add(tile);
        LoadTile(tile);
        return tile;
    }

    private void DestroyTile(DashboardTileControl tile)
    {
        tile.DismissRequested -= Tile_DismissRequested;
        tile.UnpinRequested -= Tile_UnpinRequested;
        tile.EditStarted -= Tile_EditStarted;
        tile.EditDelta -= Tile_EditDelta;
        tile.EditCompleted -= Tile_EditCompleted;
        TileCanvas.Children.Remove(tile);
        tile.Dispose();
        if (ReferenceEquals(_editTile, tile))
        {
            EndEdit();
        }
    }

    private void LoadTile(DashboardTileControl tile) =>
        _ = tile.LoadAsync(_hwnd, _commandManager, _uiScheduler, _pageFactory, _appHostService, _lifetime.Token);

    // Extensions may still have been loading when a tile was created, and page errors never
    // clear on their own. Try those tiles again on show.
    private void RetryUnavailableTiles()
    {
        foreach (var tile in _tiles)
        {
            if ((!tile.HasPage || tile.HasError) && !tile.IsLoading)
            {
                LoadTile(tile);
            }
        }
    }

    // Tiles hold pages from the extension instance that was loaded when they opened. After a
    // reload those are stale (or dead), so resolve each tile again against the new providers.
    private void ReloadTiles(string? providerId = null)
    {
        foreach (var tile in _tiles)
        {
            if (providerId is null || tile.ProviderId == providerId)
            {
                LoadTile(tile);
            }
        }
    }

    private void FocusFirstTile()
    {
        if (!_isShown)
        {
            return;
        }

        foreach (var tile in _tiles)
        {
            if (tile.TryFocusPage())
            {
                return;
            }
        }

        SettingsButton.Focus(FocusState.Programmatic);
    }

    // Layout
    //
    // Tiles live on a grid of DashboardLayout.CellSize cells. Settings store each tile's rect
    // along with the size of the grid it was laid out on (the design grid). When the display
    // size changes we don't rewrite the settings: we project the design layout onto the
    // current grid instead, keeping tile sizes and moving tiles proportionally, then resolve
    // any overlaps. Going back to the original display restores the original layout exactly.
    // Only an explicit edit by the user saves a new layout (and a new design grid).
    private void TileScroller_SizeChanged(object sender, SizeChangedEventArgs e) => Relayout();

    private void Relayout()
    {
        if (_editTile is not null)
        {
            return;
        }

        var width = TileScroller.ActualWidth - TileCanvas.Margin.Left - TileCanvas.Margin.Right;
        var height = TileScroller.ActualHeight - TileCanvas.Margin.Top - TileCanvas.Margin.Bottom;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        _columns = DashboardLayout.ColumnsFor(width);
        _rows = DashboardLayout.RowsFor(height);

        var desired = _settings.Tiles.Select(t => t.Rect).ToList();
        var arranged = DashboardLayout.Arrange(desired, _columns, _rows, _settings.DesignColumns, _settings.DesignRows);
        ApplyArrangement(arranged);
    }

    private void ApplyArrangement(IReadOnlyList<DashboardTileRect> arranged)
    {
        for (var i = 0; i < _tiles.Count && i < arranged.Count; i++)
        {
            _tiles[i].Rect = arranged[i];
            PlaceElement(_tiles[i], arranged[i]);
        }

        var extent = Math.Max(_rows, DashboardLayout.Extent(arranged));
        TileCanvas.Width = _columns * DashboardLayout.CellSize;
        TileCanvas.Height = extent * DashboardLayout.CellSize;
    }

    private static void PlaceElement(FrameworkElement element, DashboardTileRect rect)
    {
        Canvas.SetLeft(element, rect.Column * DashboardLayout.CellSize);
        Canvas.SetTop(element, rect.Row * DashboardLayout.CellSize);
        element.Width = Math.Max(0, (rect.ColumnSpan * DashboardLayout.CellSize) - TileGap);
        element.Height = Math.Max(0, (rect.RowSpan * DashboardLayout.CellSize) - TileGap);
    }

    // Edit mode
    private void SetEditing(bool editing)
    {
        if (editing && _tiles.Count == 0)
        {
            editing = false;
        }

        if (_isEditing == editing)
        {
            return;
        }

        _isEditing = editing;
        if (!editing)
        {
            EndEdit();
        }

        foreach (var tile in _tiles)
        {
            tile.IsEditing = editing;
        }

        EditButton.IsChecked = editing;
        EditButtonText.Text = ResourceLoader.GetString(editing ? "Dashboard_DoneButtonText" : "Dashboard_EditButtonText");
        EditHint.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        ResetLayoutButton.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Tile_EditStarted(object? sender, DashboardTileEditKind kind)
    {
        if (sender is not DashboardTileControl tile || !_isEditing)
        {
            return;
        }

        _editTile = tile;
        _editStartRect = tile.Rect;
        _editTargetRect = tile.Rect;
        Canvas.SetZIndex(tile, 2);
        Canvas.SetZIndex(DropPreview, 1);
        PlaceElement(DropPreview, tile.Rect);
        DropPreview.Visibility = Visibility.Visible;
    }

    private void Tile_EditDelta(object? sender, DashboardTileEditEventArgs e)
    {
        if (!ReferenceEquals(sender, _editTile) || _editTile is null)
        {
            return;
        }

        var cell = DashboardLayout.CellSize;
        var start = _editStartRect;
        var rowsAvailable = Math.Max(_rows, DashboardLayout.Extent(_tiles.Select(t => t.Rect).ToList()));

        if (e.Kind == DashboardTileEditKind.Move)
        {
            var maxLeft = Math.Max(0, (_columns - start.ColumnSpan) * cell);
            var maxTop = Math.Max(0, (rowsAvailable - start.RowSpan) * cell);
            var left = Math.Clamp((start.Column * cell) + e.Delta.X, 0, maxLeft);
            var top = Math.Clamp((start.Row * cell) + e.Delta.Y, 0, maxTop);
            Canvas.SetLeft(_editTile, left);
            Canvas.SetTop(_editTile, top);
            _editTargetRect = start with { Column = DashboardLayout.ToCells(left), Row = DashboardLayout.ToCells(top) };
        }
        else
        {
            var maxWidth = Math.Max(DashboardLayout.MinColumnSpan, _columns - start.Column) * cell;
            var maxHeight = Math.Max(DashboardLayout.MinRowSpan, rowsAvailable - start.Row) * cell;
            var width = Math.Clamp((start.ColumnSpan * cell) + e.Delta.X, DashboardLayout.MinColumnSpan * cell, maxWidth);
            var height = Math.Clamp((start.RowSpan * cell) + e.Delta.Y, DashboardLayout.MinRowSpan * cell, maxHeight);
            _editTile.Width = width - TileGap;
            _editTile.Height = height - TileGap;
            _editTargetRect = start with
            {
                ColumnSpan = Math.Max(DashboardLayout.MinColumnSpan, DashboardLayout.ToCells(width)),
                RowSpan = Math.Max(DashboardLayout.MinRowSpan, DashboardLayout.ToCells(height)),
            };
        }

        PlaceElement(DropPreview, _editTargetRect);
    }

    private void Tile_EditCompleted(object? sender, DashboardTileEditKind kind)
    {
        if (!ReferenceEquals(sender, _editTile) || _editTile is null)
        {
            return;
        }

        var index = _tiles.IndexOf(_editTile);
        var target = _editTargetRect;
        EndEdit();
        if (index < 0)
        {
            return;
        }

        var desired = _tiles.Select(t => t.Rect).ToArray();
        desired[index] = target;
        var arranged = DashboardLayout.Arrange(desired, _columns, _rows, priorityIndex: index);
        SaveLayout(arranged);
    }

    private void EndEdit()
    {
        if (_editTile is not null)
        {
            Canvas.SetZIndex(_editTile, 0);
            _editTile = null;
        }

        DropPreview.Visibility = Visibility.Collapsed;
        Relayout();
    }

    private void SaveLayout(IReadOnlyList<DashboardTileRect> arranged)
    {
        var keys = _tiles.Select(t => (t.ProviderId, t.CommandId)).ToList();
        var columns = _columns;
        var rows = _rows;
        _settingsService.UpdateSettings(s =>
        {
            var dashboard = s.DashboardSettings;
            if (!HasTiles(dashboard, keys))
            {
                // The pinned tiles changed under us; drop this edit.
                return s;
            }

            return s with { DashboardSettings = dashboard.WithLayout(arranged, columns, rows) };
        });

        // Show the result right away rather than waiting for the settings round trip.
        if (HasTiles(_settingsService.Settings.DashboardSettings, keys))
        {
            _settings = _settingsService.Settings.DashboardSettings;
        }

        Relayout();
    }

    private static bool HasTiles(DashboardSettings settings, List<(string ProviderId, string CommandId)> keys)
    {
        if (settings.Tiles.Count != keys.Count)
        {
            return false;
        }

        for (var i = 0; i < keys.Count; i++)
        {
            if (!settings.Tiles[i].Matches(keys[i].ProviderId, keys[i].CommandId))
            {
                return false;
            }
        }

        return true;
    }

    private void Tile_UnpinRequested(object? sender, EventArgs e)
    {
        if (sender is not DashboardTileControl tile)
        {
            return;
        }

        _settingsService.UpdateSettings(s => s with
        {
            DashboardSettings = s.DashboardSettings.WithTileUnpinned(tile.ProviderId, tile.CommandId),
        });
    }

    private void Tile_DismissRequested(object? sender, EventArgs e) => HideDashboard();

    // Toolbar and input
    private void EditButton_Checked(object sender, RoutedEventArgs e) => SetEditing(true);

    private void EditButton_Unchecked(object sender, RoutedEventArgs e) => SetEditing(false);

    private void ResetLayoutButton_Click(object sender, RoutedEventArgs e) =>
        _settingsService.UpdateSettings(s => s with { DashboardSettings = s.DashboardSettings.WithLayoutReset() });

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        HideDashboard();
        WeakReferenceMessenger.Default.Send(new OpenSettingsMessage(SettingsPageTags.Dashboard));
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => HideDashboard();

    private void Scrim_Tapped(object sender, TappedRoutedEventArgs e)
    {
        e.Handled = true;
        if (_isEditing)
        {
            SetEditing(false);
        }
        else
        {
            HideDashboard();
        }
    }

    private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Escape || e.Handled)
        {
            return;
        }

        e.Handled = true;
        if (_isEditing)
        {
            SetEditing(false);
        }
        else
        {
            HideDashboard();
        }
    }

    private void DashboardWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        UpdateWindowFrame();
        if (args.WindowActivationState != WindowActivationState.Deactivated || !_isShown)
        {
            return;
        }

        // Let the new foreground window settle before deciding whether focus really left us.
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            if (!_isShown || !_settings.DismissOnFocusLost || _editTile is not null)
            {
                return;
            }

            if (_tiles.Any(t => t.HasOpenTransientUi) || IsOurs(PInvoke.GetForegroundWindow()))
            {
                return;
            }

            HideDashboard();
        });
    }

    // Popups and menus from our tiles are separate windows owned by this one.
    private bool IsOurs(HWND hwnd)
    {
        if (hwnd == HWND.Null)
        {
            return false;
        }

        var current = PInvoke.GetAncestor(hwnd, GET_ANCESTOR_FLAGS.GA_ROOT);
        for (var depth = 0; current != HWND.Null && depth < 16; depth++)
        {
            if (current == _hwnd)
            {
                return true;
            }

            current = PInvoke.GetWindow(current, GET_WINDOW_CMD.GW_OWNER);
        }

        return false;
    }

    private void FadeOutStoryboard_Completed(object? sender, object e)
    {
        if (_isShown || _isDisposed)
        {
            return;
        }

        PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_HIDE);
        foreach (var tile in _tiles)
        {
            _ = tile.GoHomeAsync();
        }
    }

    // Command routing (mirrors DockPageFlyoutController)
    public void Receive(PerformCommandMessage message)
    {
        if (!IsForThisWindow(message.DockRoute))
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() =>
        {
            var navigation = FindNavigation(message.DockRoute);
            if (message.Command.IsPage)
            {
                if (navigation is not null)
                {
                    _ = NavigateAsync(navigation, message);
                }

                return;
            }

            ForwardToShell(message, navigation);
        });
    }

    public void Receive(HandleCommandResultMessage message)
    {
        if (!IsForThisWindow(message.DockRoute))
        {
            return;
        }

        DispatcherQueue.TryEnqueue(() => ForwardToShell(message, FindNavigation(message.DockRoute)));
    }

    public void Receive(ShowWindowMessage message)
    {
        // The palette is about to show; get out of its way.
        DispatcherQueue.TryEnqueue(HideDashboard);
    }

    public void Receive(ReloadFinishedMessage message) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_isDisposed)
            {
                ReloadTiles();
            }
        });

    public void Receive(ProviderEnabledStateChangedMessage message) =>
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_isDisposed)
            {
                ReloadTiles(message.ProviderId);
            }
        });

    private bool IsForThisWindow(DockCommandRoute? route) =>
        route is { } value && value.OwnerHwnd == (nint)_hwnd;

    private DockPageNavigationViewModel? FindNavigation(DockCommandRoute? route) =>
        route is null ? null : _tiles.Select(t => t.Navigation).FirstOrDefault(n => n is not null && n.Route == route.Value);

    private static async Task NavigateAsync(DockPageNavigationViewModel navigation, PerformCommandMessage message)
    {
        try
        {
            await navigation.NavigateAsync(message);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to open a page in a dashboard tile.", ex);
        }
    }

    private void ForwardToShell(PerformCommandMessage message, DockPageNavigationViewModel? navigation) =>
        WeakReferenceMessenger.Default.Send(message with
        {
            DockRoute = null,
            OnBeforeShowConfirmation = CreateConfirmationCallback(message.OnBeforeShowConfirmation),
            ResultHandler = CreateResultHandler(message.ResultHandler, navigation, message.DockRoute, message.Context?.Page),
        });

    private void ForwardToShell(HandleCommandResultMessage message, DockPageNavigationViewModel? navigation) =>
        WeakReferenceMessenger.Default.Send(message with
        {
            DockRoute = null,
            OnBeforeShowConfirmation = CreateConfirmationCallback(message.OnBeforeShowConfirmation),
            ResultHandler = CreateResultHandler(message.ResultHandler, navigation, message.DockRoute, message.Context?.Page),
        });

    // Confirmations belong to the palette, which can't show over the dashboard.
    private Action CreateConfirmationCallback(Action? existingCallback)
    {
        var dispatcherQueue = DispatcherQueue;
        return () =>
        {
            existingCallback?.Invoke();
            dispatcherQueue.TryEnqueue(HideDashboard);
            WeakReferenceMessenger.Default.Send(new ShowWindowMessage(IntPtr.Zero));
        };
    }

    // Results from a tile's current page go back to that tile, which asks us to close on
    // Dismiss/Hide. Anything else keeps only shell UI (toasts, confirmations), but still
    // dismisses the dashboard whenever it would have dismissed the palette.
    private Func<ICommandResult, bool> CreateResultHandler(
        Func<ICommandResult, bool>? existingHandler,
        DockPageNavigationViewModel? navigation,
        DockCommandRoute? route,
        PageViewModel? sourcePage)
    {
        if (navigation is not null &&
            route == navigation.Route &&
            sourcePage is not null &&
            navigation.OwnsSourcePage(sourcePage))
        {
            return result => existingHandler?.Invoke(result) == true ||
                navigation.HandleCommandResult(sourcePage, result);
        }

        var dispatcherQueue = DispatcherQueue;
        return result =>
        {
            if (result.Kind is CommandResultKind.Dismiss or CommandResultKind.Hide)
            {
                dispatcherQueue.TryEnqueue(HideDashboard);
            }

            return existingHandler?.Invoke(result) == true ||
                !DockPageNavigationViewModel.IsShellResult(result.Kind);
        };
    }

    private void DashboardWindow_Closed(object sender, WindowEventArgs args) => Dispose();

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        WeakReferenceMessenger.Default.UnregisterAll(this);
        _lifetime.Cancel();
        foreach (var tile in _tiles)
        {
            tile.Dispose();
        }

        _tiles.Clear();
        _lifetime.Dispose();
    }
}
