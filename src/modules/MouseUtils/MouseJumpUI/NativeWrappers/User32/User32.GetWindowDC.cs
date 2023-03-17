// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using MouseJumpUI.NativeMethods.Core;

namespace MouseJumpUI.NativeWrappers;

internal static partial class User32
{
    public static HDC GetWindowDC(HWND hWnd)
    {
        var hdc = NativeMethods.User32.GetWindowDC(hWnd);

        return hdc.IsNull
            ? throw new InvalidOperationException(
                $"{nameof(User32.GetWindowDC)} returned null")
            : hdc;
    }
}
