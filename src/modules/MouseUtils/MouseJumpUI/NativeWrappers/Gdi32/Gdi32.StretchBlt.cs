// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using MouseJumpUI.NativeMethods.Core;

namespace MouseJumpUI.NativeWrappers;

internal static partial class Gdi32
{
    public static BOOL StretchBlt(
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
        NativeMethods.Gdi32.ROP_CODE rop)
    {
        var result = NativeMethods.Gdi32.StretchBlt(
            hdcDest,
            xDest,
            yDest,
            wDest,
            hDest,
            hdcSrc,
            xSrc,
            ySrc,
            wSrc,
            hSrc,
            rop);

        return result
            ? result
            : throw new InvalidOperationException(
                $"{nameof(Gdi32.StretchBlt)} returned {result.Value}");
    }
}
