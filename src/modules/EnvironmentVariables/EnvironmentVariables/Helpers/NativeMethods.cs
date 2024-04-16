// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace EnvironmentVariables.Win32
{
    public static class NativeMethods
    {
        internal const int HWND_BROADCAST = 0xffff;

        internal delegate IntPtr WinProc(IntPtr hWnd, WindowMessage msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern int SendNotifyMessage(IntPtr hWnd, WindowMessage msg, IntPtr wParam, IntPtr lParam);

        [DllImport("User32.dll")]
        internal static extern int GetDpiForWindow(IntPtr hwnd);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        internal static extern int SetWindowLong32(IntPtr hWnd, WindowLongIndexFlags nIndex, WinProc newProc);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        internal static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, WindowLongIndexFlags nIndex, WinProc newProc);

        [DllImport("user32.dll")]
        internal static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, WindowMessage msg, IntPtr wParam, IntPtr lParam);

        [Flags]
        internal enum WindowLongIndexFlags : int
        {
            GWL_WNDPROC = -4,
        }

        internal enum WindowMessage : int
        {
            WM_SETTINGSCHANGED = 0x001A,
        }

        internal static IntPtr SetWindowLongPtr(IntPtr hWnd, WindowLongIndexFlags nIndex, WinProc newProc)
        {
            if (IntPtr.Size == 8)
            {
                return SetWindowLongPtr64(hWnd, nIndex, newProc);
            }
            else
            {
                return new IntPtr(SetWindowLong32(hWnd, nIndex, newProc));
            }
        }
    }
}
