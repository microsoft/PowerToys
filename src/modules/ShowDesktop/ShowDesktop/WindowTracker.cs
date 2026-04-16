// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ManagedCommon;

namespace ShowDesktop
{
    internal sealed class WindowTracker
    {
        private readonly List<WindowInfo> _savedWindows = new();

        public bool HasSavedWindows => _savedWindows.Count > 0;

        /// <summary>
        /// Captures all visible application windows and either minimizes or moves them off-screen.
        /// </summary>
        public void CaptureAndMinimize(PeekMode mode)
        {
            _savedWindows.Clear();

            NativeMethods.EnumWindows(
                (hwnd, _) =>
                {
                    if (!ShouldTrack(hwnd))
                    {
                        return true;
                    }

                    var placement = NativeMethods.WINDOWPLACEMENT.Default;
                    NativeMethods.GetWindowPlacement(hwnd, ref placement);
                    _savedWindows.Add(new WindowInfo(hwnd, placement));

                    switch (mode)
                    {
                        case PeekMode.Minimize:
                            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_MINIMIZE);
                            break;

                        case PeekMode.FlyAway:
                            NativeMethods.ShowWindow(hwnd, NativeMethods.SW_MINIMIZE);
                            break;
                    }

                    return true;
                },
                IntPtr.Zero);

            Logger.LogInfo($"Captured {_savedWindows.Count} windows (mode={mode}).");
        }

        /// <summary>
        /// Restores all previously saved windows to their original positions.
        /// </summary>
        public void Restore(PeekMode mode)
        {
            for (int i = _savedWindows.Count - 1; i >= 0; i--)
            {
                var info = _savedWindows[i];
                var placement = info.Placement;
                NativeMethods.SetWindowPlacement(info.Hwnd, ref placement);
            }

            Logger.LogInfo($"Restored {_savedWindows.Count} windows.");
            _savedWindows.Clear();
        }

        /// <summary>
        /// Uses Win+D key combination to toggle show desktop (native Windows behavior).
        /// </summary>
        public static void SendShowDesktop()
        {
            var inputs = new NativeMethods.INPUT[4];

            // Win key down
            inputs[0].type = NativeMethods.INPUT_KEYBOARD;
            inputs[0].u.ki.wVk = NativeMethods.VK_LWIN;
            inputs[0].u.ki.dwFlags = 0;

            // D key down
            inputs[1].type = NativeMethods.INPUT_KEYBOARD;
            inputs[1].u.ki.wVk = NativeMethods.VK_D;
            inputs[1].u.ki.dwFlags = 0;

            // D key up
            inputs[2].type = NativeMethods.INPUT_KEYBOARD;
            inputs[2].u.ki.wVk = NativeMethods.VK_D;
            inputs[2].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

            // Win key up
            inputs[3].type = NativeMethods.INPUT_KEYBOARD;
            inputs[3].u.ki.wVk = NativeMethods.VK_LWIN;
            inputs[3].u.ki.dwFlags = NativeMethods.KEYEVENTF_KEYUP;

            uint sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
            if (sent != inputs.Length)
            {
                Logger.LogError($"SendInput sent only {sent}/{inputs.Length} inputs. Error: {Marshal.GetLastWin32Error()}");
            }
            else
            {
                Logger.LogInfo("Sent Win+D (native show desktop).");
            }
        }

        /// <summary>
        /// Determines whether a window should be tracked for minimize/restore.
        /// </summary>
        private static bool ShouldTrack(IntPtr hwnd)
        {
            // Must be visible
            if (!NativeMethods.IsWindowVisible(hwnd))
            {
                return false;
            }

            // Skip already-minimized windows
            if (NativeMethods.IsIconic(hwnd))
            {
                return false;
            }

            // Skip cloaked windows (UWP suspended, virtual desktop, etc.)
            if (IsCloaked(hwnd))
            {
                return false;
            }

            // Must be a top-level "app" window: has no owner and is either WS_EX_APPWINDOW
            // or not a tool window.
            long exStyle = NativeMethods.GetWindowLongA(hwnd, NativeMethods.GWL_EXSTYLE);
            IntPtr owner = NativeMethods.GetWindow(hwnd, NativeMethods.GW_OWNER);

            bool isToolWindow = (exStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0;
            bool isAppWindow = (exStyle & NativeMethods.WS_EX_APPWINDOW) != 0;
            bool isNoActivate = (exStyle & NativeMethods.WS_EX_NOACTIVATE) != 0;

            if (isNoActivate && !isAppWindow)
            {
                return false;
            }

            if (owner != IntPtr.Zero && !isAppWindow)
            {
                return false;
            }

            if (isToolWindow && !isAppWindow)
            {
                return false;
            }

            // Skip shell windows (desktop, taskbar)
            if (IsShellWindow(hwnd))
            {
                return false;
            }

            // Must have a non-empty title
            int titleLength = NativeMethods.GetWindowTextLengthA(hwnd);
            if (titleLength == 0)
            {
                return false;
            }

            return true;
        }

        private static bool IsCloaked(IntPtr hwnd)
        {
            int hr = NativeMethods.DwmGetWindowAttribute(
                hwnd,
                NativeMethods.DWMWA_CLOAKED,
                out int cloaked,
                sizeof(int));

            return hr == 0 && cloaked != 0;
        }

        private static bool IsShellWindow(IntPtr hwnd)
        {
            IntPtr shellWindow = NativeMethods.GetShellWindow();
            if (hwnd == shellWindow)
            {
                return true;
            }

            string className = GetClassName(hwnd);
            return className is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
        }

        private static string GetClassName(IntPtr hwnd)
        {
            var buffer = new char[256];
            int length = NativeMethods.GetClassName(hwnd, buffer, buffer.Length);
            return length > 0 ? new string(buffer, 0, length) : string.Empty;
        }
    }
}
