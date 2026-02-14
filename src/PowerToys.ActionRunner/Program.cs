// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PowerToys.ActionRunner;

internal sealed partial class Program
{
    private static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Environment.Exit(1);
            return;
        }

        switch (args[0])
        {
            case "-run-non-elevated":
                ExecuteRunNonElevated(args[1..]);
                break;
            default:
                Environment.Exit(1);
                break;
        }
    }

    private static void ExecuteRunNonElevated(string[] args)
    {
        string? target = null;
        string? pidFile = null;
        string? arguments = null;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg == "-target" && i + 1 < args.Length)
            {
                target = args[i + 1];
                i++;
                continue;
            }
            else if (arg == "-pidFile" && i + 1 < args.Length)
            {
                pidFile = args[i + 1];
                i++;
                continue;
            }

            arguments = args[i + 1] + " ";
            i++;

            if (target == null)
            {
                Environment.Exit(1);
                return;
            }

            if (!string.IsNullOrEmpty(pidFile))
            {
                IntPtr pidBuffer = IntPtr.Zero;
                IntPtr mapFile = OpenFileMapping(0x0002 /* FILE_MAP_WRITE */, false, pidFile);
                if (mapFile != IntPtr.Zero)
                {
                    pidBuffer = MapViewOfFile(mapFile, 0x001F /* FILE_MAP_ALL_ACCESS */, 0, 0, sizeof(uint));
                    if (pidBuffer != IntPtr.Zero)
                    {
                        Marshal.WriteInt32(pidBuffer, 0);
                    }
                }

                Process? p = Process.Start(new ProcessStartInfo
                {
                    FileName = target,
                    Arguments = arguments.Trim(),
                    UseShellExecute = true,
                });

                if (pidBuffer != IntPtr.Zero)
                {
                    Marshal.WriteInt32(pidBuffer, p?.Id ?? 0);
                    FlushViewOfFile(pidBuffer, sizeof(uint));
                    UnmapViewOfFile(pidBuffer);
                }

                if (mapFile != IntPtr.Zero)
                {
                    FlushFileBuffers(mapFile);
                    CloseHandle(mapFile);
                }
            }
        }
    }

    [LibraryImport("Kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial IntPtr OpenFileMapping(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, string lpName);

    [LibraryImport("Kernel32.dll", SetLastError = true)]
    private static partial IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, UIntPtr dwNumberOfBytesToMap);

    [LibraryImport("Kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnmapViewOfFile(IntPtr lpBaseAddress);

    [LibraryImport("Kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FlushViewOfFile(IntPtr lpBaseAddress, uint dwNumberOfBytesToFlush);

    [LibraryImport("Kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool FlushFileBuffers(IntPtr hFile);

    [LibraryImport("Kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr hObject);
}
