// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Vanara.PInvoke;

namespace PowerAccent.Core.Tools;

internal static class WindowsFunctions
{
    public static void Insert(string s, bool back = false)
    {
        unsafe
        {
            if (back)
            {
                // Split in 2 different SendInput (Powershell doesn't take back issue)
                var inputsBack = new User32.INPUT[]
                {
                    new User32.INPUT { type = User32.INPUTTYPE.INPUT_KEYBOARD, ki = new User32.KEYBDINPUT { wVk = (ushort)User32.VK.VK_BACK } },
                    new User32.INPUT { type = User32.INPUTTYPE.INPUT_KEYBOARD, ki = new User32.KEYBDINPUT { wVk = (ushort)User32.VK.VK_BACK, dwFlags = User32.KEYEVENTF.KEYEVENTF_KEYUP } },
                };

                var temp1 = User32.SendInput((uint)inputsBack.Length, inputsBack, sizeof(User32.INPUT));
                System.Threading.Thread.Sleep(1); // Some apps, like Terminal, need a little wait to process the sent backspace or they'll ignore it.
            }

            foreach (char c in s)
            {
                // Letter
                var inputsInsert = new User32.INPUT[]
                {
                new User32.INPUT { type = User32.INPUTTYPE.INPUT_KEYBOARD, ki = new User32.KEYBDINPUT { wVk = 0, dwFlags = User32.KEYEVENTF.KEYEVENTF_UNICODE, wScan = c } },
                new User32.INPUT { type = User32.INPUTTYPE.INPUT_KEYBOARD, ki = new User32.KEYBDINPUT { wVk = 0, dwFlags = User32.KEYEVENTF.KEYEVENTF_UNICODE | User32.KEYEVENTF.KEYEVENTF_KEYUP, wScan = c } },
                };
                var temp2 = User32.SendInput((uint)inputsInsert.Length, inputsInsert, sizeof(User32.INPUT));
            }
        }
    }

    public static Point GetCaretPosition()
    {
        User32.GUITHREADINFO guiInfo = default;
        guiInfo.cbSize = (uint)Marshal.SizeOf(guiInfo);
        User32.GetGUIThreadInfo(0, ref guiInfo);
        POINT caretPosition = new POINT(guiInfo.rcCaret.left, guiInfo.rcCaret.top);
        User32.ClientToScreen(guiInfo.hwndCaret, ref caretPosition);

        if (caretPosition.X == 0)
        {
            POINT testPoint;
            User32.GetCaretPos(out testPoint);
            return testPoint;
        }

        return caretPosition;
    }

    public static (Point Location, Size Size, double Dpi) GetActiveDisplay()
    {
        User32.GUITHREADINFO guiInfo = default;
        guiInfo.cbSize = (uint)Marshal.SizeOf(guiInfo);
        User32.GetGUIThreadInfo(0, ref guiInfo);
        var res = User32.MonitorFromWindow(guiInfo.hwndActive, User32.MonitorFlags.MONITOR_DEFAULTTONEAREST);

        User32.MONITORINFO monitorInfo = default;
        monitorInfo.cbSize = (uint)Marshal.SizeOf(monitorInfo);
        User32.GetMonitorInfo(res, ref monitorInfo);

        double dpi = User32.GetDpiForWindow(guiInfo.hwndActive) / 96d;

        return (monitorInfo.rcWork.Location, monitorInfo.rcWork.Size, dpi);
    }

    public static bool IsCapsLockState()
    {
        var capital = User32.GetKeyState((int)User32.VK.VK_CAPITAL);
        return capital != 0;
    }

    public static bool IsShiftState()
    {
        var shift = User32.GetKeyState((int)User32.VK.VK_SHIFT);
        return shift < 0;
    }
}
