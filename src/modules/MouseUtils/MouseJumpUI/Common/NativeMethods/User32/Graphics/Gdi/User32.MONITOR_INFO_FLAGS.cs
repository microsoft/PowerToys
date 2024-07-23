// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

namespace MouseJumpUI.Common.NativeMethods;

[SuppressMessage("SA1310", "SA1310:FieldNamesMustNotContainUnderscore", Justification = "Names match Win32 api")]
internal static partial class User32
{
    /// <summary>
    /// A set of flags that represent attributes of the display monitor.
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-monitorinfo
    /// </remarks>
    internal enum MONITOR_INFO_FLAGS : uint
    {
        MONITORINFOF_PRIMARY = 1,
    }
}
