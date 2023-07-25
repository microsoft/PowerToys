// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using MouseJumpUI.NativeMethods;
using static MouseJumpUI.NativeMethods.Core;
using static MouseJumpUI.NativeMethods.User32;

namespace MouseJumpUI.HotKeys;

internal static class HotKeyHelper
{
    [SuppressMessage("SA1310", "SA1310:FieldNamesMustNotContainUnderscore", Justification = "Names match Win32 api")]
    public const uint WM_PRIV_UNREGISTER_HOTKEY = (uint)MESSAGE_TYPE.WM_USER + 2;

    [SuppressMessage("SA1310", "SA1310:FieldNamesMustNotContainUnderscore", Justification = "Names match Win32 api")]
    public const uint WM_PRIV_REGISTER_HOTKEY = (uint)MESSAGE_TYPE.WM_USER + 1;

    public static (ATOM ClassAtom, HWND Hwnd) CreateWindow(WNDPROC wndProc)
    {
        // see https://learn.microsoft.com/en-us/windows/win32/winmsg/using-messages-and-message-queues
        var hInstance = (HINSTANCE)Process.GetCurrentProcess().Handle;

        const string className = "FancyMouseMessageClass";

        // register the window class
        // see https://stackoverflow.com/a/30992796/3156906
        var wndClass = new WNDCLASSEXW(
            cbSize: (uint)Marshal.SizeOf(typeof(WNDCLASSEXW)),
            style: 0,
            lpfnWndProc: wndProc,
            cbClsExtra: 0,
            cbWndExtra: 0,
            hInstance: hInstance,
            hIcon: HICON.Null,
            hCursor: HCURSOR.Null,
            hbrBackground: HBRUSH.Null,
            lpszMenuName: PCWSTR.Null,
            lpszClassName: className,
            hIconSm: HICON.Null);
        var atom = User32.RegisterClassExW(
            unnamedParam1: wndClass);
        if (atom.Value == 0)
        {
            var lastWin32Error = Marshal.GetLastWin32Error();
            var message =
                $"{nameof(User32.RegisterClassExW)} failed with result {atom}. GetLastWin32Error returned '{lastWin32Error}'.";
            throw new InvalidOperationException(message, new Win32Exception(lastWin32Error));
        }

        // create the window
        // see https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#message-only-windows
        //     https://devblogs.microsoft.com/oldnewthing/20171218-00/?p=97595
        //     https://stackoverflow.com/a/30992796/3156906
        var hWnd = User32.CreateWindowExW(
            dwExStyle: 0,
            lpClassName: "FancyMouseMessageClass",
            lpWindowName: "FancyMouseMessageWindow",
            dwStyle: 0,
            x: 0,
            y: 0,
            nWidth: 300,
            nHeight: 400,
            hWndParent: HWND.HWND_MESSAGE, // message-only window
            hMenu: HMENU.Null,
            hInstance: hInstance,
            lpParam: LPVOID.Null);
        if (hWnd.IsNull)
        {
            var lastWin32Error = Marshal.GetLastWin32Error();
            var message =
                $"{nameof(User32.CreateWindowExW)} failed with result {hWnd}. GetLastWin32Error returned '{lastWin32Error}'.";
            throw new InvalidOperationException(message, new Win32Exception(lastWin32Error));
        }

        return (atom, hWnd);
    }

    public static void PostPrivateThreadMessage(DWORD nativeThreadId, uint messageId)
    {
        var uResult = User32.PostThreadMessageW(
            idThread: nativeThreadId,
            Msg: (MESSAGE_TYPE)messageId,
            wParam: WPARAM.Null,
            lParam: LPARAM.Null);
        if (!uResult)
        {
            var lastWin32Error = Marshal.GetLastWin32Error();
            var message =
                $"{nameof(User32.PostThreadMessageW)} failed with result {uResult}. GetLastWin32Error returned '{lastWin32Error}'.";
            throw new InvalidOperationException(message, new Win32Exception(lastWin32Error));
        }
    }

    public static void PostPrivateMessage(HWND hWnd, uint messageId)
    {
        var uResult = User32.PostMessageW(
            hWnd: hWnd,
            Msg: (MESSAGE_TYPE)messageId,
            wParam: WPARAM.Null,
            lParam: LPARAM.Null);
        if (!uResult)
        {
            var lastWin32Error = Marshal.GetLastWin32Error();
            var message =
                $"{nameof(User32.PostMessageW)} failed with result {uResult}. GetLastWin32Error returned '{lastWin32Error}'.";
            throw new InvalidOperationException(message, new Win32Exception(lastWin32Error));
        }
    }

    public static void RegisterHotKey(HWND hWnd, Keystroke hotKey, int hotKeyId)
    {
        var modifiers = (HOT_KEY_MODIFIERS)(hotKey ?? throw new InvalidOperationException()).Modifiers;
        var result = User32.RegisterHotKey(
            hWnd: hWnd,
            id: hotKeyId,
            fsModifiers: modifiers,
            vk: (uint)hotKey.Key);
        if (!result)
        {
            var lastWin32Error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"{nameof(User32.RegisterHotKey)} failed with result {result}. GetLastWin32Error returned '{lastWin32Error}'.",
                new Win32Exception(lastWin32Error));
        }
    }

    public static void UnregisterHotKey(HWND hWnd, int hotKeyId)
    {
        var result = User32.UnregisterHotKey(
            hWnd: hWnd,
            id: hotKeyId);
        if (!result)
        {
            var lastWin32Error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException(
                $"{nameof(User32.UnregisterHotKey)} failed with result {result}. GetLastWin32Error returned '{lastWin32Error}'.",
                new Win32Exception(lastWin32Error));
        }
    }
}
