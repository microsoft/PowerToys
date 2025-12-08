// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.UI.Xaml;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;

namespace PowerDisplay.Helpers
{
    /// <summary>
    /// Window procedure delegate for handling window messages.
    /// Uses primitive types to avoid accessibility issues with CsWin32-generated types.
    /// </summary>
    /// <param name="hwnd">Handle to the window.</param>
    /// <param name="msg">The message.</param>
    /// <param name="wParam">Additional message information.</param>
    /// <param name="lParam">Additional message.</param>
    /// <returns>The result of the message processing.</returns>
    internal delegate nint WndProcDelegate(nint hwnd, uint msg, nuint wParam, nint lParam);

    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1310:Field names should not contain underscore", Justification = "Stylistically, window messages are WM_*")]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1306:Field names should begin with lower-case letter", Justification = "Stylistically, window messages are WM_*")]
    internal sealed partial class TrayIconService
    {
        private const uint MY_NOTIFY_ID = 1001;
        private const uint WM_TRAY_ICON = PInvoke.WM_USER + 1;

        private readonly ISettingsUtils _settingsUtils;
        private readonly Action _toggleWindowAction;
        private readonly Action _exitAction;
        private readonly Action _openSettingsAction;
        private readonly uint WM_TASKBAR_RESTART;

        private Window? _window;
        private nint _hwnd;
        private nint _originalWndProc;
        private WndProcDelegate? _trayWndProc;
        private NOTIFYICONDATAW? _trayIconData;
        private nint _largeIcon;
        private nint _popupMenu;

        public TrayIconService(
            ISettingsUtils settingsUtils,
            Action showWindowAction,
            Action toggleWindowAction,
            Action exitAction,
            Action openSettingsAction)
        {
            _settingsUtils = settingsUtils;
            _toggleWindowAction = toggleWindowAction;
            _exitAction = exitAction;
            _openSettingsAction = openSettingsAction;

            // TaskbarCreated is the message that's broadcast when explorer.exe
            // restarts. We need to know when that happens to be able to bring our
            // notification area icon back
            WM_TASKBAR_RESTART = RegisterWindowMessageNative("TaskbarCreated");
        }

        public void SetupTrayIcon(bool? showSystemTrayIcon = null)
        {
            var settings = _settingsUtils.GetSettingsOrDefault<PowerDisplaySettings>(PowerDisplaySettings.ModuleName);
            bool shouldShow = showSystemTrayIcon ?? settings.Properties.ShowSystemTrayIcon;

            if (shouldShow)
            {
                if (_window is null)
                {
                    _window = new Window();
                    _hwnd = WindowNative.GetWindowHandle(_window);

                    // LOAD BEARING: If you don't stick the pointer to HotKeyPrc into a
                    // member (and instead like, use a local), then the pointer we marshal
                    // into the WindowLongPtr will be useless after we leave this function,
                    // and our **WindProc will explode**.
                    _trayWndProc = WindowProc;
                    var hotKeyPrcPointer = Marshal.GetFunctionPointerForDelegate(_trayWndProc);
                    _originalWndProc = SetWindowLongPtrNative(_hwnd, GWL_WNDPROC, hotKeyPrcPointer);
                }

                if (_trayIconData is null)
                {
                    // We need to stash this handle, so it doesn't clean itself up. If
                    // explorer restarts, we'll come back through here, and we don't
                    // really need to re-load the icon in that case. We can just use
                    // the handle from the first time.
                    _largeIcon = GetAppIconHandle();
                    unsafe
                    {
                        _trayIconData = new NOTIFYICONDATAW()
                        {
                            cbSize = (uint)sizeof(NOTIFYICONDATAW),
                            hWnd = new HWND(_hwnd),
                            uID = MY_NOTIFY_ID,
                            uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP,
                            uCallbackMessage = WM_TRAY_ICON,
                            hIcon = new HICON(_largeIcon),
                            szTip = GetString("AppName"),
                        };
                    }
                }

                var d = (NOTIFYICONDATAW)_trayIconData;

                // Add the notification icon
                unsafe
                {
                    Shell_NotifyIconNative((uint)NOTIFY_ICON_MESSAGE.NIM_ADD, &d);
                }

                if (_popupMenu == 0)
                {
                    _popupMenu = CreatePopupMenu();
                    InsertMenuNative(_popupMenu, 0, (uint)(MENU_ITEM_FLAGS.MF_BYPOSITION | MENU_ITEM_FLAGS.MF_STRING), PInvoke.WM_USER + 1, GetString("TrayMenu_Settings"));
                    InsertMenuNative(_popupMenu, 1, (uint)(MENU_ITEM_FLAGS.MF_BYPOSITION | MENU_ITEM_FLAGS.MF_STRING), PInvoke.WM_USER + 2, GetString("TrayMenu_Exit"));
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
                unsafe
                {
                    if (Shell_NotifyIconNative((uint)NOTIFY_ICON_MESSAGE.NIM_DELETE, &d))
                    {
                        _trayIconData = null;
                    }
                }
            }

            if (_popupMenu != 0)
            {
                DestroyMenu(_popupMenu);
                _popupMenu = 0;
            }

            if (_largeIcon != 0)
            {
                DestroyIcon(_largeIcon);
                _largeIcon = 0;
            }

            if (_window is not null)
            {
                _window.Close();
                _window = null;
                _hwnd = 0;
            }
        }

        private static string GetString(string key)
        {
            try
            {
                return ResourceLoaderInstance.ResourceLoader.GetString(key);
            }
            catch
            {
                return "unknown";
            }
        }

        private nint GetAppIconHandle()
        {
            var exePath = Path.Combine(AppContext.BaseDirectory, "PowerToys.PowerDisplay.exe");
            ExtractIconExNative(exePath, 0, out var largeIcon, out _, 1);
            return largeIcon;
        }

        private nint WindowProc(
            nint hwnd,
            uint uMsg,
            nuint wParam,
            nint lParam)
        {
            switch (uMsg)
            {
                case PInvoke.WM_COMMAND:
                    {
                        if (wParam == PInvoke.WM_USER + 1)
                        {
                            // Settings menu item
                            Logger.LogInfo("[TrayIcon] Settings menu clicked");
                            _openSettingsAction?.Invoke();
                        }
                        else if (wParam == PInvoke.WM_USER + 2)
                        {
                            // Exit menu item
                            Logger.LogInfo("[TrayIcon] Exit menu clicked");
                            _exitAction?.Invoke();
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
                        Logger.LogInfo("[TrayIcon] Taskbar restarted, recreating tray icon");
                        SetupTrayIcon();
                    }
                    else if (uMsg == WM_TRAY_ICON)
                    {
                        switch ((uint)lParam)
                        {
                            case PInvoke.WM_RBUTTONUP:
                                {
                                    if (_popupMenu != 0)
                                    {
                                        GetCursorPos(out var cursorPos);
                                        SetForegroundWindow(_hwnd);
                                        TrackPopupMenuExNative(_popupMenu, (uint)TRACK_POPUP_MENU_FLAGS.TPM_LEFTALIGN | (uint)TRACK_POPUP_MENU_FLAGS.TPM_BOTTOMALIGN, cursorPos.X, cursorPos.Y, _hwnd, 0);
                                    }
                                }

                                break;
                            case PInvoke.WM_LBUTTONUP:
                            case PInvoke.WM_LBUTTONDBLCLK:
                                Logger.LogInfo("[TrayIcon] Left click/double click - toggling window");
                                _toggleWindowAction?.Invoke();
                                break;
                        }
                    }

                    break;
            }

            return CallWindowProcIntPtr(_originalWndProc, hwnd, uMsg, wParam, lParam);
        }

        [LibraryImport("user32.dll", EntryPoint = "CallWindowProcW")]
        private static partial nint CallWindowProcIntPtr(IntPtr lpPrevWndFunc, nint hWnd, uint msg, nuint wParam, nint lParam);

        [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
        private static partial uint RegisterWindowMessageNative(string lpString);

        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        private static partial nint SetWindowLongPtrNative(nint hWnd, int nIndex, nint dwNewLong);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool GetCursorPos(out POINT lpPoint);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetForegroundWindow(nint hWnd);

        // Shell APIs - use uint for enums and unsafe pointer for struct
        [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static unsafe partial bool Shell_NotifyIconNative(uint dwMessage, NOTIFYICONDATAW* lpData);

        [LibraryImport("shell32.dll", EntryPoint = "ExtractIconExW", StringMarshalling = StringMarshalling.Utf16)]
        private static partial uint ExtractIconExNative(string lpszFile, int nIconIndex, out nint phiconLarge, out nint phiconSmall, uint nIcons);

        // Menu APIs
        [LibraryImport("user32.dll")]
        private static partial nint CreatePopupMenu();

        [LibraryImport("user32.dll", EntryPoint = "InsertMenuW", StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool InsertMenuNative(nint hMenu, uint uPosition, uint uFlags, nuint uIDNewItem, string? lpNewItem);

        [LibraryImport("user32.dll", EntryPoint = "TrackPopupMenuEx")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool TrackPopupMenuExNative(nint hMenu, uint uFlags, int x, int y, nint hwnd, nint lptpm);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool DestroyMenu(nint hMenu);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool DestroyIcon(nint hIcon);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        // GWL_WNDPROC constant
        private const int GWL_WNDPROC = -4;
    }
}
