// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace LightSwitch.Cli.Ipc;

internal static partial class PipeServerIdentity
{
    private const uint ProcessQueryLimitedInformation = 0x1000;

    internal static string ExpectedServerPath => Path.Combine(AppContext.BaseDirectory, "LightSwitchService", "PowerToys.LightSwitchService.exe");

    internal static bool IsTrustedServer(PipeStream pipe) => IsTrustedServer(pipe, ExpectedServerPath);

    internal static unsafe bool IsTrustedServer(PipeStream pipe, string expectedPath)
    {
        if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint processId))
        {
            return false;
        }

        // Keep the process handle open throughout validation so its PID cannot be reused.
        using SafeProcessHandle process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
        if (process.IsInvalid)
        {
            return false;
        }

        Span<char> buffer = stackalloc char[4096];
        uint length = (uint)buffer.Length;
        fixed (char* imagePath = buffer)
        {
            if (!QueryFullProcessImageNameW(process, 0, imagePath, ref length))
            {
                return false;
            }
        }

        return PathsMatch(new string(buffer[..(int)length]), expectedPath);
    }

    internal static bool PathsMatch(string actualPath, string expectedPath)
        => string.Equals(Path.GetFullPath(actualPath), Path.GetFullPath(expectedPath), StringComparison.OrdinalIgnoreCase);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool QueryFullProcessImageNameW(SafeProcessHandle process, uint flags, char* imageName, ref uint length);
}
