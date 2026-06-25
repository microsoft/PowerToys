// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using ManagedCsWin32;

namespace Microsoft.CmdPal.Ext.PowerMode.Helpers;

internal static class ElevatedProcessHelper
{
    private const uint SeeMaskNoCloseProcess = 0x00000040;
    private const uint SeeMaskNoAsync = 0x00000100;
    private const uint SeeMaskNoConsole = 0x00008000;
    private const int SwHide = 0;
    private const int UacCancelledWin32Error = 1223;
    private const uint InfiniteWait = 0xFFFFFFFF;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    internal static bool TryRunElevated(string file, string arguments, out int exitCode, out int? win32Error)
    {
        exitCode = -1;
        win32Error = null;

        unsafe
        {
            fixed (char* filePtr = file)
            {
                fixed (char* argumentsPtr = arguments)
                {
                    fixed (char* verbPtr = "runas")
                    {
                        var info = new Shell32.SHELLEXECUTEINFOW
                        {
                            CbSize = (uint)sizeof(Shell32.SHELLEXECUTEINFOW),
                            FMask = SeeMaskNoCloseProcess | SeeMaskNoAsync | SeeMaskNoConsole,
                            LpVerb = (IntPtr)verbPtr,
                            LpFile = (IntPtr)filePtr,
                            LpParameters = (IntPtr)argumentsPtr,
                            Show = SwHide,
                        };

                        if (!Shell32.ShellExecuteEx(ref info))
                        {
                            win32Error = Marshal.GetLastWin32Error();
                            return false;
                        }

                        if (info.Process == IntPtr.Zero)
                        {
                            exitCode = 0;
                            return true;
                        }

                        _ = WaitForSingleObject(info.Process, InfiniteWait);
                        _ = GetExitCodeProcess(info.Process, out uint processExitCode);
                        exitCode = (int)processExitCode;
                        _ = CloseHandle(info.Process);
                        return exitCode == 0;
                    }
                }
            }
        }
    }

    internal static bool IsUacCancelled(int? win32Error) =>
        win32Error == UacCancelledWin32Error;
}
