// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using static MouseJumpUI.NativeMethods.Core;

namespace MouseJumpUI.NativeMethods;

internal static partial class User32
{
    /// <summary>
    /// Contains message information from a thread's message queue.
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-msg
    ///     https://github.com/dotnet/runtime/blob/main/src/libraries/Common/src/Interop/Windows/User32/Interop.MSG.cs
    /// </remarks>
    [SuppressMessage("SA1307", "SA1307:AccessibleFieldsMustBeginWithUpperCaseLetter", Justification = "Names match Win32 api")]
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct MSG
    {
        public readonly HWND hwnd;
        public readonly MESSAGE_TYPE message;
        public readonly WPARAM wParam;
        public readonly LPARAM lParam;
        public readonly DWORD time;
        public readonly POINT pt;
        public readonly DWORD lPrivate;

        public MSG(
            HWND hwnd,
            MESSAGE_TYPE message,
            WPARAM wParam,
            LPARAM lParam,
            DWORD time,
            POINT pt,
            DWORD lPrivate)
        {
            this.hwnd = hwnd;
            this.message = message;
            this.wParam = wParam;
            this.lParam = lParam;
            this.time = time;
            this.pt = pt;
            this.lPrivate = lPrivate;
        }

        public static int Size =>
            Marshal.SizeOf(typeof(MSG));
    }
}
