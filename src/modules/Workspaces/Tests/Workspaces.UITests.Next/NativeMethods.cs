// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Workspaces.UITests
{
    internal static class NativeMethods
    {
        private const int AppModelErrorNoPackage = 15700;
        private const int ErrorInsufficientBuffer = 122;

        [DllImport("user32.dll")]
        internal static extern uint GetDpiForWindow(IntPtr window);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsIconic(IntPtr window);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetPackageFullName(IntPtr process, ref uint length, StringBuilder? packageFullName);

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
