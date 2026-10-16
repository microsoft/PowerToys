// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

using static MouseJump.Kicker.NativeMethods.Core;

namespace MouseJump.Kicker.NativeMethods;

internal static partial class Kernel32
{
    /// <summary>
    /// Sets the specified event object to the signaled state.
    /// </summary>
    /// <returns>
    /// If the function succeeds, the return value is nonzero.
    /// If the function fails, the return value is zero.
    /// To get extended error information, call GetLastError.
    /// </returns>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaymonitors
    /// </remarks>
    [LibraryImport(Libraries.Kernel32, SetLastError = true)]
    internal static partial Core.BOOL SetEvent(
        HANDLE hEvent);
}
