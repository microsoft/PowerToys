// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

using Windows.Win32;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

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
                var inputsBack = new INPUT[]
                {
                    new INPUT
                    {
                        type = INPUT_TYPE.INPUT_KEYBOARD,
                        Anonymous = new INPUT._Anonymous_e__Union
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = VIRTUAL_KEY.VK_BACK,
                            },
                        },
                    },
                    new INPUT
                    {
                        type = INPUT_TYPE.INPUT_KEYBOARD,
                        Anonymous = new INPUT._Anonymous_e__Union
                        {
                            ki = new KEYBDINPUT
                            {
                                wVk = VIRTUAL_KEY.VK_BACK,
                                dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP,
                            },
                        },
                    },
                };

                _ = PInvoke.SendInput(inputsBack, Marshal.SizeOf<INPUT>());
                Thread.Sleep(1); // Some apps, like Terminal, need a little wait to process the sent backspace or they'll ignore it.
            }

            foreach (char c in s)
            {
                // Letter
                var inputsInsert = new INPUT[]
                {
                    new INPUT
                    {
                        type = INPUT_TYPE.INPUT_KEYBOARD,
                        Anonymous = new INPUT._Anonymous_e__Union
                        {
                            ki = new KEYBDINPUT
                            {
                                wScan = c,
                                dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE,
                            },
                        },
                    },
                    new INPUT
                    {
                        type = INPUT_TYPE.INPUT_KEYBOARD,
                        Anonymous = new INPUT._Anonymous_e__Union
                        {
                            ki = new KEYBDINPUT
                            {
                                wScan = c,
                                dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP,
                            },
                        },
                    },
                };

                _ = PInvoke.SendInput(inputsInsert, Marshal.SizeOf<INPUT>());
            }
        }
    }

    public static (Point Location, Size Size, double Dpi) GetActiveDisplay()
    {
        GUITHREADINFO guiInfo = default;
        guiInfo.cbSize = (uint)Marshal.SizeOf(guiInfo);
        PInvoke.GetGUIThreadInfo(0, ref guiInfo);
        var res = PInvoke.MonitorFromWindow(guiInfo.hwndActive, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);

        MONITORINFO monitorInfo = default;
        monitorInfo.cbSize = (uint)Marshal.SizeOf(monitorInfo);
        PInvoke.GetMonitorInfo(res, ref monitorInfo);

        double dpi = PInvoke.GetDpiForWindow(guiInfo.hwndActive) / 96d;
        var location = new Point(monitorInfo.rcWork.left, monitorInfo.rcWork.top);
        return (location, monitorInfo.rcWork.Size, dpi);
    }

    public static bool IsCapsLockState()
    {
        var capital = PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_CAPITAL);
        return capital != 0;
    }

    public static bool IsShiftState()
    {
        var shift = PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_SHIFT);
        return shift < 0;
    }
}
