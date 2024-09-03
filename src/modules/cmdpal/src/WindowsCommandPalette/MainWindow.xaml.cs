// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.CmdPal.Common.Contracts;
using Microsoft.CmdPal.Common.Extensions;
using Microsoft.CmdPal.Common.Services;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Graphics;
using Windows.System;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using WindowsCommandPalette.Views;

namespace DeveloperCommandPalette;

/// <summary>
/// An empty window that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    private readonly AppWindow m_AppWindow;

    private MainViewModel _mainViewModel { get; init; }

    private readonly HWND hwnd;
    private const uint DOT_KEY = 0xBE;
    private const uint WM_HOTKEY = 0x0312;
    private WNDPROC? origPrc;
    private WNDPROC? hotKeyPrc;

    private Windows.Win32.Foundation.LRESULT HotKeyPrc(Windows.Win32.Foundation.HWND hwnd,
            uint uMsg,
            Windows.Win32.Foundation.WPARAM wParam,
            Windows.Win32.Foundation.LPARAM lParam)
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
        //Windows.Win32.PInvoke.SetFocus(hwnd);
        Windows.Win32.PInvoke.SetActiveWindow(hwnd);
        MainPage.ViewModel.Summon();
    }

    public MainWindow()
    {
        this.InitializeComponent();
        this._mainViewModel = MainPage.ViewModel;


         hwnd = new Windows.Win32.Foundation.HWND(WinRT.Interop.WindowNative.GetWindowHandle(this).ToInt32());

        _ = SetupHotkey();

        // Assumes "this" is a XAML Window. In projects that don't use
        // WinUI 3 1.3 or later, use interop APIs to get the AppWindow.
        m_AppWindow = this.AppWindow;

        Activated += MainWindow_Activated;
        AppTitleBar.SizeChanged += AppTitleBar_SizeChanged;

        ExtendsContentIntoTitleBar = true;
        // Hide our titlebar. We'll make the sides draggable later
        m_AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Collapsed;
        AppTitleTextBlock.Text = AppInfo.Current.DisplayInfo.DisplayName;

        m_AppWindow.Title = AppTitleTextBlock.Text;

        Application.Current.GetService<ILocalSettingsService>().SaveSettingAsync("ThisIsAVeryBizarreString", true);

        //PositionForStartMenu();
        PositionCentered();
        _mainViewModel.HideRequested += _mainViewModel_HideRequested;

        _mainViewModel.QuitRequested += (s, e) =>
        {
            this.Close();
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
        m_AppWindow.Resize(new SizeInt32 { Width = 860, Height = 512 });
        DisplayArea displayArea = DisplayArea.GetFromWindowId(m_AppWindow.Id, DisplayAreaFallback.Nearest);
        if (displayArea is not null)
        {
            var CenteredPosition = AppWindow.Position;
            CenteredPosition.X = ((displayArea.WorkArea.Width - AppWindow.Size.Width) / 2);
            CenteredPosition.Y = ((displayArea.WorkArea.Height - AppWindow.Size.Height) / 2);
            AppWindow.Move(CenteredPosition);
        }
    }

    private void PositionForStartMenu()
    {
        m_AppWindow.Resize(new Windows.Graphics.SizeInt32(768, 768));

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
                if (o != null && o is Int32 i)
                {
                    onLeft = i > 0;
                }
                if (!onLeft)
                {
                    o = key.GetValue("TaskbarAl");
                    if (o != null && o is Int32 j)
                    {
                        onLeft = j == 0;
                    }
                }
            }
        }
        catch (Exception)
        {
            //react appropriately
        }

        Microsoft.UI.Windowing.DisplayArea displayArea = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(m_AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest);
        if (displayArea is not null)
        {
            var CenteredPosition = m_AppWindow.Position;
            if (onLeft)
            {
                CenteredPosition.X = 16;
                CenteredPosition.Y = ((displayArea.WorkArea.Height - m_AppWindow.Size.Height) - 16);
            }
            else
            {
                CenteredPosition.X = ((displayArea.WorkArea.Width - m_AppWindow.Size.Width) / 2);
                CenteredPosition.Y = ((displayArea.WorkArea.Height - m_AppWindow.Size.Height) - 16);
            }
            m_AppWindow.Move(CenteredPosition);
        }
    }

    private void _mainViewModel_HideRequested(object sender, object? args)
    {
        Windows.Win32.PInvoke.ShowWindow(hwnd, Windows.Win32.UI.WindowsAndMessaging.SHOW_WINDOW_CMD.SW_HIDE);
    }

    private void AppTitleBar_Loaded(object sender, RoutedEventArgs e)
    {
        if (ExtendsContentIntoTitleBar == true)
        {
            // Set the initial interactive regions.
            SetRegionsForCustomTitleBar();
        }
    }

    private void AppTitleBar_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (ExtendsContentIntoTitleBar == true)
        {
            // Update interactive regions if the size of the window changes.
            SetRegionsForCustomTitleBar();
        }
    }

    private void SetRegionsForCustomTitleBar()
    {
        // Specify the interactive regions of the title bar.

        var scaleAdjustment = AppTitleBar.XamlRoot.RasterizationScale;

        RightPaddingColumn.Width = new GridLength(m_AppWindow.TitleBar.RightInset / scaleAdjustment);
        LeftPaddingColumn.Width = new GridLength(m_AppWindow.TitleBar.LeftInset / scaleAdjustment);

        //// Get the rectangle around the content
        GeneralTransform transform = MainPage.TransformToVisual(null);
        Rect bounds = transform.TransformBounds(new Rect(0, 0,
                                                         MainPage.ActualWidth,
                                                         MainPage.ActualHeight));
        Windows.Graphics.RectInt32 contentRect = GetRect(bounds, scaleAdjustment);

        var rectArray = new Windows.Graphics.RectInt32[] { contentRect };

        InputNonClientPointerSource nonClientInputSrc =
            InputNonClientPointerSource.GetForWindowId(this.AppWindow.Id);
        nonClientInputSrc.SetRegionRects(NonClientRegionKind.Passthrough, rectArray);

        // Add four drag-able regions, around the sides of our content
        var w = ContentGrid.ActualWidth;
        var h = ContentGrid.ActualHeight;
        var dragSides = new Windows.Graphics.RectInt32[] {
            GetRect(new Rect(0, 0, w, 24), scaleAdjustment),
            GetRect(new Rect(0, h-24, ContentGrid.ActualWidth, 24), scaleAdjustment),
            GetRect(new Rect(0, 0, 24, h), scaleAdjustment),
            GetRect(new Rect(w-24, 0, 24, h), scaleAdjustment),
        };
        nonClientInputSrc.SetRegionRects(NonClientRegionKind.Caption, dragSides);
    }

    private static Windows.Graphics.RectInt32 GetRect(Rect bounds, double scale)
    {
        return new Windows.Graphics.RectInt32(
            _X: (int)Math.Round(bounds.X * scale),
            _Y: (int)Math.Round(bounds.Y * scale),
            _Width: (int)Math.Round(bounds.Width * scale),
            _Height: (int)Math.Round(bounds.Height * scale)
        );
    }

    private void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            AppTitleTextBlock.Foreground =
                (SolidColorBrush)App.Current.Resources["WindowCaptionForegroundDisabled"];

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
            AppTitleTextBlock.Foreground =
                (SolidColorBrush)App.Current.Resources["WindowCaptionForeground"];
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

    private static (VirtualKey key, VirtualKeyModifiers modifiers) StringToKeybinding(string keybinding)
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

    private static (uint vk, Windows.Win32.UI.Input.KeyboardAndMouse.HOT_KEY_MODIFIERS mod) UwpToWin32(VirtualKey key, VirtualKeyModifiers modifiers)
    {
        Windows.Win32.UI.Input.KeyboardAndMouse.HOT_KEY_MODIFIERS mod = new();
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
    }

}
