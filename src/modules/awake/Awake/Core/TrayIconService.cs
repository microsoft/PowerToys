// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.UI.Xaml;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;

namespace Awake.Core
{
    /// <summary>
    /// Window-procedure delegate exposed via primitive types so it can interop with
    /// CsWin32's SetWindowLongPtr without accessibility issues.
    /// </summary>
    /// <param name="hwnd">Handle to the window.</param>
    /// <param name="msg">The message identifier.</param>
    /// <param name="wParam">Additional message information.</param>
    /// <param name="lParam">Additional message information.</param>
    /// <returns>The result of the message processing.</returns>
    internal delegate nint AwakeTrayWndProcDelegate(nint hwnd, uint msg, nuint wParam, nint lParam);

    /// <summary>
    /// Owns the Awake notification-area icon. Mirrors PowerDisplay's TrayIconService:
    /// a hidden helper Window receives Shell_NotifyIcon callbacks; both left- and
    /// right-clicks toggle the WinUI flyout. The legacy HMENU is gone.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicProperties)]
    internal sealed partial class TrayIconService
    {
        private const uint MyNotifyId = 1000;
        private const uint WmTrayIcon = PInvoke.WM_USER + 1;

        private readonly Action _toggleWindow;
        private readonly uint _wmTaskbarRestart;

        private Window? _window;
        private nint _hwnd;
        private nint _originalWndProc;
        private AwakeTrayWndProcDelegate? _trayWndProc;
        private NOTIFYICONDATAW? _trayIconData;
        private string _currentTooltip = string.Empty;
        private nint _currentIconHandle;

        public static readonly Icon DefaultIcon = new(Path.Combine(AppContext.BaseDirectory, "Assets", "Awake", "Awake.ico"));
        public static readonly Icon TimedIcon = new(Path.Combine(AppContext.BaseDirectory, "Assets", "Awake", "timed.ico"));
        public static readonly Icon ExpirableIcon = new(Path.Combine(AppContext.BaseDirectory, "Assets", "Awake", "expirable.ico"));
        public static readonly Icon IndefiniteIcon = new(Path.Combine(AppContext.BaseDirectory, "Assets", "Awake", "indefinite.ico"));
        public static readonly Icon DisabledIcon = new(Path.Combine(AppContext.BaseDirectory, "Assets", "Awake", "disabled.ico"));

        public TrayIconService(Action toggleWindow)
        {
            _toggleWindow = toggleWindow ?? throw new ArgumentNullException(nameof(toggleWindow));
            _wmTaskbarRestart = RegisterWindowMessageNative("TaskbarCreated");
        }

        public void SetupTrayIcon(string tooltip, Icon icon)
        {
            if (_window is null)
            {
                _window = new Window();
                _hwnd = WindowNative.GetWindowHandle(_window);

                // LOAD BEARING: store the delegate in a field so the marshaled pointer
                // we hand to SetWindowLongPtr survives past this stack frame.
                _trayWndProc = WindowProc;
                var trayWndProcPointer = Marshal.GetFunctionPointerForDelegate(_trayWndProc);
                _originalWndProc = SetWindowLongPtrNative(_hwnd, GwlWndproc, trayWndProcPointer);
            }

            _currentTooltip = tooltip;
            _currentIconHandle = icon.Handle;

            if (!CreateOrUpdateTrayIcon(isAdd: true))
            {
                // Shell can refuse NIM_ADD during explorer startup; we'll retry from WM_WINDOWPOSCHANGING / TaskbarCreated.
                Logger.LogWarning("[Awake] Shell_NotifyIcon(NIM_ADD) failed; will retry when shell is ready");
                _trayIconData = null;
            }
        }

        public void UpdateIcon(Icon icon, string tooltip)
        {
            _currentIconHandle = icon.Handle;
            _currentTooltip = tooltip;

            if (_trayIconData is null)
            {
                // No icon registered yet; try to add it now.
                CreateOrUpdateTrayIcon(isAdd: true);
                return;
            }

            CreateOrUpdateTrayIcon(isAdd: false);
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

            if (_window is not null)
            {
                _window.Close();
                _window = null;
                _hwnd = 0;
            }
        }

        private bool CreateOrUpdateTrayIcon(bool isAdd)
        {
            unsafe
            {
                var data = new NOTIFYICONDATAW
                {
                    cbSize = (uint)sizeof(NOTIFYICONDATAW),
                    hWnd = new HWND(_hwnd),
                    uID = MyNotifyId,
                    uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP,
                    uCallbackMessage = WmTrayIcon,
                    hIcon = new HICON(_currentIconHandle),
                    szTip = _currentTooltip ?? string.Empty,
                };

                bool success = Shell_NotifyIconNative(
                    isAdd ? (uint)NOTIFY_ICON_MESSAGE.NIM_ADD : (uint)NOTIFY_ICON_MESSAGE.NIM_MODIFY,
                    &data);

                if (success)
                {
                    _trayIconData = data;
                }

                return success;
            }
        }

        private nint WindowProc(nint hwnd, uint uMsg, nuint wParam, nint lParam)
        {
            switch (uMsg)
            {
                // Shell can refuse NIM_ADD during explorer startup; WM_WINDOWPOSCHANGING is the first
                // reliable signal that the shell is ready, so re-attempt the add there.
                case PInvoke.WM_WINDOWPOSCHANGING:
                    if (_trayIconData is null && _currentIconHandle != 0)
                    {
                        CreateOrUpdateTrayIcon(isAdd: true);
                    }

                    break;

                default:
                    if (uMsg == _wmTaskbarRestart)
                    {
                        Logger.LogInfo("[Awake] TaskbarCreated received; re-adding tray icon");
                        _trayIconData = null;
                        if (_currentIconHandle != 0)
                        {
                            CreateOrUpdateTrayIcon(isAdd: true);
                        }
                    }
                    else if (uMsg == WmTrayIcon)
                    {
                        // Per Awake spec (#28530): both buttons open the flyout, no Win32 menu.
                        switch ((uint)lParam)
                        {
                            case PInvoke.WM_LBUTTONUP:
                            case PInvoke.WM_RBUTTONUP:
                                _toggleWindow?.Invoke();
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

        [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static unsafe partial bool Shell_NotifyIconNative(uint dwMessage, NOTIFYICONDATAW* lpData);

        private const int GwlWndproc = -4;
    }
}
