// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using ManagedCommon;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.UI.Xaml;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI.Helpers;

[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Stylistically, window messages are WM_*")]
[SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:Field names should begin with lower-case letter", Justification = "Stylistically, window messages are WM_*")]
[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "TrayIconService uses explicit Destroy() lifecycle matching other PowerToys tray helpers")]
internal sealed partial class TrayIconService
{
    private const uint MY_NOTIFY_ID = 1000;
    private const uint WM_TRAY_ICON = PInvoke.WM_USER + 1;

    private readonly ISettingsService _settingsService;
    private readonly uint WM_TASKBAR_RESTART;
    private readonly string _whiteIconPath;
    private readonly string _darkIconPath;

    private Window? _window;
    private HWND _hwnd;
    private WNDPROC? _originalWndProc;
    private WNDPROC? _trayWndProc;
    private NOTIFYICONDATAW? _trayIconData;
    private DestroyMenuSafeHandle? _popupMenu;
    private nint _trayIconHandle;
    private bool _themeAdaptiveEnabled;
    private ThemeListener? _themeListener;
    private bool _trayIconAdded;

    public TrayIconService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _whiteIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "iconWhite.ico");
        _darkIconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "iconDark.ico");

        // TaskbarCreated is the message that's broadcast when explorer.exe
        // restarts. We need to know when that happens to be able to bring our
        // notification area icon back
        WM_TASKBAR_RESTART = PInvoke.RegisterWindowMessage("TaskbarCreated");
    }

    public void SetupTrayIcon(bool? showSystemTrayIcon = null)
    {
        if (showSystemTrayIcon ?? _settingsService.Settings.ShowSystemTrayIcon)
        {
            EnsureTrayWindow();
            SetThemeAdaptiveTrayIcon(_settingsService.Settings.ShowThemeAdaptiveTrayIcon);

            if (_trayIconData is null)
            {
                _trayIconData = new NOTIFYICONDATAW()
                {
                    cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
                    hWnd = _hwnd,
                    uID = MY_NOTIFY_ID,
                    uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP,
                    uCallbackMessage = WM_TRAY_ICON,
                    hIcon = (HICON)_trayIconHandle,
                    szTip = RS_.GetString("AppStoreName"),
                };
            }
            else
            {
                UpdateTrayIconHandle();
            }

            var d = (NOTIFYICONDATAW)_trayIconData;

            // Add or update the notification icon
            PInvoke.Shell_NotifyIcon(
                _trayIconAdded ? NOTIFY_ICON_MESSAGE.NIM_MODIFY : NOTIFY_ICON_MESSAGE.NIM_ADD,
                in d);
            _trayIconAdded = true;

            if (_popupMenu is null)
            {
                _popupMenu = PInvoke.CreatePopupMenu_SafeHandle();
                PInvoke.InsertMenu(_popupMenu, 0, MENU_ITEM_FLAGS.MF_BYPOSITION | MENU_ITEM_FLAGS.MF_STRING, PInvoke.WM_USER + 1, RS_.GetString("TrayMenu_Settings"));
                PInvoke.InsertMenu(_popupMenu, 1, MENU_ITEM_FLAGS.MF_BYPOSITION | MENU_ITEM_FLAGS.MF_STRING, PInvoke.WM_USER + 2, RS_.GetString("TrayMenu_Close"));
            }
        }
        else
        {
            Destroy();
        }
    }

    public void Destroy()
    {
        if (_trayIconData is not null)
        {
            var d = (NOTIFYICONDATAW)_trayIconData;
            if (PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, in d))
            {
                _trayIconData = null;
                _trayIconAdded = false;
            }
        }

        if (_popupMenu is not null)
        {
            _popupMenu.Close();
            _popupMenu = null;
        }

        DisposeThemeListener();
        ThemeAdaptiveTrayIconHelper.DestroyIconHandle(_trayIconHandle);
        _trayIconHandle = 0;

        if (_window is not null)
        {
            _window.Close();
            _window = null;
            _hwnd = HWND.Null;
        }
    }

    private void EnsureTrayWindow()
    {
        if (_window is not null)
        {
            return;
        }

        _window = new Window();
        _hwnd = new HWND(WindowNative.GetWindowHandle(_window));

        // LOAD BEARING: If you don't stick the pointer to HotKeyPrc into a
        // member (and instead like, use a local), then the pointer we marshal
        // into the WindowLongPtr will be useless after we leave this function,
        // and our **WindProc will explode**.
        _trayWndProc = WindowProc;
        var hotKeyPrcPointer = Marshal.GetFunctionPointerForDelegate(_trayWndProc);
        _originalWndProc = Marshal.GetDelegateForFunctionPointer<WNDPROC>(PInvoke.SetWindowLongPtr(_hwnd, WINDOW_LONG_PTR_INDEX.GWL_WNDPROC, hotKeyPrcPointer));
    }

    private void SetThemeAdaptiveTrayIcon(bool themeAdaptive)
    {
        _themeAdaptiveEnabled = themeAdaptive;
        ReloadTrayIconHandle();

        if (themeAdaptive)
        {
            if (_themeListener == null)
            {
                _themeListener = new ThemeListener();
                _themeListener.SystemThemeChanged += OnSystemThemeChanged;
            }
        }
        else
        {
            DisposeThemeListener();
        }
    }

    private void ReloadTrayIconHandle()
    {
        ThemeAdaptiveTrayIconHelper.DestroyIconHandle(_trayIconHandle);
        _trayIconHandle = ThemeAdaptiveTrayIconHelper.LoadIconHandle(
            _themeAdaptiveEnabled,
            _whiteIconPath,
            _darkIconPath,
            ExtractAppIconHandle);
    }

    private void OnSystemThemeChanged(ThemeListener sender)
    {
        if (!_themeAdaptiveEnabled)
        {
            return;
        }

        ReloadTrayIconHandle();
        UpdateTrayIconHandle();
    }

    private void DisposeThemeListener()
    {
        if (_themeListener == null)
        {
            return;
        }

        _themeListener.SystemThemeChanged -= OnSystemThemeChanged;
        _themeListener.Dispose();
        _themeListener = null;
    }

    private void UpdateTrayIconHandle()
    {
        if (_trayIconData is null || _trayIconHandle == 0)
        {
            return;
        }

        var d = (NOTIFYICONDATAW)_trayIconData;
        d.hIcon = (HICON)_trayIconHandle;
        _trayIconData = d;

        if (_trayIconAdded)
        {
            PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_MODIFY, in d);
        }
    }

    private static nint ExtractAppIconHandle()
    {
        var exePath = Path.Combine(AppContext.BaseDirectory, "Microsoft.CmdPal.UI.exe");
        _ = ExtractIconExNative(exePath, 0, out var largeIcon, out _, 1);
        return largeIcon;
    }

    [LibraryImport("shell32.dll", EntryPoint = "ExtractIconExW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint ExtractIconExNative(string lpszFile, int nIconIndex, out nint phiconLarge, out nint phiconSmall, uint nIcons);

    private LRESULT WindowProc(
        HWND hwnd,
        uint uMsg,
        WPARAM wParam,
        LPARAM lParam)
    {
        switch (uMsg)
        {
            case PInvoke.WM_COMMAND:
                {
                    if (wParam == PInvoke.WM_USER + 1)
                    {
                        WeakReferenceMessenger.Default.Send(new OpenSettingsMessage());
                    }
                    else if (wParam == PInvoke.WM_USER + 2)
                    {
                        WeakReferenceMessenger.Default.Send<QuitMessage>();
                    }
                }

                break;

            // Shell_NotifyIcon can fail when we invoke it during the time explorer.exe isn't present/ready to handle it.
            // We'll also never receive WM_TASKBAR_RESTART message if the first call to Shell_NotifyIcon failed, so we use
            // WM_WINDOWPOSCHANGING which is always received on explorer startup sequence.
            case PInvoke.WM_WINDOWPOSCHANGING:
                {
                    if (_trayIconData is null)
                    {
                        SetupTrayIcon();
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
                    SetupTrayIcon();
                }
                else if (uMsg == WM_TRAY_ICON)
                {
                    switch ((uint)lParam.Value)
                    {
                        case PInvoke.WM_RBUTTONUP:
                            {
                                if (_popupMenu is not null)
                                {
                                    PInvoke.GetCursorPos(out var cursorPos);
                                    PInvoke.SetForegroundWindow(_hwnd);
                                    PInvoke.TrackPopupMenuEx(_popupMenu, (uint)TRACK_POPUP_MENU_FLAGS.TPM_LEFTALIGN | (uint)TRACK_POPUP_MENU_FLAGS.TPM_BOTTOMALIGN, cursorPos.X, cursorPos.Y, _hwnd, null);
                                }
                            }

                            break;
                        case PInvoke.WM_LBUTTONUP:
                        case PInvoke.WM_LBUTTONDBLCLK:
                            WeakReferenceMessenger.Default.Send<HotkeySummonMessage>(new(string.Empty, HWND.Null));
                            break;
                    }
                }

                break;
        }

        return PInvoke.CallWindowProc(_originalWndProc, hwnd, uMsg, wParam, lParam);
    }
}
