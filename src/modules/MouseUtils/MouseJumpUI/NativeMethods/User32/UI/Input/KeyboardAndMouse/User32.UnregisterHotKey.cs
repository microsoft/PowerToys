// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using static MouseJumpUI.NativeMethods.Core;

namespace MouseJumpUI.NativeMethods;

internal static partial class User32
{
    /// <summary>
    /// Frees a hot key previously registered by the calling thread.
    /// </summary>
    /// <param name="hWnd">
    /// A handle to the window associated with the hot key to be freed.
    /// This parameter should be NULL if the hot key is not associated with a window.
    /// </param>
    /// <param name="id">
    /// The identifier of the hot key to be freed.
    /// </param>
    /// <returns>
    /// If the function succeeds, the return value is nonzero.
    /// If the function fails, the return value is zero.
    /// To get extended error information, call GetLastError.
    /// </returns>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-unregisterhotkey
    /// </remarks>
    [LibraryImport(Libraries.User32, SetLastError = true)]
    internal static partial BOOL UnregisterHotKey(
        HWND hWnd,
        int id);
}
