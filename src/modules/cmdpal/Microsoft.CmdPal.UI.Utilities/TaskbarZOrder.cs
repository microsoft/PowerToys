// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CmdPal.UI.Utilities;

public static class TaskbarZOrder
{
    public static bool EnsureAboveTaskbar(nint windowHandle, nint taskbarHandle)
    {
        var window = new HWND(windowHandle);
        var taskbar = new HWND(taskbarHandle);
        if (window == taskbar || !PInvoke.IsWindowVisible(window) || !PInvoke.IsWindowVisible(taskbar))
        {
            return false;
        }

        if (!IsTaskbarAbove(window, taskbar))
        {
            return false;
        }

        // Both windows can be topmost. Repair their relative order only when
        // necessary, rather than repeatedly covering menus and other popups.
        if (!PInvoke.SetWindowPos(
            window,
            HWND.HWND_TOPMOST,
            0,
            0,
            0,
            0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to restore taskbar window z-order.");
        }

        return true;
    }

    private static bool IsTaskbarAbove(HWND window, HWND taskbar)
    {
        // EnumWindows (and GetWindow's sibling traversal) can omit windows after
        // shell-band promotion. Walk the desktop's direct children instead.
        var desktop = PInvoke.GetDesktopWindow();
        var visited = new HashSet<HWND>();
        var taskbarSeen = false;
        var windowSeen = false;
        for (var hwnd = PInvoke.FindWindowEx(desktop, HWND.Null, null, null);
            !hwnd.IsNull && visited.Add(hwnd);
            hwnd = PInvoke.FindWindowEx(desktop, hwnd, null, null))
        {
            if (hwnd == window)
            {
                if (taskbarSeen)
                {
                    return true;
                }

                windowSeen = true;
            }
            else if (hwnd == taskbar)
            {
                if (windowSeen)
                {
                    return false;
                }

                taskbarSeen = true;
            }
        }

        // A window disappeared, was reparented, or reordered during the walk.
        // Leave the order alone until the next update rather than raise blindly.
        return false;
    }
}
