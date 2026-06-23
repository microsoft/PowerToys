// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WorkspacesEditor.Utils
{
    internal sealed class NativeMethods
    {
        public const int SW_RESTORE = 9;
        public const int SW_NORMAL = 1;
        public const int SW_MINIMIZE = 6;

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOACTIVATE = 0x0010;

        private static readonly IntPtr DPI_AWARENESS_CONTEXT_UNAWARE = new IntPtr(-1);

        /// <summary>
        /// Positions a WPF window using DPI-unaware context to match the virtual coordinates.
        /// This fixes overlay positioning on mixed-DPI multi-monitor setups.
        /// </summary>
        public static void SetWindowPositionDpiUnaware(Window window, int x, int y, int width, int height)
        {
            var helper = new WindowInteropHelper(window).Handle;
            if (helper != IntPtr.Zero)
            {
                // Temporarily switch to DPI-unaware context to position window.
                IntPtr oldContext = SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_UNAWARE);
                try
                {
                    SetWindowPos(helper, IntPtr.Zero, x, y, width, height, SWP_NOZORDER | SWP_NOACTIVATE);
                }
                finally
                {
                    SetThreadDpiAwarenessContext(oldContext);
                }
            }
        }

        [DllImport("USER32.DLL")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern void SwitchToThisWindow(IntPtr hWnd, bool fAltTab);

        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int processId);

        [DllImport("kernel32.dll")]
        public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        public enum DpiType
        {
            EFFECTIVE = 0,
            ANGULAR = 1,
            RAW = 2,
        }

        [DllImport("User32.dll")]
        public static extern IntPtr MonitorFromPoint([In] System.Drawing.Point pt, [In] uint dwFlags);

        [DllImport("Shcore.dll")]
        public static extern IntPtr GetDpiForMonitor([In] IntPtr hmonitor, [In] DpiType dpiType, [Out] out uint dpiX, [Out] out uint dpiY);

        public const int _S_OK = 0;
        public const int _MONITOR_DEFAULTTONEAREST = 2;
        public const int _E_INVALIDARG = -2147024809;
    }
}
