// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Diagnostics;
using System.Runtime.InteropServices;

using MouseJump.Common.Interop;

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MouseJump.Common.Helpers;

public static class Win32Helper
{
    /// <summary>
    /// Creates a new "message only" window.
    /// </summary>
    /// <remarks>
    /// A message-only window is a window that is not visible and does not have a user interface.
    /// It exists solely to receive and process messages.
    /// These windows are used for background tasks that need to handle messages without displaying any graphical elements to the user.
    /// </remarks>
    public static Win32Window CreateMessageOnlyWindow(string className, string windowName, Win32WindowProc.WindowProcDelegate windowProcDelegate)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(className);
        ArgumentNullException.ThrowIfNullOrEmpty(windowName);
        ArgumentNullException.ThrowIfNull(windowProcDelegate);

        var windowClass = Win32Helper.RegisterWindowClass(className, windowProcDelegate);
        var window = Win32Helper.CreateWindow(windowClass, windowName);

        return window;
    }

    private static Win32WindowClass RegisterWindowClass(string className, Win32WindowProc.WindowProcDelegate windowProcDelegate)
    {
        ArgumentNullException.ThrowIfNullOrEmpty(className);
        ArgumentNullException.ThrowIfNull(windowProcDelegate);

        using var hModule = PInvoke.GetModuleHandle(null);
        var hInstance = (HINSTANCE)hModule.DangerousGetHandle();

        // register the window class
        // see https://stackoverflow.com/a/30992796/3156906
        var windowProc = new Win32WindowProc(windowProcDelegate);
        var classAtom = default(ushort);
        unsafe
        {
            fixed (char* classNamePtr = className)
            {
                var wndClass = new WNDCLASSEXW
                {
                    cbSize = (uint)Marshal.SizeOf<WNDCLASSEXW>(),
                    style = 0,
                    lpfnWndProc = windowProc.WndProcDelegate,
                    cbClsExtra = 0,
                    cbWndExtra = 0,
                    hInstance = hInstance,
                    hIcon = HICON.Null,
                    hCursor = HCURSOR.Null,
                    hbrBackground = HBRUSH.Null,
                    lpszMenuName = default,
                    lpszClassName = new PCWSTR(classNamePtr),
                    hIconSm = HICON.Null,
                };
                classAtom = PInvoke.RegisterClassEx(wndClass);
            }
        }

        ResultHandler.ThrowIfZero(result: classAtom, getLastError: true, memberName: nameof(PInvoke.RegisterClassEx));

        var windowClass = new Win32WindowClass(classAtom, className, windowProc);
        return windowClass;
    }

    private static Win32Window CreateWindow(Win32WindowClass windowClass, string windowName)
    {
        // create the window
        // see https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#message-only-windows
        //     https://devblogs.microsoft.com/oldnewthing/20171218-00/?p=97595
        //     https://stackoverflow.com/a/30992796/3156906
        var hWnd = default(HWND);

        using var hModule = PInvoke.GetModuleHandle(null);

        unsafe
        {
            hWnd = PInvoke.CreateWindowEx(
                dwExStyle: 0,
                lpClassName: windowClass.ClassName,
                lpWindowName: windowName,
                dwStyle: 0,
                X: 0,
                Y: 0,
                nWidth: 300,
                nHeight: 400,
                hWndParent: HWND.HWND_MESSAGE, // message-only window
                hMenu: null,
                hInstance: hModule,
                lpParam: null);
        }

        ResultHandler.ThrowIfZero(result: hWnd, getLastError: true, memberName: nameof(PInvoke.CreateWindowEx));

        var window = new Win32Window(windowClass, windowName, hWnd);
        return window;
    }
}
