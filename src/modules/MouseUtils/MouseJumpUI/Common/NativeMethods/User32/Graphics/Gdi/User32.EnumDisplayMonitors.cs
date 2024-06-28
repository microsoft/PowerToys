// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using static MouseJumpUI.Common.NativeMethods.Core;

namespace MouseJumpUI.Common.NativeMethods;

internal static partial class User32
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
    /// If the function succeeds, the return value is nonzero.
    /// If the function fails, the return value is zero.
    /// </returns>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaymonitors
    /// </remarks>
    [LibraryImport(Libraries.User32)]
    internal static partial BOOL EnumDisplayMonitors(
        HDC hdc,
        LPCRECT lprcClip,
        MONITORENUMPROC lpfnEnum,
        LPARAM dwData);
}
