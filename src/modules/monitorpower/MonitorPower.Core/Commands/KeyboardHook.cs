// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace MonitorPower;

internal sealed partial class KeyboardHook : IDisposable
{
    private const int HookTypeKeyboardLowLevel = 13;
    private const int WmKeydown = 0x0100;
    private const int VkEscape = 0x1B;

    private readonly LowLevelKeyboardProc _proc;
    private readonly Dictionary<int, Func<bool>> _keyActions = [];
    private nint _hookId = nint.Zero;
    private Thread? _hookThread;
    private CancellationTokenSource? _cts;

    public KeyboardHook()
    {
        _proc = HookCallback;
        _cts = new CancellationTokenSource();
        _hookThread = new Thread(HookThreadProc)
        {
            Name = "KeyboardHook",
            IsBackground = true,
        };
        _hookThread.Start();
    }

    public static KeyboardHook CreateWithEsc(Action onEsc)
    {
        var hook = new KeyboardHook();
        hook._keyActions[VkEscape] = () =>
        {
            onEsc();
            return true;
        };
        return hook;
    }

    public void Register(int vkCode, Action action)
    {
        lock (_keyActions)
        {
            _keyActions[vkCode] = () =>
            {
                action();
                return true;
            };
        }
    }

    public void RegisterConditional(int vkCode, Func<bool> handler)
    {
        lock (_keyActions)
        {
            _keyActions[vkCode] = handler;
        }
    }

    public void Unregister(int vkCode)
    {
        lock (_keyActions)
        {
            _keyActions.Remove(vkCode);
        }
    }

    private void HookThreadProc()
    {
        var moduleHandle = GetModuleHandle(null);
        _hookId = SetWindowsHookEx(HookTypeKeyboardLowLevel, _proc, moduleHandle, 0);

        if (_hookId == nint.Zero)
        {
            return;
        }

        try
        {
            while (_cts is { IsCancellationRequested: false })
            {
                var result = GetMessage(out var msg, nint.Zero, 0, 0);
                if (result == -1 || result == 0)
                {
                    break;
                }

                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }
        finally
        {
            if (_hookId != nint.Zero)
            {
                UnhookWindowsHookEx(_hookId);
                _hookId = nint.Zero;
            }
        }
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && wParam == WmKeydown)
        {
            var vkCode = Marshal.ReadInt32(lParam);
            lock (_keyActions)
            {
                if (_keyActions.TryGetValue(vkCode, out var handler) && handler())
                {
                    return (nint)1;
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

        if (_hookId != nint.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = nint.Zero;
        }

        _hookThread = null;
    }

    private delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public nint Hwnd;
        public uint Message;
        public nint WParam;
        public nint LParam;
        public uint Time;
        public int PtX;
        public int PtY;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint GetModuleHandle([MarshalAs(UnmanagedType.LPWStr)] string? lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetMessage(out MSG lpMsg, nint hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool TranslateMessage([In] ref MSG lpMsg);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint DispatchMessage([In] ref MSG lpmsg);
}
