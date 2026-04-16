// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCommon;

namespace ShowDesktop
{
    internal sealed class MouseHook : IDisposable
    {
        private const int DoubleClickTimeMs = 500;

        private IntPtr _hookId;
        private NativeMethods.LowLevelMouseProc? _proc;
        private DateTime _lastClickTime = DateTime.MinValue;
        private bool _disposed;

        public event Action<MouseHookEventArgs>? DesktopClicked;

        public void Install()
        {
            _proc = HookCallback;
            _hookId = NativeMethods.SetWindowsHookEx(
                NativeMethods.WH_MOUSE_LL,
                _proc,
                IntPtr.Zero,
                0);

            if (_hookId == IntPtr.Zero)
            {
                Logger.LogError($"Failed to install mouse hook. Error: {Marshal.GetLastWin32Error()}");
            }
            else
            {
                Logger.LogInfo("Mouse hook installed successfully.");
            }
        }

        public void Uninstall()
        {
            if (_hookId != IntPtr.Zero)
            {
                NativeMethods.UnhookWindowsHookEx(_hookId);
                _hookId = IntPtr.Zero;
                Logger.LogInfo("Mouse hook uninstalled.");
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                if (msg == NativeMethods.WM_LBUTTONDOWN || msg == NativeMethods.WM_LBUTTONDBLCLK)
                {
                    var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                    int x = hookStruct.pt.X;
                    int y = hookStruct.pt.Y;

                    bool isDoubleClick = msg == NativeMethods.WM_LBUTTONDBLCLK;
                    if (!isDoubleClick)
                    {
                        var now = DateTime.UtcNow;
                        if ((now - _lastClickTime).TotalMilliseconds <= DoubleClickTimeMs)
                        {
                            isDoubleClick = true;
                        }

                        _lastClickTime = now;
                    }

                    bool isDesktop = DesktopDetector.IsDesktopClick(x, y);
                    bool isTaskbar = !isDesktop && DesktopDetector.IsTaskbarClick(x, y);

                    if (isDesktop || isTaskbar)
                    {
                        var args = new MouseHookEventArgs
                        {
                            X = x,
                            Y = y,
                            IsDoubleClick = isDoubleClick,
                            IsTaskbar = isTaskbar,
                        };

                        DesktopClicked?.Invoke(args);
                    }
                }
            }

            return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Uninstall();
                _disposed = true;
            }
        }
    }
}
