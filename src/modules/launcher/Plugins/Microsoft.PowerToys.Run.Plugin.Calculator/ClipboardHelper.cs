// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.PowerToys.Run.Plugin.Calculator
{
    internal static class ClipboardHelper
    {
        private const uint UnicodeTextFormat = 13;
        private const uint MoveableZeroInitializedMemory = 0x0042;

        internal static void SetText(string text)
        {
            ArgumentNullException.ThrowIfNull(text);

            // Own the clipboard through a message-only window. Passing a null owner to
            // OpenClipboard followed by EmptyClipboard can make SetClipboardData fail.
            var window = CreateWindowEx(0, "STATIC", null, 0, 0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (window == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var memory = IntPtr.Zero;
            try
            {
                memory = GlobalAlloc(MoveableZeroInitializedMemory, (UIntPtr)checked((text.Length + 1) * sizeof(char)));
                if (memory == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                var pointer = GlobalLock(memory);
                if (pointer == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                try
                {
                    Marshal.Copy(text.ToCharArray(), 0, pointer, text.Length);
                }
                finally
                {
                    GlobalUnlock(memory);
                }

                var opened = false;
                for (var attempt = 0; attempt < 10; attempt++)
                {
                    opened = OpenClipboard(window);
                    if (opened)
                    {
                        break;
                    }

                    if (attempt < 9)
                    {
                        Thread.Sleep(10);
                    }
                }

                if (!opened)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                try
                {
                    if (!EmptyClipboard() || SetClipboardData(UnicodeTextFormat, memory) == IntPtr.Zero)
                    {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }

                    // The system owns the buffer after SetClipboardData succeeds. This
                    // eagerly stored text needs no OLE flush after releasing the clipboard.
                    memory = IntPtr.Zero;
                }
                finally
                {
                    CloseClipboard();
                }
            }
            finally
            {
                if (memory != IntPtr.Zero)
                {
                    GlobalFree(memory);
                }

                DestroyWindow(window);
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateWindowEx(uint extendedStyle, string className, string windowName, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyWindow(IntPtr window);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool OpenClipboard(IntPtr window);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint format, IntPtr memory);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr memory);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalUnlock(IntPtr memory);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr memory);
    }
}
