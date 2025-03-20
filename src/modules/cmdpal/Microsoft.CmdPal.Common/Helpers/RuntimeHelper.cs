// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Security.Principal;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Microsoft.CmdPal.Common.Helpers;

public static class RuntimeHelper
{
    public static bool IsMSIX
    {
        get
        {
            // TODO: for whatever reason, when I ported this into the PT
            // codebase, this no longer compiled. We're only ever using it for
            // the hacked up settings and ignoring it anyways, so I'm leaving
            // it commented out for now.
            //
            // See also:
            // * https://github.com/microsoft/win32metadata/commit/6fee67ba73bfe1b126ce524f7de8d367f0317715
            // * https://github.com/microsoft/win32metadata/issues/1311
            // uint length = 0;
            // return PInvoke.GetCurrentPackageFullName(ref length, null) != WIN32_ERROR.APPMODEL_ERROR_NO_PACKAGE;
#pragma warning disable IDE0025 // Use expression body for property
            return true;
#pragma warning restore IDE0025 // Use expression body for property
        }
    }

    public static bool IsOnWindows11
    {
        get
        {
            var version = Environment.OSVersion.Version;
            return version.Major >= 10 && version.Build >= 22000;
        }
    }

    public static bool IsCurrentProcessRunningAsAdmin()
    {
        var identity = WindowsIdentity.GetCurrent();
        return identity.Owner?.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid) ?? false;
    }

    public static void VerifyCurrentProcessRunningAsAdmin()
    {
        if (!IsCurrentProcessRunningAsAdmin())
        {
            throw new UnauthorizedAccessException("This operation requires elevated privileges.");
        }
    }
}
