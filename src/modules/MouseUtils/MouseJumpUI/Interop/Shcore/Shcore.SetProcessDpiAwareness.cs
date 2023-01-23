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
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/shellscalingapi/nf-shellscalingapi-setprocessdpiawareness
    /// </remarks>
    [LibraryImport(Libraries.Shcore)]
    public static partial int SetProcessDpiAwareness(
        PROCESS_DPI_AWARENESS value);
}
