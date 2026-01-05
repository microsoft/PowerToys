// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace PowerToysExtension.Helpers;

internal static class FancyZonesNotifier
{
    private const string AppliedLayoutsFileUpdateMessage = "{2ef2c8a7-e0d5-4f31-9ede-52aade2d284d}";
    private static readonly uint WmPrivAppliedLayoutsFileUpdate = RegisterWindowMessageW(AppliedLayoutsFileUpdateMessage);

    public static void NotifyAppliedLayoutsChanged()
    {
        _ = PostMessageW(new IntPtr(0xFFFF), WmPrivAppliedLayoutsFileUpdate, UIntPtr.Zero, IntPtr.Zero);
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint RegisterWindowMessageW(string lpString);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool PostMessageW(IntPtr hWnd, uint msg, UIntPtr wParam, IntPtr lParam);
}
