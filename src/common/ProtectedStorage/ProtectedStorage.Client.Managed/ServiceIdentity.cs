// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PowerToys.ProtectedStorage;

internal static class ServiceIdentity
{
    internal static void VerifyRuntime(SafeProcessHandle runtime, string owner, string image)
    {
        using var manager = OpenSCManager(null, null, 1);
        using var service = OpenService(manager, $"PowerToysProtectedStorage_{owner}", 5);
        if (manager.IsInvalid || service.IsInvalid)
        {
            throw new ProtectedStorageException("Unauthorized");
        }

        QueryServiceConfig(service, IntPtr.Zero, 0, out int length);
        if (length <= 0 || length > 65536)
        {
            throw new ProtectedStorageException("Unauthorized");
        }

        IntPtr buffer = Marshal.AllocHGlobal(length);
        try
        {
            if (!QueryServiceConfig(service, buffer, length, out _))
            {
                throw new ProtectedStorageException("Unauthorized");
            }

            var config = Marshal.PtrToStructure<ServiceConfig>(buffer);
            string bootstrap = Path.Combine(Path.GetDirectoryName(image)!, "Bootstrap.exe");
            string expected = $"\"{bootstrap}\" --service \"{owner}\"";
            if (config.ServiceType != 0x10 ||
                !string.Equals(Marshal.PtrToStringUni(config.BinaryPath), expected, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Marshal.PtrToStringUni(config.StartName), $@"NT SERVICE\PowerToysProtectedStorage_{owner}", StringComparison.OrdinalIgnoreCase))
            {
                throw new ProtectedStorageException("Unauthorized");
            }

            if (!QueryServiceStatusEx(service, 0, out var status, Marshal.SizeOf<ServiceStatus>(), out _) ||
                status.ProcessId == 0 || status.CurrentState is not (2 or 4) ||
                NtQueryInformationProcess(runtime, 0, out var process, Marshal.SizeOf<ProcessInformation>(), out _) < 0 ||
                (ulong)process.ParentProcessId.ToInt64() != status.ProcessId)
            {
                throw new ProtectedStorageException("Unauthorized");
            }

            using var parent = OpenProcess(0x101000, false, status.ProcessId);
            var name = new char[32768];
            uint size = (uint)name.Length;
            if (parent.IsInvalid || WaitForSingleObject(parent, 0) != 258 || WaitForSingleObject(runtime, 0) != 258 ||
                !QueryFullProcessImageName(parent, 0, name, ref size) ||
                !string.Equals(new string(name, 0, (int)size), bootstrap, StringComparison.OrdinalIgnoreCase) ||
                !GetProcessTimes(parent, out long parentBirth, out _, out _, out _) ||
                !GetProcessTimes(runtime, out long birth, out _, out _, out _) || parentBirth > birth ||
                !QueryServiceStatusEx(service, 0, out var current, Marshal.SizeOf<ServiceStatus>(), out _) ||
                current.ProcessId != status.ProcessId)
            {
                throw new ProtectedStorageException("Unauthorized");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "OpenSCManagerW")]
    private static extern ServiceHandle OpenSCManager(string? machine, string? database, uint access);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "OpenServiceW")]
    private static extern ServiceHandle OpenService(ServiceHandle manager, string name, uint access);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "QueryServiceConfigW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceConfig(ServiceHandle service, IntPtr config, int size, out int needed);

    [DllImport("advapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryServiceStatusEx(ServiceHandle service, int level, out ServiceStatus status, int size, out int needed);

    [DllImport("advapi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseServiceHandle(IntPtr handle);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(SafeProcessHandle process, int informationClass, out ProcessInformation information, int size, out int needed);

    [DllImport("kernel32.dll")]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "QueryFullProcessImageNameW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, [Out] char[] image, ref uint size);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(SafeProcessHandle process, out long creation, out long exit, out long kernel, out long user);

    [DllImport("kernel32.dll")]
    private static extern uint WaitForSingleObject(SafeProcessHandle process, uint milliseconds);

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceConfig
    {
        public uint ServiceType;
        public uint StartType;
        public uint ErrorControl;
        public IntPtr BinaryPath;
        public IntPtr LoadOrderGroup;
        public uint TagId;
        public IntPtr Dependencies;
        public IntPtr StartName;
        public IntPtr DisplayName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatus
    {
        public uint ServiceType;
        public uint CurrentState;
        public uint ControlsAccepted;
        public uint Win32ExitCode;
        public uint ServiceSpecificExitCode;
        public uint CheckPoint;
        public uint WaitHint;
        public uint ProcessId;
        public uint ServiceFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Reserved1;
        public IntPtr Peb;
        public IntPtr Reserved2;
        public IntPtr Reserved3;
        public IntPtr ProcessId;
        public IntPtr ParentProcessId;
    }

    private sealed class ServiceHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public ServiceHandle()
            : base(true)
        {
        }

        protected override bool ReleaseHandle() => CloseServiceHandle(handle);
    }
}
