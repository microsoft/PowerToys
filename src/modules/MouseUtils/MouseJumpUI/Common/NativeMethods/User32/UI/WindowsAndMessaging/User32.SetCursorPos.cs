// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using static MouseJumpUI.Common.NativeMethods.Core;

namespace MouseJumpUI.Common.NativeMethods;

internal static partial class User32
{
    /// <summary>
    /// Moves the cursor to the specified screen coordinates. If the new coordinates are not within
    /// the screen rectangle set by the most recent ClipCursor function call, the system automatically
    /// adjusts the coordinates so that the cursor stays within the rectangle.
    /// </summary>
    /// <returns>
    /// Returns nonzero if successful or zero otherwise.
    /// To get extended error information, call GetLastError.
    /// </returns>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getcursorpos
    /// </remarks>
    [LibraryImport(Libraries.User32, SetLastError = true)]
    internal static partial BOOL SetCursorPos(
        int X,
        int Y);
}
