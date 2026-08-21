// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

using static MouseJump.Kicker.NativeMethods.Core;

namespace MouseJump.Kicker.NativeMethods;

internal static partial class User32
{
    /// <summary>
    /// Brings the thread that created the specified window into the foreground and
    /// activates the window.
    /// </summary>
    /// <returns>
    /// If the window was brought to the foreground, the return value is nonzero.
    /// If the window was not brought to the foreground, the return value is zero.
    /// </returns>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow
    /// </remarks>
    [LibraryImport(Libraries.User32, SetLastError = true)]
    internal static partial BOOL SetForegroundWindow(
        HWND hWnd);
}
