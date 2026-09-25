// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Microsoft.CmdPal.UI.Utilities;

public static unsafe class TaskbarZOrder
{
    public static bool EnsureAboveTaskbar(nint windowHandle, nint taskbarHandle)
    {
        var window = new HWND(windowHandle);
        var taskbar = new HWND(taskbarHandle);
        if (window == taskbar || !PInvoke.IsWindowVisible(window) || !PInvoke.IsWindowVisible(taskbar))
        {
            return false;
        }

        var order = new WindowOrder { Window = window, Taskbar = taskbar };
        if (!PInvoke.EnumWindows(&FindFirstWindow, (LPARAM)(nint)(&order)) && !order.Found)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to check taskbar window z-order.");
        }

        if (!order.TaskbarIsAbove)
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

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static BOOL FindFirstWindow(HWND hwnd, LPARAM lParam)
    {
        var order = (WindowOrder*)lParam.Value;
        if (hwnd == order->Window || hwnd == order->Taskbar)
        {
            order->Found = true;
            order->TaskbarIsAbove = hwnd == order->Taskbar;
            return false;
        }

        return true;
    }

    private struct WindowOrder
    {
        public HWND Window;
        public HWND Taskbar;
        public bool Found;
        public bool TaskbarIsAbove;
    }
}
