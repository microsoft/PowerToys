// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace PowerToysExtension.Helpers;

internal static class FancyZonesVirtualDesktop
{
    private const string VirtualDesktopsKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops";
    private const string SessionVirtualDesktopsKeyPrefix = @"Software\Microsoft\Windows\CurrentVersion\Explorer\SessionInfo\";
    private const string SessionVirtualDesktopsKeySuffix = @"\VirtualDesktops";
    private const string CurrentVirtualDesktopValue = "CurrentVirtualDesktop";
    private const string VirtualDesktopIdsValue = "VirtualDesktopIDs";

    public static string GetCurrentVirtualDesktopIdString()
    {
        var id = TryGetCurrentVirtualDesktopId()
            ?? TryGetCurrentVirtualDesktopIdFromSession()
            ?? TryGetFirstVirtualDesktopId()
            ?? Guid.Empty;

        return "{" + id.ToString().ToUpperInvariant() + "}";
    }

    private static Guid? TryGetCurrentVirtualDesktopId()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(VirtualDesktopsKey, writable: false);
            var bytes = key?.GetValue(CurrentVirtualDesktopValue) as byte[];
            return TryGetGuid(bytes);
        }
        catch
        {
            return null;
        }
    }

    private static Guid? TryGetCurrentVirtualDesktopIdFromSession()
    {
        try
        {
            if (!ProcessIdToSessionId((uint)Environment.ProcessId, out var sessionId))
            {
                return null;
            }

            var path = SessionVirtualDesktopsKeyPrefix + sessionId + SessionVirtualDesktopsKeySuffix;
            using var key = Registry.CurrentUser.OpenSubKey(path, writable: false);
            var bytes = key?.GetValue(CurrentVirtualDesktopValue) as byte[];
            return TryGetGuid(bytes);
        }
        catch
        {
            return null;
        }
    }

    private static Guid? TryGetFirstVirtualDesktopId()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(VirtualDesktopsKey, writable: false);
            var bytes = key?.GetValue(VirtualDesktopIdsValue) as byte[];
            if (bytes is null || bytes.Length < 16)
            {
                return null;
            }

            var first = new byte[16];
            Array.Copy(bytes, 0, first, 0, 16);
            return TryGetGuid(first);
        }
        catch
        {
            return null;
        }
    }

    private static Guid? TryGetGuid(byte[]? bytes)
    {
        try
        {
            if (bytes is null || bytes.Length < 16)
            {
                return null;
            }

            return new Guid(bytes.AsSpan(0, 16));
        }
        catch
        {
            return null;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ProcessIdToSessionId(uint dwProcessId, out uint pSessionId);
}
