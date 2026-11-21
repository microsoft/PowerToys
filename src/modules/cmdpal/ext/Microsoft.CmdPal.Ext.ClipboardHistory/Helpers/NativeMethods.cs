// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Helpers;

public static partial class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct PointInter
    {
        public int X;
        public int Y;

        public static explicit operator Point(PointInter point) => new(point.X, point.Y);
    }

    [DllImport("user32.dll")]
    internal static extern bool GetCursorPos(out PointInter lpPoint);

    [LibraryImport("ole32.dll")]
    internal static partial int CoInitialize(IntPtr pvReserved);

    [LibraryImport("ole32.dll")]
    internal static partial void CoUninitialize();
}
