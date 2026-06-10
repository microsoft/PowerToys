// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Global mouse input via Win32 <c>SetCursorPos</c> and <c>mouse_event</c>. Required for
/// scenarios like clicking inside the ColorPicker overlay, which is a transparent window that
/// can't be targeted via UIA / <c>winapp ui click</c>.
/// </summary>
public static class MouseHelper
{
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
#pragma warning disable SA1300 // win32 API name
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
#pragma warning restore SA1300

    private const uint MOUSEEVENTF_LEFTDOWN = 0x02;
    private const uint MOUSEEVENTF_LEFTUP = 0x04;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x08;
    private const uint MOUSEEVENTF_RIGHTUP = 0x10;

    /// <summary>Move the OS cursor to absolute screen coordinates.</summary>
    public static void MoveTo(int x, int y) => SetCursorPos(x, y);

    /// <summary>Press + release left mouse button at the current cursor position.</summary>
    public static void LeftClick()
    {
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(50);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
    }

    /// <summary>Move cursor to (x,y) and left-click.</summary>
    public static void LeftClickAt(int x, int y)
    {
        MoveTo(x, y);
        Thread.Sleep(40);
        LeftClick();
    }

    /// <summary>Press + release right mouse button at the current cursor position.</summary>
    public static void RightClick()
    {
        mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(50);
        mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
    }
}
