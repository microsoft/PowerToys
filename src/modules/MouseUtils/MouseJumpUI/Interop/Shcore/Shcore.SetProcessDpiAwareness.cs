// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace MouseJumpUI.Interop;

internal static partial class Shcore
{
    /// <summary>
    /// Sets the process-default DPI awareness level.
    /// </summary>
    /// <param name="value">The DPI awareness value to set. Possible values are from the PROCESS_DPI_AWARENESS enumeration.</param>
    /// <returns>
    /// This function returns one of the following values.
    ///
    /// <table>
    ///   <tr>
    ///     <td>Return code</td>
    ///     <td>Description</td>
    ///   </tr>
    ///   <tr>
    ///     <td>S_OK</td>
    ///     <td>The DPI awareness for the app was set successfully.</td>
    ///   </tr>
    ///   <tr>
    ///     <td>E_INVALIDARG</td>
    ///     <td>The value passed in is not valid.</td>
    ///   </tr>
    ///   <tr>
    ///     <td>E_ACCESSDENIED</td>
    ///     <td>The DPI awareness is already set, either by calling this API previously or through the application (.exe) manifest.</td>
    ///   </tr>
    /// </table>
    /// </returns>
    [LibraryImport("shcore.dll")]
    public static partial int SetProcessDpiAwareness(
        PROCESS_DPI_AWARENESS value);
}
