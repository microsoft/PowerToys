// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using static MouseJumpUI.Common.NativeMethods.Core;

namespace MouseJumpUI.Common.NativeMethods;

internal static partial class User32
{
    /// <summary>
    /// Retrieves the position of the mouse cursor, in screen coordinates.
    /// </summary>
    /// <returns>
    /// Returns nonzero if successful or zero otherwise.
    /// To get extended error information, call GetLastError.
    /// </returns>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getcursorpos
    /// </remarks>
    [LibraryImport(Libraries.User32, SetLastError = true)]
    internal static partial BOOL GetCursorPos(
        LPPOINT lpPoint);
}
