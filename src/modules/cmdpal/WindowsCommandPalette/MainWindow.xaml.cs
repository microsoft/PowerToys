// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.CmdPal.Common.Contracts;
using Microsoft.CmdPal.Common.Extensions;
using Microsoft.CmdPal.Common.Services;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using WindowsCommandPalette.Views;
using WinRT;

namespace WindowsCommandPalette;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
public sealed partial class MainWindow : Window
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
{
    private readonly AppWindow _appWindow;

    private readonly MainViewModel _mainViewModel;

    private readonly HWND hwnd;
    private WNDPROC? origPrc;
    private WNDPROC? hotKeyPrc;

#pragma warning disable SA1310 // Field names should not contain underscore
    private const uint DOT_KEY = 0xBE;
    private const uint WM_HOTKEY = 0x0312;
#pragma warning restore SA1310 // Field names should not contain underscore

    private LRESULT HotKeyPrc(
        HWND hwnd,
        uint uMsg,
        WPARAM wParam,
        LPARAM lParam)
    {
        if (uMsg == WM_HOTKEY)
        {
            if (!this.Visible)
            {
                Summon();
            }
            else
            {
                Windows.Win32.PInvoke.ShowWindow(hwnd, Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_HIDE);
            }

            return (Windows.Win32.Foundation.LRESULT)IntPtr.Zero;
        }

        return Windows.Win32.PInvoke.CallWindowProc(origPrc, hwnd, uMsg, wParam, lParam);
    }

    public void Summon()
    {
        Windows.Win32.PInvoke.ShowWindow(hwnd, Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_SHOW);
        Windows.Win32.PInvoke.SetForegroundWindow(hwnd);

        // Windows.Win32.PInvoke.SetFocus(hwnd);
        Windows.Win32.PInvoke.SetActiveWindow(hwnd);
        MainPage.ViewModel.Summon();
    }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public MainWindow()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    {
        InitializeComponent();
        _mainViewModel = MainPage.ViewModel;

        hwnd = new Windows.Win32.Foundation.HWND(WinRT.Interop.WindowNative.GetWindowHandle(this).ToInt32());

        _ = SetupHotkey();

        // Assumes "this" is a XAML Window. In projects that don't use
        // WinUI 3 1.3 or later, use interop APIs to get the AppWindow.
        _appWindow = AppWindow;

        Activated += MainWindow_Activated;
        SetAcrylic();
        ExtendsContentIntoTitleBar = true;

        // Hide our titlebar. We'll make the sides draggable later
        _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;

        Application.Current.GetService<ILocalSettingsService>().SaveSettingAsync("ThisIsAVeryBizarreString", true);

        // PositionForStartMenu();
        PositionCentered();
        _mainViewModel.HideRequested += MainViewModel_HideRequested;

        _mainViewModel.QuitRequested += (s, e) =>
        {
            Close();

            // Application.Current.Exit();
        };
    }

    private async Task SetupHotkey()
    {
        var hotkeySettingString = await Application.Current.GetService<ILocalSettingsService>().ReadSettingAsync<string>("GlobalHotkey") ?? "win+ctrl+.";
        var (key, modifiers) = StringToKeybinding(hotkeySettingString);
        var (vk, mod) = UwpToWin32(key, modifiers);
        var success = Windows.Win32.PInvoke.RegisterHotKey(hwnd, 0, mod, vk);
        hotKeyPrc = HotKeyPrc;
        var hotKeyPrcPointer = Marshal.GetFunctionPointerForDelegate(hotKeyPrc);
        origPrc = Marshal.GetDelegateForFunctionPointer<WNDPROC>((IntPtr)Windows.Win32.PInvoke.SetWindowLongPtr(hwnd, WINDOW_LONG_PTR_INDEX.GWL_WNDPROC, hotKeyPrcPointer));
    }

    private void PositionCentered()
    {
        _appWindow.Resize(new SizeInt32 { Width = 860, Height = 560 });
        DisplayArea displayArea = DisplayArea.GetFromWindowId(_appWindow.Id, DisplayAreaFallback.Nearest);
        if (displayArea is not null)
        {
            var centeredPosition = AppWindow.Position;
            centeredPosition.X = (displayArea.WorkArea.Width - AppWindow.Size.Width) / 2;
            centeredPosition.Y = (displayArea.WorkArea.Height - AppWindow.Size.Height) / 2;

            AppWindow.Move(centeredPosition);
        }
    }

    private void PositionForStartMenu()
    {
        _appWindow.Resize(new Windows.Graphics.SizeInt32(768, 768));

        // now put the window in the right place
        //
        // HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced
        // * TaskbarGlomLevel > 0 === no glomming, therefore on the left
        // * TaskbarAl = 0 ===  on the left.
        var onLeft = false;
        try
        {
            using RegistryKey? key = Registry.CurrentUser?.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced");
            if (key != null)
            {
                var o = key.GetValue("TaskbarGlomLevel");
                if (o != null && o is int i)
                {
                    onLeft = i > 0;
                }

                if (!onLeft)
                {
                    o = key.GetValue("TaskbarAl");
                    if (o != null && o is int j)
                    {
                        onLeft = j == 0;
                    }
                }
            }
        }
        catch (Exception)
        {
            // react appropriately
        }

        Microsoft.UI.Windowing.DisplayArea displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(_appWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
        if (displayArea is not null)
        {
            var centeredPosition = _appWindow.Position;
            if (onLeft)
            {
                centeredPosition.X = 16;
                centeredPosition.Y = displayArea.WorkArea.Height - _appWindow.Size.Height - 16;
            }
            else
            {
                centeredPosition.X = (displayArea.WorkArea.Width - _appWindow.Size.Width) / 2;
                centeredPosition.Y = displayArea.WorkArea.Height - _appWindow.Size.Height - 16;
            }

            _appWindow.Move(centeredPosition);
        }
    }

    private void MainViewModel_HideRequested(object sender, object? args)
    {
        Windows.Win32.PInvoke.ShowWindow(hwnd, Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_HIDE);
    }

    private static RectInt32 GetRect(Rect bounds, double scale)
    {
        return new RectInt32(
            _X: (int)Math.Round(bounds.X * scale),
            _Y: (int)Math.Round(bounds.Y * scale),
            _Width: (int)Math.Round(bounds.Width * scale),
            _Height: (int)Math.Round(bounds.Height * scale));
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            _configurationSource.IsInputActive = false;

            // If there's a debugger attached...
            if (System.Diagnostics.Debugger.IsAttached)
            {
                // ... then don't hide the window when it loses focus.
                return;
            }
            else
            {
                Windows.Win32.PInvoke.ShowWindow(hwnd, Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_HIDE);
            }
        }
        else
        {
            _configurationSource.IsInputActive = true;
        }
    }

    private static string KeybindingToString(VirtualKey key, VirtualKeyModifiers modifiers)
    {
        var keyString = key.ToString();
        if (keyString.Length == 1)
        {
            keyString = keyString.ToUpper(System.Globalization.CultureInfo.CurrentCulture);
        }
        else
        {
            keyString = Regex.Replace(keyString, "([a-z])([A-Z])", "$1+$2");
        }

        var modifierString = string.Empty;
        if (modifiers.HasFlag(VirtualKeyModifiers.Control))
        {
            modifierString += "ctrl+";
        }

        if (modifiers.HasFlag(VirtualKeyModifiers.Shift))
        {
            modifierString += "shift+";
        }

        if (modifiers.HasFlag(VirtualKeyModifiers.Menu))
        {
            modifierString += "alt+";
        }

        if (modifiers.HasFlag(VirtualKeyModifiers.Windows))
        {
            modifierString += "win+";
        }

        return modifierString + keyString;
    }

    private static (VirtualKey Key, VirtualKeyModifiers Modifiers) StringToKeybinding(string keybinding)
    {
        var parts = keybinding.Split('+');
        var modifiers = VirtualKeyModifiers.None;
        var key = VirtualKey.None;

        foreach (var part in parts)
        {
            switch (part.ToLower(System.Globalization.CultureInfo.CurrentCulture))
            {
                case "ctrl":
                    modifiers |= VirtualKeyModifiers.Control;
                    break;
                case "shift":
                    modifiers |= VirtualKeyModifiers.Shift;
                    break;
                case "alt":
                    modifiers |= VirtualKeyModifiers.Menu;
                    break;
                case "win":
                    modifiers |= VirtualKeyModifiers.Windows;
                    break;
                case ".":
                    key = (VirtualKey)DOT_KEY;
                    break;
                default:
                    key = (VirtualKey)Enum.Parse(typeof(VirtualKey), part, true);
                    break;
            }
        }

        return (key, modifiers);
    }

    private static (uint Vk, Windows.Win32.UI.Input.KeyboardAndMouse.HOT_KEY_MODIFIERS Mod) UwpToWin32(VirtualKey key, VirtualKeyModifiers modifiers)
    {
        Windows.Win32.UI.Input.KeyboardAndMouse.HOT_KEY_MODIFIERS mod = default;

        if (modifiers.HasFlag(VirtualKeyModifiers.Control))
        {
            mod |= Windows.Win32.UI.Input.KeyboardAndMouse.HOT_KEY_MODIFIERS.MOD_CONTROL;
        }

        if (modifiers.HasFlag(VirtualKeyModifiers.Shift))
        {
            mod |= Windows.Win32.UI.Input.KeyboardAndMouse.HOT_KEY_MODIFIERS.MOD_SHIFT;
        }

        if (modifiers.HasFlag(VirtualKeyModifiers.Menu))
        {
            mod |= Windows.Win32.UI.Input.KeyboardAndMouse.HOT_KEY_MODIFIERS.MOD_ALT;
        }

        if (modifiers.HasFlag(VirtualKeyModifiers.Windows))
        {
            mod |= Windows.Win32.UI.Input.KeyboardAndMouse.HOT_KEY_MODIFIERS.MOD_WIN;
        }

        return ((uint)key, mod);
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        Application.Current.GetService<IExtensionService>().SignalStopExtensionsAsync();

        // Log.Information("Terminating via MainWindow_Closed.");

        // WinUI bug is causing a crash on shutdown when FailFastOnErrors is set to true (#51773592).
        // Workaround by turning it off before shutdown.
        App.Current.DebugSettings.FailFastOnErrors = false;
        DisposeAcrylic();
    }

    private DesktopAcrylicController _acrylicController;
    private SystemBackdropConfiguration _configurationSource;

    // We want to use DesktopAcrylicKind.Thin and custom colors as this is the default material other Shell surfaces are using, this cannot be set in XAML however.
    private void SetAcrylic()
    {
        if (DesktopAcrylicController.IsSupported())
        {
            // Hooking up the policy object.
            _configurationSource = new SystemBackdropConfiguration();

            ((FrameworkElement)this.Content).ActualThemeChanged += MainWindow_ActualThemeChanged;

            // Initial configuration state.
            _configurationSource.IsInputActive = true;
            UpdateAcrylic();
        }
    }

    private void UpdateAcrylic()
    {
        _acrylicController = GetAcrylicConfig();

        // Enable the system backdrop.
        // Note: Be sure to have "using WinRT;" to support the Window.As<...>() call.
        _acrylicController.AddSystemBackdropTarget(this.As<ICompositionSupportsSystemBackdrop>());
        _acrylicController.SetSystemBackdropConfiguration(_configurationSource);
    }

    private DesktopAcrylicController GetAcrylicConfig()
    {
        if (((FrameworkElement)this.Content).ActualTheme == ElementTheme.Light)
        {
            return new DesktopAcrylicController()
            {
                Kind = DesktopAcrylicKind.Thin,
                TintColor = Windows.UI.Color.FromArgb(255, 243, 243, 243),
                LuminosityOpacity = 0.90f,
                TintOpacity = 0.0f,
                FallbackColor = Windows.UI.Color.FromArgb(255, 238, 238, 238),
            };
        }
        else
        {
            return new DesktopAcrylicController()
            {
                Kind = DesktopAcrylicKind.Thin,
                TintColor = Windows.UI.Color.FromArgb(255, 32, 32, 32),
                LuminosityOpacity = 0.96f,
                TintOpacity = 0.5f,
                FallbackColor = Windows.UI.Color.FromArgb(255, 28, 28, 28),
            };
        }
    }

    private void MainWindow_ActualThemeChanged(FrameworkElement sender, object args)
    {
        SetConfigurationSourceTheme(sender.ActualTheme);
        UpdateAcrylic();
    }

    private void SetConfigurationSourceTheme(ElementTheme theme)
    {
        switch (theme)
        {
            case ElementTheme.Dark: _configurationSource.Theme = SystemBackdropTheme.Dark; break;
            case ElementTheme.Light: _configurationSource.Theme = SystemBackdropTheme.Light; break;
            case ElementTheme.Default: _configurationSource.Theme = SystemBackdropTheme.Default; break;
        }
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
}
