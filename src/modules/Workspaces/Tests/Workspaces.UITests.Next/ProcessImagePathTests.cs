// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Workspaces.UITests
{
    [TestClass]
    public sealed class ProcessImagePathTests
    {
        private const uint CreateSuspended = 0x00000004;
        private const uint CreateNoWindow = 0x08000000;

        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public void ImagePathIsAvailableBeforeModuleInitialization()
        {
            var executable = Path.Combine(AppContext.BaseDirectory, "Fixture", "Workspaces.TestApp.exe");
            Assert.IsTrue(File.Exists(executable), "The test fixture executable must be staged.");
            var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>() };
            if (!CreateProcess(executable, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, false, CreateSuspended | CreateNoWindow, IntPtr.Zero, null, ref startup, out var child))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var cleanupError = 0;
            uint cleanupWait = uint.MaxValue;
            try
            {
                using var process = Process.GetProcessById(checked((int)child.ProcessId));
                Assert.IsFalse(process.HasExited);
                var image = NativeMethods.ProcessImagePath(process);
                Assert.IsTrue(string.Equals(executable, image, StringComparison.OrdinalIgnoreCase), $"Unexpected process image: {image}");
            }
            finally
            {
                if (!TerminateProcess(child.Process, 0))
                {
                    cleanupError = Marshal.GetLastWin32Error();
                }
                else
                {
                    cleanupWait = WaitForSingleObject(child.Process, 5_000);
                }

                CloseHandle(child.Thread);
                CloseHandle(child.Process);
                if (cleanupError != 0 || cleanupWait != 0)
                {
                    TestContext.WriteLine($"Suspended fixture cleanup failed: error={cleanupError}, wait={cleanupWait}.");
                }
            }

            Assert.AreEqual(0, cleanupError, "Could not terminate the owned suspended fixture.");
            Assert.AreEqual(0U, cleanupWait, "The owned suspended fixture did not exit.");
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct StartupInfo
        {
            public int Size;
            public string? Reserved;
            public string? Desktop;
            public string? Title;
            public uint X;
            public uint Y;
            public uint Width;
            public uint Height;
            public uint XCountChars;
            public uint YCountChars;
            public uint FillAttribute;
            public uint Flags;
            public ushort ShowWindow;
            public ushort ReservedSize;
            public IntPtr ReservedData;
            public IntPtr StandardInput;
            public IntPtr StandardOutput;
            public IntPtr StandardError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessInfo
        {
            public IntPtr Process;
            public IntPtr Thread;
            public uint ProcessId;
            public uint ThreadId;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CreateProcess(string application, IntPtr commandLine, IntPtr processAttributes, IntPtr threadAttributes, [MarshalAs(UnmanagedType.Bool)] bool inheritHandles, uint flags, IntPtr environment, string? directory, ref StartupInfo startup, out ProcessInfo process);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateProcess(IntPtr process, uint code);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint WaitForSingleObject(IntPtr handle, uint timeout);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
