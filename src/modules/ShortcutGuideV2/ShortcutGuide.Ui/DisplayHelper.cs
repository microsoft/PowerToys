// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Windows.Foundation;
using WinUIEx;

namespace ShortcutGuide
{
    public static partial class DisplayHelper
    {
        private enum MonitorFromWindowDwFlags : int
        {
            MONITOR_DEFAULTTONEAREST = 2,
        }

        public static Rect GetWorkAreaForDisplayWithWindow(IntPtr hwnd)
        {
            foundMonitorIndex = -1;
            monitorIndex = 0;
            var monitor = MonitorFromWindow(hwnd, (int)MonitorFromWindowDwFlags.MONITOR_DEFAULTTONEAREST);
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorEnumProc, new LPARAM(monitor));
            return MonitorInfo.GetDisplayMonitors()[foundMonitorIndex].RectWork;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private static int foundMonitorIndex = -1;
        private static int monitorIndex;

        private static bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
        {
            IntPtr targetMonitor = dwData;

            if (hMonitor == targetMonitor)
            {
                foundMonitorIndex = monitorIndex;
                return false; // Stop enumeration
            }

            monitorIndex++;
            return true; // Continue enumeration
        }

        private delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

        [LibraryImport("User32.dll")]
        private static partial IntPtr MonitorFromWindow(nint hwnd, int dwFlags);

        [LibraryImport("User32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        public struct LPARAM(IntPtr value)
        {
            public IntPtr Value = value;

            public static implicit operator IntPtr(LPARAM lParam)
            {
                return lParam.Value;
            }

            public static implicit operator LPARAM(IntPtr value)
            {
                return new LPARAM(value);
            }

            public static implicit operator LPARAM(int value)
            {
                return new LPARAM(new IntPtr(value));
            }

            public static implicit operator int(LPARAM lParam)
            {
                return lParam.Value.ToInt32();
            }
        }
    }
}
