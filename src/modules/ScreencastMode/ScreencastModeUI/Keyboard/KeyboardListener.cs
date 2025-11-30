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
    /// <summary>
    /// A low-level keyboard hook that observes keystrokes without consuming them.
    /// This allows keystrokes to pass through to other applications while still
    /// being reported to the Screencast Mode overlay.
    /// </summary>
    internal sealed class KeyboardListener : IDisposable
    {
        private readonly HookProc _hookProc;

        private const int WHKEYBOARDLL = 13;

        private nint _windowsHookHandle;
        private nint _user32LibraryHandle;
        private bool _disposed;

        private delegate nint HookProc(int nCode, nint wParam, nint lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern nint LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern bool FreeLibrary(nint hModule);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern nint SetWindowsHookEx(int idHook, HookProc lpfn, nint hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(nint hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint VkCode;
            public uint ScanCode;
            public uint Flags;
            public uint Time;
            public nuint DwExtraInfo;
        }

        /// <summary>
        /// Event raised when a key is pressed or released.
        /// </summary>
        public event EventHandler<KeyboardEventArgs>? KeyboardEvent;

        public KeyboardListener()
        {
            _hookProc = LowLevelKeyboardProc;

            _user32LibraryHandle = LoadLibrary("User32");
            if (_user32LibraryHandle == nint.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                throw new Win32Exception(errorCode, $"Failed to load library 'User32.dll'. Error {errorCode}.");
            }

            _windowsHookHandle = SetWindowsHookEx(WHKEYBOARDLL, _hookProc, _user32LibraryHandle, 0);
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

                // Determine if this is a key down or key up event
                bool isKeyDown = message is 0x0100 or 0x0104; // WM_KEYDOWN or WM_SYSKEYDOWN

                // Raise the event
                KeyboardEvent?.Invoke(this, new KeyboardEventArgs(key, isKeyDown));
            }

            // Intially the PT InterOp Keyboard hook consumed the keystrokes.
            // The KeyboardListener is intended to observe keystrokes without consuming them
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

            if (_user32LibraryHandle != nint.Zero)
            {
                FreeLibrary(_user32LibraryHandle);
                _user32LibraryHandle = nint.Zero;
            }
        }
    }
}
