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
    /// Contains information about a simulated keyboard event.
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-keybdinput
    /// </remarks>
    [SuppressMessage("SA1307", "SA1307:AccessibleFieldsMustBeginWithUpperCaseLetter", Justification = "Parameter name matches Win32 api")]
    [StructLayout(LayoutKind.Sequential)]
    internal readonly struct KEYBDINPUT
    {
        public readonly WORD wVk;
        public readonly WORD wScan;
        public readonly DWORD dwFlags;
        public readonly DWORD time;
        public readonly ULONG_PTR dwExtraInfo;

        public KEYBDINPUT(
            WORD wVk,
            WORD wScan,
            DWORD dwFlags,
            DWORD time,
            ULONG_PTR dwExtraInfo)
        {
            this.wVk = wVk;
            this.wScan = wScan;
            this.dwFlags = dwFlags;
            this.time = time;
            this.dwExtraInfo = dwExtraInfo;
        }
    }
}
