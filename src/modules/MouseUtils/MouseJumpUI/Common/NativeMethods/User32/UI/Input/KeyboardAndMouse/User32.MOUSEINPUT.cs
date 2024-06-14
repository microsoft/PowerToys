// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using static MouseJumpUI.Common.NativeMethods.Core;

namespace MouseJumpUI.Common.NativeMethods;

internal static partial class User32
{
    /// <summary>
    /// Contains information about a simulated mouse event.
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-mouseinput
    /// </remarks>
    [SuppressMessage("SA1307", "SA1307:AccessibleFieldsMustBeginWithUpperCaseLetter", Justification = "Parameter name matches Win32 api")]
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct MOUSEINPUT
    {
        public readonly int dx;
        public readonly int dy;
        public readonly DWORD mouseData;
        public readonly MOUSE_EVENT_FLAGS dwFlags;
        public readonly DWORD time;
        public readonly ULONG_PTR dwExtraInfo;

        public MOUSEINPUT(
            int dx,
            int dy,
            DWORD mouseData,
            MOUSE_EVENT_FLAGS dwFlags,
            DWORD time,
            ULONG_PTR dwExtraInfo)
        {
            this.dx = dx;
            this.dy = dy;
            this.mouseData = mouseData;
            this.dwFlags = dwFlags;
            this.time = time;
            this.dwExtraInfo = dwExtraInfo;
        }
    }
}
