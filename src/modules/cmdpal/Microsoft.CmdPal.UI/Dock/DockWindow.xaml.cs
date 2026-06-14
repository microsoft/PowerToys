// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT;
using WinRT.Interop;
using WinUIEx;
using MonitorInfo = Microsoft.CmdPal.UI.ViewModels.Models.MonitorInfo;
using POINT = Microsoft.PowerToys.Settings.UI.Helpers.POINT;
using RECT = Windows.Win32.Foundation.RECT;

namespace Microsoft.CmdPal.UI.Dock;

#pragma warning disable SA1402 // File may only contain a single type

/// <summary>
/// The main window for the dock feature. Uses the Windows AppBar API to reserve
/// screen work area and position itself at the edge of the display.
///
/// <para><strong>Phase 1 UX Contract:</strong></para>
/// <list type="bullet">
///   <item><description><strong>Pinned mode</strong>: The dock is always-visible and reserves work area
///     via <c>ABM_NEW</c>/<c>ABM_SETPOS</c>. Maximized windows will not overlap the dock.</description></item>
///   <item><description><strong>Auto-hide mode</strong>: The dock uses
///     <c>ABM_SETAUTOHIDEBAR</c> and will NOT reserve work area — maximized windows will
///     extend beneath the hidden dock.</description></item>
///   <item><description><strong>v1 scope</strong>: Global-only dock positioning. Per-monitor side overrides
///     exist but per-monitor enable/disable and customized bands are future work.</description></item>
/// </list>
/// </summary>
public sealed partial class DockWindow : WindowEx,
    IRecipient<BringToTopMessage>,
    IRecipient<RequestShowPaletteAtMessage>,
    IRecipient<QuitMessage>,
    IDisposable
{
    private enum DockAppBarMode
    {
        None,
        Pinned,
        AutoHide,
    }

#pragma warning disable SA1306 // Field names should begin with lower-case letter
#pragma warning disable SA1310 // Field names should not contain underscore
    private readonly uint WM_TASKBAR_RESTART;
#pragma warning restore SA1310 // Field names should not contain underscore
#pragma warning restore SA1306 // Field names should begin with lower-case letter

    private readonly IThemeService _themeService;
    private readonly ISettingsService _settingsService;
    private readonly IMonitorService _monitorService;
    private readonly DockWindowViewModel _windowViewModel;
    private readonly HiddenOwnerWindowBehavior _hiddenOwnerWindowBehavior = new();

    private HWND _hwnd = HWND.Null;
    private APPBARDATA _appBarData;
    private uint _callbackMessageId;
    private bool _isWindowTopmost;
    private bool _isFullScreenAppOpen;

    private DockSettings _settings;
    private DockViewModel viewModel;
    private DockControl _dock;
    private DesktopAcrylicController? _acrylicController;
    private SystemBackdropConfiguration? _configurationSource;
    private bool _isUpdatingBackdrop;
    private BackdropParameters? _lastAppliedAcrylicBackdrop;
    private DockSize _lastSize;
    private bool _isDisposed;
    private DockAppBarMode _appBarMode;
    private bool _autoHideRegistrationSucceeded;
    private bool _isDockRevealed = true;
    private bool _trackingMouseLeave;
    private RECT _revealedRect;
    private RECT _collapsedRect;
    private DispatcherQueueTimer? _collapseTimer;
    private DispatcherQueueTimer? _revealPollTimer;
    private DispatcherQueueTimer? _slideTimer;
    private RECT _slideFromRect;
    private RECT _slideToRect;
    private double _slideProgress;
    private bool _slideIsRevealing;
    private bool _paletteOpenedFromDock;
    private const int AutoHideCollapsedThicknessDips = 0;
    private const int RevealHitTestMarginPixels = 1;
    private static readonly TimeSpan AutoHideCollapseDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan RevealPollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan SlideRevealDuration = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan SlideCollapseDuration = TimeSpan.FromMilliseconds(150);
    private static readonly TimeSpan SlideFrameInterval = TimeSpan.FromMilliseconds(16);

    /// <summary>
    /// The monitor this dock window is displayed on. Null means primary monitor (legacy behavior).
    /// </summary>
    private MonitorInfo? _targetMonitor;

    /// <summary>
    /// Per-monitor dock side override. Null means use the global setting.
    /// </summary>
    private DockSide? _sideOverride;

    /// <summary>
    /// Gets the effective dock side for this window, respecting per-monitor overrides.
    /// </summary>
    private DockSide EffectiveSide => _sideOverride ?? _settings.Side;

    // Store the original WndProc
    private WNDPROC? _originalWndProc;
    private WNDPROC? _customWndProc;

    // internal Settings CurrentSettings => _settings;
    public DockWindow()
        : this(App.Current.Services.GetService<DockViewModel>()!)
    {
    }

    public DockWindow(DockViewModel dockViewModel)
        : this(dockViewModel, null, null)
    {
    }

    public DockWindow(DockViewModel dockViewModel, MonitorInfo? targetMonitor, DockSide? sideOverride)
    {
        _targetMonitor = targetMonitor;
        _sideOverride = sideOverride;

        var serviceProvider = App.Current.Services;
        var mainSettings = serviceProvider.GetRequiredService<ISettingsService>().Settings;
        _settingsService = serviceProvider.GetRequiredService<ISettingsService>();
        _settingsService.SettingsChanged += SettingsChangedHandler;
        _monitorService = serviceProvider.GetRequiredService<IMonitorService>();
        _settings = mainSettings.DockSettings;
        _lastSize = EffectiveDockSize(_settings, EffectiveSide);

        viewModel = dockViewModel;
        _themeService = serviceProvider.GetRequiredService<IThemeService>();
        _themeService.ThemeChanged += ThemeService_ThemeChanged;
        InitializeBackdropSupport();
        _windowViewModel = new DockWindowViewModel(_themeService);
        _dock = new DockControl(viewModel);

        InitializeComponent();
        Root.Children.Add(_dock);
        ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
        _hiddenOwnerWindowBehavior.ShowInTaskbar(this, false);
        if (AppWindow.Presenter is OverlappedPresenter overlappedPresenter)
        {
            overlappedPresenter.SetBorderAndTitleBar(false, false);
            overlappedPresenter.IsResizable = false;
        }

        _hwnd = GetWindowHandle(this);
        _dock.OwnerHwnd = (nint)_hwnd;

        // immediately when we're created: make sure to remove our window frame
        // and shadow. We don't _always_ get an Activated when we're first
        // created.
        UpdateWindowFrame();
        this.Activated += DockWindow_Activated;

        WeakReferenceMessenger.Default.Register<BringToTopMessage>(this);
        WeakReferenceMessenger.Default.Register<RequestShowPaletteAtMessage>(this);
        WeakReferenceMessenger.Default.Register<QuitMessage>(this);

        // Subclass the window to intercept messages
        //
        // Set up custom window procedure to listen for display changes
        // LOAD BEARING: If you don't stick the pointer to HotKeyPrc into a
        // member (and instead like, use a local), then the pointer we marshal
        // into the WindowLongPtr will be useless after we leave this function,
        // and our **WindProc will explode**.
        _customWndProc = CustomWndProc;

        _callbackMessageId = PInvoke.RegisterWindowMessage($"CmdPal_ABM_{_hwnd}");

        // TaskbarCreated is the message that's broadcast when explorer.exe
        // restarts. We need to know when that happens to be able to bring our
        // app bar back
        // And this apparently happens on lock screens / hibernates, too
        WM_TASKBAR_RESTART = PInvoke.RegisterWindowMessage("TaskbarCreated");

        var hotKeyPrcPointer = Marshal.GetFunctionPointerForDelegate(_customWndProc);
        _originalWndProc = Marshal.GetDelegateForFunctionPointer<WNDPROC>(PInvoke.SetWindowLongPtr(_hwnd, WINDOW_LONG_PTR_INDEX.GWL_WNDPROC, hotKeyPrcPointer));

        // Disable minimize and maximize box
        var style = (WINDOW_STYLE)PInvoke.GetWindowLong(_hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE);
        style &= ~WINDOW_STYLE.WS_MINIMIZEBOX; // Remove WS_MINIMIZEBOX
        style &= ~WINDOW_STYLE.WS_MAXIMIZEBOX; // Remove WS_MAXIMIZEBOX
        _ = PInvoke.SetWindowLong(_hwnd, WINDOW_LONG_PTR_INDEX.GWL_STYLE, (int)style);

        ShowDesktop.AddHook(this);
        var userNotificationFlags = WindowHelper.GetUserNotificationFlags();
        _isFullScreenAppOpen = userNotificationFlags.IsFullscreenState || userNotificationFlags.IsBusy;
        UpdateSettingsOnUiThread();
    }

    private void SettingsChangedHandler(ISettingsService sender, SettingsModel args)
    {
        if (_isDisposed)
        {
            return;
        }

        _settings = args.DockSettings;
        RefreshSideOverride();
        DispatcherQueue.TryEnqueue(UpdateSettingsOnUiThread);
    }

    private void DockWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        UpdateWindowFrame();
        UpdateTopmostState();
    }

    private void UpdateWindowFrame()
    {
        // These are used for removing the very subtle shadow/border that we get from Windows 11
        HwndExtensions.ToggleWindowStyle(_hwnd, false, WindowStyle.TiledWindow);
        unsafe
        {
            BOOL value = false;
            PInvoke.DwmSetWindowAttribute(_hwnd, DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, &value, (uint)sizeof(BOOL));
        }
    }

    private HWND GetWindowHandle(Window window)
    {
        var hwnd = WindowNative.GetWindowHandle(window);
        return new HWND(hwnd);
    }

    private void UpdateSettingsOnUiThread()
    {
        if (_isDisposed)
        {
            return;
        }

        this.viewModel.UpdateSettings(_settings);
        UpdateBackdrop();

        _dock.UpdateSettings(_settings, EffectiveSide);

        var side = DockSettingsToViews.GetAppBarEdge(EffectiveSide);
        var desiredMode = GetDesiredAppBarMode();
        var effectiveSize = EffectiveDockSize(_settings, EffectiveSide);

        if (_appBarData.hWnd != IntPtr.Zero)
        {
            var sameEdge = _appBarData.uEdge == side;
            var sameSize = _lastSize == effectiveSize;
            var sameMode = _appBarMode == desiredMode;

            if (sameEdge && sameSize && sameMode)
            {
                if (_appBarMode == DockAppBarMode.AutoHide)
                {
                    UpdateAutoHideShellState();
                    UpdateWindowPosition();
                }

                UpdateTopmostState();
                return;
            }

            DestroyAppBar(_hwnd);
        }

        CreateAppBar(_hwnd);
        UpdateTopmostState();
    }

    private void InitializeBackdropSupport()
    {
        if (DesktopAcrylicController.IsSupported())
        {
            _configurationSource = new SystemBackdropConfiguration
            {
                IsInputActive = true,
            };
        }
    }

    private void UpdateBackdrop()
    {
        // Prevent re-entrance when backdrop changes trigger theme refresh work.
        if (_isUpdatingBackdrop)
        {
            return;
        }

        _isUpdatingBackdrop = true;

        try
        {
            switch (_settings.Backdrop)
            {
                case DockBackdrop.Transparent:
                    if (SystemBackdrop is not TransparentTintBackdrop)
                    {
                        CleanupBackdropControllers();
                        SetupTransparentBackdrop();
                    }

                    break;

                case DockBackdrop.Acrylic:
                default:
                    SetupDesktopAcrylic(_themeService.CurrentDockTheme.BackdropParameters);
                    break;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to update dock backdrop", ex);
        }
        finally
        {
            _isUpdatingBackdrop = false;
        }
    }

    private void SetupTransparentBackdrop()
    {
        if (SystemBackdrop is not TransparentTintBackdrop)
        {
            SystemBackdrop = new TransparentTintBackdrop();
        }

        _lastAppliedAcrylicBackdrop = null;
    }

    private void CleanupBackdropControllers()
    {
        if (_acrylicController is not null)
        {
            _acrylicController.RemoveAllSystemBackdropTargets();
            _acrylicController.Dispose();
            _acrylicController = null;
        }

        _lastAppliedAcrylicBackdrop = null;
    }

    private void SetupDesktopAcrylic(BackdropParameters backdrop)
    {
        var needsAcrylicUpdate = _acrylicController is null || _lastAppliedAcrylicBackdrop != backdrop;
        if (!needsAcrylicUpdate)
        {
            return;
        }

        CleanupBackdropControllers();

        // Fall back to the transparent backdrop if acrylic is not supported.
        if (_configurationSource is null || !DesktopAcrylicController.IsSupported())
        {
            SetupTransparentBackdrop();
            return;
        }

        // DesktopAcrylicController and SystemBackdrop can't be active simultaneously.
        SystemBackdrop = null;

        _acrylicController = new DesktopAcrylicController
        {
            Kind = DesktopAcrylicKind.Thin,
            TintColor = backdrop.TintColor,
            TintOpacity = backdrop.EffectiveOpacity,
            FallbackColor = backdrop.FallbackColor,
            LuminosityOpacity = backdrop.EffectiveLuminosityOpacity,
        };

        // Enable the system backdrop.
        // Note: Be sure to have "using WinRT;" to support the Window.As<...>() call.
        _acrylicController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
        _acrylicController.SetSystemBackdropConfiguration(_configurationSource);
        _lastAppliedAcrylicBackdrop = backdrop;
    }

    private void DisposeAcrylic()
    {
        CleanupBackdropControllers();
        _configurationSource = null;
    }

    private void ThemeService_ThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            UpdateBackdrop();

            // ActualTheme / RequestedTheme sync,
            // as pilfered from WindowThemeSynchronizer
            // LOAD BEARING: Changing the RequestedTheme to Dark then Light then target forces
            // a refresh of the theme.
            Root.RequestedTheme = ElementTheme.Dark;
            Root.RequestedTheme = ElementTheme.Light;
            Root.RequestedTheme = _themeService.CurrentDockTheme.Theme;
        });
    }

    /// <summary>
    /// Registers this window as a Windows AppBar, reserving work area on the
    /// target edge. In Phase 1 (pinned mode), the dock always reserves space
    /// so maximized windows do not overlap it. Future auto-hide mode will use
    /// <c>ABM_SETAUTOHIDEBAR</c> instead and will NOT reserve work area.
    /// </summary>
    private void CreateAppBar(HWND hwnd)
    {
        _appBarData = new APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<APPBARDATA>(),
            hWnd = hwnd,
            uCallbackMessage = _callbackMessageId,
        };

        _ = PInvoke.SHAppBarMessage(PInvoke.ABM_NEW, ref _appBarData);

        _appBarMode = DockAppBarMode.None;
        _autoHideRegistrationSucceeded = false;

        if (GetDesiredAppBarMode() == DockAppBarMode.AutoHide && TryRegisterAutoHideAppBar())
        {
            _appBarMode = DockAppBarMode.AutoHide;
            _autoHideRegistrationSucceeded = true;
            UpdateAutoHideShellState();
            WeakReferenceMessenger.Default.Send(new ViewModels.Messages.DockAutoHideConflictMessage(false));
        }
        else
        {
            _appBarMode = DockAppBarMode.Pinned;
            if (_settings.AutoHide)
            {
                Logger.LogWarning($"Dock auto-hide registration rejected on edge {EffectiveSide} for monitor {MonitorForLogs()}. Falling back to pinned mode.");
                WeakReferenceMessenger.Default.Send(new ViewModels.Messages.DockAutoHideConflictMessage(true));
            }
        }

        _lastSize = EffectiveDockSize(_settings, EffectiveSide);
        UpdateWindowPosition();

        if (_appBarMode == DockAppBarMode.AutoHide)
        {
            // Briefly show the dock at the new position so users can confirm
            // the move, then schedule a collapse after the standard delay.
            _isDockRevealed = true;
            ApplyAutoHideRect(_revealedRect);
            ScheduleCollapseAutoHideDock();
        }
    }

    private void DestroyAppBar(HWND hwnd)
    {
        StopCollapseTimer();
        StopRevealPollTimer();
        StopSlideAnimation();

        if (_appBarMode == DockAppBarMode.AutoHide && _autoHideRegistrationSucceeded)
        {
            _ = TrySetAutoHideRegistration(register: false);
            _autoHideRegistrationSucceeded = false;
        }

        _ = PInvoke.SHAppBarMessage(PInvoke.ABM_REMOVE, ref _appBarData);
        _appBarData = default;
        _appBarMode = DockAppBarMode.None;
        _trackingMouseLeave = false;
        _isDockRevealed = true;
        _revealedRect = default;
        _collapsedRect = default;
    }

    private void UpdateTopmostState(bool bringToFront = false)
    {
        var shouldStayOnTop = _settings.AlwaysOnTop && !_isFullScreenAppOpen;
        const SET_WINDOW_POS_FLAGS flags = SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;

        if (shouldStayOnTop)
        {
            if (_isWindowTopmost && !bringToFront)
            {
                return;
            }

            PInvoke.SetWindowPos(_hwnd, HWND.HWND_TOPMOST, 0, 0, 0, 0, flags);
            _isWindowTopmost = true;
            return;
        }

        if (bringToFront)
        {
            // Win32 trick: briefly set HWND_TOPMOST then immediately clear it
            // with HWND_NOTOPMOST. This brings the window to the foreground
            // without permanently pinning it as topmost.
            PInvoke.SetWindowPos(_hwnd, HWND.HWND_TOPMOST, 0, 0, 0, 0, flags);
        }

        if (!_isWindowTopmost && !bringToFront)
        {
            return;
        }

        var zOrder = _isFullScreenAppOpen ? HWND.HWND_BOTTOM : HWND.HWND_NOTOPMOST;
        PInvoke.SetWindowPos(_hwnd, zOrder, 0, 0, 0, 0, flags);
        _isWindowTopmost = false;
    }

    private void UpdateWindowPosition()
    {
        Logger.LogDebug($"UpdateWindowPosition mode={_appBarMode} autoHideRequested={_settings.AutoHide} monitor={MonitorForLogs()}");

        var dpi = PInvoke.GetDpiForWindow(_hwnd);
        var scaleFactor = dpi / 96.0;
        var effectiveSize = EffectiveDockSize(_settings, EffectiveSide);

        if (_appBarMode == DockAppBarMode.AutoHide)
        {
            UpdateAutoHideWindowPosition(effectiveSize, scaleFactor);
            return;
        }

        UpdatePinnedWindowPosition(effectiveSize, scaleFactor);
    }

    private void UpdatePinnedWindowPosition(DockSize effectiveSize, double scaleFactor)
    {
        UpdateAppBarDataForEdge(EffectiveSide, effectiveSize, scaleFactor);

        _ = PInvoke.SHAppBarMessage(PInvoke.ABM_QUERYPOS, ref _appBarData);

        switch (EffectiveSide)
        {
            case DockSide.Top:
                _appBarData.rc.bottom = _appBarData.rc.top + (int)(DockSettingsToViews.HeightForSize(effectiveSize) * scaleFactor);
                break;
            case DockSide.Bottom:
                _appBarData.rc.top = _appBarData.rc.bottom - (int)(DockSettingsToViews.HeightForSize(effectiveSize) * scaleFactor);
                break;
            case DockSide.Left:
                _appBarData.rc.right = _appBarData.rc.left + (int)(DockSettingsToViews.WidthForSize(effectiveSize) * scaleFactor);
                break;
            case DockSide.Right:
                _appBarData.rc.left = _appBarData.rc.right - (int)(DockSettingsToViews.WidthForSize(effectiveSize) * scaleFactor);
                break;
        }

        _ = PInvoke.SHAppBarMessage(PInvoke.ABM_SETPOS, ref _appBarData);

        PInvoke.MoveWindow(
            _hwnd,
            _appBarData.rc.left,
            _appBarData.rc.top,
            _appBarData.rc.right - _appBarData.rc.left,
            _appBarData.rc.bottom - _appBarData.rc.top,
            true);

        _isDockRevealed = true;
    }

    /// <summary>
    /// Re-resolves <see cref="_targetMonitor"/> against the current monitor list.
    /// <see cref="MonitorInfo"/> is an immutable record, so the instance captured
    /// at construction time becomes stale whenever the display topography changes.
    /// If the monitor is no longer connected we keep the stale reference; the
    /// <see cref="DockWindowManager"/> will close this window shortly.
    /// </summary>
    private void RefreshTargetMonitor()
    {
        if (_targetMonitor is null)
        {
            return;
        }

        var refreshed = _monitorService.GetMonitorByStableId(_targetMonitor.StableId);
        if (refreshed is not null)
        {
            _targetMonitor = refreshed;
        }
    }

    private void RefreshSideOverride()
    {
        if (_targetMonitor is null)
        {
            _sideOverride = null;
            return;
        }

        _sideOverride = _settings.GetSideForMonitor(_targetMonitor.StableId);
    }

    /// <summary>
    /// Compact mode is only supported for Top/Bottom dock positions.
    /// For Left/Right, always use Default size.
    /// </summary>
    private static DockSize EffectiveDockSize(DockSettings settings, DockSide side)
    {
        var isHorizontal = side == DockSide.Top || side == DockSide.Bottom;
        return isHorizontal ? settings.DockSize : DockSize.Default;
    }

    private DockAppBarMode GetDesiredAppBarMode()
    {
        return _settings.AutoHide ? DockAppBarMode.AutoHide : DockAppBarMode.Pinned;
    }

    private bool TryRegisterAutoHideAppBar()
    {
        return TrySetAutoHideRegistration(register: true);
    }

    private bool TrySetAutoHideRegistration(bool register)
    {
        _appBarData.rc = GetMonitorBoundsRect();
        _appBarData.uEdge = DockSettingsToViews.GetAppBarEdge(EffectiveSide);
        _appBarData.lParam = register ? new LPARAM(1) : new LPARAM(0);

        if (_targetMonitor is null)
        {
            var result = PInvoke.SHAppBarMessage(PInvoke.ABM_SETAUTOHIDEBAR, ref _appBarData);
            return result != 0;
        }

        var exResult = PInvoke.SHAppBarMessage(PInvoke.ABM_SETAUTOHIDEBAREX, ref _appBarData);
        return exResult != 0;
    }

    private void UpdateAutoHideShellState()
    {
        var state = (nint)PInvoke.ABS_AUTOHIDE;
        if (_settings.AlwaysOnTop)
        {
            state |= (nint)PInvoke.ABS_ALWAYSONTOP;
        }

        _appBarData.lParam = new LPARAM(state);
        _ = PInvoke.SHAppBarMessage(PInvoke.ABM_SETSTATE, ref _appBarData);
    }

    private string MonitorForLogs()
    {
        return _targetMonitor?.StableId ?? "primary";
    }

    private RECT GetMonitorBoundsRect()
    {
        if (_targetMonitor is not null)
        {
            return new RECT
            {
                left = _targetMonitor.Bounds.Left,
                top = _targetMonitor.Bounds.Top,
                right = _targetMonitor.Bounds.Right,
                bottom = _targetMonitor.Bounds.Bottom,
            };
        }

        return new RECT
        {
            left = 0,
            top = 0,
            right = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN),
            bottom = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSCREEN),
        };
    }

    private void UpdateAutoHideWindowPosition(DockSize effectiveSize, double scaleFactor)
    {
        UpdateAppBarDataForEdge(EffectiveSide, effectiveSize, scaleFactor);

        _revealedRect = _appBarData.rc;
        _collapsedRect = BuildCollapsedRect(_revealedRect, EffectiveSide, scaleFactor);

        ApplyAutoHideRect(_isDockRevealed ? _revealedRect : _collapsedRect);

        if (_isDockRevealed)
        {
            EnsureMouseLeaveTracking();
        }
    }

    private RECT BuildCollapsedRect(RECT revealedRect, DockSide side, double scaleFactor)
    {
        var collapsedRect = revealedRect;
        var thickness = (int)Math.Round(AutoHideCollapsedThicknessDips * scaleFactor);

        switch (side)
        {
            case DockSide.Top:
                collapsedRect.bottom = collapsedRect.top + thickness;
                break;
            case DockSide.Bottom:
                collapsedRect.top = collapsedRect.bottom - thickness;
                break;
            case DockSide.Left:
                collapsedRect.right = collapsedRect.left + thickness;
                break;
            case DockSide.Right:
                collapsedRect.left = collapsedRect.right - thickness;
                break;
        }

        return collapsedRect;
    }

    private void ApplyAutoHideRect(RECT rect)
    {
        var width = rect.right - rect.left;
        var height = rect.bottom - rect.top;

        if (width <= 0 || height <= 0)
        {
            // Window is fully collapsed — hide it
            PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_HIDE);
            return;
        }

        PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
        PInvoke.MoveWindow(
            _hwnd,
            rect.left,
            rect.top,
            width,
            height,
            true);
    }

    private void RevealAutoHideDock(bool immediate = false)
    {
        if (_appBarMode != DockAppBarMode.AutoHide || _isDockRevealed)
        {
            return;
        }

        StopCollapseTimer();
        StopRevealPollTimer();
        _isDockRevealed = true;

        if (immediate)
        {
            StopSlideAnimation();
            ApplyAutoHideRect(_revealedRect);
            UpdateTopmostState(bringToFront: true);
        }
        else
        {
            StartSlideAnimation(_collapsedRect, _revealedRect, isRevealing: true);
            UpdateTopmostState(bringToFront: true);
        }

        EnsureMouseLeaveTracking();
    }

    private void CollapseAutoHideDock(bool immediate = false)
    {
        if (_appBarMode != DockAppBarMode.AutoHide || !_isDockRevealed)
        {
            return;
        }

        if (!immediate && !CanCollapseAutoHideDock())
        {
            return;
        }

        StopCollapseTimer();
        _isDockRevealed = false;

        if (immediate)
        {
            StopSlideAnimation();
            ApplyAutoHideRect(_collapsedRect);
        }
        else
        {
            StartSlideAnimation(_revealedRect, _collapsedRect, isRevealing: false);
        }

        // Only start the reveal poll timer if no fullscreen app is blocking this monitor
        if (!_isFullScreenAppOpen)
        {
            StartRevealPollTimer();
        }
    }

    private bool CanCollapseAutoHideDock()
    {
        if (_dock.IsEditMode || _dock.HasOpenTransientUi || _dock.IsDragOperationActive)
        {
            return false;
        }

        if (_paletteOpenedFromDock)
        {
            return false;
        }

        if (!PInvoke.GetCursorPos(out var cursor))
        {
            return true;
        }

        // Only block collapse if cursor is over our actual window (not just in the same screen area)
        var cursorPoint = new POINT(cursor.X, cursor.Y);
        if (IsPointInRect(_revealedRect, cursorPoint))
        {
            var windowUnderCursor = PInvoke.WindowFromPoint(new System.Drawing.Point(cursor.X, cursor.Y));
            if (windowUnderCursor == _hwnd)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsPointInRect(RECT rect, POINT point)
    {
        return point.X >= rect.left && point.X < rect.right && point.Y >= rect.top && point.Y < rect.bottom;
    }

    private void ScheduleCollapseAutoHideDock()
    {
        if (_appBarMode != DockAppBarMode.AutoHide || !_isDockRevealed)
        {
            return;
        }

        _collapseTimer ??= CreateCollapseTimer();
        _collapseTimer.Stop();
        _collapseTimer.Start();
    }

    private DispatcherQueueTimer CreateCollapseTimer()
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = AutoHideCollapseDelay;
        timer.IsRepeating = false;
        timer.Tick += (sender, _) =>
        {
            sender.Stop();
            CollapseAutoHideDock();
        };

        return timer;
    }

    private void StopCollapseTimer()
    {
        _collapseTimer?.Stop();
    }

    private void StartRevealPollTimer()
    {
        _revealPollTimer ??= CreateRevealPollTimer();
        _revealPollTimer.Start();
    }

    private void StopRevealPollTimer()
    {
        _revealPollTimer?.Stop();
    }

    private DispatcherQueueTimer CreateRevealPollTimer()
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = RevealPollInterval;
        timer.IsRepeating = true;
        timer.Tick += (_, _) => PollCursorForReveal();
        return timer;
    }

    private void PollCursorForReveal()
    {
        if (_appBarMode != DockAppBarMode.AutoHide || _isDockRevealed)
        {
            StopRevealPollTimer();
            return;
        }

        if (!PInvoke.GetCursorPos(out var cursor))
        {
            return;
        }

        if (IsCursorAtDockEdge(new POINT(cursor.X, cursor.Y)))
        {
            StopRevealPollTimer();
            RevealAutoHideDock(immediate: false);
        }
    }

    private bool IsCursorAtDockEdge(POINT cursor)
    {
        var monRect = GetMonitorBoundsRect();

        // Constrain to the dock's actual span (horizontal or vertical extent of _revealedRect)
        switch (EffectiveSide)
        {
            case DockSide.Top:
                return cursor.Y <= monRect.top + RevealHitTestMarginPixels
                    && cursor.X >= _revealedRect.left && cursor.X < _revealedRect.right;
            case DockSide.Bottom:
                return cursor.Y >= monRect.bottom - RevealHitTestMarginPixels
                    && cursor.X >= _revealedRect.left && cursor.X < _revealedRect.right;
            case DockSide.Left:
                return cursor.X <= monRect.left + RevealHitTestMarginPixels
                    && cursor.Y >= _revealedRect.top && cursor.Y < _revealedRect.bottom;
            case DockSide.Right:
                return cursor.X >= monRect.right - RevealHitTestMarginPixels
                    && cursor.Y >= _revealedRect.top && cursor.Y < _revealedRect.bottom;
            default:
                return false;
        }
    }

    private void StartSlideAnimation(RECT from, RECT to, bool isRevealing)
    {
        StopSlideAnimation();

        _slideFromRect = from;
        _slideToRect = to;
        _slideProgress = 0.0;
        _slideIsRevealing = isRevealing;

        // Ensure window is visible at start of reveal animation
        if (isRevealing)
        {
            PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_SHOWNOACTIVATE);
        }

        _slideTimer ??= CreateSlideTimer();
        _slideTimer.Start();
    }

    private void StopSlideAnimation()
    {
        _slideTimer?.Stop();
    }

    private DispatcherQueueTimer CreateSlideTimer()
    {
        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = SlideFrameInterval;
        timer.IsRepeating = true;
        timer.Tick += (_, _) => OnSlideTimerTick();
        return timer;
    }

    private void OnSlideTimerTick()
    {
        var duration = _slideIsRevealing ? SlideRevealDuration : SlideCollapseDuration;
        var step = SlideFrameInterval.TotalMilliseconds / duration.TotalMilliseconds;
        _slideProgress = Math.Min(1.0, _slideProgress + step);

        var easedProgress = _slideIsRevealing
            ? EaseOutCubic(_slideProgress)
            : EaseInCubic(_slideProgress);

        var currentRect = LerpRect(_slideFromRect, _slideToRect, easedProgress);
        ApplyAutoHideRect(currentRect);

        if (_slideProgress >= 1.0)
        {
            StopSlideAnimation();

            // Ensure final position is exact
            ApplyAutoHideRect(_slideToRect);
        }
    }

    private static double EaseOutCubic(double t) => 1.0 - Math.Pow(1.0 - t, 3);

    private static double EaseInCubic(double t) => t * t * t;

    private static RECT LerpRect(RECT a, RECT b, double t)
    {
        return new RECT
        {
            left = Lerp(a.left, b.left, t),
            top = Lerp(a.top, b.top, t),
            right = Lerp(a.right, b.right, t),
            bottom = Lerp(a.bottom, b.bottom, t),
        };
    }

    private static int Lerp(int a, int b, double t) => (int)Math.Round(a + ((b - a) * t));

    private void EnsureMouseLeaveTracking()
    {
        if (_trackingMouseLeave)
        {
            return;
        }

        var track = new NativeTrackMouseEvent
        {
            cbSize = (uint)Marshal.SizeOf<NativeTrackMouseEvent>(),
            dwFlags = 0x00000002, // TME_LEAVE
            hwndTrack = _hwnd,
            dwHoverTime = 0,
        };

        if (TrackMouseEventInterop(ref track))
        {
            _trackingMouseLeave = true;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
#pragma warning disable SA1307 // Field names should begin with upper-case letter
    private struct NativeTrackMouseEvent
    {
        public uint cbSize;
        public uint dwFlags;
        public HWND hwndTrack;
        public uint dwHoverTime;
    }
#pragma warning restore SA1307 // Field names should begin with upper-case letter

    [DllImport("user32.dll", EntryPoint = "TrackMouseEvent", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TrackMouseEventInterop(ref NativeTrackMouseEvent lpEventTrack);

    private void HandleMouseMoveForAutoHide()
    {
        if (_appBarMode != DockAppBarMode.AutoHide)
        {
            return;
        }

        // The poll timer handles reveal when the dock is collapsed/hidden.
        // WM_MOUSEMOVE only arrives when the dock is visible (revealed),
        // so we just reset the collapse timer to keep it open while active.
        if (!_isDockRevealed)
        {
            return;
        }

        StopCollapseTimer();
        EnsureMouseLeaveTracking();
    }

    private void HandleMouseLeaveForAutoHide()
    {
        _trackingMouseLeave = false;

        if (_appBarMode != DockAppBarMode.AutoHide)
        {
            return;
        }

        ScheduleCollapseAutoHideDock();
    }

    private void HandleDeactivationForAutoHide()
    {
        if (_appBarMode != DockAppBarMode.AutoHide)
        {
            return;
        }

        // When the dock loses activation, the palette (if opened from dock) is closing
        _paletteOpenedFromDock = false;
        ScheduleCollapseAutoHideDock();
    }

    private void UpdateAppBarDataForEdge(DockSide side, DockSize size, double scaleFactor)
    {
        Logger.LogDebug("UpdateAppBarDataForEdge");
        var horizontalHeightDips = DockSettingsToViews.HeightForSize(size);
        var verticalWidthDips = DockSettingsToViews.WidthForSize(size);

        // Use monitor-specific bounds when available; fall back to primary screen metrics
        int monLeft, monTop, monRight, monBottom;
        if (_targetMonitor is not null)
        {
            monLeft = _targetMonitor.Bounds.Left;
            monTop = _targetMonitor.Bounds.Top;
            monRight = _targetMonitor.Bounds.Right;
            monBottom = _targetMonitor.Bounds.Bottom;
        }
        else
        {
            monLeft = 0;
            monTop = 0;
            monRight = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN);
            monBottom = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSCREEN);
        }

        if (side == DockSide.Top)
        {
            _appBarData.uEdge = PInvoke.ABE_TOP;
            _appBarData.rc.left = monLeft;
            _appBarData.rc.top = monTop;
            _appBarData.rc.right = monRight;
            _appBarData.rc.bottom = monTop + (int)(horizontalHeightDips * scaleFactor);
        }
        else if (side == DockSide.Bottom)
        {
            var heightPixels = (int)(horizontalHeightDips * scaleFactor);

            _appBarData.uEdge = PInvoke.ABE_BOTTOM;
            _appBarData.rc.left = monLeft;
            _appBarData.rc.top = monBottom - heightPixels;
            _appBarData.rc.right = monRight;
            _appBarData.rc.bottom = monBottom;
        }
        else if (side == DockSide.Left)
        {
            var widthPixels = (int)(verticalWidthDips * scaleFactor);

            _appBarData.uEdge = PInvoke.ABE_LEFT;
            _appBarData.rc.left = monLeft;
            _appBarData.rc.top = monTop;
            _appBarData.rc.right = monLeft + widthPixels;
            _appBarData.rc.bottom = monBottom;
        }
        else if (side == DockSide.Right)
        {
            var widthPixels = (int)(verticalWidthDips * scaleFactor);

            _appBarData.uEdge = PInvoke.ABE_RIGHT;
            _appBarData.rc.left = monRight - widthPixels;
            _appBarData.rc.top = monTop;
            _appBarData.rc.right = monRight;
            _appBarData.rc.bottom = monBottom;
        }
        else
        {
            return;
        }
    }

    private LRESULT CustomWndProc(HWND hwnd, uint msg, WPARAM wParam, LPARAM lParam)
    {
        // check settings changed
        if (msg == PInvoke.WM_SETTINGCHANGE)
        {
            if (wParam == (uint)SYSTEM_PARAMETERS_INFO_ACTION.SPI_SETWORKAREA)
            {
                Logger.LogDebug($"WM_SETTINGCHANGE(SPI_SETWORKAREA)");

                // Use debounced call to throttle rapid successive calls
                DispatcherQueue.TryEnqueue(() => UpdateWindowPosition());
            }
        }
        else if (msg == PInvoke.WM_DISPLAYCHANGE)
        {
            Logger.LogDebug("WM_DISPLAYCHANGE");

            // Invalidate the monitor cache so DockWindowManager can reconcile
            _monitorService.NotifyMonitorsChanged();

            // Use dispatcher to ensure we're on the UI thread.
            // Refresh _targetMonitor before re-positioning: the MonitorInfo
            // captured at construction is an immutable record, so its Bounds
            // are stale after a topology change (e.g. an external display was
            // disconnected, shifting our monitor's virtual-screen origin).
            // Without this, UpdateAppBarDataForEdge would compute the AppBar
            // rect against the old coordinates and produce a wildly incorrect
            // size/position.
            DispatcherQueue.TryEnqueue(() =>
            {
                if (_isDisposed)
                {
                    return;
                }

                RefreshTargetMonitor();

                if (_appBarData.hWnd != IntPtr.Zero)
                {
                    DestroyAppBar(_hwnd);
                    CreateAppBar(_hwnd);
                }
                else
                {
                    UpdateWindowPosition();
                }
            });
        }
        else if (msg == PInvoke.WM_MOUSEMOVE)
        {
            HandleMouseMoveForAutoHide();
        }
        else if (msg == PInvoke.WM_MOUSELEAVE)
        {
            HandleMouseLeaveForAutoHide();
        }
        else if (msg == PInvoke.WM_ACTIVATEAPP && wParam.Value == 0)
        {
            HandleDeactivationForAutoHide();
        }
        else if (msg == PInvoke.WM_ACTIVATE && (wParam.Value & 0xFFFF) == PInvoke.WA_INACTIVE)
        {
            HandleDeactivationForAutoHide();
        }

        // Intercept WM_SYSCOMMAND to prevent minimize and maximize
        else if (msg == PInvoke.WM_SYSCOMMAND)
        {
            var command = (int)(wParam.Value & 0xFFF0);
            if (command == PInvoke.SC_MINIMIZE || command == PInvoke.SC_MAXIMIZE)
            {
                // Block minimize and maximize commands
                return new LRESULT(0);
            }
        }

        // Stop min/max on WM_WINDOWPOSCHANGING too
        else if (msg == PInvoke.WM_WINDOWPOSCHANGING)
        {
            unsafe
            {
                var pWindowPos = (WINDOWPOS*)lParam.Value;

                // Check if the window is being hidden (minimized) or if flags suggest minimize/maximize.
                // Allow hiding when auto-hide is collapsing the dock intentionally.
                if ((pWindowPos->flags & SET_WINDOW_POS_FLAGS.SWP_HIDEWINDOW) != 0
                    && !(_appBarMode == DockAppBarMode.AutoHide && !_isDockRevealed))
                {
                    // Prevent hiding the window (minimize)
                    pWindowPos->flags &= ~SET_WINDOW_POS_FLAGS.SWP_HIDEWINDOW;
                    pWindowPos->flags |= SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW;
                }

                // Additional check: if the window position suggests it's being minimized or maximized
                // by checking for dramatic size changes
                if (pWindowPos->cx <= 0 || pWindowPos->cy <= 0)
                {
                    // Prevent zero or negative size changes (minimize)
                    pWindowPos->flags |= SET_WINDOW_POS_FLAGS.SWP_NOSIZE;
                }
            }
        }

        // Handle WM_SIZE to prevent minimize/maximize state changes
        else if (msg == PInvoke.WM_SIZE)
        {
            var sizeType = (int)wParam.Value;
            if (sizeType == PInvoke.SIZE_MINIMIZED || sizeType == PInvoke.SIZE_MAXIMIZED)
            {
                // Block the size change by not calling the original window procedure
                return new LRESULT(0);
            }
        }

        // Handle WM_SHOWWINDOW to prevent hiding (minimize)
        else if (msg == PInvoke.WM_SHOWWINDOW)
        {
            var isBeingShown = wParam.Value != 0;
            if (!isBeingShown && !(_appBarMode == DockAppBarMode.AutoHide && !_isDockRevealed))
            {
                // Prevent hiding the window (unless auto-hide is intentionally collapsing)
                return new LRESULT(0);
            }
        }

        // Handle double-click on title bar (non-client area)
        else if (msg == PInvoke.WM_NCLBUTTONDBLCLK)
        {
            var hitTest = (int)wParam.Value;
            if (hitTest == PInvoke.HTCAPTION)
            {
                // Block double-click on title bar to prevent maximize
                return new LRESULT(0);
            }
        }

        // Handle WM_GETMINMAXINFO to allow the dock to be smaller than
        // the default minimum window size (SM_CYMINTRACK ~36px).
        else if (msg == PInvoke.WM_GETMINMAXINFO)
        {
            // Call the original WndProc first so it fills default values,
            // then override the minimum tracking size.
            var result = PInvoke.CallWindowProc(_originalWndProc, hwnd, msg, wParam, lParam);
            unsafe
            {
                var minMaxInfo = (MINMAXINFO*)lParam.Value;
                minMaxInfo->ptMinTrackSize.X = 1;
                minMaxInfo->ptMinTrackSize.Y = 1;
            }

            return result;
        }

        // Handle the AppBarMessage message
        // This is needed to update the position when the work area changes.
        // (notably, when the user toggles auto-hide taskbars)
        else if (msg == _callbackMessageId)
        {
            if (wParam.Value == PInvoke.ABN_POSCHANGED)
            {
                UpdateWindowPosition();
            }
            else if (wParam.Value == PInvoke.ABN_FULLSCREENAPP)
            {
                _isFullScreenAppOpen = lParam != 0;
                if (_isFullScreenAppOpen)
                {
                    StopRevealPollTimer();
                    CollapseAutoHideDock(immediate: true);
                }
                else if (_appBarMode == DockAppBarMode.AutoHide && !_isDockRevealed)
                {
                    // Fullscreen app exited — restart edge detection
                    StartRevealPollTimer();
                }

                UpdateTopmostState();
            }
        }
        else if (msg == WM_TASKBAR_RESTART)
        {
            Logger.LogDebug("WM_TASKBAR_RESTART");

            DispatcherQueue.TryEnqueue(() =>
            {
                if (_appBarData.hWnd != IntPtr.Zero)
                {
                    DestroyAppBar(_hwnd);
                }

                CreateAppBar(_hwnd);
            });

            WeakReferenceMessenger.Default.Send<BringToTopMessage>(new(false));
        }

        // Call the original window procedure for all other messages
        return PInvoke.CallWindowProc(_originalWndProc, hwnd, msg, wParam, lParam);
    }

    void IRecipient<BringToTopMessage>.Receive(BringToTopMessage message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (message.BringToFront)
            {
                RevealAutoHideDock(immediate: true);
            }

            UpdateTopmostState(message.BringToFront);
        });
    }

    public void Receive(QuitMessage message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            DestroyAppBar(_hwnd);

            this.Close();
        });
    }

    void IRecipient<RequestShowPaletteAtMessage>.Receive(RequestShowPaletteAtMessage message)
    {
        if (_isDisposed || message.OwnerHwnd != (nint)_hwnd)
        {
            return;
        }

        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
        {
            _paletteOpenedFromDock = true;
            RevealAutoHideDock(immediate: true);
            RequestShowPaletteOnUiThread(message.PosDips);
        });
    }

    private void RequestShowPaletteOnUiThread(Point posDips)
    {
        // pos is relative to our root. We need to convert to absolute
        // virtual-screen coords.
        //
        // TransformToVisual(null) yields a point in the XamlRoot's coordinate
        // space (i.e. the window's client area in DIPs), NOT in screen space.
        // To get true screen coordinates we must offset by the window's
        // screen-space origin (GetWindowRect, which is in pixels). Without
        // this offset, X (for Top/Bottom docks) or Y (for Left/Right docks)
        // stays in window-local pixels and the palette ends up on the primary
        // monitor when the dock lives on a secondary monitor.
        var rootPosDips = Root.TransformToVisual(null).TransformPoint(new Point(0, 0));
        var screenPosDips = new Point(rootPosDips.X + posDips.X, rootPosDips.Y + posDips.Y);

        var dpi = PInvoke.GetDpiForWindow(_hwnd);
        var scaleFactor = dpi / 96.0;
        PInvoke.GetWindowRect(_hwnd, out var ourRect);

        var screenPosPixels = new Point(
            ourRect.left + (screenPosDips.X * scaleFactor),
            ourRect.top + (screenPosDips.Y * scaleFactor));

        // Use monitor-specific bounds when available
        // Note: we compute the quadrant in monitor-local coordinates, but
        // keep screenPosPixels in absolute virtual-screen coordinates. Mixing
        // the two below (when only one axis is overridden from ourRect, which
        // is in virtual-screen coords) produced an off-screen final position
        // on secondary monitors.
        int screenWidth, screenHeight;
        double localX, localY;
        if (_targetMonitor is not null)
        {
            screenWidth = _targetMonitor.Bounds.Width;
            screenHeight = _targetMonitor.Bounds.Height;
            localX = screenPosPixels.X - _targetMonitor.Bounds.Left;
            localY = screenPosPixels.Y - _targetMonitor.Bounds.Top;
        }
        else
        {
            screenWidth = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN);
            screenHeight = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSCREEN);
            localX = screenPosPixels.X;
            localY = screenPosPixels.Y;
        }

        // Now we're going to find the best position for the palette.

        // We want to anchor the palette on the dock side.
        // on the top:
        //   - anchor to the top, left if we're on the left half of the screen
        //   - anchor to the top, right if we're on the right half of the screen
        // On the left:
        //   - anchor to the top, left if we're on the top half of the screen
        //   - anchor to the bottom, left if we're on the bottom half of the screen
        // On the right:
        //   - anchor to the top, right if we're on the top half of the screen
        //   - anchor to the bottom, right if we're on the bottom half of the screen
        // On the bottom:
        //   - anchor to the bottom, left if we're on the left half of the screen
        //   - anchor to the bottom, right if we're on the right half of the screen
        var onTopHalf = localY < screenHeight / 2;
        var onLeftHalf = localX < screenWidth / 2;
        var onRightHalf = !onLeftHalf;
        var onBottomHalf = !onTopHalf;

        var anchorPoint = EffectiveSide switch
        {
            DockSide.Top => onLeftHalf ? AnchorPoint.TopLeft : AnchorPoint.TopRight,
            DockSide.Bottom => onLeftHalf ? AnchorPoint.BottomLeft : AnchorPoint.BottomRight,
            DockSide.Left => onTopHalf ? AnchorPoint.TopLeft : AnchorPoint.BottomLeft,
            DockSide.Right => onTopHalf ? AnchorPoint.TopRight : AnchorPoint.BottomRight,
            _ => AnchorPoint.TopLeft,
        };

        // we also need to slide the anchor point a bit away from the dock
        var paddingDips = 8;
        var paddingPixels = paddingDips * scaleFactor;

        // Depending on the side we're on, we need to offset differently
        switch (EffectiveSide)
        {
            case DockSide.Top:
                screenPosPixels.Y = ourRect.bottom + paddingPixels;
                break;
            case DockSide.Bottom:
                screenPosPixels.Y = ourRect.top - paddingPixels;
                break;
            case DockSide.Left:
                screenPosPixels.X = ourRect.right + paddingPixels;
                break;
            case DockSide.Right:
                screenPosPixels.X = ourRect.left - paddingPixels;
                break;
        }

        // Now that we know the anchor corner, and where to attempt to place it, we can
        // ask the palette to show itself there.
        WeakReferenceMessenger.Default.Send<ShowPaletteAtMessage>(new(screenPosPixels, anchorPoint));
    }

    public DockWindowViewModel WindowViewModel => _windowViewModel;

    public string? MonitorDeviceId => viewModel.MonitorDeviceId;

    public void Dispose()
    {
        Cleanup();
        GC.SuppressFinalize(this);
    }

    private void DockWindow_Closed(object sender, WindowEventArgs args)
    {
        Dispose();
    }

    private void Cleanup()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        _settingsService?.SettingsChanged -= SettingsChangedHandler;

        Activated -= DockWindow_Activated;
        _themeService.ThemeChanged -= ThemeService_ThemeChanged;
        WeakReferenceMessenger.Default.UnregisterAll(this);

        StopCollapseTimer();
        DisposeAcrylic();
        _windowViewModel.Dispose();

        // Remove our app bar registration
        if (_appBarData.hWnd != IntPtr.Zero)
        {
            DestroyAppBar(_hwnd);
        }

        // Unhook the window procedure
        ShowDesktop.RemoveHook();
    }
}

// Thank you to https://stackoverflow.com/a/35422795/1481137
internal static class ShowDesktop
{
    private const string WORKERW = "WorkerW";
    private const string PROGMAN = "Progman";

    private static WINEVENTPROC? _hookProc;
    private static IntPtr _hookHandle = IntPtr.Zero;

    public static void AddHook(Window window)
    {
        if (IsHooked)
        {
            return;
        }

        IsHooked = true;

        _hookProc = (WINEVENTPROC)WinEventCallback;
        _hookHandle = PInvoke.SetWinEventHook(PInvoke.EVENT_SYSTEM_FOREGROUND, PInvoke.EVENT_SYSTEM_FOREGROUND, HMODULE.Null, _hookProc, 0, 0, PInvoke.WINEVENT_OUTOFCONTEXT);
    }

    public static void RemoveHook()
    {
        if (!IsHooked)
        {
            return;
        }

        IsHooked = false;

        PInvoke.UnhookWinEvent((HWINEVENTHOOK)_hookHandle);
        _hookProc = null;
        _hookHandle = IntPtr.Zero;
    }

    private static string GetWindowClass(HWND hwnd)
    {
        unsafe
        {
            fixed (char* c = new char[32])
            {
                _ = PInvoke.GetClassName(hwnd, (PWSTR)c, 32);
                return new string(c);
            }
        }
    }

    internal delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

    private static void WinEventCallback(
        HWINEVENTHOOK hWinEventHook,
        uint eventType,
        HWND hwnd,
        int idObject,
        int idChild,
        uint dwEventThread,
        uint dwmsEventTime)
    {
        if (eventType == PInvoke.EVENT_SYSTEM_FOREGROUND)
        {
            var @class = GetWindowClass(hwnd);
            var bringToFront = string.Equals(@class, WORKERW, StringComparison.Ordinal) || string.Equals(@class, PROGMAN, StringComparison.Ordinal);
            if (bringToFront)
            {
                Logger.LogDebug("ShowDesktop invoked. Bring us back");
            }

            WeakReferenceMessenger.Default.Send<BringToTopMessage>(new(bringToFront));
        }
    }

    public static bool IsHooked { get; private set; }
}

internal sealed record BringToTopMessage(bool BringToFront);

internal sealed record RequestShowPaletteAtMessage(Point PosDips, IntPtr OwnerHwnd);

internal sealed record ShowPaletteAtMessage(Point PosPixels, AnchorPoint Anchor);

internal enum AnchorPoint
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

#pragma warning restore SA1402 // File may only contain a single type
