// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using PowerOCR.Core.Models;

namespace PowerOCR.Helpers;

internal static class CursorClipper
{
    internal static bool Clip(DisplayBounds bounds)
    {
        var rect = new OSInterop.RECT
        {
            Left = bounds.X,
            Top = bounds.Y,
            Right = bounds.X + bounds.Width,
            Bottom = bounds.Y + bounds.Height,
        };
        return OSInterop.ClipCursor(ref rect);
    }

    internal static void UnClip()
    {
        OSInterop.ClipCursor(IntPtr.Zero);
    }
}
