// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Global mouse input via Win32 <c>SetCursorPos</c> and <c>SendInput</c>. Required for
/// scenarios like clicking inside the ColorPicker overlay, which is a transparent window that
/// can't be targeted via UIA / <c>winapp ui click</c>.
/// </summary>
public static class MouseHelper
{
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint DwFlags;
        public uint Time;
        public UIntPtr DwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint Type;
        public MOUSEINPUT Mi;
    }

    private const uint INPUT_MOUSE = 0;

    private const uint MOUSEEVENTF_LEFTDOWN = 0x02;
    private const uint MOUSEEVENTF_LEFTUP = 0x04;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x08;
    private const uint MOUSEEVENTF_RIGHTUP = 0x10;
    private const uint MOUSEEVENTF_MIDDLEDOWN = 0x20;
    private const uint MOUSEEVENTF_MIDDLEUP = 0x40;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;

    private const int ClickDelayMs = 100;
    private const int WheelTick = 120;

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    /// <summary>Move the OS cursor to absolute screen coordinates.</summary>
    public static void MoveTo(int x, int y) => SetCursorPos(x, y);

    /// <summary>Current cursor position in screen pixels.</summary>
    public static (int X, int Y) GetMousePosition()
    {
        GetCursorPos(out var p);
        return (p.X, p.Y);
    }

    /// <summary>Press the left mouse button down at the current position.</summary>
    public static void LeftDown() => SendMouseInput(MOUSEEVENTF_LEFTDOWN);

    /// <summary>Release the left mouse button.</summary>
    public static void LeftUp() => SendMouseInput(MOUSEEVENTF_LEFTUP);

    /// <summary>Press the right mouse button down at the current position.</summary>
    public static void RightDown() => SendMouseInput(MOUSEEVENTF_RIGHTDOWN);

    /// <summary>Release the right mouse button.</summary>
    public static void RightUp() => SendMouseInput(MOUSEEVENTF_RIGHTUP);

    /// <summary>Press the middle mouse button down at the current position.</summary>
    public static void MiddleDown() => SendMouseInput(MOUSEEVENTF_MIDDLEDOWN);

    /// <summary>Release the middle mouse button.</summary>
    public static void MiddleUp() => SendMouseInput(MOUSEEVENTF_MIDDLEUP);

    /// <summary>Press + release left mouse button at the current cursor position.</summary>
    public static void LeftClick()
    {
        LeftDown();
        Thread.Sleep(ClickDelayMs);
        LeftUp();
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
        RightDown();
        Thread.Sleep(ClickDelayMs);
        RightUp();
    }

    /// <summary>Press + release middle mouse button at the current cursor position.</summary>
    public static void MiddleClick()
    {
        MiddleDown();
        Thread.Sleep(ClickDelayMs);
        MiddleUp();
    }

    /// <summary>Left double-click at the current cursor position.</summary>
    public static void DoubleClick()
    {
        LeftClick();
        Thread.Sleep(ClickDelayMs);
        LeftClick();
    }

    /// <summary>Scroll the wheel by a raw amount (positive = up, negative = down; one tick = 120).</summary>
    public static void ScrollWheel(int amount) => SendMouseInput(MOUSEEVENTF_WHEEL, amount);

    /// <summary>Scroll the wheel up by one tick.</summary>
    public static void ScrollUp() => ScrollWheel(WheelTick);

    /// <summary>Scroll the wheel down by one tick.</summary>
    public static void ScrollDown() => ScrollWheel(-WheelTick);

    /// <summary>
    /// Drag from one absolute screen point to another with real mouse input: move → left-down →
    /// stepped move → left-up. Moving in <paramref name="steps"/> increments lets a tracking overlay
    /// (e.g. a screen-capture measurement) see the cursor travel instead of teleport, then settles
    /// exactly on the target. winappcli has no drag verb, so this stays Win32. Coordinates are physical
    /// screen pixels (matching <c>winapp ui search</c> bounds).
    /// </summary>
    public static void Drag(int fromX, int fromY, int toX, int toY, int steps = 10)
    {
        if (steps < 1)
        {
            steps = 1;
        }

        MoveTo(fromX, fromY);
        Thread.Sleep(100);

        LeftDown();
        Thread.Sleep(100);

        var dx = (double)(toX - fromX) / steps;
        var dy = (double)(toY - fromY) / steps;
        for (var i = 1; i <= steps; i++)
        {
            MoveTo(fromX + (int)Math.Round(dx * i), fromY + (int)Math.Round(dy * i));
            Thread.Sleep(15);
        }

        MoveTo(toX, toY);
        Thread.Sleep(200);

        LeftUp();
    }

    /// <summary>
    /// Injects a single mouse event into the system input queue via <see cref="SendInput"/>.
    /// Button and wheel events fire at the current cursor position, so <paramref name="data"/>
    /// only carries the wheel delta for <c>MOUSEEVENTF_WHEEL</c>.
    /// </summary>
    private static void SendMouseInput(uint flags, int data = 0)
    {
        var inputs = new INPUT[]
        {
            new INPUT
            {
                Type = INPUT_MOUSE,
                Mi = new MOUSEINPUT
                {
                    Dx = 0,
                    Dy = 0,
                    MouseData = (uint)data,
                    DwFlags = flags,
                    Time = 0,
                    DwExtraInfo = UIntPtr.Zero,
                },
            },
        };

        var sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        if (sent != inputs.Length)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }
}
