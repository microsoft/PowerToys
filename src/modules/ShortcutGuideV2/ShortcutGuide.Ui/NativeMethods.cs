// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Windows.Graphics;

internal static partial class NativeMethods
{
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    internal static readonly IntPtr HWND_TOPMOST = new System.IntPtr(-1);
    internal const uint SWP_NOSIZE = 0x0001;
    internal const uint SWP_NOMOVE = 0x0002;
    internal const uint SWP_NOACTIVATE = 0x0010;
    internal const uint SWP_SHOWWINDOW = 0x0040;

    [LibraryImport("user32.dll", SetLastError = true)]
    internal static partial int GetWindowLongW(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll")]
    internal static partial int SetWindowLongW(IntPtr hWnd, int nIndex, int dwNewLong);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int FindWindowA(in string lpClassName, in string? lpWindowName);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int FindWindowExA(int hwndParent, int hwndChildAfter, in string lpClassName, in string? lpWindowName);

    [LibraryImport("User32.dll")]
    internal static partial IntPtr MonitorFromWindow(int hwnd, int dwFlags);

    [LibraryImport("Shcore.dll")]
    internal static partial long GetDpiForMonitor(int hmonitor, int dpiType, ref int dpiX, ref int dpiY);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out POINT lpPoint);

    public struct POINT
    {
        public int X;
        public int Y;

        public static implicit operator PointInt32(POINT point)
        {
            return new PointInt32(point.X, point.Y);
        }
    }

    internal const int GWL_STYLE = -16;
    internal const int WS_CAPTION = 0x00C00000;
}
