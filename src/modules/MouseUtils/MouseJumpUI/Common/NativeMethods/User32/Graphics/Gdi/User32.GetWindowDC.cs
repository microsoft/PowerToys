// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using static MouseJumpUI.Common.NativeMethods.Core;

namespace MouseJumpUI.Common.NativeMethods;

internal static partial class User32
{
    /// <summary>
    /// The GetWindowDC function retrieves the device context (DC) for the entire window,
    /// including title bar, menus, and scroll bars. A window device context permits painting
    /// anywhere in a window, because the origin of the device context is the upper-left
    /// corner of the window instead of the client area.
    ///
    /// GetWindowDC assigns default attributes to the window device context each time it
    /// retrieves the device context. Previous attributes are lost.
    /// </summary>
    /// <returns>
    /// If the function succeeds, the return value is a handle to a device context for the specified window.
    /// If the function fails, the return value is NULL, indicating an error or an invalid hWnd parameter.
    /// </returns>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowdc
    /// </remarks>
    [LibraryImport(Libraries.User32)]
    internal static partial HDC GetWindowDC(
        HWND hWnd);
}
