// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using static MouseJumpUI.Common.NativeMethods.Core;

namespace MouseJumpUI.Common.NativeMethods;

internal static partial class Gdi32
{
    /// <summary>
    /// The StretchBlt function copies a bitmap from a source rectangle into a destination
    /// rectangle, stretching or compressing the bitmap to fit the dimensions of the
    /// destination rectangle, if necessary. The system stretches or compresses the bitmap
    /// according to the stretching mode currently set in the destination device context.
    /// </summary>
    /// <returns>
    /// If the function succeeds, the return value is nonzero.
    /// If the function fails, the return value is zero.
    /// </returns>
    /// <remarks>
    /// See https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-stretchblt
    /// </remarks>
    [LibraryImport(Libraries.Gdi32)]
    internal static partial BOOL StretchBlt(
      HDC hdcDest,
      int xDest,
      int yDest,
      int wDest,
      int hDest,
      HDC hdcSrc,
      int xSrc,
      int ySrc,
      int wSrc,
      int hSrc,
      ROP_CODE rop);
}
