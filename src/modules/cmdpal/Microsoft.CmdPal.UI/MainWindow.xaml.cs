// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Common.Messages;
using Microsoft.CmdPal.Common.Services;
using Microsoft.CmdPal.UI.Events;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.Graphics;
using Windows.UI;
using Windows.UI.WindowManagement;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI;

public sealed partial class MainWindow : Window,
    IRecipient<DismissMessage>,
    IRecipient<ShowWindowMessage>,
    IRecipient<HideWindowMessage>,
    IRecipient<QuitMessage>
{
    private readonly HWND _hwnd;
    private readonly WNDPROC? _hotkeyWndProc;
    private readonly WNDPROC? _originalWndProc;
    private readonly List<TopLevelHotkey> _hotkeys = [];

    // Stylistically, window messages are WM_*
#pragma warning disable SA1310 // Field names should not contain underscore
#pragma warning disable SA1306 // Field names should begin with lower-case letter
    private const uint MY_NOTIFY_ID = 1000;
    private const uint WM_TRAY_ICON = PInvoke.WM_USER + 1;
    private readonly uint WM_TASKBAR_RESTART;
#pragma warning restore SA1306 // Field names should begin with lower-case letter
#pragma warning restore SA1310 // Field names should not contain underscore

    // Notification Area ("Tray") icon data
    private NOTIFYICONDATAW? _trayIconData;
    private bool _createdIcon;
    private DestroyIconSafeHandle? _largeIcon;

    private DesktopAcrylicController? _acrylicController;
    private SystemBackdropConfiguration? _configurationSource;

    public MainWindow()
    {
        InitializeComponent();

        _hwnd = new HWND(WinRT.Interop.WindowNative.GetWindowHandle(this).ToInt32());
        CommandPaletteHost.SetHostHwnd((ulong)_hwnd.Value);

        // TaskbarCreated is the message that's broadcast when explorer.exe
        // restarts. We need to know when that happens to be able to bring our
        // notification area icon back
        WM_TASKBAR_RESTART = PInvoke.RegisterWindowMessage("TaskbarCreated");

        this.SetIcon();
        AppWindow.Title = RS_.GetString("AppName");
        AppWindow.Resize(new SizeInt32 { Width = 1000, Height = 620 });
        PositionCentered();
        SetAcrylic();

        WeakReferenceMessenger.Default.Register<DismissMessage>(this);
        WeakReferenceMessenger.Default.Register<QuitMessage>(this);
        WeakReferenceMessenger.Default.Register<ShowWindowMessage>(this);
        WeakReferenceMessenger.Default.Register<HideWindowMessage>(this);

        // Hide our titlebar.
        // We need to both ExtendsContentIntoTitleBar, then set the height to Collapsed
        // to hide the old caption buttons. Then, in UpdateRegionsForCustomTitleBar,
        // we'll make the top drag-able again. (after our content loads)
        ExtendsContentIntoTitleBar = true;
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
        SizeChanged += WindowSizeChanged;
        RootShellPage.Loaded += RootShellPage_Loaded;

        // LOAD BEARING: If you don't stick the pointer to HotKeyPrc into a
        // member (and instead like, use a local), then the pointer we marshal
        // into the WindowLongPtr will be useless after we leave this function,
        // and our **WindProc will explode**.
        _hotkeyWndProc = HotKeyPrc;
        var hotKeyPrcPointer = Marshal.GetFunctionPointerForDelegate(_hotkeyWndProc);
        _originalWndProc = Marshal.GetDelegateForFunctionPointer<WNDPROC>(PInvoke.SetWindowLongPtr(_hwnd, WINDOW_LONG_PTR_INDEX.GWL_WNDPROC, hotKeyPrcPointer));
        AddNotificationIcon();

        // Load our settings, and then also wire up a settings changed handler
        HotReloadSettings();
        App.Current.Services.GetService<SettingsModel>()!.SettingsChanged += SettingsChangedHandler;

        // Make sure that we update the acrylic theme when the OS theme changes
        RootShellPage.ActualThemeChanged += (s, e) => UpdateAcrylic();

        // Hardcoding event name to avoid bringing in the PowerToys.interop dependency. Event name must match CMDPAL_SHOW_EVENT from shared_constants.h
        NativeEventWaiter.WaitForEventLoop("Local\\PowerToysCmdPal-ShowEvent-62336fcd-8611-4023-9b30-091a6af4cc5a", () =>
        {
            Summon(string.Empty);
        });
    }

    private void SettingsChangedHandler(SettingsModel sender, object? args) => HotReloadSettings();

    private void RootShellPage_Loaded(object sender, RoutedEventArgs e) =>

        // Now that our content has loaded, we can update our draggable regions
        UpdateRegionsForCustomTitleBar();

    private void WindowSizeChanged(object sender, WindowSizeChangedEventArgs args) => UpdateRegionsForCustomTitleBar();

    private void PositionCentered()
    {
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Nearest);
        PositionCentered(displayArea);
    }

    private void PositionCentered(DisplayArea displayArea)
    {
        if (displayArea is not null)
        {
            var centeredPosition = AppWindow.Position;
            centeredPosition.X = (displayArea.WorkArea.Width - AppWindow.Size.Width) / 2;
            centeredPosition.Y = (displayArea.WorkArea.Height - AppWindow.Size.Height) / 2;

            centeredPosition.X += displayArea.WorkArea.X;
            centeredPosition.Y += displayArea.WorkArea.Y;
            AppWindow.Move(centeredPosition);
        }
    }

    private void HotReloadSettings()
    {
        var settings = App.Current.Services.GetService<SettingsModel>()!;

        SetupHotkey(settings);

        // This will prevent our window from appearing in alt+tab or the taskbar.
        // You'll _need_ to use the hotkey to summon it.
        AppWindow.IsShownInSwitchers = System.Diagnostics.Debugger.IsAttached;
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
        _acrylicController = GetAcrylicConfig(Content);

        // Enable the system backdrop.
        // Note: Be sure to have "using WinRT;" to support the Window.As<...>() call.
        _acrylicController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
        _acrylicController.SetSystemBackdropConfiguration(_configurationSource);
    }

    private static DesktopAcrylicController GetAcrylicConfig(UIElement content)
    {
        var feContent = content as FrameworkElement;

        return feContent?.ActualTheme == ElementTheme.Light
            ? new DesktopAcrylicController()
            {
                Kind = DesktopAcrylicKind.Thin,
                TintColor = Color.FromArgb(255, 243, 243, 243),
                LuminosityOpacity = 0.90f,
                TintOpacity = 0.0f,
                FallbackColor = Color.FromArgb(255, 238, 238, 238),
            }
            : new DesktopAcrylicController()
            {
                Kind = DesktopAcrylicKind.Thin,
                TintColor = Color.FromArgb(255, 32, 32, 32),
                LuminosityOpacity = 0.96f,
                TintOpacity = 0.5f,
                FallbackColor = Color.FromArgb(255, 28, 28, 28),
            };
    }

    private void ShowHwnd(IntPtr hwndValue, MonitorBehavior target)
    {
        var hwnd = new HWND(hwndValue);

        // Remember, IsIconic == "minimized", which is entirely different state
        // from "show/hide"
        // If we're currently minimized, restore us first, before we reveal
        // our window. Otherwise we'd just be showing a minimized window -
        // which would remain not visible to the user.
        if (PInvoke.IsIconic(hwnd))
        {
            PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_RESTORE);
        }

        var display = GetScreen(hwnd, target);
        PositionCentered(display);

        PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_SHOW);
        PInvoke.SetForegroundWindow(hwnd);
        PInvoke.SetActiveWindow(hwnd);
    }

    private DisplayArea GetScreen(HWND currentHwnd, MonitorBehavior target)
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

        ShowHwnd(message.Hwnd, settings.SummonOn);
    }

    public void Receive(HideWindowMessage message)
    {
        PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_HIDE);
    }

    public void Receive(QuitMessage message)
    {
        // This might come in on a background thread
        DispatcherQueue.TryEnqueue(() => Close());
    }

    public void Receive(DismissMessage message) =>
        PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_HIDE);

    internal void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        var serviceProvider = App.Current.Services;
        var extensionService = serviceProvider.GetService<IExtensionService>()!;
        extensionService.SignalStopExtensionsAsync();

        RemoveNotificationIcon();

        // WinUI bug is causing a crash on shutdown when FailFastOnErrors is set to true (#51773592).
        // Workaround by turning it off before shutdown.
        App.Current.DebugSettings.FailFastOnErrors = false;
        DisposeAcrylic();
    }

    private void DisposeAcrylic()
    {
        if (_acrylicController != null)
        {
            _acrylicController.Dispose();
            _acrylicController = null!;
            _configurationSource = null!;
        }
    }

    // Updates our window s.t. the top of the window is draggable.
    private void UpdateRegionsForCustomTitleBar()
    {
        // Specify the interactive regions of the title bar.
        var scaleAdjustment = RootShellPage.XamlRoot.RasterizationScale;

        // Get the rectangle around our XAML content. We're going to mark this
        // rectangle as "Passthrough", so that the normal window operations
        // (resizing, dragging) don't apply in this space.
        var transform = RootShellPage.TransformToVisual(null);

        // Reserve 16px of space at the top for dragging.
        var topHeight = 16;
        var bounds = transform.TransformBounds(new Rect(
            0,
            topHeight,
            RootShellPage.ActualWidth,
            RootShellPage.ActualHeight));
        var contentRect = GetRect(bounds, scaleAdjustment);
        var rectArray = new RectInt32[] { contentRect };
        var nonClientInputSrc = InputNonClientPointerSource.GetForWindowId(this.AppWindow.Id);
        nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, rectArray);

        // Add a drag-able region on top
        var w = RootShellPage.ActualWidth;
        _ = RootShellPage.ActualHeight;
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
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            // If there's a debugger attached...
            if (System.Diagnostics.Debugger.IsAttached)
            {
                // ... then don't hide the window when it loses focus.
                return;
            }
            else
            {
                PInvoke.ShowWindow(_hwnd, SHOW_WINDOW_CMD.SW_HIDE);

                PowerToysTelemetry.Log.WriteEvent(new CmdPalDismissedOnLostFocus());
            }
        }

        if (_configurationSource != null)
        {
            _configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;
        }
    }

    public void Summon(string commandId)
    {
        // The actual showing and hiding of the window will be done by the
        // ShellPage. This is because we don't want to show the window if the
        // user bound a hotkey to just an invokable command, which we can't
        // know till the message is being handled.
        WeakReferenceMessenger.Default.Send<HotkeySummonMessage>(new(commandId, _hwnd));
    }

#pragma warning disable SA1310 // Field names should not contain underscore
    private const uint DOT_KEY = 0xBE;
    private const uint WM_HOTKEY = 0x0312;
#pragma warning restore SA1310 // Field names should not contain underscore

    private void UnregisterHotkeys()
    {
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
        if (globalHotkey != null)
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

        foreach (var commandHotkey in settings.CommandHotkeys)
        {
            var key = commandHotkey.Hotkey;

            if (key != null)
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

    private LRESULT HotKeyPrc(
        HWND hwnd,
        uint uMsg,
        WPARAM wParam,
        LPARAM lParam)
    {
        switch (uMsg)
        {
            case WM_HOTKEY:
                {
                    var hotkeyIndex = (int)wParam.Value;
                    if (hotkeyIndex < _hotkeys.Count)
                    {
                        var hotkey = _hotkeys[hotkeyIndex];
                        var isRootHotkey = string.IsNullOrEmpty(hotkey.CommandId);
                        PowerToysTelemetry.Log.WriteEvent(new CmdPalHotkeySummoned(isRootHotkey));

                        // Note to future us: the wParam will have the index of the hotkey we registered.
                        // We can use that in the future to differentiate the hotkeys we've pressed
                        // so that we can bind hotkeys to individual commands
                        if (!this.Visible || !isRootHotkey)
                        {
                            Activate();

                            Summon(hotkey.CommandId);
                        }
                        else if (isRootHotkey)
                        {
                            PInvoke.ShowWindow(hwnd, SHOW_WINDOW_CMD.SW_HIDE);
                        }
                    }

                    return (LRESULT)IntPtr.Zero;
                }

            // Shell_NotifyIcon can fail when we invoke it during the time explorer.exe isn't present/ready to handle it.
            // We'll also never receive WM_TASKBAR_RESTART message if the first call to Shell_NotifyIcon failed, so we use
            // WM_WINDOWPOSCHANGING which is always received on explorer startup sequence.
            case PInvoke.WM_WINDOWPOSCHANGING:
                {
                    if (!_createdIcon)
                    {
                        AddNotificationIcon();
                    }
                }

                break;
            default:
                // WM_TASKBAR_RESTART isn't a compile-time constant, so we can't
                // use it in a case label
                if (uMsg == WM_TASKBAR_RESTART)
                {
                    // Handle the case where explorer.exe restarts.
                    // Even if we created it before, do it again
                    AddNotificationIcon();
                }
                else if (uMsg == WM_TRAY_ICON)
                {
                    switch ((uint)lParam.Value)
                    {
                        case PInvoke.WM_RBUTTONUP:
                        case PInvoke.WM_LBUTTONUP:
                        case PInvoke.WM_LBUTTONDBLCLK:
                            Summon(string.Empty);
                            break;
                    }
                }

                break;
        }

        return PInvoke.CallWindowProc(_originalWndProc, hwnd, uMsg, wParam, lParam);
    }

    private void AddNotificationIcon()
    {
        // We only need to build the tray data once.
        if (_trayIconData == null)
        {
            // We need to stash this handle, so it doesn't clean itself up. If
            // explorer restarts, we'll come back through here, and we don't
            // really need to re-load the icon in that case. We can just use
            // the handle from the first time.
            _largeIcon = GetAppIconHandle();
            _trayIconData = new NOTIFYICONDATAW()
            {
                cbSize = (uint)Marshal.SizeOf(typeof(NOTIFYICONDATAW)),
                hWnd = _hwnd,
                uID = MY_NOTIFY_ID,
                uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP,
                uCallbackMessage = WM_TRAY_ICON,
                hIcon = (HICON)_largeIcon.DangerousGetHandle(),
                szTip = RS_.GetString("AppStoreName"),
            };
        }

        var d = (NOTIFYICONDATAW)_trayIconData;

        // Add the notification icon
        if (PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, in d))
        {
            _createdIcon = true;
        }
    }

    private void RemoveNotificationIcon()
    {
        if (_trayIconData != null && _createdIcon)
        {
            var d = (NOTIFYICONDATAW)_trayIconData;
            if (PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, in d))
            {
                _createdIcon = false;
            }
        }
    }

    private DestroyIconSafeHandle GetAppIconHandle()
    {
        var exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
        DestroyIconSafeHandle largeIcon;
        DestroyIconSafeHandle smallIcon;
        PInvoke.ExtractIconEx(exePath, 0, out largeIcon, out smallIcon, 1);
        return largeIcon;
    }
}
