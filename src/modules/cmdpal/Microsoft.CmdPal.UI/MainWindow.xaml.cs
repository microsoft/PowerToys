// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using CmdPalKeyboardService;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.Core.Common.Helpers;
using Microsoft.CmdPal.Core.Common.Services;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.Ext.ClipboardHistory.Messages;
using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.Events;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.Services;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;
using Windows.UI;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT;
using WinUIEx;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI;

public sealed partial class MainWindow : WindowEx,
    IRecipient<DismissMessage>,
    IRecipient<ShowWindowMessage>,
    IRecipient<HideWindowMessage>,
    IRecipient<QuitMessage>,
    IRecipient<NavigateToPageMessage>,
    IRecipient<NavigationDepthMessage>,
    IRecipient<SearchQueryMessage>,
    IRecipient<ErrorOccurredMessage>,
    IRecipient<DragStartedMessage>,
    IRecipient<DragCompletedMessage>,
    IRecipient<ToggleDevRibbonMessage>,
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
    private bool _ignoreHotKeyWhenFullScreen = true;
    private bool _suppressDpiChange;
    private bool _themeServiceInitialized;

    // Session tracking for telemetry
    private Stopwatch? _sessionStopwatch;
    private int _sessionCommandsExecuted;
    private int _sessionPagesVisited;
    private int _sessionSearchQueriesCount;
    private int _sessionMaxNavigationDepth;
    private int _sessionErrorCount;

    private DesktopAcrylicController? _acrylicController;
    private MicaController? _micaController;
    private SystemBackdropConfiguration? _configurationSource;
    private bool _isUpdatingBackdrop;
    private TimeSpan _autoGoHomeInterval = Timeout.InfiniteTimeSpan;

    private WindowPosition _currentWindowPosition = new();

    private bool _preventHideWhenDeactivated;

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

        InitializeBackdropSupport();

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
        RestoreWindowPosition();
        UpdateWindowPositionInMemory();

        WeakReferenceMessenger.Default.Register<DismissMessage>(this);
        WeakReferenceMessenger.Default.Register<QuitMessage>(this);
        WeakReferenceMessenger.Default.Register<ShowWindowMessage>(this);
        WeakReferenceMessenger.Default.Register<HideWindowMessage>(this);
        WeakReferenceMessenger.Default.Register<NavigateToPageMessage>(this);
        WeakReferenceMessenger.Default.Register<NavigationDepthMessage>(this);
        WeakReferenceMessenger.Default.Register<SearchQueryMessage>(this);
        WeakReferenceMessenger.Default.Register<ErrorOccurredMessage>(this);
        WeakReferenceMessenger.Default.Register<DragStartedMessage>(this);
        WeakReferenceMessenger.Default.Register<DragCompletedMessage>(this);
        WeakReferenceMessenger.Default.Register<ToggleDevRibbonMessage>(this);

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
        App.Current.Services.GetService<SettingsModel>()!.SettingsChanged += SettingsChangedHandler;

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

    private void SettingsChangedHandler(SettingsModel sender, object? args) => HotReloadSettings();

    private void RootElementLoaded(object sender, RoutedEventArgs e)
    {
        // Now that our content has loaded, we can update our draggable regions
        UpdateRegionsForCustomTitleBar();

        // Also update regions when DPI changes. SizeChanged only fires when the logical
        // (DIP) size changes — a DPI change that scales the physical size while preserving
        // the DIP size won't trigger it, leaving drag regions at the old physical coordinates.
        RootElement.XamlRoot.Changed += XamlRoot_Changed;

        // Add dev ribbon if enabled
        if (!BuildInfo.IsCiBuild)
        {
            _devRibbon = new DevRibbon { Margin = new Thickness(-1, -1, 120, -1) };
            RootElement.Children.Add(_devRibbon);
        }
    }

    private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args) => UpdateRegionsForCustomTitleBar();

    private void WindowSizeChanged(object sender, WindowSizeChangedEventArgs args) => UpdateRegionsForCustomTitleBar();

    private void PositionCentered()
    {
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        PositionCentered(displayArea);
    }

    private void PositionCentered(DisplayArea displayArea)
    {
        var rect = WindowPositionHelper.CenterOnDisplay(
            displayArea,
            AppWindow.Size,
            (int)this.GetDpiForWindow());

        if (rect is not null)
        {
            MoveAndResizeDpiAware(rect.Value);
        }
    }

    private void RestoreWindowPosition()
    {
        var settings = App.Current.Services.GetService<SettingsModel>();
        if (settings?.LastWindowPosition is not { Width: > 0, Height: > 0 } savedPosition)
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
        _currentWindowPosition = new WindowPosition
        {
            X = rect.X,
            Y = rect.Y,
            Width = rect.Width,
            Height = rect.Height,
            Dpi = (int)this.GetDpiForWindow(),
            ScreenWidth = displayArea.WorkArea.Width,
            ScreenHeight = displayArea.WorkArea.Height,
        };
    }

    private void HotReloadSettings()
    {
        var settings = App.Current.Services.GetService<SettingsModel>()!;

        SetupHotkey(settings);
        App.Current.Services.GetService<TrayIconService>()!.SetupTrayIcon(settings.ShowSystemTrayIcon);

        _ignoreHotKeyWhenFullScreen = settings.IgnoreShortcutWhenFullscreen;

        _autoGoHomeInterval = settings.AutoGoHomeInterval;
        _autoGoHomeTimer.Interval = _autoGoHomeInterval;
    }

    private void InitializeBackdropSupport()
    {
        if (DesktopAcrylicController.IsSupported() || MicaController.IsSupported())
        {
            _configurationSource = new SystemBackdropConfiguration
            {
                IsInputActive = true,
            };
        }
    }

    private void UpdateBackdrop()
    {
        // Prevent re-entrance when backdrop changes trigger ActualThemeChanged
        if (_isUpdatingBackdrop)
        {
            return;
        }

        _isUpdatingBackdrop = true;

        var backdrop = _themeService.Current.BackdropParameters;
        var isImageMode = ViewModel.ShowBackgroundImage;
        var config = BackdropStyles.Get(backdrop.Style);

        try
        {
            switch (config.ControllerKind)
            {
                case BackdropControllerKind.Solid:
                    CleanupBackdropControllers();
                    var tintColor = Color.FromArgb(
                        (byte)(backdrop.EffectiveOpacity * 255),
                        backdrop.TintColor.R,
                        backdrop.TintColor.G,
                        backdrop.TintColor.B);
                    SetupTransparentBackdrop(tintColor);
                    break;

                case BackdropControllerKind.Mica:
                case BackdropControllerKind.MicaAlt:
                    SetupMica(backdrop, isImageMode, config.ControllerKind);
                    break;

                case BackdropControllerKind.Acrylic:
                case BackdropControllerKind.AcrylicThin:
                default:
                    SetupDesktopAcrylic(backdrop, isImageMode, config.ControllerKind);
                    break;
            }
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

    private void SetupTransparentBackdrop(Color tintColor)
    {
        if (SystemBackdrop is TransparentTintBackdrop existingBackdrop)
        {
            existingBackdrop.TintColor = tintColor;
        }
        else
        {
            SystemBackdrop = new TransparentTintBackdrop { TintColor = tintColor };
        }
    }

    private void CleanupBackdropControllers()
    {
        if (_acrylicController is not null)
        {
            _acrylicController.RemoveAllSystemBackdropTargets();
            _acrylicController.Dispose();
            _acrylicController = null;
        }

        if (_micaController is not null)
        {
            _micaController.RemoveAllSystemBackdropTargets();
            _micaController.Dispose();
            _micaController = null;
        }
    }

    private void SetupDesktopAcrylic(BackdropParameters backdrop, bool isImageMode, BackdropControllerKind kind)
    {
        CleanupBackdropControllers();

        // Fall back to solid color if acrylic not supported
        if (_configurationSource is null || !DesktopAcrylicController.IsSupported())
        {
            SetupTransparentBackdrop(backdrop.FallbackColor);
            return;
        }

        // DesktopAcrylicController and SystemBackdrop can't be active simultaneously
        SystemBackdrop = null;

        // Image mode: no tint here, BlurImageControl handles it (avoids double-tinting)
        var effectiveTintOpacity = isImageMode
            ? 0.0f
            : backdrop.EffectiveOpacity;

        _acrylicController = new DesktopAcrylicController
        {
            Kind = kind == BackdropControllerKind.AcrylicThin
                ? DesktopAcrylicKind.Thin
                : DesktopAcrylicKind.Default,
            TintColor = backdrop.TintColor,
            TintOpacity = effectiveTintOpacity,
            FallbackColor = backdrop.FallbackColor,
            LuminosityOpacity = backdrop.EffectiveLuminosityOpacity,
        };

        // Requires "using WinRT;" for Window.As<>()
        _acrylicController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
        _acrylicController.SetSystemBackdropConfiguration(_configurationSource);
    }

    private void SetupMica(BackdropParameters backdrop, bool isImageMode, BackdropControllerKind kind)
    {
        CleanupBackdropControllers();

        // Fall back to solid color if Mica not supported
        if (_configurationSource is null || !MicaController.IsSupported())
        {
            SetupTransparentBackdrop(backdrop.FallbackColor);
            return;
        }

        // MicaController and SystemBackdrop can't be active simultaneously
        SystemBackdrop = null;
        _configurationSource.Theme = _themeService.Current.Theme == ElementTheme.Dark
            ? SystemBackdropTheme.Dark
            : SystemBackdropTheme.Light;

        var hasColorization = _themeService.Current.HasColorization || isImageMode;

        _micaController = new MicaController
        {
            Kind = kind == BackdropControllerKind.MicaAlt
                ? MicaKind.BaseAlt
                : MicaKind.Base,
        };

        // Only set tint properties when colorization is active
        // Otherwise let system handle light/dark theme defaults automatically
        if (hasColorization)
        {
            // Image mode: no tint here, BlurImageControl handles it (avoids double-tinting)
            _micaController.TintColor = backdrop.TintColor;
            _micaController.TintOpacity = isImageMode ? 0.0f : backdrop.EffectiveOpacity;
            _micaController.FallbackColor = backdrop.FallbackColor;
            _micaController.LuminosityOpacity = backdrop.EffectiveLuminosityOpacity;
        }

        _micaController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
        _micaController.SetSystemBackdropConfiguration(_configurationSource);
    }

    private void ShowHwnd(IntPtr hwndValue, MonitorBehavior target)
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

        // Check if the debugger is attached. If it is, we don't want to apply the tool window style,
        // because that would make it hard to debug the app
        if (Debugger.IsAttached)
        {
            _hiddenOwnerBehavior.ShowInTaskbar(this, true);
        }

        // Just to be sure, SHOW our hwnd.
        PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_SHOW);

        // Once we're done, uncloak to avoid all animations
        Uncloak();

        PInvoke.SetForegroundWindow(hwnd);
        PInvoke.SetActiveWindow(hwnd);

        // Push our window to the top of the Z-order and make it the topmost, so that it appears above all other windows.
        // We want to remove the topmost status when we hide the window (because we cloak it instead of hiding it).
        PInvoke.SetWindowPos(hwnd, HWND.HWND_TOPMOST, 0, 0, 0, 0, SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE);
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
        var settings = App.Current.Services.GetService<SettingsModel>()!;

        // Start session tracking
        _sessionStopwatch = Stopwatch.StartNew();
        _sessionCommandsExecuted = 0;
        _sessionPagesVisited = 0;

        ShowHwnd(message.Hwnd, settings.SummonOn);
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
        UpdateWindowPositionInMemory();

        var settings = serviceProvider.GetService<SettingsModel>();
        if (settings is not null)
        {
            // a quick sanity check, so we don't overwrite correct values
            if (_currentWindowPosition.IsSizeValid)
            {
                settings.LastWindowPosition = _currentWindowPosition;
                SettingsModel.SaveSettings(settings);
            }
        }

        var extensionService = serviceProvider.GetService<IExtensionService>()!;
        extensionService.SignalStopExtensionsAsync();

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
        CleanupBackdropControllers();
        _configurationSource = null!;
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

        // Get the rectangle around our XAML content. We're going to mark this
        // rectangle as "Passthrough", so that the normal window operations
        // (resizing, dragging) don't apply in this space.
        var transform = RootElement.TransformToVisual(null);

        // Reserve 16px of space at the top for dragging.
        var topHeight = 16;
        var bounds = transform.TransformBounds(new Rect(
            0,
            topHeight,
            RootElement.ActualWidth,
            RootElement.ActualHeight));
        var contentRect = GetRect(bounds, scaleAdjustment);
        var rectArray = new RectInt32[] { contentRect };
        var nonClientInputSrc = InputNonClientPointerSource.GetForWindowId(this.AppWindow.Id);
        nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, rectArray);

        // Add a drag-able region on top
        var w = RootElement.ActualWidth;
        _ = RootElement.ActualHeight;
        var dragSides = new RectInt32[]
        {
            GetRect(new Rect(0, 0, w, topHeight), scaleAdjustment), // the top, {topHeight=16} tall
        };
        nonClientInputSrc.SetRegionRects(NonClientRegionKind.Caption, dragSides);
    }

    private static RectInt32 GetRect(Rect bounds, double scale)
    {
        return new RectInt32(
            _X: (int)Math.Round(bounds.X * scale),
            _Y: (int)Math.Round(bounds.Y * scale),
            _Width: (int)Math.Round(bounds.Width * scale),
            _Height: (int)Math.Round(bounds.Height * scale));
    }

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
            UpdateWindowPositionInMemory();

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

        if (_configurationSource is not null)
        {
            _configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
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
                            var settings = App.Current.Services.GetService<SettingsModel>();
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
        if (_ignoreHotKeyWhenFullScreen)
        {
            // If we're in full screen mode, ignore the hotkey
            if (WindowHelper.IsWindowFullscreen())
            {
                return;
            }
        }

        HandleSummonCore(commandId);
    }

    private void HandleSummonCore(string commandId)
    {
        var isRootHotkey = string.IsNullOrEmpty(commandId);
        PowerToysTelemetry.Log.WriteEvent(new CmdPalHotkeySummoned(isRootHotkey));

        var isVisible = this.Visible;

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

    public void Dispose()
    {
        _localKeyboardListener.Dispose();
        _windowThemeSynchronizer.Dispose();
        DisposeAcrylic();
    }

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
}
