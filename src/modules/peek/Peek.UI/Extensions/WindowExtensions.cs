// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.UI.Extensions
{
    using System;
    using Microsoft.UI.Xaml;
    using Peek.UI.Native;
    using Windows.Foundation;
    using Windows.Win32;
    using Windows.Win32.Foundation;
    using Windows.Win32.Graphics.Gdi;
    using WinUIEx;

    public static class WindowExtensions
    {
        public static Size GetMonitorSize(this Window window)
        {
            var hwnd = new HWND(window.GetWindowHandle());
            var hwndDesktop = PInvoke.MonitorFromWindow(hwnd, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
            MONITORINFO info = new ();
            info.cbSize = 40;
            PInvoke.GetMonitorInfo(hwndDesktop, ref info);
            double monitorWidth = info.rcMonitor.left + info.rcMonitor.right;
            double monitorHeight = info.rcMonitor.bottom + info.rcMonitor.top;

            return new Size(monitorWidth, monitorHeight);
        }

        public static double GetMonitorScale(this Window window)
        {
            var hwnd = new HWND(window.GetWindowHandle());
            var dpi = PInvoke.GetDpiForWindow(new HWND(hwnd));
            double scalingFactor = dpi / 96d;

            return scalingFactor;
        }

        public static void BringToForeground(this Window window)
        {
            var windowHandle = window.GetWindowHandle();

            // Restore the window.
            _ = NativeMethods.SendMessage(windowHandle, NativeMethods.WM_SYSCOMMAND, NativeMethods.SC_RESTORE, -2);

            // Bring the window to the front.
            if (!NativeMethods.SetWindowPos(
                windowHandle,
                NativeMethods.HWND_TOP,
                0,
                0,
                0,
                0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_DRAWFRAME | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_SHOWWINDOW))
            {
                throw new InvalidOperationException("Failed to set window position.");
            }

            // Grab the SetForegroundWindow privilege from the shell process.
            AcquireForegroundPrivilege();

            // Make our window the foreground window.
            _ = NativeMethods.SetForegroundWindow(windowHandle);
        }

        private static void AcquireForegroundPrivilege()
        {
            IntPtr remoteProcessHandle = 0;
            IntPtr user32Handle = 0;
            IntPtr remoteThreadHandle = 0;

            try
            {
                // Get the handle of the shell window.
                IntPtr topHandle = NativeMethods.GetShellWindow();
                if (topHandle == 0)
                {
                    throw new InvalidOperationException("Failed to get the shell desktop window.");
                }

                // Open the process that owns it.
                IntPtr remoteProcessId = 0;
                NativeMethods.GetWindowThreadProcessId(topHandle, ref remoteProcessId);
                if (remoteProcessId == 0)
                {
                    throw new InvalidOperationException("Failed to get the shell process ID.");
                }

                remoteProcessHandle = NativeMethods.OpenProcess(NativeMethods.PROCESS_ALL_ACCESS, false, remoteProcessId);
                if (remoteProcessHandle == 0)
                {
                    throw new InvalidOperationException("Failed to open the shell process.");
                }

                // Get the address of the AllowSetForegroundWindow API.
                user32Handle = NativeMethods.LoadLibrary("user32.dll");
                IntPtr entryPoint = NativeMethods.GetProcAddress(user32Handle, "AllowSetForegroundWindow");

                // Create a remote thread in the other process and make it call the API.
                remoteThreadHandle = NativeMethods.CreateRemoteThread(
                    remoteProcessHandle,
                    0,
                    100000,
                    entryPoint,
                    NativeMethods.GetCurrentProcessId(),
                    0,
                    0);
                if (remoteThreadHandle == 0)
                {
                    throw new InvalidOperationException("Failed to create the remote thread.");
                }

                // Wait for the remote thread to terminate.
                _ = NativeMethods.WaitForSingleObject(remoteThreadHandle, 5000);
            }
            finally
            {
                if (remoteProcessHandle != 0)
                {
                    _ = NativeMethods.CloseHandle(remoteProcessHandle);
                }

                if (remoteThreadHandle != 0)
                {
                    _ = NativeMethods.CloseHandle(remoteThreadHandle);
                }

                if (user32Handle != 0)
                {
                    _ = NativeMethods.FreeLibrary(user32Handle);
                }
            }
        }
    }
}
