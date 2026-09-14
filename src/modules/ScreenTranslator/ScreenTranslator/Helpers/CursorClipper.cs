// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ScreenTranslator.Core.Translation;

namespace ScreenTranslator.Helpers;

internal static class CursorClipper
{
    internal static void ClipCursorToRect(PhysicalRect rect)
    {
        OSInterop.RECT r = new()
        {
            Left = (int)rect.Left,
            Top = (int)rect.Top,
            Right = (int)rect.Right,
            Bottom = (int)rect.Bottom,
        };

        OSInterop.ClipCursor(ref r);
    }

    internal static void UnclipCursor()
    {
        OSInterop.ClipCursor(IntPtr.Zero);
    }
}
