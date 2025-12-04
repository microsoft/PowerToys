// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.System;

namespace ScreencastModeUI.Keyboard
{
    internal sealed class KeyboardListener : IDisposable
    {
        private readonly HookProc _hookProc;
        private const int WHKEYBOARDLL = 13;
        private nint _windowsHookHandle;
        private bool _disposed;

        private delegate nint HookProc(int nCode, nint wParam, nint lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern nint SetWindowsHookEx(int idHook, HookProc lpfn, nint hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(nint hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern nint GetModuleHandle(string? lpModuleName);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint VkCode;
            public uint ScanCode;
            public uint Flags;
            public uint Time;
            public nuint DwExtraInfo;
        }

        public event EventHandler<KeyboardEventArgs>? KeyboardEvent;

        public KeyboardListener()
        {
            _hookProc = LowLevelKeyboardProc;

            nint currentModuleHandle = GetModuleHandle(null);
            _windowsHookHandle = SetWindowsHookEx(WHKEYBOARDLL, _hookProc, currentModuleHandle, 0);

            if (_windowsHookHandle == nint.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                throw new Win32Exception(errorCode, $"Failed to set keyboard hook. Error {errorCode}.");
            }
        }

        private nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam)
        {
            if (nCode >= 0)
            {
                var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                var key = (VirtualKey)hookStruct.VkCode;
                var message = wParam.ToInt32();

                // WM_KEYDOWN (0x0100) or WM_SYSKEYDOWN (0x0104)
                bool isKeyDown = message == 0x0100 || message == 0x0104;

                KeyboardEvent?.Invoke(this, new KeyboardEventArgs(key, isKeyDown));
            }

            return CallNextHookEx(_windowsHookHandle, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_windowsHookHandle != nint.Zero)
            {
                UnhookWindowsHookEx(_windowsHookHandle);
                _windowsHookHandle = nint.Zero;
            }

            // REMOVED: FreeLibrary logic
        }
    }
}
