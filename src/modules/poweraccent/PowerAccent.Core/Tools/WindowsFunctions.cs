using System.Runtime.InteropServices;
using Vanara.PInvoke;

namespace PowerAccent.Core.Tools;

internal static class WindowsFunctions
{
    public static void Insert(char c, bool back = false)
    {
        unsafe
        {
            if (back)
            {
                // Split in 2 different SendInput (Powershell doesn't take back issue)
                var inputsBack = new User32.INPUT[]
                {
                    new User32.INPUT {type = User32.INPUTTYPE.INPUT_KEYBOARD, ki = new User32.KEYBDINPUT {wVk = (ushort) User32.VK.VK_BACK}},
                    new User32.INPUT {type = User32.INPUTTYPE.INPUT_KEYBOARD, ki = new User32.KEYBDINPUT {wVk = (ushort) User32.VK.VK_BACK, dwFlags = User32.KEYEVENTF.KEYEVENTF_KEYUP}}
                };

                // DO NOT REMOVE Trace.WriteLine (Powershell doesn't take back issue)
                var temp1 = User32.SendInput((uint)inputsBack.Length, inputsBack, sizeof(User32.INPUT));
                System.Diagnostics.Trace.WriteLine(temp1);
            }

            // Letter
            var inputsInsert = new User32.INPUT[1]
            {
                new User32.INPUT {type = User32.INPUTTYPE.INPUT_KEYBOARD, ki = new User32.KEYBDINPUT {wVk = 0, dwFlags = User32.KEYEVENTF.KEYEVENTF_UNICODE, wScan = c}}
            };
            var temp2 = User32.SendInput((uint)inputsInsert.Length, inputsInsert, sizeof(User32.INPUT));
            System.Diagnostics.Trace.WriteLine(temp2);
        }
    }

    public static Point GetCaretPosition()
    {
        User32.GUITHREADINFO guiInfo = new User32.GUITHREADINFO();
        guiInfo.cbSize = (uint)Marshal.SizeOf(guiInfo);
        User32.GetGUIThreadInfo(0, ref guiInfo);
        System.Drawing.Point caretPosition = new System.Drawing.Point(guiInfo.rcCaret.left, guiInfo.rcCaret.top);
        User32.ClientToScreen(guiInfo.hwndCaret, ref caretPosition);

        if (caretPosition.X == 0)
        {
            System.Drawing.Point testPoint;
            User32.GetCaretPos(out testPoint);
            return testPoint;
        }

        return caretPosition;
    }

    public static (Point Location, Size Size, double Dpi) GetActiveDisplay()
    {
        User32.GUITHREADINFO guiInfo = new User32.GUITHREADINFO();
        guiInfo.cbSize = (uint)Marshal.SizeOf(guiInfo);
        User32.GetGUIThreadInfo(0, ref guiInfo);
        var res = User32.MonitorFromWindow(guiInfo.hwndActive, User32.MonitorFlags.MONITOR_DEFAULTTONEAREST);

        User32.MONITORINFO monitorInfo = new User32.MONITORINFO();
        monitorInfo.cbSize = (uint)Marshal.SizeOf(monitorInfo);
        User32.GetMonitorInfo(res, ref monitorInfo);

        double dpi = User32.GetDpiForWindow(guiInfo.hwndActive) / 96d;

        return (monitorInfo.rcWork.Location, monitorInfo.rcWork.Size, dpi);
    }

    public static bool IsCapitalState()
    {
        var capital = User32.GetKeyState((int)User32.VK.VK_CAPITAL);
        var shift = User32.GetKeyState((int)User32.VK.VK_SHIFT);
        return capital != 0 || shift < 0;
    }
}
