// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using WinUIEx;

namespace ShortcutGuide
{
    public static class DisplayHelper
    {
        private enum MonitorFromWindowDwFlags : int
        {
            MONITOR_DEFAULTTONEAREST = 2,
        }

        public static Rect GetWorkAreaForDisplayWithWindow(IntPtr hwnd)
        {
            foundMonitorIndex = -1;
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

        [DllImport("User32.dll")]
        private static extern IntPtr MonitorFromWindow(nint hwnd, int dwFlags);

        [DllImport("User32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        public struct LPARAM
        {
            public IntPtr Value;

            public LPARAM(IntPtr value)
            {
                Value = value;
            }

            public static implicit operator IntPtr(LPARAM lParam)
            {
                return lParam.Value;
            }

            public static implicit operator LPARAM(IntPtr ptr)
            {
                return new LPARAM(ptr);
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
