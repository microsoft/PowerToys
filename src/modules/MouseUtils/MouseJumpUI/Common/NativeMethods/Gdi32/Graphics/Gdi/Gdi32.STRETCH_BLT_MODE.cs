// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace MouseJumpUI.Common.NativeMethods;

internal static partial class Gdi32
{
    /// <summary>
    /// The stretching mode.
    /// </summary>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-setstretchbltmode
    /// </remarks>
    internal enum STRETCH_BLT_MODE : int
    {
        BLACKONWHITE = 1,
        COLORONCOLOR = 3,
        HALFTONE = 4,
        WHITEONBLACK = 2,
        STRETCH_ANDSCANS = STRETCH_BLT_MODE.BLACKONWHITE,
        STRETCH_DELETESCANS = STRETCH_BLT_MODE.COLORONCOLOR,
        STRETCH_HALFTONE = STRETCH_BLT_MODE.HALFTONE,
        STRETCH_ORSCANS = STRETCH_BLT_MODE.WHITEONBLACK,
    }
}
