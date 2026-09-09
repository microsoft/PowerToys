// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.PowerToys.UITest.Next;

namespace Microsoft.AlwaysOnTop.UITests;

internal static class DesktopHygiene
{
    private const uint WmClose = 0x0010;

    internal static void DismissForegroundShellSurface(WindowControl.ForegroundWindowInfo foreground)
    {
        if (!foreground.ProcessName.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _ = PostMessageW(foreground.Hwnd, WmClose, IntPtr.Zero, IntPtr.Zero);
        Thread.Sleep(250);
        if (!IsConfirmedForeground(foreground.Hwnd))
        {
            return;
        }

        var display = WindowHelper.GetDisplaySize();

        // Windows notifications anchor their rightmost action above the taskbar at this inset.
        MouseHelper.LeftClickAt(display.Width - 100, display.Height - 84);
        Thread.Sleep(500);
        if (!IsConfirmedForeground(foreground.Hwnd))
        {
            return;
        }

        KeyboardHelper.SendKeys(Key.Alt, Key.F4);
        Thread.Sleep(500);
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessageW(IntPtr hWnd, uint message, IntPtr wParam, IntPtr lParam);

    private static bool IsConfirmedForeground(IntPtr hwnd)
    {
        return WindowControl.GetForegroundWindowInfo().Hwnd == hwnd;
    }
}
