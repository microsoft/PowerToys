// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
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
internal sealed partial class TrayIconService
{
    private const uint MY_NOTIFY_ID = 1000;
    private const uint WM_TRAY_ICON = PInvoke.WM_USER + 1;

    private readonly SettingsModel _settingsModel;
    private readonly uint WM_TASKBAR_RESTART;

    private Window? _window;
    private HWND _hwnd;
    private WNDPROC? _originalWndProc;
    private WNDPROC? _trayWndProc;
    private NOTIFYICONDATAW? _trayIconData;
    private DestroyIconSafeHandle? _largeIcon;
    private DestroyMenuSafeHandle? _popupMenu;

    public TrayIconService(SettingsModel settingsModel)
    {
        _settingsModel = settingsModel;

        // TaskbarCreated is the message that's broadcast when explorer.exe
        // restarts. We need to know when that happens to be able to bring our
        // notification area icon back
        WM_TASKBAR_RESTART = PInvoke.RegisterWindowMessage("TaskbarCreated");
    }

    public void SetupTrayIcon(bool? showSystemTrayIcon = null)
    {
        if (showSystemTrayIcon ?? _settingsModel.ShowSystemTrayIcon)
        {
            if (_window is null)
            {
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

            if (_trayIconData is null)
            {
                // We need to stash this handle, so it doesn't clean itself up. If
                // explorer restarts, we'll come back through here, and we don't
                // really need to re-load the icon in that case. We can just use
                // the handle from the first time.
                _largeIcon = GetAppIconHandle();
                _trayIconData = new NOTIFYICONDATAW()
                {
                    cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATAW>(),
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
            PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, in d);

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
            }
        }

        if (_popupMenu is not null)
        {
            _popupMenu.Close();
            _popupMenu = null;
        }

        if (_largeIcon is not null)
        {
            _largeIcon.Close();
            _largeIcon = null;
        }

        if (_window is not null)
        {
            _window.Close();
            _window = null;
            _hwnd = HWND.Null;
        }
    }

    private DestroyIconSafeHandle GetAppIconHandle()
    {
        var exePath = Path.Combine(AppContext.BaseDirectory, "Microsoft.CmdPal.UI.exe");
        DestroyIconSafeHandle largeIcon;
        PInvoke.ExtractIconEx(exePath, 0, out largeIcon, out _, 1);
        return largeIcon;
    }

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
                        WeakReferenceMessenger.Default.Send<OpenSettingsMessage>();
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
