// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using static FancyMouse.NativeMethods.Core;

namespace FancyMouse.NativeMethods;

internal static partial class Shcore
{
    /// <summary>
    /// The EnumDisplayMonitors function enumerates display monitors (including invisible
    /// pseudo-monitors associated with the mirroring drivers) that intersect a region formed
    /// by the intersection of a specified clipping rectangle and the visible region of a
    /// device context. EnumDisplayMonitors calls an application-defined MonitorEnumProc
    /// callback function once for each monitor that is enumerated. Note that
    /// GetSystemMetrics (SM_CMONITORS) counts only the display monitors.
    /// </summary>
    /// <returns>
    /// This function returns one of the following values.
    ///
    /// Return code  Description
    /// -----------  -----------
    /// S_OK         The function successfully returns the X and Y DPI values for the specified monitor.
    /// E_INVALIDARG The handle, DPI type, or pointers passed in are not valid.
    /// </returns>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/shellscalingapi/nf-shellscalingapi-getdpiformonitor
    /// </remarks>
    [LibraryImport(Libraries.Shcore)]
    internal static partial Core.HRESULT GetDpiForMonitor(
        HMONITOR hmonitor,
        MONITOR_DPI_TYPE dpiType,
        ref UINT dpiX,
        ref UINT dpiY);
}
