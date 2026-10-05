// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;

namespace PowerOCR.UITests;

internal static partial class CursorStateReader
{
    private const int CursorShowing = 1;

    internal static CursorState Read()
    {
        var info = new CursorInfo { Size = Marshal.SizeOf<CursorInfo>() };
        if (!GetCursorInfo(ref info))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        }

        return new CursorState(new Point(info.X, info.Y), info.Cursor, (info.Flags & CursorShowing) != 0);
    }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorInfo(ref CursorInfo info);

    internal readonly record struct CursorState(Point Position, IntPtr Handle, bool IsVisible);

    [StructLayout(LayoutKind.Sequential)]
    private struct CursorInfo
    {
        public int Size;
        public int Flags;
        public IntPtr Cursor;
        public int X;
        public int Y;
    }
}
