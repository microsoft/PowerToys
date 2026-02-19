// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CmdPal.UI.ViewModels.Messages;
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

namespace Microsoft.CmdPal.UI.Dock;

#pragma warning disable SA1402 // File may only contain a single type

public sealed partial class DockWindow : WindowEx,
                                                 IRecipient<BringToTopMessage>,
                                                 IRecipient<RequestShowPaletteAtMessage>,
    IRecipient<QuitMessage>,
    IDisposable
{
#pragma warning disable SA1306 // Field names should begin with lower-case letter
#pragma warning disable SA1310 // Field names should not contain underscore
    private readonly uint WM_TASKBAR_RESTART;
#pragma warning restore SA1310 // Field names should not contain underscore
#pragma warning restore SA1306 // Field names should begin with lower-case letter

    private readonly IThemeService _themeService;
    private readonly DockWindowViewModel _windowViewModel;

    private HWND _hwnd = HWND.Null;
    private APPBARDATA _appBarData;
    private uint _callbackMessageId;

    private DockSettings _settings;
    private DockViewModel viewModel;
    private DockControl _dock;
    private DesktopAcrylicController? _acrylicController;
    private SystemBackdropConfiguration? _configurationSource;
    private DockSize _lastSize;

    // Store the original WndProc
    private WNDPROC? _originalWndProc;
    private WNDPROC? _customWndProc;

    // internal Settings CurrentSettings => _settings;
    public DockWindow()
    {
        var serviceProvider = App.Current.Services;
        var mainSettings = serviceProvider.GetService<SettingsModel>()!;
        mainSettings.SettingsChanged += SettingsChangedHandler;
        _settings = mainSettings.DockSettings;
        _lastSize = _settings.DockSize;

        viewModel = serviceProvider.GetService<DockViewModel>()!;
        _themeService = serviceProvider.GetRequiredService<IThemeService>();
        _themeService.ThemeChanged += ThemeService_ThemeChanged;
        _windowViewModel = new DockWindowViewModel(_themeService);
        _dock = new DockControl(viewModel);

        InitializeComponent();
        Root.Children.Add(_dock);
        ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
        AppWindow.IsShownInSwitchers = false;
        if (AppWindow.Presenter is OverlappedPresenter overlappedPresenter)
        {
            overlappedPresenter.SetBorderAndTitleBar(false, false);
            overlappedPresenter.IsResizable = false;
        }

        this.Activated += DockWindow_Activated;

        WeakReferenceMessenger.Default.Register<BringToTopMessage>(this);
        WeakReferenceMessenger.Default.Register<RequestShowPaletteAtMessage>(this);
        WeakReferenceMessenger.Default.Register<QuitMessage>(this);

        _hwnd = GetWindowHandle(this);

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
        UpdateSettings();
    }

    private void SettingsChangedHandler(SettingsModel sender, object? args)
    {
        _settings = sender.DockSettings;
        UpdateSettings();
    }

    private void DockWindow_Activated(object sender, WindowActivatedEventArgs args)
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

    private void UpdateSettings()
    {
        this.viewModel.UpdateSettings(_settings);

        SystemBackdrop = DockSettingsToViews.GetSystemBackdrop(_settings.Backdrop);

        // If the backdrop is acrylic, things are more complicated
        if (_settings.Backdrop == DockBackdrop.Acrylic)
        {
            SetAcrylic();
        }

        _dock.UpdateSettings(_settings);
        var side = DockSettingsToViews.GetAppBarEdge(_settings.Side);

        if (_appBarData.hWnd != IntPtr.Zero)
        {
            var sameEdge = _appBarData.uEdge == side;
            var sameSize = _lastSize == _settings.DockSize;
            if (sameEdge && sameSize)
            {
                return;
            }

            DestroyAppBar(_hwnd);
        }

        CreateAppBar(_hwnd);
    }

    // We want to use DesktopAcrylicKind.Thin and custom colors as this is the default material
    // other Shell surfaces are using, this cannot be set in XAML however.
    private void SetAcrylic()
    {
        if (DesktopAcrylicController.IsSupported())
        {
            // Hooking up the policy object.
            _configurationSource = new SystemBackdropConfiguration
            {
                // Initial configuration state.
                IsInputActive = true,
            };
            UpdateAcrylic();
        }
    }

    private void UpdateAcrylic()
    {
        if (_acrylicController != null)
        {
            _acrylicController.RemoveAllSystemBackdropTargets();
            _acrylicController.Dispose();
        }

        var backdrop = _themeService.CurrentDockTheme.BackdropParameters;
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
    }

    private void DisposeAcrylic()
    {
        if (_acrylicController is not null)
        {
            _acrylicController.Dispose();
            _acrylicController = null!;
            _configurationSource = null!;
        }
    }

    private void ThemeService_ThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            // We only need to handle acrylic here.
            // Transparent background is handled directly in XAML by binding to
            // the DockWindowViewModel's ColorizationColor properties.
            if (_settings.Backdrop == DockBackdrop.Acrylic)
            {
                UpdateAcrylic();
            }

            // ActualTheme / RequestedTheme sync,
            // as pilfered from WindowThemeSynchronizer
            // LOAD BEARING: Changing the RequestedTheme to Dark then Light then target forces
            // a refresh of the theme.
            Root.RequestedTheme = ElementTheme.Dark;
            Root.RequestedTheme = ElementTheme.Light;
            Root.RequestedTheme = _themeService.CurrentDockTheme.Theme;
        });
    }

    private void CreateAppBar(HWND hwnd)
    {
        _appBarData = new APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf<APPBARDATA>(),
            hWnd = hwnd,
            uCallbackMessage = _callbackMessageId,
        };

        // Register this window as an app bar
        PInvoke.SHAppBarMessage(PInvoke.ABM_NEW, ref _appBarData);

        // Stash the last size we created the bar at, so we know when to hot-
        // reload it
        _lastSize = _settings.DockSize;

        UpdateWindowPosition();
    }

    private void DestroyAppBar(HWND hwnd)
    {
        PInvoke.SHAppBarMessage(PInvoke.ABM_REMOVE, ref _appBarData);
        _appBarData = default;
    }

    private void UpdateWindowPosition()
    {
        Logger.LogDebug("UpdateWindowPosition");

        var dpi = PInvoke.GetDpiForWindow(_hwnd);

        var screenWidth = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN);

        // Get system border metrics
        var borderWidth = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXBORDER);
        var edgeWidth = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXEDGE);
        var frameWidth = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXFRAME);

        UpdateAppBarDataForEdge(_settings.Side, _settings.DockSize, dpi / 96.0);

        // Query and set position
        PInvoke.SHAppBarMessage(PInvoke.ABM_QUERYPOS, ref _appBarData);
        PInvoke.SHAppBarMessage(PInvoke.ABM_SETPOS, ref _appBarData);

        // TODO: investigate ABS_AUTOHIDE and auto hide bars.
        // I think it's something like this, but I don't totally know
        //   _appBarData.lParam = ABS_ALWAYSONTOP;
        //   _appBarData.lParam = (LPARAM)(int)PInvoke.ABS_AUTOHIDE;
        //   PInvoke.SHAppBarMessage(ABM_SETSTATE, ref _appBarData);
        //   PInvoke.SHAppBarMessage(PInvoke.ABM_SETAUTOHIDEBAR, ref _appBarData);

        // Account for system borders when moving the window
        // Adjust position to account for window frame/border
        var adjustedLeft = _appBarData.rc.left - frameWidth;
        var adjustedTop = _appBarData.rc.top - frameWidth;
        var adjustedWidth = (_appBarData.rc.right - _appBarData.rc.left) + (2 * frameWidth);
        var adjustedHeight = (_appBarData.rc.bottom - _appBarData.rc.top) + (2 * frameWidth);

        // Move the actual window
        PInvoke.MoveWindow(
            _hwnd,
            adjustedLeft,
            adjustedTop,
            adjustedWidth,
            adjustedHeight,
            true);
    }

    private void UpdateAppBarDataForEdge(DockSide side, DockSize size, double scaleFactor)
    {
        Logger.LogDebug("UpdateAppBarDataForEdge");
        var horizontalHeightDips = DockSettingsToViews.HeightForSize(size);
        var verticalWidthDips = DockSettingsToViews.WidthForSize(size);
        var screenHeight = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSCREEN);
        var screenWidth = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN);

        if (side == DockSide.Top)
        {
            _appBarData.uEdge = PInvoke.ABE_TOP;
            _appBarData.rc.left = 0;
            _appBarData.rc.top = 0;
            _appBarData.rc.right = screenWidth;
            _appBarData.rc.bottom = (int)(horizontalHeightDips * scaleFactor);
        }
        else if (side == DockSide.Bottom)
        {
            var heightPixels = (int)(horizontalHeightDips * scaleFactor);

            _appBarData.uEdge = PInvoke.ABE_BOTTOM;
            _appBarData.rc.left = 0;
            _appBarData.rc.top = screenHeight - heightPixels;
            _appBarData.rc.right = screenWidth;
            _appBarData.rc.bottom = screenHeight;
        }
        else if (side == DockSide.Left)
        {
            var widthPixels = (int)(verticalWidthDips * scaleFactor);

            _appBarData.uEdge = PInvoke.ABE_LEFT;
            _appBarData.rc.left = 0;
            _appBarData.rc.top = 0;
            _appBarData.rc.right = widthPixels;
            _appBarData.rc.bottom = screenHeight;
        }
        else if (side == DockSide.Right)
        {
            var widthPixels = (int)(verticalWidthDips * scaleFactor);

            _appBarData.uEdge = PInvoke.ABE_RIGHT;
            _appBarData.rc.left = screenWidth - widthPixels;
            _appBarData.rc.top = 0;
            _appBarData.rc.right = screenWidth;
            _appBarData.rc.bottom = screenHeight;
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
            var isFullscreen = IsWindowFullscreen();

            Logger.LogDebug($"WM_SETTINGCHANGE ({isFullscreen})");

            if (isFullscreen)
            {
                this.Hide();
            }
            else
            {
                this.Show();
            }

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

            // Use dispatcher to ensure we're on the UI thread
            DispatcherQueue.TryEnqueue(() => UpdateWindowPosition());
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

                // Check if the window is being hidden (minimized) or if flags suggest minimize/maximize
                if ((pWindowPos->flags & SET_WINDOW_POS_FLAGS.SWP_HIDEWINDOW) != 0)
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
            if (!isBeingShown)
            {
                // Prevent hiding the window
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

        // Handle WM_GETMINMAXINFO to control window size limits
        else if (msg == PInvoke.WM_GETMINMAXINFO)
        {
            // We can modify the min/max tracking info here if needed
            // For now, let it pass through but we could restrict max size
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
        }
        else if (msg == WM_TASKBAR_RESTART)
        {
            Logger.LogDebug("WM_TASKBAR_RESTART");

            DispatcherQueue.TryEnqueue(() => CreateAppBar(_hwnd));

            WeakReferenceMessenger.Default.Send<BringToTopMessage>(new(false));
        }

        // Call the original window procedure for all other messages
        return PInvoke.CallWindowProc(_originalWndProc, hwnd, msg, wParam, lParam);
    }

    void IRecipient<BringToTopMessage>.Receive(BringToTopMessage message)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            var onTop = message.OnTop ? HWND.HWND_TOPMOST : HWND.HWND_NOTOPMOST;
            PInvoke.SetWindowPos(_hwnd, onTop, 0, 0, 0, 0, SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE);
            PInvoke.SetWindowPos(_hwnd, HWND.HWND_NOTOPMOST, 0, 0, 0, 0, SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE);
        });
    }

    public static bool IsWindowFullscreen()
    {
        // https://learn.microsoft.com/en-us/windows/win32/api/shellapi/ne-shellapi-query_user_notification_state
        if (Marshal.GetExceptionForHR(PInvoke.SHQueryUserNotificationState(out var state)) is null)
        {
            if (state == QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN ||
                state == QUERY_USER_NOTIFICATION_STATE.QUNS_BUSY ||
                state == QUERY_USER_NOTIFICATION_STATE.QUNS_PRESENTATION_MODE)
            {
                return true;
            }
        }

        return false;
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
        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => RequestShowPaletteOnUiThread(message.PosDips));
    }

    private void RequestShowPaletteOnUiThread(Point posDips)
    {
        // pos is relative to our root. We need to convert to screen coords.
        var rootPosDips = Root.TransformToVisual(null).TransformPoint(new Point(0, 0));
        var screenPosDips = new Point(rootPosDips.X + posDips.X, rootPosDips.Y + posDips.Y);

        var dpi = PInvoke.GetDpiForWindow(_hwnd);
        var scaleFactor = dpi / 96.0;
        var screenPosPixels = new Point(screenPosDips.X * scaleFactor, screenPosDips.Y * scaleFactor);

        var screenWidth = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN);
        var screenHeight = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSCREEN);

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
        var onTopHalf = screenPosPixels.Y < screenHeight / 2;
        var onLeftHalf = screenPosPixels.X < screenWidth / 2;
        var onRightHalf = !onLeftHalf;
        var onBottomHalf = !onTopHalf;

        var anchorPoint = _settings.Side switch
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
        PInvoke.GetWindowRect(_hwnd, out var ourRect);

        // Depending on the side we're on, we need to offset differently
        switch (_settings.Side)
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

    public void Dispose()
    {
        DisposeAcrylic();
        viewModel.Dispose();
        _windowViewModel.Dispose();
    }

    private void DockWindow_Closed(object sender, WindowEventArgs args)
    {
        var serviceProvider = App.Current.Services;
        var settings = serviceProvider.GetService<SettingsModel>();
        settings?.SettingsChanged -= SettingsChangedHandler;
        _themeService.ThemeChanged -= ThemeService_ThemeChanged;
        DisposeAcrylic();

        // Remove our app bar registration
        DestroyAppBar(_hwnd);

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
            if (string.Equals(@class, WORKERW, StringComparison.Ordinal) || string.Equals(@class, PROGMAN, StringComparison.Ordinal))
            {
                Logger.LogDebug("ShowDesktop invoked. Bring us back");
                WeakReferenceMessenger.Default.Send<BringToTopMessage>(new(true));
            }
        }
    }

    public static bool IsHooked { get; private set; }
}

internal sealed record BringToTopMessage(bool OnTop);

internal sealed record RequestShowPaletteAtMessage(Point PosDips);

internal sealed record ShowPaletteAtMessage(Point PosPixels, AnchorPoint Anchor);

internal enum AnchorPoint
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

#pragma warning restore SA1402 // File may only contain a single type
