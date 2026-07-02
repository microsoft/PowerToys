// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using static MouseJump.Kicker.NativeMethods.Core;

namespace MouseJump.Kicker.NativeMethods;

internal static partial class Kernel32
{
    /// <summary>
    /// Creates or opens a named or unnamed event object.
    /// To specify an access mask for the object, use the CreateEventEx function.
    /// </summary>
    /// <returns>
    /// If the function succeeds, the return value is a handle to the event object.
    /// If the named event object existed before the function call, the function returns
    /// a handle to the existing object and GetLastError returns ERROR_ALREADY_EXISTS.
    /// If the function fails, the return value is NULL.
    /// To get extended error information, call GetLastError.
    /// </returns>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaymonitors
    /// </remarks>
    [LibraryImport(Libraries.Kernel32, StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    internal static partial HANDLE CreateEventW(
        LPSECURITY_ATTRIBUTES lpEventAttributes,
        BOOL bManualReset,
        BOOL bInitialState,
        LPCWSTR lpName);
}
