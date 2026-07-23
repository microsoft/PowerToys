// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

using static MouseJump.Kicker.NativeMethods.Core;

namespace MouseJump.Kicker.NativeMethods;

internal static partial class User32
{
    /// <summary>
    /// An application-defined callback function for use with EnumWindows.
    /// Return TRUE to continue enumeration, FALSE to stop.
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/previous-versions/windows/desktop/legacy/ms633498(v=vs.85)
    /// </remarks>
    internal delegate BOOL EnumWindowsProc(HWND hwnd, LPARAM lParam);

    /// <summary>
    /// Enumerates all top-level windows on the screen, including hidden windows,
    /// by passing the handle to each window in turn to an application-defined callback function.
    /// </summary>
    /// <returns>
    /// If the function succeeds, the return value is nonzero.
    /// If the function fails, the return value is zero.
    /// To get extended error information, call GetLastError.
    /// </returns>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumwindows
    /// </remarks>
    [DllImport(Libraries.User32, SetLastError = true)]
    internal static extern BOOL EnumWindows(
        EnumWindowsProc lpEnumFunc,
        LPARAM lParam);
}
