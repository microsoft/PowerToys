// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

using static MouseJump.Kicker.NativeMethods.Core;

namespace MouseJump.Kicker.NativeMethods;

internal static partial class User32
{
    /// <summary>
    /// Retrieves the identifier of the thread that created the specified window and,
    /// optionally, the identifier of the process that created the window.
    /// </summary>
    /// <returns>
    /// The return value is the identifier of the thread that created the window.
    /// </returns>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowthreadprocessid
    /// </remarks>
    [LibraryImport(Libraries.User32, SetLastError = true)]
    internal static partial uint GetWindowThreadProcessId(
        HWND hWnd,
        out uint lpdwProcessId);
}
