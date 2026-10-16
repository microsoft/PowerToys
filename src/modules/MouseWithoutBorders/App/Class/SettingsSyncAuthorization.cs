// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;

using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.Win32.SafeHandles;

namespace MouseWithoutBorders.Class
{
    internal interface ISettingsSyncAuthorization
    {
        bool CanManageServicePairing { get; }
    }

    internal sealed class SettingsSyncAuthorization : ISettingsSyncAuthorization
    {
        internal SettingsSyncAuthorization(bool canManageServicePairing)
        {
            CanManageServicePairing = canManageServicePairing;
        }

        public bool CanManageServicePairing { get; }

        internal static SettingsSyncAuthorization FromVerifiedConnection(NamedPipeServerStream stream)
        {
            if (!NamedPipePeerVerification.TryGetClientProcessId(stream, out var processId))
            {
                return new SettingsSyncAuthorization(false);
            }

            try
            {
                using var process = Process.GetProcessById(unchecked((int)processId));
                if (!NativeMethods.OpenProcessToken(process.Handle, NativeMethods.TokenQuery, out var token))
                {
                    return new SettingsSyncAuthorization(false);
                }

                using (token)
                using (var identity = new WindowsIdentity(token.DangerousGetHandle()))
                {
                    var principal = new WindowsPrincipal(identity);
                    return new SettingsSyncAuthorization(
                        IsElevated(token) &&
                        principal.IsInRole(WindowsBuiltInRole.Administrator));
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return new SettingsSyncAuthorization(false);
            }
        }

        private static bool IsElevated(SafeAccessTokenHandle token)
        {
            return NativeMethods.GetTokenInformation(
                token,
                NativeMethods.TokenElevation,
                out var elevation,
                Marshal.SizeOf<uint>(),
                out _) &&
                elevation != 0;
        }

        private static class NativeMethods
        {
            internal const uint TokenQuery = 0x0008;
            internal const int TokenElevation = 20;

            [DllImport("advapi32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool OpenProcessToken(
                IntPtr processHandle,
                uint desiredAccess,
                out SafeAccessTokenHandle tokenHandle);

            [DllImport("advapi32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GetTokenInformation(
                SafeAccessTokenHandle tokenHandle,
                int tokenInformationClass,
                out uint tokenInformation,
                int tokenInformationLength,
                out int returnLength);
        }
    }
}
