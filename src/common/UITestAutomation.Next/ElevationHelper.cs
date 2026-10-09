// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.PowerToys.UITest.Next;

/// <summary>
/// Win32 helpers to determine whether a process is running elevated (admin). winappcli exposes no
/// elevation query, so this stays native. Useful for tests that must branch on, or assert, the
/// runner's elevation state.
/// </summary>
public static class ElevationHelper
{
    private const uint TOKEN_QUERY = 0x0008;

    // TOKEN_INFORMATION_CLASS.TokenElevation
    private const int TokenElevation = 20;

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(IntPtr tokenHandle, int tokenInformationClass, out uint tokenInformation, uint tokenInformationLength, out uint returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    /// <summary>True when the current test-host process is elevated.</summary>
    public static bool IsCurrentProcessElevated()
    {
        using var p = Process.GetCurrentProcess();
        return IsHandleElevated(p.Handle);
    }

    /// <summary>True when process <paramref name="processId"/> is elevated; null if it can't be queried.</summary>
    public static bool? IsProcessElevated(int processId)
    {
        try
        {
            using var p = Process.GetProcessById(processId);
            return IsHandleElevated(p.Handle);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsHandleElevated(IntPtr processHandle)
    {
        if (!OpenProcessToken(processHandle, TOKEN_QUERY, out var token))
        {
            return false;
        }

        try
        {
            return GetTokenInformation(token, TokenElevation, out var elevated, sizeof(uint), out _) && elevated != 0;
        }
        finally
        {
            CloseHandle(token);
        }
    }
}
