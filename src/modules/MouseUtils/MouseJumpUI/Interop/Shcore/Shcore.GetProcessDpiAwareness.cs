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
    /// <returns>
    /// This function returns one of the following values.
    ///
    /// <table>
    ///   <tr>
    ///     <td>Return code</td>
    ///     <td>Description</td>
    ///   </tr></table>
    ///   <tr>
    ///     <td>S_OK</td>
    ///     <td>The function successfully retrieved the DPI awareness of the specified process.</td>
    ///   </tr>
    ///   <tr>
    ///     <td>E_INVALIDARG</td>
    ///     <td>The handle or pointer passed in is not valid.</td>
    ///   </tr>
    ///   <tr>
    ///     <td>E_ACCESSDENIED</td>
    ///     <td>The application does not have sufficient privileges.</td>
    ///   </tr>
    /// </table>
    /// </returns>
    [LibraryImport(Libraries.Shcore)]
    public static partial int GetProcessDpiAwareness(
      IntPtr hProcess,
      out PROCESS_DPI_AWARENESS value
    );
}
