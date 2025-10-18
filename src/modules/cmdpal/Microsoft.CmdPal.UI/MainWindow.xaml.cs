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
using Microsoft.CmdPal.UI.Events;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
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
using Windows.UI.WindowManagement;
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
    IDisposable
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Stylistically, window messages are WM_")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:Field names should begin with lower-case letter", Justification = "Stylistically, window messages are WM_")]
    private readonly uint WM_TASKBAR_RESTART;
    private readonly HWND _hwnd;
    private readonly WNDPROC? _hotkeyWndProc;
    private readonly WNDPROC? _originalWndProc;
    private readonly List<TopLevelHotkey> _hotkeys = [];
    private readonly KeyboardListener _keyboardListener;
    private readonly LocalKeyboardListener _localKeyboardListener;
    private bool _ignoreHotKeyWhenFullScreen = true;

    private DesktopAcrylicController? _acrylicController;
    private SystemBackdropConfiguration? _configurationSource;

    public MainWindow()
    {
        InitializeComponent();

        _hwnd = new HWND(WinRT.Interop.WindowNative.GetWindowHandle(this).ToInt32());

        unsafe
        {
            CommandPaletteHost.SetHostHwnd((ulong)_hwnd.Value);
        }

        _keyboardListener = new KeyboardListener();
        _keyboardListener.Start();

        _keyboardListener.SetProcessCommand(new CmdPalKeyboardService.ProcessCommand(HandleSummon));

        this.SetIcon();
        AppWindow.Title = RS_.GetString("AppName");
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

        WM_TASKBAR_RESTART = PInvoke.RegisterWindowMessage("TaskbarCreated");

        // LOAD BEARING: If you don't stick the pointer to HotKeyPrc into a
        // member (and instead like, use a local), then the pointer we marshal
        // into the WindowLongPtr will be useless after we leave this function,
        // and our **WindProc will explode**.
        _hotkeyWndProc = HotKeyPrc;
        var hotKeyPrcPointer = Marshal.GetFunctionPointerForDelegate(_hotkeyWndProc);
        _originalWndProc = Marshal.GetDelegateForFunctionPointer<WNDPROC>(PInvoke.SetWindowLongPtr(_hwnd, WINDOW_LONG_PTR_INDEX.GWL_WNDPROC, hotKeyPrcPointer));

        // Load our settings, and then also wire up a settings changed handler
        HotReloadSettings();
        App.Current.Services.GetService<SettingsModel>()!.SettingsChanged += SettingsChangedHandler;

        // Make sure that we update the acrylic theme when the OS theme changes
        RootShellPage.ActualThemeChanged += (s, e) => DispatcherQueue.TryEnqueue(UpdateAcrylic);

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

        ApplyWindowStyle();
    }

    private void ApplyWindowStyle()
    {
        // Tool windows don't show up in ALT+TAB, and don't show up in the taskbar
        // Since tool windows have smaller corner radii, we need to force the normal ones
        this.ToggleExtendedWindowStyle(WINDOW_EX_STYLE.WS_EX_TOOLWINDOW, !Debugger.IsAttached);
        this.SetCornerPreference(DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND);
    }

    private static void LocalKeyboardListener_OnKeyPressed(object? sender, LocalKeyboardListenerKeyPressedEventArgs e)
    {
        if (e.Key == VirtualKey.GoBack)
        {
            WeakReferenceMessenger.Default.Send(new GoBackMessage());
        }
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
        App.Current.Services.GetService<TrayIconService>()!.SetupTrayIcon(settings.ShowSystemTrayIcon);

        _ignoreHotKeyWhenFullScreen = settings.IgnoreShortcutWhenFullscreen;
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

        var display = GetScreen(hwnd, target);
        PositionCentered(display);

        // Check if the debugger is attached. If it is, we don't want to apply the tool window style,
        // because that would make it hard to debug the app
        if (Debugger.IsAttached)
        {
            ApplyWindowStyle();
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
        // This might come in off the UI thread. Make sure to hop back.
        DispatcherQueue.TryEnqueue(() =>
        {
            HideWindow();
        });
    }

    public void Receive(QuitMessage message) =>

        // This might come in on a background thread
        DispatcherQueue.TryEnqueue(() => Close());

    public void Receive(DismissMessage message)
    {
        // This might come in off the UI thread. Make sure to hop back.
        DispatcherQueue.TryEnqueue(() =>
        {
            HideWindow();
        });
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

            wasCloaked = hr.Succeeded;
        }

        if (wasCloaked)
        {
            // Because we're only cloaking the window, bury it at the bottom in case something can
            // see it - e.g. some accessibility helper (note: this also removes the top-most status).
            PInvoke.SetWindowPos(_hwnd, HWND.HWND_BOTTOM, 0, 0, 0, 0, SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE);
        }

        return wasCloaked;
    }

    private void Uncloak()
    {
        unsafe
        {
            BOOL value = false;
            PInvoke.DwmSetWindowAttribute(_hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAK, &value, (uint)sizeof(BOOL));
        }
    }

    internal void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        var serviceProvider = App.Current.Services;
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
        if (_acrylicController is not null)
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

            // Are we disabled? If we are, then we don't want to dismiss on focus lost.
            // This can happen if an extension wanted to show a modal dialog on top of our
            // window i.e. in the case of an MSAL auth window.
            if (PInvoke.IsWindowEnabled(_hwnd) == 0)
            {
                return;
            }

            // This will DWM cloak our window:
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
        DisposeAcrylic();
    }
}
