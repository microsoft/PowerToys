// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;

using Microsoft.Win32.SafeHandles;

#pragma warning disable SA1402 // Mouse Without Borders IPC helpers stay together intentionally.

namespace Microsoft.PowerToys.Settings.UI.Library.Utilities
{
    public static class MouseWithoutBordersIpc
    {
        public const string SettingsSyncProtocol = "PowerToys.MouseWithoutBorders.v2.SettingsSync";
        public const string SettingsExecutableFileName = "PowerToys.Settings.exe";
        public const string MouseWithoutBordersExecutableFileName = "PowerToys.MouseWithoutBorders.exe";

        public static string GetSettingsSyncPipeName(int sessionId)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(sessionId);

            return $"{SettingsSyncProtocol}.Session.{sessionId}";
        }

        public static void GrantCurrentProcessQueryAccess(SecurityIdentifier allowedUser)
        {
            ArgumentNullException.ThrowIfNull(allowedUser);

            using var processToken = OpenCurrentProcessTokenForDaclUpdate();
            SetKernelObjectDacl(
                processToken.DangerousGetHandle(),
                $"D:P(A;;0x{MouseWithoutBordersIpcNativeMethods.TokenQuery:X};;;{allowedUser.Value})(A;;GA;;;SY)(A;;GA;;;BA)");
            SetKernelObjectDacl(
                MouseWithoutBordersIpcNativeMethods.GetCurrentProcess(),
                $"D:P(A;;0x{MouseWithoutBordersIpcNativeMethods.ProcessQueryLimitedInformation:X};;;{allowedUser.Value})(A;;GA;;;SY)(A;;GA;;;BA)");
        }

        private static SafeAccessTokenHandle OpenCurrentProcessTokenForDaclUpdate()
        {
            if (!MouseWithoutBordersIpcNativeMethods.OpenCurrentProcessToken(
                    MouseWithoutBordersIpcNativeMethods.GetCurrentProcess(),
                    MouseWithoutBordersIpcNativeMethods.TokenQuery | MouseWithoutBordersIpcNativeMethods.ReadControl | MouseWithoutBordersIpcNativeMethods.WriteDac,
                    out var processToken))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return processToken;
        }

        private static void SetKernelObjectDacl(IntPtr handle, string sddl)
        {
            if (!MouseWithoutBordersIpcNativeMethods.ConvertStringSecurityDescriptorToSecurityDescriptor(
                    sddl,
                    MouseWithoutBordersIpcNativeMethods.SddlRevision1,
                    out var securityDescriptor,
                    out _))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                if (!MouseWithoutBordersIpcNativeMethods.SetKernelObjectSecurity(
                        handle,
                        MouseWithoutBordersIpcNativeMethods.DaclSecurityInformation,
                        securityDescriptor))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            finally
            {
                MouseWithoutBordersIpcNativeMethods.LocalFree(securityDescriptor);
            }
        }
    }

    public static class RestrictedNamedPipeServer
    {
        public static NamedPipeServerStream Create(string pipeName, SecurityIdentifier allowedUser)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
            ArgumentNullException.ThrowIfNull(allowedUser);

            var systemSid = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            var sddl = $"D:P(A;;GA;;;{allowedUser.Value})";
            if (!allowedUser.Equals(systemSid))
            {
                sddl += $"(A;;GA;;;{systemSid.Value})";
            }

            if (!MouseWithoutBordersIpcNativeMethods.ConvertStringSecurityDescriptorToSecurityDescriptor(
                    sddl,
                    MouseWithoutBordersIpcNativeMethods.SddlRevision1,
                    out var securityDescriptor,
                    out _))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            try
            {
                var securityAttributes = new MouseWithoutBordersIpcNativeMethods.SecurityAttributes
                {
                    Length = Marshal.SizeOf<MouseWithoutBordersIpcNativeMethods.SecurityAttributes>(),
                    SecurityDescriptor = securityDescriptor,
                    InheritHandle = false,
                };

                var handle = MouseWithoutBordersIpcNativeMethods.CreateNamedPipe(
                    $@"\\.\pipe\{pipeName}",
                    MouseWithoutBordersIpcNativeMethods.PipeAccessDuplex | MouseWithoutBordersIpcNativeMethods.FileFlagOverlapped | MouseWithoutBordersIpcNativeMethods.FileFlagFirstPipeInstance,
                    MouseWithoutBordersIpcNativeMethods.PipeTypeByte | MouseWithoutBordersIpcNativeMethods.PipeReadModeByte | MouseWithoutBordersIpcNativeMethods.PipeWait | MouseWithoutBordersIpcNativeMethods.PipeRejectRemoteClients,
                    1,
                    4096,
                    4096,
                    0,
                    ref securityAttributes);

                if (handle.IsInvalid)
                {
                    var error = Marshal.GetLastWin32Error();
                    handle.Dispose();
                    throw new Win32Exception(error);
                }

                return new NamedPipeServerStream(PipeDirection.InOut, true, false, handle);
            }
            finally
            {
                MouseWithoutBordersIpcNativeMethods.LocalFree(securityDescriptor);
            }
        }
    }

    internal static class MouseWithoutBordersIpcNativeMethods
    {
        internal const uint ProcessQueryLimitedInformation = 0x1000;
        internal const uint TokenQuery = 0x0008;
        internal const uint ReadControl = 0x00020000;
        internal const uint WriteDac = 0x00040000;
        internal const uint PipeAccessDuplex = 0x00000003;
        internal const uint FileFlagFirstPipeInstance = 0x00080000;
        internal const uint FileFlagOverlapped = 0x40000000;
        internal const uint PipeTypeByte = 0x00000000;
        internal const uint PipeReadModeByte = 0x00000000;
        internal const uint PipeWait = 0x00000000;
        internal const uint PipeRejectRemoteClients = 0x00000008;
        internal const uint SddlRevision1 = 1;
        internal const uint DaclSecurityInformation = 0x00000004;

        [StructLayout(LayoutKind.Sequential)]
        internal struct SecurityAttributes
        {
            internal int Length;
            internal IntPtr SecurityDescriptor;

            [MarshalAs(UnmanagedType.Bool)]
            internal bool InheritHandle;
        }

        [DllImport("kernel32.dll", EntryPoint = "CreateNamedPipeW", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafePipeHandle CreateNamedPipe(
            string name,
            uint openMode,
            uint pipeMode,
            uint maxInstances,
            uint outBufferSize,
            uint inBufferSize,
            uint defaultTimeout,
            ref SecurityAttributes securityAttributes);

        [DllImport("advapi32.dll", EntryPoint = "ConvertStringSecurityDescriptorToSecurityDescriptorW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(
            string stringSecurityDescriptor,
            uint stringSdRevision,
            out IntPtr securityDescriptor,
            out uint securityDescriptorSize);

        [DllImport("kernel32.dll")]
        internal static extern IntPtr LocalFree(IntPtr memory);

        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetCurrentProcess();

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetKernelObjectSecurity(
            IntPtr handle,
            uint securityInformation,
            IntPtr securityDescriptor);

        [DllImport("advapi32.dll", EntryPoint = "OpenProcessToken", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool OpenCurrentProcessToken(
            IntPtr processHandle,
            uint desiredAccess,
            out SafeAccessTokenHandle tokenHandle);
    }
}

#pragma warning restore SA1402
