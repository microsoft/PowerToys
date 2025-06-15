// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Windows.Graphics;

internal static partial class NativeMethods
{
    internal static readonly IntPtr HWND_TOPMOST = new(-1);
    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOMOVE = 0x0002;
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_SHOWWINDOW = 0x0040;
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

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial IntPtr FindWindowExA(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

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

    [LibraryImport("ShortcutGuide.CPPProject.dll", EntryPoint = "get_buttons")]
    internal static partial IntPtr GetTasklistButtons(IntPtr monitor, out int size);

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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct TasklistButton
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string Name;

        public int X;

        public int Y;

        public int Width;

        public int Height;

        public int Keynum;
    }
}
