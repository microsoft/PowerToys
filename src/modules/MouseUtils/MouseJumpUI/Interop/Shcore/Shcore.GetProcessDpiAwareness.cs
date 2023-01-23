// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace MouseJumpUI.Interop;

internal static partial class Shcore
{
    /// <summary>
    /// Retrieves the dots per inch (dpi) awareness of the specified process.
    /// </summary>
    /// <param name="hProcess">Handle of the process that is being queried. If this parameter is NULL, the current process is queried.</param>
    /// <param name="value">The DPI awareness of the specified process. Possible values are from the PROCESS_DPI_AWARENESS enumeration.</param>
    /// <returns></returns>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/shellscalingapi/nf-shellscalingapi-getprocessdpiawareness
    /// </remarks>
    /// <returns>
    /// This function returns one of the following values.
    ///
    /// Return code    Description
    /// S_OK           The function successfully retrieved the DPI awareness of the specified process.
    /// E_INVALIDARG   The handle or pointer passed in is not valid.
    /// E_ACCESSDENIED The application does not have sufficient privileges.
    /// </returns>
    [LibraryImport(Libraries.Shcore)]
    public static partial int GetProcessDpiAwareness(
        IntPtr hProcess,
        out PROCESS_DPI_AWARENESS value
    );
}
