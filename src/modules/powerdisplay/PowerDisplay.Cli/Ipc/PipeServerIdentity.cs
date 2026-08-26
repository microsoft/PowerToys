// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PowerDisplay.Cli.Ipc;

/// <summary>
/// Authenticates that the process on the other end of a connected named-pipe stream is exactly
/// the sibling <c>PowerToys.PowerDisplay.exe</c> next to this CLI executable.
/// <para>
/// This defends against a different (and possibly hostile) process squatting on the well-known
/// pipe name before the real app starts. It works even when the server (<c>PowerToys.PowerDisplay.exe</c>)
/// is elevated and the CLI is not: <c>PROCESS_QUERY_LIMITED_INFORMATION</c> only requires the ability
/// to open a handle to the process, not equal or higher privilege than the target.
/// </para>
/// </summary>
internal static partial class PipeServerIdentity
{
    /// <summary>The only file name this CLI ever trusts as a pipe server.</summary>
    internal const string ExpectedServerFileName = "PowerToys.PowerDisplay.exe";

    private const uint ProcessQueryLimitedInformation = 0x1000;

    // Generously larger than MAX_PATH; QueryFullProcessImageNameW never needs more than this for a
    // real installed-app path, and a short-lived stackalloc keeps this allocation-free.
    private const int ImageNameBufferLength = 4096;

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="pipe"/>'s server process image path matches
    /// the sibling <see cref="ExpectedServerFileName"/> in <see cref="AppContext.BaseDirectory"/>.
    /// </summary>
    /// <param name="pipe">A connected pipe stream (client- or server-side).</param>
    public static bool IsTrustedServer(PipeStream pipe)
        => IsTrustedServer(pipe, Path.Combine(AppContext.BaseDirectory, ExpectedServerFileName));

    /// <summary>
    /// Overload that accepts an explicit expected full path, so tests can verify the real
    /// <c>GetNamedPipeServerProcessId</c> / <c>QueryFullProcessImageNameW</c> round trip without
    /// depending on <see cref="AppContext.BaseDirectory"/> or a real <c>PowerToys.PowerDisplay.exe</c>.
    /// </summary>
    internal static bool IsTrustedServer(PipeStream pipe, string expectedFullPath)
    {
        ArgumentNullException.ThrowIfNull(pipe);
        ArgumentException.ThrowIfNullOrEmpty(expectedFullPath);

        if (!GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint serverProcessId))
        {
            return false;
        }

        string? actualPath = TryGetProcessImagePath(serverProcessId);
        return actualPath is not null && PathsMatch(actualPath, expectedFullPath);
    }

    /// <summary>Pure, case-insensitive full-path comparison. Exposed for focused unit testing.</summary>
    internal static bool PathsMatch(string actualFullPath, string expectedFullPath)
        => string.Equals(
            Path.GetFullPath(actualFullPath),
            Path.GetFullPath(expectedFullPath),
            StringComparison.OrdinalIgnoreCase);

    private static unsafe string? TryGetProcessImagePath(uint processId)
    {
        IntPtr processHandle = OpenProcess(ProcessQueryLimitedInformation, bInheritHandle: false, processId);
        if (processHandle == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            Span<char> buffer = stackalloc char[ImageNameBufferLength];
            uint size = (uint)buffer.Length;

            fixed (char* pBuffer = buffer)
            {
                if (!QueryFullProcessImageNameW(processHandle, 0, pBuffer, ref size))
                {
                    return null;
                }
            }

            return new string(buffer[..(int)size]);
        }
        finally
        {
            // Always close the handle, on every return path (success, failure, or exception).
            CloseHandle(processHandle);
        }
    }

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial IntPtr OpenProcess(uint dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwProcessId);

    [LibraryImport("kernel32.dll", SetLastError = true, EntryPoint = "QueryFullProcessImageNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool QueryFullProcessImageNameW(IntPtr hProcess, uint dwFlags, char* lpExeName, ref uint lpdwSize);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(IntPtr hObject);
}
