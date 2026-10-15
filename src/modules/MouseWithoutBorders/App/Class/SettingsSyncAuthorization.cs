// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security;
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
            return FromVerifiedConnection(stream, CanProcessManageServicePairing);
        }

        internal static SettingsSyncAuthorization FromVerifiedConnection(
            NamedPipeServerStream stream,
            Func<uint, bool> inspectProcess)
        {
            if (!NamedPipePeerVerification.TryGetClientProcessId(stream, out var processId))
            {
                return new SettingsSyncAuthorization(false);
            }

            try
            {
                return new SettingsSyncAuthorization(inspectProcess(processId));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception or UnauthorizedAccessException or SecurityException)
            {
                return new SettingsSyncAuthorization(false);
            }
        }

        private static bool CanProcessManageServicePairing(uint processId)
        {
            if (!TryOpenProcessTokenForMembership(processId, out var token))
            {
                return false;
            }

            using (token)
            using (var identity = new WindowsIdentity(token.DangerousGetHandle()))
            {
                var principal = new WindowsPrincipal(identity);
                return IsElevated(token) &&
                    principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        internal static bool TryOpenProcessTokenForMembership(uint processId, out SafeAccessTokenHandle token)
        {
            using var process = NativeMethods.OpenProcess(NativeMethods.ProcessQueryLimitedInformation, false, processId);
            if (process.IsInvalid)
            {
                token = SafeAccessTokenHandle.InvalidHandle;
                return false;
            }

            // WindowsPrincipal duplicates a primary token before checking membership.
            return NativeMethods.OpenProcessToken(process, NativeMethods.TokenQuery | NativeMethods.TokenDuplicate, out token);
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
            internal const uint ProcessQueryLimitedInformation = 0x1000;
            internal const uint TokenQuery = 0x0008;
            internal const uint TokenDuplicate = 0x0002;
            internal const int TokenElevation = 20;

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern SafeProcessHandle OpenProcess(
                uint processAccess,
                [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
                uint processId);

            [DllImport("advapi32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool OpenProcessToken(
                SafeProcessHandle processHandle,
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
