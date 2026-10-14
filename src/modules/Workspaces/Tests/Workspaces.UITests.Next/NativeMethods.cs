// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.Workspaces.UITests
{
    internal static class NativeMethods
    {
        private const int AppModelErrorNoPackage = 15700;
        private const int ErrorInsufficientBuffer = 122;
        private const uint ProcessQueryLimitedInformation = 0x1000;

        [DllImport("user32.dll")]
        internal static extern uint GetDpiForWindow(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsIconic(IntPtr window);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetPackageFullName(IntPtr process, ref uint length, StringBuilder? packageFullName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, int processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder imageName, ref uint length);

        internal static string ProcessImagePath(Process process)
        {
            using var handle = OpenProcess(ProcessQueryLimitedInformation, false, process.Id);
            if (handle.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not open PID {process.Id} for image lookup.");
            }

            // The image path is available before the process's module list is initialized.
            var name = new StringBuilder(32768);
            uint length = (uint)name.Capacity;
            if (!QueryFullProcessImageName(handle, 0, name, ref length))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"Could not read the image path for PID {process.Id}.");
            }

            return name.ToString();
        }

        internal static string? PackageFullName(int processId)
        {
            using var process = Process.GetProcessById(processId);
            uint length = 0;
            var result = GetPackageFullName(process.Handle, ref length, null);
            if (result == AppModelErrorNoPackage)
            {
                return null;
            }

            if (result != ErrorInsufficientBuffer)
            {
                throw new Win32Exception(result, $"Could not read the package identity for PID {processId}.");
            }

            var name = new StringBuilder(checked((int)length));
            result = GetPackageFullName(process.Handle, ref length, name);
            if (result != 0)
            {
                throw new Win32Exception(result, $"Could not read the package identity for PID {processId}.");
            }

            return name.ToString();
        }
    }
}
