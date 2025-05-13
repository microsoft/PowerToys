// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.Settings.UI.Helpers;

public static partial class NativeMethods
{
    private const int WS_POPUP = 1 << 31; // 0x80000000
    internal const int GWL_STYLE = -16;
    internal const int WS_CAPTION = 0x00C00000;
    internal const int SPI_GETDESKWALLPAPER = 0x0073;
    internal const int SW_SHOWNORMAL = 1;
    internal const int SW_SHOWMAXIMIZED = 3;
    internal const int SW_HIDE = 0;

    [LibraryImport("user32.dll")]
    public static partial uint SendInput(uint nInputs, NativeKeyboardHelper.INPUT[] pInputs, int cbSize);

    [LibraryImport("user32.dll")]
#pragma warning disable CA1401 // P/Invokes should not be visible
    public static partial short GetAsyncKeyState(int vKey);
#pragma warning restore CA1401 // P/Invokes should not be visible
}
