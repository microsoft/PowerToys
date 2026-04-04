// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace ShortcutGuide;

internal static partial class NativeMethods
{
    internal const int GWL_STYLE = -16;
    internal const int WS_CAPTION = 0x00C00000;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial int GetWindowLongW(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll")]
    internal static partial int SetWindowLongW(IntPtr hWnd, int nIndex, int dwNewLong);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial IntPtr FindWindowA(in string lpClassName, in string? lpWindowName);

    [LibraryImport("User32.dll")]
    internal static partial IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

    [LibraryImport("Shcore.dll")]
    internal static partial long GetDpiForMonitor(IntPtr hmonitor, int dpiType, ref int dpiX, ref int dpiY);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorPos(out POINT lpPoint);

    [LibraryImport("user32.dll")]
    internal static partial IntPtr GetForegroundWindow();

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [LibraryImport("user32.dll")]
    internal static partial short GetAsyncKeyState(int vKey);

    [DllImport("../PowerToys.Interop.dll", EntryPoint = "get_buttons")]
    internal static extern IntPtr GetTasklistButtons(IntPtr monitor, out int size);

    [LibraryImport("../PowerToys.Interop.dll", EntryPoint = "IsCurrentWindowExcludedFromShortcutGuide")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool IsCurrentWindowExcludedFromShortcutGuide();

    [LibraryImport("User32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

    internal delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    internal struct LPARAM(IntPtr value)
    {
        internal IntPtr Value = value;

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

    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    internal struct POINT
    {
        internal int X;
        internal int Y;

        public static implicit operator PointInt32(POINT point)
        {
            return new PointInt32(point.X, point.Y);
        }
    }

    public enum MonitorFromWindowDwFlags
    {
        MONITOR_DEFAULTTONEAREST = 2,
    }
}
