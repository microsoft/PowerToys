// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using MouseJumpUI.NativeMethods.Core;

namespace MouseJumpUI.NativeWrappers;

internal static partial class User32
{
    public static int ReleaseDC(HWND hWnd, HDC hDC)
    {
        var result = NativeMethods.User32.ReleaseDC(hWnd, hDC);

        return result == 0
            ? throw new InvalidOperationException(
                $"{nameof(User32.ReleaseDC)} returned {result}")
            : result;
    }
}
