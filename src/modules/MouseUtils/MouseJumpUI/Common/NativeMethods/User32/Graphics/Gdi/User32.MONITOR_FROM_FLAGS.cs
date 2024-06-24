// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;

namespace MouseJumpUI.Common.NativeMethods;

[SuppressMessage("SA1310", "SA1310:FieldNamesMustNotContainUnderscore", Justification = "Names match Win32 api")]
internal static partial class User32
{
    /// <summary>
    /// Determines the function's return value if the point is not contained within any display monitor.
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-monitorfrompoint
    /// </remarks>
    internal enum MONITOR_FROM_FLAGS : uint
    {
        MONITOR_DEFAULTTONULL = 0x00000000,
        MONITOR_DEFAULTTOPRIMARY = 0x00000001,
        MONITOR_DEFAULTTONEAREST = 0x00000002,
    }
}
