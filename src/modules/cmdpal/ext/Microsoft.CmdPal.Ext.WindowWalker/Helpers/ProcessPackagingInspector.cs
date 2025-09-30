// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.CmdPal.Ext.WindowWalker.Helpers;

internal static class ProcessPackagingInspector
{
#pragma warning disable SA1310 // Field names should not contain underscore
    private const int ERROR_INSUFFICIENT_BUFFER = 122;
    private const int APPMODEL_ERROR_NO_PACKAGE = 15700;
#pragma warning restore SA1310 // Field names should not contain underscore

    /// <summary>
    /// Inspect a process by PID and classify its packaging.
    /// </summary>
    public static ProcessPackagingInfo Inspect(int pid)
    {
        var hProcess = NativeMethods.OpenProcess(ProcessAccessFlags.QueryLimitedInformation, false, pid);
        using var process = new SafeProcessHandle(hProcess, true);
        if (process.IsInvalid)
        {
            return new ProcessPackagingInfo(
                pid,
                ProcessPackagingKind.Unknown,
                HasPackageIdentity: false,
                IsAppContainer: false,
                PackageFullName: null,
                LastError: Marshal.GetLastPInvokeError());
        }

        // 1) Check package identity
        var hasPackage = TryGetPackageFullName(process, out var packageFullName, out _);

        // 2) If packaged, check AppContainer -> strict UWP
        var isAppContainer = false;
        int? tokenErr = null;
        if (hasPackage)
        {
            isAppContainer = TryIsAppContainer(process, out tokenErr);
        }

        var kind =
            !hasPackage ? ProcessPackagingKind.UnpackagedWin32 :
            isAppContainer ? ProcessPackagingKind.UwpApp :
            ProcessPackagingKind.PackagedWin32;

        return new ProcessPackagingInfo(
            pid,
            kind,
            HasPackageIdentity: hasPackage,
            IsAppContainer: isAppContainer,
            PackageFullName: packageFullName,
            LastError: null);
    }

    private static bool TryGetPackageFullName(SafeProcessHandle hProcess, out string? packageFullName, out int? lastError)
    {
        packageFullName = null;
        lastError = null;

        uint len = 0;
        var rc = NativeMethods.GetPackageFullName(hProcess, ref len, null);
        if (rc == APPMODEL_ERROR_NO_PACKAGE)
        {
            return false; // no package identity
        }

        if (rc != ERROR_INSUFFICIENT_BUFFER && rc != 0)
        {
            lastError = rc;
            return false; // unexpected error
        }

        if (len == 0)
        {
            return false;
        }

        var sb = new StringBuilder((int)len);
        rc = NativeMethods.GetPackageFullName(hProcess, ref len, sb);
        if (rc == 0)
        {
            packageFullName = sb.ToString();
            return true;
        }

        lastError = rc;
        return false;
    }

    private static bool TryIsAppContainer(SafeProcessHandle hProcess, out int? lastError)
    {
        lastError = null;

        if (!NativeMethods.OpenProcessToken(hProcess, TokenAccess.TOKEN_QUERY, out var token))
        {
            lastError = Marshal.GetLastPInvokeError();
            return false; // can't decide; treat as not-UWP for classification
        }

        using (token)
        {
            if (!NativeMethods.GetTokenInformation(
                    token,
                    TOKEN_INFORMATION_CLASS.TokenIsAppContainer,
                    out var val,
                    sizeof(int),
                    out _))
            {
                lastError = Marshal.GetLastPInvokeError();
                return false;
            }

            return val != 0; // true => AppContainer (UWP)
        }
    }
}
