// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using static PowerOCR.NativeMethods.Core;

namespace PowerOCR.NativeMethods;

internal static partial class PInvoke
{
    [LibraryImport("Shcore.dll")]
    internal static partial Core.UINT GetDpiForMonitor(
        HMONITOR hmonitor,
        UINT dpiType,
        ref UINT dpiX,
        ref UINT dpiY);
}
