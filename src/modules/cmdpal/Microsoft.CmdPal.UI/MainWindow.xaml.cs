// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using CmdPalKeyboardService;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Messages;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.Dock;
using Microsoft.CmdPal.UI.Events;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.Pages;
using Microsoft.CmdPal.UI.Services;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.ViewModels.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using WinUIEx;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI;

public sealed partial class MainWindow : WindowEx,
    IRecipient<DismissMessage>,
    IRecipient<ShowWindowMessage>,
    IRecipient<ShowPaletteAtMessage>,
    IRecipient<HideWindowMessage>,
    IRecipient<QuitMessage>,
    IRecipient<NavigateToPageMessage>,
    IRecipient<NavigationDepthMessage>,
    IRecipient<SearchQueryMessage>,
    IRecipient<ErrorOccurredMessage>,
    IRecipient<DragStartedMessage>,
    IRecipient<DragCompletedMessage>,
    IRecipient<ToggleDevRibbonMessage>,
    IRecipient<GetHwndMessage>,
    IRecipient<ExpandCompactModeMessage>,
    IDisposable,
    IHostWindow
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Stylistically, window messages are WM_")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:Field names should begin with lower-case letter", Justification = "Stylistically, window messages are WM_")]
    private readonly uint WM_TASKBAR_RESTART;
    private readonly HWND _hwnd;
    private readonly DispatcherTimer _autoGoHomeTimer;
    private readonly WNDPROC? _hotkeyWndProc;
    private readonly WNDPROC? _originalWndProc;
    private readonly List<TopLevelHotkey> _hotkeys = [];
    private readonly KeyboardListener _keyboardListener;
    private readonly LocalKeyboardListener _localKeyboardListener;
    private readonly HiddenOwnerWindowBehavior _hiddenOwnerBehavior = new();
    private readonly IThemeService _themeService;
    private readonly WindowThemeSynchronizer _windowThemeSynchronizer;
    private readonly List<long> _breakthroughTimestamps = [];

    private bool _ignoreHotKeyWhenFullScreen = true;
    private bool _ignoreHotKeyWhenBusy;
    private bool _allowBreakthroughShortcut;
    private bool _suppressDpiChange;
    private bool _themeServiceInitialized;

    // Session tracking for telemetry
    private Stopwatch? _sessionStopwatch;
    private int _sessionCommandsExecuted;
    private int _sessionPagesVisited;
    private int _sessionSearchQueriesCount;
    private int _sessionMaxNavigationDepth;
    private int _sessionErrorCount;

    private bool _isUpdatingBackdrop;
    private TimeSpan _autoGoHomeInterval = Timeout.InfiniteTimeSpan;

    // Tracks the chrome mode currently applied to the HWND. Nullable so the first
    // call to ApplyHwndFrameMode always runs, regardless of which mode we land in.
    private bool? _hwndFrameVisible;

    // Thickness (in DIPs) of the resize grip around the visible card's border. Shared
    // by the InputNonClientPointerSource region registration (so WM_NCHITTEST actually
    // fires over the border) and the WM_NCHITTEST handler (so it returns resize codes
    // over the same band). These MUST match or the two disagree about where resizing is.
    private const int ResizeBorderThicknessDip = 8;

    private WindowPosition _currentWindowPosition = new();

    private bool _preventHideWhenDeactivated;
    private bool _isLoadedFromDock;

    private DevRibbon? _devRibbon;

    private MainWindowViewModel ViewModel { get; }

    public bool IsVisibleToUser { get; private set; } = true;

    public MainWindow()
    {
        InitializeComponent();

        ViewModel = App.Current.Services.GetService<MainWindowViewModel>()!;

        _autoGoHomeTimer = new DispatcherTimer();
        _autoGoHomeTimer.Tick += OnAutoGoHomeTimerOnTick;

        _themeService = App.Current.Services.GetRequiredService<IThemeService>();
        _themeService.ThemeChanged += ThemeServiceOnThemeChanged;
        _windowThemeSynchronizer = new WindowThemeSynchronizer(_themeService, this);

        _hwnd = new HWND(WinRT.Interop.WindowNative.GetWindowHandle(this).ToInt32());

        unsafe
        {
            CommandPaletteHost.SetHostHwnd((ulong)_hwnd.Value);
        }

        // The HWND itself is borderless / transparent — the visible card lives inside
        // RootElement (CmdPalMainControl) and draws its own corners, border, shadow, and
        // backdrop via the SystemBackdropElement. The frame can be re-enabled via an
        // internal-only setting (hot-reloaded through HotReloadSettings) to make the
        // HWND bounds visible while debugging.
        var initialSettings = App.Current.Services.GetRequiredService<ISettingsService>().Settings;
        ApplyHwndFrameMode(ShouldShowHwndFrame(initialSettings));

        _hiddenOwnerBehavior.ShowInTaskbar(this, Debugger.IsAttached);

        _keyboardListener = new KeyboardListener();
        _keyboardListener.Start();

        _keyboardListener.SetProcessCommand(new CmdPalKeyboardService.ProcessCommand(HandleSummon));

        WM_TASKBAR_RESTART = PInvoke.RegisterWindowMessage("TaskbarCreated");

        // LOAD BEARING: If you don't stick the pointer to HotKeyPrc into a
        // member (and instead like, use a local), then the pointer we marshal
        // into the WindowLongPtr will be useless after we leave this function,
        // and our **WindProc will explode**.
        _hotkeyWndProc = HotKeyPrc;
        var hotKeyPrcPointer = Marshal.GetFunctionPointerForDelegate(_hotkeyWndProc);
        _originalWndProc = Marshal.GetDelegateForFunctionPointer<WNDPROC>(PInvoke.SetWindowLongPtr(_hwnd, WINDOW_LONG_PTR_INDEX.GWL_WNDPROC, hotKeyPrcPointer));

        this.SetIcon();
        AppWindow.Title = RS_.GetString("AppName");
        RestoreWindowPositionFromSavedSettings();
        UpdateWindowPositionInMemory();

        WeakReferenceMessenger.Default.Register<DismissMessage>(this);
        WeakReferenceMessenger.Default.Register<QuitMessage>(this);
        WeakReferenceMessenger.Default.Register<ShowWindowMessage>(this);
        WeakReferenceMessenger.Default.Register<ShowPaletteAtMessage>(this);
        WeakReferenceMessenger.Default.Register<HideWindowMessage>(this);
        WeakReferenceMessenger.Default.Register<NavigateToPageMessage>(this);
        WeakReferenceMessenger.Default.Register<NavigationDepthMessage>(this);
        WeakReferenceMessenger.Default.Register<SearchQueryMessage>(this);
        WeakReferenceMessenger.Default.Register<ErrorOccurredMessage>(this);
        WeakReferenceMessenger.Default.Register<DragStartedMessage>(this);
        WeakReferenceMessenger.Default.Register<DragCompletedMessage>(this);
        WeakReferenceMessenger.Default.Register<ToggleDevRibbonMessage>(this);
        WeakReferenceMessenger.Default.Register<GetHwndMessage>(this);
        WeakReferenceMessenger.Default.Register<ExpandCompactModeMessage>(this);

        // Hide our titlebar.
        // We need to both ExtendsContentIntoTitleBar, then set the height to Collapsed
        // to hide the old caption buttons. Then, in UpdateRegionsForCustomTitleBar,
        // we'll make the top drag-able again. (after our content loads)
        ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
        SizeChanged += WindowSizeChanged;
        RootElement.Loaded += RootElementLoaded;

        // Load our settings, and then also wire up a settings changed handler
        HotReloadSettings();
        App.Current.Services.GetRequiredService<ISettingsService>().SettingsChanged += SettingsChangedHandler;

        // Make sure that we update the acrylic theme when the OS theme changes
        RootElement.ActualThemeChanged += (s, e) => DispatcherQueue.TryEnqueue(UpdateBackdrop);

        // Hardcoding event name to avoid bringing in the PowerToys.interop dependency. Event name must match CMDPAL_SHOW_EVENT from shared_constants.h
        NativeEventWaiter.WaitForEventLoop("Local\\PowerToysCmdPal-ShowEvent-62336fcd-8611-4023-9b30-091a6af4cc5a", () =>
        {
            Summon(string.Empty);
        });

        _localKeyboardListener = new LocalKeyboardListener();
        _localKeyboardListener.KeyPressed += LocalKeyboardListener_OnKeyPressed;
        _localKeyboardListener.Start();

        // Force window to be created, and then cloaked. This will offset initial animation when the window is shown.
        HideWindow();
    }

    private void OnAutoGoHomeTimerOnTick(object? s, object e)
    {
        _autoGoHomeTimer.Stop();

        // BEAR LOADING: Focus Search must be suppressed here; otherwise it may steal focus (for example, from the system tray icon)
        // and prevent the user from opening its context menu.
        WeakReferenceMessenger.Default.Send(new GoHomeMessage(WithAnimation: false, FocusSearch: false));
    }

    private void ThemeServiceOnThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        UpdateBackdrop();
    }

    private static void LocalKeyboardListener_OnKeyPressed(object? sender, LocalKeyboardListenerKeyPressedEventArgs e)
    {
        if (e.Key == VirtualKey.GoBack)
        {
            WeakReferenceMessenger.Default.Send(new GoBackMessage());
        }
    }

    private void SettingsChangedHandler(ISettingsService sender, SettingsModel args)
    {
        DispatcherQueue.TryEnqueue(HotReloadSettings);
    }

    private void RootElementLoaded(object sender, RoutedEventArgs e)
    {
        // Now that our content has loaded, we can update our draggable regions
        UpdateRegionsForCustomTitleBar();

        // Also update regions when DPI changes. SizeChanged only fires when the logical
        // (DIP) size changes — a DPI change that scales the physical size while preserving
        // the DIP size won't trigger it, leaving drag regions at the old physical coordinates.
        RootElement.XamlRoot.Changed += XamlRoot_Changed;

        // The visible card resizes inside the fixed-size HWND (e.g. compact <-> expanded),
        // which does not raise WindowSizeChanged. Recompute the drag regions and the HWND
        // clip region whenever the card's own size changes so they keep tracking it.
        RootElement.CardElement.SizeChanged += CardElement_SizeChanged;

        // Add dev ribbon if enabled. The ribbon lives inside the visible card so it
        // doesn't draw into the transparent shadow area outside the rounded border.
        if (!BuildInfo.IsCiBuild)
        {
            _devRibbon = new DevRibbon { Margin = new Thickness(-1, -1, 120, -1) };
            RootElement.CardContentPanel.Children.Add(_devRibbon);
        }
    }

    private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args) => UpdateRegionsForCustomTitleBar();

    private void CardElement_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateRegionsForCustomTitleBar();

    private void WindowSizeChanged(object sender, WindowSizeChangedEventArgs args) => UpdateRegionsForCustomTitleBar();

    private void PositionCentered()
    {
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        PositionCentered(displayArea);
    }

    private void PositionCentered(DisplayArea displayArea)
    {
        // Use the saved window size when available so that a dock-resized HWND
        // (hidden but not destroyed) doesn't dictate the size on normal reopen.
        SizeInt32 windowSize;
        int windowDpi;

        if (_currentWindowPosition.IsSizeValid)
        {
            windowSize = new SizeInt32(_currentWindowPosition.Width, _currentWindowPosition.Height);
            windowDpi = _currentWindowPosition.Dpi;
        }
        else
        {
            windowSize = AppWindow.Size;
            windowDpi = (int)this.GetDpiForWindow();
        }

        var rect = WindowPositionHelper.CenterOnDisplay(
           displayArea,
           windowSize,
           windowDpi);

        if (rect is not null)
        {
            var finalRect = rect.Value;

            // In compact mode, center the *visible collapsed card* (the search box) on the
            // display, not the much larger transparent HWND. The card is anchored to the top
            // of the HWND, so we offset the HWND upward by the card's center so that growing
            // the card downward (when results appear) keeps the search box where it was.
            if (TryGetCompactCardCenterOffsetPhysical(windowDpi, out var cardCenterFromHwndTop))
            {
                var settings = App.Current.Services.GetRequiredService<ISettingsService>().Settings;
                var workArea = displayArea.WorkArea;

                // The setting is the relative height measured from the *bottom* of the screen,
                // so a larger percentage places the search box higher up the display.
                var fractionFromTop = GetCompactCenterFractionFromTop(settings);
                var desiredCardCenterY = workArea.Y + (int)Math.Round(workArea.Height * fractionFromTop);
                finalRect.Y = desiredCardCenterY - cardCenterFromHwndTop;

                if (finalRect.Y < workArea.Y)
                {
                    finalRect.Y = workArea.Y;
                }
            }

            MoveAndResizeDpiAware(finalRect);
        }
    }

    /// <summary>
    /// When the palette is in compact mode and is being centered on launch, computes the
    /// distance (in physical pixels) from the top of the HWND to the vertical center of the
    /// collapsed card, so the caller can position the HWND such that the card is centered.
    /// Returns false when the card should not be re-centered (compact mode off, or a summon
    /// behavior that restores the last position).
    /// </summary>
    private bool TryGetCompactCardCenterOffsetPhysical(int windowDpi, out int cardCenterFromHwndTop)
    {
        cardCenterFromHwndTop = 0;

        var settings = App.Current.Services.GetRequiredService<ISettingsService>().Settings;
        if (!settings.CompactMode || !IsCenteringSummon(settings))
        {
            return false;
        }

        // Make sure the card is actually collapsed before we measure it.
        (RootElement.MainContent as ShellPage)?.EnsureCompactLayout();

        var cardHeightDip = RootElement.GetCardHeight();
        if (cardHeightDip <= 0)
        {
            return false;
        }

        var scale = windowDpi / 96.0;
        var cardTopDip = RootElement.ShadowPadding.Top;
        cardCenterFromHwndTop = (int)Math.Round((cardTopDip + (cardHeightDip / 2.0)) * scale);
        return true;
    }

    // Every summon behavior except ToLast centers the window on its target display.
    private static bool IsCenteringSummon(SettingsModel settings) => settings.SummonOn != MonitorBehavior.ToLast;

    // Converts the "center height" setting (a percentage measured up from the bottom of the
    // screen) into the fraction of the work area, measured from the top, at which the
    // collapsed search box should be centered.
    private static double GetCompactCenterFractionFromTop(SettingsModel settings)
    {
        var pct = Math.Clamp(settings.CompactCenterHeightPercentage, 0, 100);
        return 1.0 - (pct / 100.0);
    }

    private void RestoreWindowPosition(WindowPosition? savedPosition)
    {
        if (savedPosition?.IsSizeValid != true)
        {
            // don't try to restore if the saved position is invalid, just recenter
            PositionCentered();
            return;
        }

        var newRect = WindowPositionHelper.AdjustRectForVisibility(
            savedPosition.ToPhysicalWindowRectangle(),
            new SizeInt32(savedPosition.ScreenWidth, savedPosition.ScreenHeight),
            savedPosition.Dpi);

        MoveAndResizeDpiAware(newRect);
    }

    private void RestoreWindowPositionFromSavedSettings()
    {
        var settings = App.Current.Services.GetRequiredService<ISettingsService>().Settings;
        RestoreWindowPosition(settings?.LastWindowPosition);
    }

    private void RestoreWindowPositionFromMemory()
    {
        RestoreWindowPosition(_currentWindowPosition);
    }

    /// <summary>
    /// Moves and resizes the window while suppressing WM_DPICHANGED.
    /// The caller is expected to provide a rect already scaled for the target display's DPI.
    /// Without suppression, the framework would apply its own DPI scaling on top, double-scaling the window.
    /// </summary>
    private void MoveAndResizeDpiAware(RectInt32 rect)
    {
        var originalMinHeight = MinHeight;
        var originalMinWidth = MinWidth;

        _suppressDpiChange = true;

        try
        {
            // WindowEx is uses current DPI to calculate the minimum window size
            MinHeight = 0;
            MinWidth = 0;
            AppWindow.MoveAndResize(rect);
        }
        finally
        {
            MinHeight = originalMinHeight;
            MinWidth = originalMinWidth;
            _suppressDpiChange = false;
        }
    }

    private void UpdateWindowPositionInMemory()
    {
        var placement = new WINDOWPLACEMENT { length = (uint)Marshal.SizeOf<WINDOWPLACEMENT>() };
        if (!PInvoke.GetWindowPlacement(_hwnd, ref placement))
        {
            return;
        }

        var rect = placement.rcNormalPosition;
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest) ?? DisplayArea.Primary;

        // GetWindowPlacement returns rcNormalPosition in workspace coordinates for
        // normal windows, but in screen coordinates for tool windows (WS_EX_TOOLWINDOW).
        // HiddenOwnerWindowBehavior applies WS_EX_TOOLWINDOW to hide from taskbar/Alt+Tab,
        // so we must check the current style before converting coordinates.
        //
        // To be on the safe side, we should consider the possibility that setting
        // WS_EX_TOOLWINDOW failed or isn't applied while debugging.
        var workArea = displayArea.WorkArea;
        var isToolWindow = this.HasExtendedStyle(WINDOW_EX_STYLE.WS_EX_TOOLWINDOW);

        _currentWindowPosition = new WindowPosition
        {
            X = rect.X + (isToolWindow ? 0 : workArea.X),
            Y = rect.Y + (isToolWindow ? 0 : workArea.Y),
            Width = rect.Width,
            Height = rect.Height,
            Dpi = (int)this.GetDpiForWindow(),
            ScreenWidth = workArea.Width,
            ScreenHeight = workArea.Height,
        };
    }

    private void HotReloadSettings()
    {
        var settings = App.Current.Services.GetRequiredService<ISettingsService>().Settings;

        SetupHotkey(settings);
        App.Current.Services.GetService<TrayIconService>()!.SetupTrayIcon(settings.ShowSystemTrayIcon);

        _ignoreHotKeyWhenFullScreen = settings.IgnoreShortcutWhenFullscreen;
        _ignoreHotKeyWhenBusy = settings.IgnoreShortcutWhenBusy;
        _allowBreakthroughShortcut = settings.AllowBreakthroughShortcut;

        _autoGoHomeInterval = settings.AutoGoHomeInterval;
        _autoGoHomeTimer.Interval = _autoGoHomeInterval;

        ApplyHwndFrameMode(ShouldShowHwndFrame(settings));

        // Start collapsed: the card shrinks to just the search box until there is a query.
        HandleExpandCompactOnUiThread(false);
    }

    /// <summary>
    /// Returns true if the user has opted in to seeing the OS-drawn HWND chrome (an internal
    /// debugging setting). Always false in CI / release builds.
    /// </summary>
    private static bool ShouldShowHwndFrame(SettingsModel settings) =>
        !BuildInfo.IsCiBuild && settings.ShowHwndFrame;

    /// <summary>
    /// Configures the HWND for the borderless / transparent main-window mode and (when
    /// the internal debug toggle is enabled) overlays the OS-drawn chrome so the HWND's
    /// real bounds are easy to spot. Hit testing is always handled by
    /// <see cref="HitTestForCardResize"/> — the frame flag is purely visual.
    /// </summary>
    private void ApplyHwndFrameMode(bool showFrame)
    {
        if (_hwndFrameVisible == showFrame)
        {
            return;
        }

        _hwndFrameVisible = showFrame;

        // The HWND itself never paints — the card draws the backdrop. Re-applying this
        // each toggle is safe (it just reassigns SystemBackdrop) and guards against the
        // OS replacing it when chrome changes.
        InitializeBackdropSupport();

        if (AppWindow.Presenter is OverlappedPresenter overlappedPresenter)
        {
            // When the debug flag is off we hide the OS chrome (no title bar, no border).
            // When on we let the OS draw both so the HWND outline is obvious.
            // This must actually be applied (not just relied on via WM_NCCALCSIZE): now
            // that the HWND is clipped to the card region, the OS-drawn title bar / frame
            // is no longer covered by our full-window transparent content, so DWM would
            // otherwise repaint it (most visibly the inactive caption) behind the card
            // when the window loses focus.
            overlappedPresenter.SetBorderAndTitleBar(showFrame, showFrame);

            // IsResizable must stay true so WS_THICKFRAME is present. The OS only honors
            // resize-style WM_NCHITTEST results (HTLEFT, HTRIGHT, HT{TOP,BOTTOM}{,LEFT,RIGHT})
            // when the window has a sizing frame, even though we drive the resize from a
            // custom NCHITTEST handler. Setting it after SetBorderAndTitleBar makes sure a
            // borderless window still keeps its sizing frame.
            overlappedPresenter.IsResizable = true;
        }

        ApplyHwndBorderAttributes(showFrame);

        // Drag regions are computed relative to the visible card; the chrome change can
        // shift its on-screen position, so refresh.
        UpdateRegionsForCustomTitleBar();
    }

    /// <summary>
    /// Applies the DWM corner and border attributes for the current frame mode. This is
    /// split out from <see cref="ApplyHwndFrameMode"/> because the DWM border color does
    /// not reliably "take" when first set during window construction (before the HWND has
    /// been shown on a cold process start) — leaving the faint OS outline visible until
    /// the chrome is toggled. Re-applying it each time the window is shown guarantees the
    /// borderless look on a cold start.
    /// </summary>
    private void ApplyHwndBorderAttributes(bool showFrame)
    {
        unsafe
        {
            // Rounded corners: let the OS pick when the debug frame is on, suppress
            // otherwise so the card's CornerRadius isn't doubled by an OS rounding.
            var corner = (uint)(showFrame
                ? DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DEFAULT
                : DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_DONOTROUND);
            PInvoke.DwmSetWindowAttribute(_hwnd, DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE, &corner, sizeof(uint));

            // DWMWA_BORDER_COLOR: 0xFFFFFFFE = DWMWA_COLOR_NONE (no border drawn);
            // 0xFFFFFFFF = DWMWA_COLOR_DEFAULT (system default). With WS_THICKFRAME still
            // on, DWM otherwise draws a faint 1px outline around the HWND — which the
            // user sees as the "frame still appears around the sides" even when our
            // ShowHwndFrame setting is off. Setting COLOR_NONE removes it.
            const uint DWMWA_COLOR_NONE = 0xFFFFFFFEu;
            const uint DWMWA_COLOR_DEFAULT = 0xFFFFFFFFu;
            var borderColor = showFrame ? DWMWA_COLOR_DEFAULT : DWMWA_COLOR_NONE;
            PInvoke.DwmSetWindowAttribute(_hwnd, DWMWINDOWATTRIBUTE.DWMWA_BORDER_COLOR, &borderColor, sizeof(uint));
        }
    }

    private void InitializeBackdropSupport()
    {
        // The window itself paints nothing (it's transparent). All actual backdrop
        // rendering lives on the SystemBackdropElement inside CmdPalMainControl, so the
        // mica/acrylic only fills the rounded card instead of the whole HWND. The empty
        // tint here keeps the HWND fully transparent.
        SystemBackdrop = new TransparentTintBackdrop { TintColor = Colors.Transparent };
    }

    private void UpdateBackdrop()
    {
        // Prevent re-entrance when backdrop changes trigger ActualThemeChanged
        if (_isUpdatingBackdrop)
        {
            return;
        }

        _isUpdatingBackdrop = true;

        try
        {
            var backdrop = _themeService.Current.BackdropParameters;
            var isImageMode = ViewModel.ShowBackgroundImage;
            var config = BackdropStyles.Get(backdrop.Style);
            var hasColorization = _themeService.Current.HasColorization;

            RootElement.ApplyBackdrop(backdrop, config.ControllerKind, isImageMode, hasColorization);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to update backdrop", ex);
        }
        finally
        {
            _isUpdatingBackdrop = false;
        }
    }

    private void ShowHwnd(IntPtr hwndValue, MonitorBehavior target)
    {
        var positionWindowForTargetMonitor = (HWND hwnd) =>
        {
            if (target == MonitorBehavior.ToLast)
            {
                var originalScreen = new SizeInt32(_currentWindowPosition.ScreenWidth, _currentWindowPosition.ScreenHeight);
                var newRect = WindowPositionHelper.AdjustRectForVisibility(_currentWindowPosition.ToPhysicalWindowRectangle(), originalScreen, _currentWindowPosition.Dpi);
                MoveAndResizeDpiAware(newRect);
            }
            else
            {
                var display = GetScreen(hwnd, target);
                PositionCentered(display);
            }
        };
        ShowHwnd(hwndValue, positionWindowForTargetMonitor);
    }

    private void ShowHwnd(IntPtr hwndValue, Point anchorInPixels, AnchorPoint anchorCorner)
    {
        var positionWindowForAnchor = (HWND hwnd) =>
        {
            PInvoke.GetWindowRect(hwnd, out var bounds);
            var swpFlags = SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER;
            switch (anchorCorner)
            {
                case AnchorPoint.TopLeft:
                    PInvoke.SetWindowPos(
                        hwnd,
                        HWND.HWND_TOP,
                        (int)anchorInPixels.X,
                        (int)anchorInPixels.Y,
                        0,
                        0,
                        swpFlags);
                    break;
                case AnchorPoint.TopRight:
                    PInvoke.SetWindowPos(
                        hwnd,
                        HWND.HWND_TOP,
                        (int)(anchorInPixels.X - bounds.Width),
                        (int)anchorInPixels.Y,
                        0,
                        0,
                        swpFlags);
                    break;
                case AnchorPoint.BottomLeft:
                    PInvoke.SetWindowPos(
                        hwnd,
                        HWND.HWND_TOP,
                        (int)anchorInPixels.X,
                        (int)(anchorInPixels.Y - bounds.Height),
                        0,
                        0,
                        swpFlags);
                    break;
                case AnchorPoint.BottomRight:
                    PInvoke.SetWindowPos(
                        hwnd,
                        HWND.HWND_TOP,
                        (int)(anchorInPixels.X - bounds.Width),
                        (int)(anchorInPixels.Y - bounds.Height),
                        0,
                        0,
                        swpFlags);
                    break;
            }
        };
        ShowHwnd(hwndValue, positionWindowForAnchor);
    }

    private void ShowHwnd(IntPtr hwndValue, Action<HWND>? positionWindow)
    {
        StopAutoGoHome();

        var hwnd = new HWND(hwndValue != 0 ? hwndValue : _hwnd);

        // Remember, IsIconic == "minimized", which is entirely different state
        // from "show/hide"
        // If we're currently minimized, restore us first, before we reveal
        // our window. Otherwise, we'd just be showing a minimized window -
        // which would remain not visible to the user.
        if (PInvoke.IsIconic(hwnd))
        {
            // Make sure our HWND is cloaked before any possible window manipulations
            Cloak();

            PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_RESTORE);
        }

        if (positionWindow is not null)
        {
            positionWindow(hwnd);
        }

        // Check if the debugger is attached. If it is, we don't want to apply the tool window style,
        // because that would make it hard to debug the app
        if (Debugger.IsAttached)
        {
            _hiddenOwnerBehavior.ShowInTaskbar(this, true);
        }

        // Just to be sure, SHOW our hwnd.
        PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_SHOW);

        // Re-apply the borderless DWM attributes now that the window is
        // actually shown. On a cold launch these are first set during
        // construction before the HWND has ever been displayed, and DWM doesn't
        // reliably honor the border color until the window exists on-screen —
        // which left the faint OS outline visible until the chrome was toggled.
        ApplyHwndBorderAttributes(_hwndFrameVisible ?? false);

        // Once we're done, uncloak to avoid all animations
        Uncloak();

        PInvoke.SetForegroundWindow(hwnd);
        PInvoke.SetActiveWindow(hwnd);

        // Push our window to the top of the Z-order and make it the topmost, so
        // that it appears above all other windows. We want to remove the
        // topmost status when we hide the window (because we cloak it instead
        // of hiding it).
        //
        // SWP_FRAMECHANGED is load-bearing for the borderless look on a cold
        // start.  Asking for SWP_FRAMECHANGED here re-sends WM_NCCALCSIZE and
        // forces the NC repaint every time we show, so the frame is gone from
        // the very first summon.
        PInvoke.SetWindowPos(hwnd, HWND.HWND_TOPMOST, 0, 0, 0, 0, SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_FRAMECHANGED);
    }

    private static DisplayArea GetScreen(HWND currentHwnd, MonitorBehavior target)
    {
        // Leaving a note here, in case we ever need it:
        // https://github.com/microsoft/microsoft-ui-xaml/issues/6454
        // If we need to ever FindAll, we'll need to iterate manually
        var displayAreas = Microsoft.UI.Windowing.DisplayArea.FindAll();
        switch (target)
        {
            case MonitorBehavior.InPlace:
                if (PInvoke.GetWindowRect(currentHwnd, out var bounds))
                {
                    RectInt32 converted = new(bounds.X, bounds.Y, bounds.Width, bounds.Height);
                    return DisplayArea.GetFromRect(converted, DisplayAreaFallback.Nearest);
                }

                break;

            case MonitorBehavior.ToFocusedWindow:
                var foregroundWindowHandle = PInvoke.GetForegroundWindow();
                if (foregroundWindowHandle != IntPtr.Zero)
                {
                    if (PInvoke.GetWindowRect(foregroundWindowHandle, out var fgBounds))
                    {
                        RectInt32 converted = new(fgBounds.X, fgBounds.Y, fgBounds.Width, fgBounds.Height);
                        return DisplayArea.GetFromRect(converted, DisplayAreaFallback.Nearest);
                    }
                }

                break;

            case MonitorBehavior.ToPrimary:
                return DisplayArea.Primary;

            case MonitorBehavior.ToMouse:
            default:
                if (PInvoke.GetCursorPos(out var cursorPos))
                {
                    return DisplayArea.GetFromPoint(new PointInt32(cursorPos.X, cursorPos.Y), DisplayAreaFallback.Nearest);
                }

                break;
        }

        return DisplayArea.Primary;
    }

    public void Receive(ShowWindowMessage message)
    {
        _isLoadedFromDock = false;

        var settings = App.Current.Services.GetRequiredService<ISettingsService>().Settings;

        // Start session tracking
        _sessionStopwatch = Stopwatch.StartNew();
        _sessionCommandsExecuted = 0;
        _sessionPagesVisited = 0;

        ShowHwnd(message.Hwnd, settings.SummonOn);
    }

    internal void Receive(ShowPaletteAtMessage message)
    {
        _isLoadedFromDock = true;

        // Reset the size in case users have resized a dock window.
        // Ideally in the future, we'll have defined sizes that opening
        // a dock window will adhere to, but alas, that's the future.
        RestoreWindowPositionFromMemory();

        ShowHwnd(HWND.Null, message.PosPixels, message.Anchor);
    }

    public void Receive(HideWindowMessage message)
    {
        // This might come in off the UI thread. Make sure to hop back.
        DispatcherQueue.TryEnqueue(() =>
        {
            EndSession("Hide");
            HideWindow();
        });
    }

    public void Receive(QuitMessage message) =>

        // This might come in on a background thread
        DispatcherQueue.TryEnqueue(() => Close());

    public void Receive(DismissMessage message)
    {
        if (message.ForceGoHome)
        {
            WeakReferenceMessenger.Default.Send(new GoHomeMessage(false, false));
        }

        // This might come in off the UI thread. Make sure to hop back.
        DispatcherQueue.TryEnqueue(() =>
        {
            EndSession("Dismiss");
            HideWindow();
        });
    }

    // Session telemetry: Track metrics during the Command Palette session
    // These receivers increment counters that are sent when EndSession is called
    public void Receive(NavigateToPageMessage message)
    {
        _sessionPagesVisited++;
    }

    public void Receive(NavigationDepthMessage message)
    {
        if (message.Depth > _sessionMaxNavigationDepth)
        {
            _sessionMaxNavigationDepth = message.Depth;
        }
    }

    public void Receive(SearchQueryMessage message)
    {
        _sessionSearchQueriesCount++;
    }

    public void Receive(ErrorOccurredMessage message)
    {
        _sessionErrorCount++;
    }

    /// <summary>
    /// Ends the current telemetry session and emits the CmdPal_SessionDuration event.
    /// Aggregates all session metrics collected since ShowWindow and sends them to telemetry.
    /// </summary>
    /// <param name="dismissalReason">The reason the session ended (e.g., Dismiss, Hide, LostFocus).</param>
    private void EndSession(string dismissalReason)
    {
        if (_sessionStopwatch is not null)
        {
            _sessionStopwatch.Stop();
            TelemetryForwarder.LogSessionDuration(
                (ulong)_sessionStopwatch.ElapsedMilliseconds,
                _sessionCommandsExecuted,
                _sessionPagesVisited,
                dismissalReason,
                _sessionSearchQueriesCount,
                _sessionMaxNavigationDepth,
                _sessionErrorCount);
            _sessionStopwatch = null;
        }
    }

    /// <summary>
    /// Increments the session commands executed counter for telemetry.
    /// Called by TelemetryForwarder when an extension command is invoked.
    /// </summary>
    internal void IncrementCommandsExecuted()
    {
        _sessionCommandsExecuted++;
    }

    private void HideWindow()
    {
        // Cloak our HWND to avoid all animations.
        var cloaked = Cloak();

        // Then hide our HWND, to make sure that the OS gives the FG / focus back to another app
        // (there's no way for us to guess what the right hwnd might be, only the OS can do it right)
        PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_HIDE);

        if (cloaked)
        {
            // TRICKY: show our HWND again. This will trick XAML into painting our
            // HWND again, so that we avoid the "flicker" caused by a WinUI3 app
            // window being first shown
            // SW_SHOWNA will prevent us for trying to fight the focus back
            PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_SHOWNA);

            // Intentionally leave the window cloaked. So our window is "visible",
            // but also cloaked, so you can't see it.

            // If the window was not cloaked, then leave it hidden.
            // Sure, it's not ideal, but at least it's not visible.
        }

        WeakReferenceMessenger.Default.Send(new WindowHiddenMessage());

        // Start auto-go-home timer
        RestartAutoGoHome();
    }

    private void StopAutoGoHome()
    {
        _autoGoHomeTimer.Stop();
    }

    private void RestartAutoGoHome()
    {
        if (_autoGoHomeInterval == Timeout.InfiniteTimeSpan)
        {
            return;
        }

        _autoGoHomeTimer.Stop();
        _autoGoHomeTimer.Start();
    }

    private bool Cloak()
    {
        bool wasCloaked;
        unsafe
        {
            BOOL value = true;
            var hr = PInvoke.DwmSetWindowAttribute(_hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAK, &value, (uint)sizeof(BOOL));
            if (hr.Failed)
            {
                Logger.LogWarning($"DWM cloaking of the main window failed. HRESULT: {hr.Value}.");
            }
            else
            {
                IsVisibleToUser = false;
            }

            wasCloaked = hr.Succeeded;
        }

        return wasCloaked;
    }

    private void Uncloak()
    {
        unsafe
        {
            BOOL value = false;
            PInvoke.DwmSetWindowAttribute(_hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAK, &value, (uint)sizeof(BOOL));
            IsVisibleToUser = true;
        }
    }

    internal void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        var serviceProvider = App.Current.Services;

        if (!_isLoadedFromDock)
        {
            UpdateWindowPositionInMemory();
        }

        var settingsService = serviceProvider.GetRequiredService<ISettingsService>();
        var settings = settingsService.Settings;
        if (settings is not null)
        {
            // If we were last shown from the dock, _currentWindowPosition still holds
            // the last non-dock placement because dock sessions intentionally skip updates.
            if (_currentWindowPosition.IsSizeValid)
            {
                settingsService.UpdateSettings(s => s with { LastWindowPosition = _currentWindowPosition });
            }
        }

        var extensionServices = serviceProvider.GetServices<IExtensionService>();
        foreach (var extensionService in extensionServices)
        {
            extensionService.SignalStopAsync();
        }

        App.Current.Services.GetService<TrayIconService>()!.Destroy();

        // WinUI bug is causing a crash on shutdown when FailFastOnErrors is set to true (#51773592).
        // Workaround by turning it off before shutdown.
        App.Current.DebugSettings.FailFastOnErrors = false;
        _localKeyboardListener.Dispose();
        DisposeAcrylic();

        _keyboardListener.Stop();
        Environment.Exit(0);
    }

    private void DisposeAcrylic()
    {
        // The backdrop controllers now live on the SystemBackdropElement inside
        // CmdPalMainControl. Clearing its SystemBackdrop fires OnTargetDisconnected on the
        // current backdrop, which removes targets and disposes the underlying controller.
        try
        {
            RootElement?.ClearBackdrop();
        }
        catch
        {
            // Best-effort cleanup; ignore errors during shutdown.
        }
    }

    // Updates our window s.t. the top of the window is draggable.
    private void UpdateRegionsForCustomTitleBar()
    {
        var xamlRoot = RootElement.XamlRoot;
        if (xamlRoot is null)
        {
            return;
        }

        // Specify the interactive regions of the title bar.
        var scaleAdjustment = xamlRoot.RasterizationScale;

        // Drag/passthrough regions are computed against the visible card (the rounded
        // border inside CmdPalMainControl), not the whole HWND. The HWND extends beyond
        // the card to make room for the drop shadow, and we don't want that transparent
        // shadow area to be draggable.
        var card = RootElement.CardElement;
        if (card.ActualWidth <= 0 || card.ActualHeight <= 0)
        {
            return;
        }

        // All coordinates below are in the card's own (DIP) space: (0,0) is the
        // top-left of the visible card, (w,h) is the bottom-right. GetRect transforms
        // them into the physical-pixel client coordinates that
        // InputNonClientPointerSource expects.
        var transform = card.TransformToVisual(null);
        var w = card.ActualWidth;
        var h = card.ActualHeight;

        RectInt32 CardRect(double x, double y, double rw, double rh) =>
            GetRect(transform.TransformBounds(new Rect(x, y, rw, rh)), scaleAdjustment);

        // Reserve some space at the top for dragging the window (caption).
        const double dragHeight = 16;

        // The resize grip straddles each card edge by `grip` DIPs on either side so the
        // affordance reaches a little into the drop-shadow padding too.
        const double grip = ResizeBorderThicknessDip;

        var nonClientInputSrc = InputNonClientPointerSource.GetForWindowId(this.AppWindow.Id);

        // Mark the card's border ring + top drag bar as Caption. Only regions
        // registered here generate WM_NCHITTEST on our wndproc - without the
        // side/bottom strips the XAML island swallows the pointer and we never
        // get a resize. HotKeyPrc's WM_NCHITTEST then picks drag vs. resize
        // per-pixel.
        var caption = new RectInt32[]
        {
            CardRect(0, 0, w, dragHeight),                       // top drag bar
            CardRect(-grip, -grip, 2 * grip, h + (2 * grip)),   // left edge
            CardRect(w - grip, -grip, 2 * grip, h + (2 * grip)), // right edge
            CardRect(-grip, -grip, w + (2 * grip), 2 * grip),   // top edge
            CardRect(-grip, h - grip, w + (2 * grip), 2 * grip), // bottom edge
        };
        nonClientInputSrc.SetRegionRects(NonClientRegionKind.Caption, caption);

        // Everything inside the border ring (and below the drag bar) is
        // interactive content. Marking it Passthrough keeps the search box,
        // list, etc. clickable and explicitly carves it out of the caption
        // regions above.
        var interiorWidth = Math.Max(0, w - (2 * grip));
        var interiorHeight = Math.Max(0, h - dragHeight - grip);
        var passthrough = new RectInt32[]
        {
            CardRect(grip, dragHeight, interiorWidth, interiorHeight),
        };
        nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, passthrough);

        // Clip the HWND to the card + its shadow. Card is inset by
        // ShadowPadding on each side, so card + full padding lands flush with
        // the HWND edge - and a region flush with the edge makes WS_THICKFRAME
        // draw its border there. So inset 1px on every side to hide that.
        // Bottom is clamped to 1px inside the HWND so the border doesn't
        // reappear as the card grows tall.
        var shadowPadding = RootElement.ShadowPadding;
        var cardPhysical = CardRect(0, 0, w, h);

        const int EdgeInsetPx = 1;
        var windowWidthPx = AppWindow.Size.Width;
        var windowHeightPx = AppWindow.Size.Height;

        var clipLeft = EdgeInsetPx;
        var clipTop = EdgeInsetPx;
        var clipRight = windowWidthPx - EdgeInsetPx;

        var bottomShadowPx = (int)Math.Round(shadowPadding.Bottom * scaleAdjustment);
        var clipBottom = Math.Min(
            cardPhysical.Y + cardPhysical.Height + bottomShadowPx,
            windowHeightPx - EdgeInsetPx);

        ApplyCardWindowRegion(new RectInt32(
            clipLeft,
            clipTop,
            Math.Max(0, clipRight - clipLeft),
            Math.Max(0, clipBottom - clipTop)));
    }

    /// <summary>
    /// Restricts the HWND's visible / hit-testable area to the supplied
    /// rectangle (in physical client pixels), which covers the visible card and
    /// its drop-shadow margin. Everything outside — the empty transparent area
    /// of the (larger) HWND — becomes click-through and is excluded from the
    /// window region. When the debug HWND frame is enabled the clip is removed
    /// so the full window stays visible.
    /// </summary>
    private void ApplyCardWindowRegion(RectInt32 regionPhysical)
    {
        nint hwnd;
        unsafe
        {
            hwnd = (nint)_hwnd.Value;
        }

        // Debug frame mode: keep the whole window visible / interactive, no clip.
        if (_hwndFrameVisible == true)
        {
            _ = SetWindowRgn(hwnd, IntPtr.Zero, true);
            return;
        }

        // CreateRectRgn coordinates are relative to the window's top-left. For this
        // borderless popup the client origin coincides with the window origin, so the
        // region's client-space physical rect maps directly into window space.
        var region = CreateRectRgn(
            regionPhysical.X,
            regionPhysical.Y,
            regionPhysical.X + regionPhysical.Width,
            regionPhysical.Y + regionPhysical.Height);
        if (region == IntPtr.Zero)
        {
            return;
        }

        // On success SetWindowRgn takes ownership of the region (the OS frees it), so we
        // only delete it ourselves if the call failed.
        if (SetWindowRgn(hwnd, region, true) == 0)
        {
            _ = DeleteObject(region);
        }
    }

    private static RectInt32 GetRect(Rect bounds, double scale)
    {
        return new RectInt32(
            _X: (int)Math.Round(bounds.X * scale),
            _Y: (int)Math.Round(bounds.Y * scale),
            _Width: (int)Math.Round(bounds.Width * scale),
            _Height: (int)Math.Round(bounds.Height * scale));
    }

    // Raw interop for the window-region clip. Declared here (rather than via CsWin32)
    // because SetWindowRgn transfers ownership of the HRGN to the OS on success, which is
    // awkward to express through CsWin32's SafeHandle-returning region creator.
    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, [MarshalAs(UnmanagedType.Bool)] bool bRedraw);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    internal void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (!_themeServiceInitialized && args.WindowActivationState != WindowActivationState.Deactivated)
        {
            try
            {
                _themeService.Initialize();
                _themeServiceInitialized = true;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to initialize ThemeService", ex);
            }
        }

        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            // Save the current window position before hiding the window
            // but not when opened from dock — preserve the pre-dock size.
            if (!_isLoadedFromDock)
            {
                UpdateWindowPositionInMemory();
            }

            // If there's a debugger attached...
            if (System.Diagnostics.Debugger.IsAttached)
            {
                // ... then don't hide the window when it loses focus.
                return;
            }

            // Are we disabled? If we are, then we don't want to dismiss on focus lost.
            // This can happen if an extension wanted to show a modal dialog on top of our
            // window i.e. in the case of an MSAL auth window.
            if (PInvoke.IsWindowEnabled(_hwnd) == 0)
            {
                return;
            }

            // We're doing something that requires us to lose focus, but we don't want to hide the window
            if (_preventHideWhenDeactivated)
            {
                return;
            }

            // This will DWM cloak our window:
            EndSession("LostFocus");
            HideWindow();

            PowerToysTelemetry.Log.WriteEvent(new CmdPalDismissedOnLostFocus());
        }

        if (RootElement is not null)
        {
            RootElement.SetIsInputActive(args.WindowActivationState != WindowActivationState.Deactivated);
        }
    }

    public void HandleLaunchNonUI(AppActivationArguments? activatedEventArgs)
    {
        // LOAD BEARING
        // Any reading and processing of the activation arguments must be done
        // synchronously in this method, before it returns. The sending instance
        // remains blocked until this returns; afterward it may quit, causing
        // the activation arguments to be lost.
        if (activatedEventArgs is null)
        {
            Summon(string.Empty);
            return;
        }

        try
        {
            if (activatedEventArgs.Kind == ExtendedActivationKind.StartupTask)
            {
                return;
            }

            if (activatedEventArgs.Kind == ExtendedActivationKind.Protocol)
            {
                if (activatedEventArgs.Data is IProtocolActivatedEventArgs protocolArgs)
                {
                    if (protocolArgs.Uri.ToString() is string uri)
                    {
                        // was the URI "x-cmdpal://background" ?
                        if (uri.StartsWith("x-cmdpal://background", StringComparison.OrdinalIgnoreCase))
                        {
                            // we're running, we don't want to activate our window. bail
                            return;
                        }
                        else if (uri.StartsWith("x-cmdpal://settings", StringComparison.OrdinalIgnoreCase))
                        {
                            WeakReferenceMessenger.Default.Send<OpenSettingsMessage>(new());
                            return;
                        }
                        else if (uri.StartsWith("x-cmdpal://reload", StringComparison.OrdinalIgnoreCase))
                        {
                            var settings = App.Current.Services.GetRequiredService<ISettingsService>().Settings;
                            if (settings?.AllowExternalReload == true)
                            {
                                Logger.LogInfo("External Reload triggered");
                                WeakReferenceMessenger.Default.Send<ReloadCommandsMessage>(new());
                            }
                            else
                            {
                                Logger.LogInfo("External Reload is disabled");
                            }

                            return;
                        }
                    }
                }
            }
        }
        catch (COMException ex)
        {
            // https://learn.microsoft.com/en-us/windows/win32/rpc/rpc-return-values
            const int RPC_S_SERVER_UNAVAILABLE = -2147023174;
            const int RPC_S_CALL_FAILED = 2147023170;

            // Accessing properties activatedEventArgs.Kind and activatedEventArgs.Data might cause COMException
            // if the args are not valid or not passed correctly.
            if (ex.HResult is RPC_S_SERVER_UNAVAILABLE or RPC_S_CALL_FAILED)
            {
                Logger.LogWarning(
                    $"COM exception (HRESULT {ex.HResult}) when accessing activation arguments. " +
                    $"This might be due to the calling application not passing them correctly or exiting before we could read them. " +
                    $"The application will continue running and fall back to showing the Command Palette window.");
            }
            else
            {
                Logger.LogError(
                    $"COM exception (HRESULT {ex.HResult}) when activating the application. " +
                    $"The application will continue running and fall back to showing the Command Palette window.",
                    ex);
            }
        }

        Summon(string.Empty);
    }

    public void Summon(string commandId) =>

        // The actual showing and hiding of the window will be done by the
        // ShellPage. This is because we don't want to show the window if the
        // user bound a hotkey to just an invokable command, which we can't
        // know till the message is being handled.
        WeakReferenceMessenger.Default.Send<HotkeySummonMessage>(new(commandId, _hwnd));

    private void UnregisterHotkeys()
    {
        _keyboardListener.ClearHotkeys();

        while (_hotkeys.Count > 0)
        {
            PInvoke.UnregisterHotKey(_hwnd, _hotkeys.Count - 1);
            _hotkeys.RemoveAt(_hotkeys.Count - 1);
        }
    }

    private void SetupHotkey(SettingsModel settings)
    {
        UnregisterHotkeys();

        var globalHotkey = settings.Hotkey;
        if (globalHotkey is not null)
        {
            if (settings.UseLowLevelGlobalHotkey)
            {
                _keyboardListener.SetHotkeyAction(globalHotkey.Win, globalHotkey.Ctrl, globalHotkey.Shift, globalHotkey.Alt, (byte)globalHotkey.Code, string.Empty);

                _hotkeys.Add(new(globalHotkey, string.Empty));
            }
            else
            {
                var vk = globalHotkey.Code;
                var modifiers =
                                (globalHotkey.Alt ? HOT_KEY_MODIFIERS.MOD_ALT : 0) |
                                (globalHotkey.Ctrl ? HOT_KEY_MODIFIERS.MOD_CONTROL : 0) |
                                (globalHotkey.Shift ? HOT_KEY_MODIFIERS.MOD_SHIFT : 0) |
                                (globalHotkey.Win ? HOT_KEY_MODIFIERS.MOD_WIN : 0)
                                ;

                var success = PInvoke.RegisterHotKey(_hwnd, _hotkeys.Count, modifiers, (uint)vk);
                if (success)
                {
                    _hotkeys.Add(new(globalHotkey, string.Empty));
                }
            }
        }

        foreach (var commandHotkey in settings.CommandHotkeys)
        {
            var key = commandHotkey.Hotkey;

            if (key is not null)
            {
                if (settings.UseLowLevelGlobalHotkey)
                {
                    _keyboardListener.SetHotkeyAction(key.Win, key.Ctrl, key.Shift, key.Alt, (byte)key.Code, commandHotkey.CommandId);

                    _hotkeys.Add(new(globalHotkey, string.Empty));
                }
                else
                {
                    var vk = key.Code;
                    var modifiers =
                        (key.Alt ? HOT_KEY_MODIFIERS.MOD_ALT : 0) |
                        (key.Ctrl ? HOT_KEY_MODIFIERS.MOD_CONTROL : 0) |
                        (key.Shift ? HOT_KEY_MODIFIERS.MOD_SHIFT : 0) |
                        (key.Win ? HOT_KEY_MODIFIERS.MOD_WIN : 0)
                        ;

                    var success = PInvoke.RegisterHotKey(_hwnd, _hotkeys.Count, modifiers, (uint)vk);
                    if (success)
                    {
                        _hotkeys.Add(commandHotkey);
                    }
                }
            }
        }
    }

    private void HandleSummon(string commandId)
    {
        var isRootHotkey = string.IsNullOrEmpty(commandId);
        if (isRootHotkey && IsPaletteVisibleToUser())
        {
            HandleSummonCore(commandId);
            return;
        }

        var notificationFlags = WindowHelper.GetUserNotificationFlags();
        var shouldSuppress =
            (_ignoreHotKeyWhenFullScreen && notificationFlags.IsFullscreenState) ||
            (_ignoreHotKeyWhenBusy && notificationFlags.IsBusy);

        if (shouldSuppress)
        {
            if (_allowBreakthroughShortcut && IsBreakthroughTriggered())
            {
                // Rapid-press breakthrough: let it through
            }
            else
            {
                return;
            }
        }

        HandleSummonCore(commandId);
    }

    private bool IsPaletteVisibleToUser()
    {
        var isVisible = Visible;

        unsafe
        {
            // We need to check if our window is cloaked or not. A cloaked window is still
            // technically visible, because SHOW/HIDE != iconic (minimized) != cloaked
            // (these are all separate states)
            long attr = 0;
            PInvoke.DwmGetWindowAttribute(_hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &attr, sizeof(long));
            if (attr == 1 /* DWM_CLOAKED_APP */)
            {
                isVisible = false;
            }
        }

        return isVisible;
    }

    private bool IsBreakthroughTriggered()
    {
        const int requiredPresses = 3;
        var windowTicks = 2 * Stopwatch.Frequency; // 2 seconds
        var now = Stopwatch.GetTimestamp();

        _breakthroughTimestamps.Add(now);

        // Prune timestamps outside the window
        _breakthroughTimestamps.RemoveAll(t => now - t > windowTicks);

        if (_breakthroughTimestamps.Count >= requiredPresses)
        {
            _breakthroughTimestamps.Clear();
            return true;
        }

        return false;
    }

    private void HandleSummonCore(string commandId)
    {
        var isRootHotkey = string.IsNullOrEmpty(commandId);
        PowerToysTelemetry.Log.WriteEvent(new CmdPalHotkeySummoned(isRootHotkey));

        var isVisible = IsPaletteVisibleToUser();

        // Note to future us: the wParam will have the index of the hotkey we registered.
        // We can use that in the future to differentiate the hotkeys we've pressed
        // so that we can bind hotkeys to individual commands
        if (!isVisible || !isRootHotkey)
        {
            Summon(commandId);
        }
        else if (isRootHotkey)
        {
            // If there's a debugger attached...
            if (System.Diagnostics.Debugger.IsAttached)
            {
                // ... then manually hide our window. When debugged, we won't get the cool cloaking,
                // but that's the price to pay for having the HWND not light-dismiss while we're debugging.
                Cloak();
                this.Hide();
                WeakReferenceMessenger.Default.Send(new WindowHiddenMessage());

                return;
            }

            HideWindow();
        }
    }

    private LRESULT HotKeyPrc(
        HWND hwnd,
        uint uMsg,
        WPARAM wParam,
        LPARAM lParam)
    {
        switch (uMsg)
        {
            // Prevent the window from maximizing when double-clicking the title bar area
            case PInvoke.WM_NCLBUTTONDBLCLK:
                return (LRESULT)IntPtr.Zero;

            // When restoring a saved position across monitors with different DPIs,
            // MoveAndResize already sets the correctly-scaled size. Suppress the
            // framework's automatic DPI resize to avoid double-scaling.
            case PInvoke.WM_DPICHANGED when _suppressDpiChange:
                return (LRESULT)IntPtr.Zero;

            case PInvoke.WM_NCHITTEST:
                {
                    var ht = HitTestForCardResize(lParam);
                    if (ht != 0)
                    {
                        return (LRESULT)(nint)ht;
                    }

                    break;
                }

            // Borderless mode: claim the entire window rectangle as client area.
            // A resizable window has WS_THICKFRAME, which makes the OS reserve a
            // non-client sizing frame *and* gives the window a DWM drop shadow / a thin
            // frame line along the top. We keep WS_THICKFRAME (so our custom WM_NCHITTEST
            // can still drive resizing) but tell the OS the whole window is client by
            // returning 0 from WM_NCCALCSIZE — which removes that frame and its shadow.
            // The visible card draws its own border + shadow inside the transparent HWND.
            // When the debug frame is on we fall through to the default handling so the
            // real OS chrome appears.
            case PInvoke.WM_NCCALCSIZE when wParam.Value != 0 && _hwndFrameVisible != true:
                return (LRESULT)0;

            case PInvoke.WM_HOTKEY:
                {
                    var hotkeyIndex = (int)wParam.Value;
                    if (hotkeyIndex < _hotkeys.Count)
                    {
                        var hotkey = _hotkeys[hotkeyIndex];
                        HandleSummon(hotkey.CommandId);
                    }

                    return (LRESULT)IntPtr.Zero;
                }

            default:
                if (uMsg == WM_TASKBAR_RESTART)
                {
                    HotReloadSettings();
                }

                break;
        }

        return PInvoke.CallWindowProc(_originalWndProc, hwnd, uMsg, wParam, lParam);
    }

    /// <summary>
    /// Custom WM_NCHITTEST handler that turns the visible card's border (the rounded
    /// stroke drawn by <see cref="CmdPalMainControl"/>) into the window's resize handles.
    /// Without this the borderless / transparent HWND has no visible resize affordance,
    /// even though the OS still allows resizing along the (invisible) HWND edges.
    /// </summary>
    /// <returns>
    /// A non-zero HT* value to override the system hit test, or 0 to fall through to
    /// the default WndProc (which lets the InputNonClientPointerSource Caption /
    /// Passthrough regions decide caption vs. client behavior inside the card).
    /// </returns>
    private uint HitTestForCardResize(LPARAM lParam)
    {
        // NB: We intentionally do *not* short-circuit when the debug frame is showing.
        // The HWND frame toggle is purely a visual diagnostic; resize hit-testing
        // remains ours in both modes so the card's border is always the grab area.
        if (RootElement is null || RootElement.XamlRoot is null)
        {
            return 0;
        }

        // LPARAM packs the screen-space pointer position: low word = x, high word = y,
        // both as signed 16-bit ints.
        var ptX = (short)(lParam.Value & 0xFFFF);
        var ptY = (short)((lParam.Value >> 16) & 0xFFFF);

        if (!PInvoke.GetWindowRect(_hwnd, out var windowRect))
        {
            return 0;
        }

        // Convert the card's ShadowPadding (DIPs) into screen pixels so we can locate
        // the visible card rect within the (larger, transparent) HWND.
        var dpi = PInvoke.GetDpiForWindow(_hwnd);
        var scale = dpi / 96.0;
        var padding = RootElement.ShadowPadding;

        var cardLeft = windowRect.left + (int)Math.Round(padding.Left * scale);
        var cardTop = windowRect.top + (int)Math.Round(padding.Top * scale);
        var cardRight = windowRect.right - (int)Math.Round(padding.Right * scale);
        var cardBottom = windowRect.bottom - (int)Math.Round(padding.Bottom * scale);

        // Width of the resize grip around the card's visible border, in screen pixels.
        // Shared with the InputNonClientPointerSource region registration in
        // UpdateRegionsForCustomTitleBar so the band where WM_NCHITTEST fires lines up
        // exactly with the band where we return resize codes.
        var grip = (int)Math.Round(ResizeBorderThicknessDip * scale);

        var onLeftEdge = ptX >= cardLeft - grip && ptX < cardLeft + grip;
        var onRightEdge = ptX > cardRight - grip && ptX <= cardRight + grip;
        var onTopEdge = ptY >= cardTop - grip && ptY < cardTop + grip;
        var onBottomEdge = ptY > cardBottom - grip && ptY <= cardBottom + grip;

        // Corners get priority over edges.
        if (onTopEdge && onLeftEdge)
        {
            return PInvoke.HTTOPLEFT;
        }

        if (onTopEdge && onRightEdge)
        {
            return PInvoke.HTTOPRIGHT;
        }

        if (onBottomEdge && onLeftEdge)
        {
            return PInvoke.HTBOTTOMLEFT;
        }

        if (onBottomEdge && onRightEdge)
        {
            return PInvoke.HTBOTTOMRIGHT;
        }

        var withinHorizontalSpan = ptX >= cardLeft - grip && ptX <= cardRight + grip;
        var withinVerticalSpan = ptY >= cardTop - grip && ptY <= cardBottom + grip;

        if (onTopEdge && withinHorizontalSpan)
        {
            return PInvoke.HTTOP;
        }

        if (onBottomEdge && withinHorizontalSpan)
        {
            return PInvoke.HTBOTTOM;
        }

        if (onLeftEdge && withinVerticalSpan)
        {
            return PInvoke.HTLEFT;
        }

        if (onRightEdge && withinVerticalSpan)
        {
            return PInvoke.HTRIGHT;
        }

        // Pointer is inside the card but away from the border: defer to the default
        // hit test so the InputNonClientPointerSource Caption/Passthrough regions take
        // effect for dragging vs. normal input.
        if (ptX >= cardLeft && ptX <= cardRight && ptY >= cardTop && ptY <= cardBottom)
        {
            return 0;
        }

        // Pointer is in the transparent shadow padding around the card. Make that area
        // click-through so the window behind us receives the mouse input.
        return unchecked((uint)PInvoke.HTTRANSPARENT);
    }

    public void Dispose()
    {
        _localKeyboardListener.Dispose();
        _windowThemeSynchronizer.Dispose();
        DisposeAcrylic();
    }

    void IRecipient<ShowPaletteAtMessage>.Receive(ShowPaletteAtMessage message) => Receive(message);

    public void Receive(ToggleDevRibbonMessage message)
    {
        _devRibbon?.Visibility = _devRibbon.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    }

    public void Receive(DragStartedMessage message)
    {
        _preventHideWhenDeactivated = true;
    }

    public void Receive(DragCompletedMessage message)
    {
        _preventHideWhenDeactivated = false;
        Task.Delay(200).ContinueWith(_ =>
        {
            DispatcherQueue.TryEnqueue(StealForeground);
        });
    }

    private unsafe void StealForeground()
    {
        var foregroundWindow = PInvoke.GetForegroundWindow();
        if (foregroundWindow == _hwnd)
        {
            return;
        }

        // This is bad, evil, and I'll have to forgo today's dinner dessert to punish myself
        // for  writing this. But there's no way to make this work without it.
        // If the window is not reactivated, the UX breaks down: a deactivated window has to
        // be activated and then deactivated again to hide.
        var currentThreadId = PInvoke.GetCurrentThreadId();
        var foregroundThreadId = PInvoke.GetWindowThreadProcessId(foregroundWindow, null);
        if (foregroundThreadId != currentThreadId)
        {
            PInvoke.AttachThreadInput(currentThreadId, foregroundThreadId, true);
            PInvoke.SetForegroundWindow(_hwnd);
            PInvoke.AttachThreadInput(currentThreadId, foregroundThreadId, false);
        }
        else
        {
            PInvoke.SetForegroundWindow(_hwnd);
        }
    }

    public void Receive(GetHwndMessage message)
    {
        message.Hwnd = this.GetWindowHandle();
    }

    public void Receive(ExpandCompactModeMessage message)
    {
        this.DispatcherQueue.TryEnqueue(() => HandleExpandCompactOnUiThread(message.Expanded));
    }

    // The HWND is already as large as it will ever need to be (and it's transparent), so
    // instead of resizing the window we simply shrink or grow the visible card inside it.
    private void HandleExpandCompactOnUiThread(bool expanded)
    {
        var settings = App.Current.Services.GetRequiredService<ISettingsService>().Settings;

        // Only the compact + centered configuration needs a screen-fit clamp. There the card
        // is anchored near the vertical center of the display, so an expanded list could run
        // off the bottom edge; cap its height so it always fits. In every other case the card
        // is free to fill the (fixed-size) HWND as before.
        if (expanded && settings.CompactMode && IsCenteringSummon(settings))
        {
            RootElement.SetCardMaxHeight(ComputeExpandedCardMaxHeightDip());
        }
        else
        {
            RootElement.SetCardMaxHeight(double.PositiveInfinity);
        }
    }

    // Computes how tall (in DIPs) the visible card may grow before it would extend past the
    // bottom of the work area, given the card's current top on screen.
    private double ComputeExpandedCardMaxHeightDip()
    {
        var dpi = (int)this.GetDpiForWindow();
        var scale = dpi / 96.0;

        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        var workArea = displayArea.WorkArea;

        var padding = RootElement.ShadowPadding;
        var cardTopPhysical = AppWindow.Position.Y + (padding.Top * scale);
        var availablePhysical = (workArea.Y + workArea.Height) - cardTopPhysical - (padding.Bottom * scale);

        if (availablePhysical <= 0)
        {
            return double.PositiveInfinity;
        }

        return availablePhysical / scale;
    }
}
