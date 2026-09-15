// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace WorkspacesLauncherUI.IPC
{
    internal sealed class LauncherProcessIdentity : IDisposable
    {
        private readonly SafeProcessHandle _handle;

        private LauncherProcessIdentity(SafeProcessHandle handle)
        {
            _handle = handle;
        }

        internal static LauncherProcessIdentity Open(int processId)
        {
            return Open(processId, ObserveProcess);
        }

        internal static LauncherProcessIdentity Open(int processId, Func<SafeProcessHandle, ProcessObservation> observe)
        {
            const uint synchronizeAndQueryLimitedInformation = 0x00101000;
            var handle = NativeMethods.OpenProcess(synchronizeAndQueryLimitedInformation, false, (uint)processId);
            if (handle.IsInvalid)
            {
                var error = new Win32Exception(Marshal.GetLastWin32Error());
                handle.Dispose();
                throw error;
            }

            var identity = new LauncherProcessIdentity(handle);
            try
            {
                var observation = observe(handle);
                if (observation.ParentProcessId != (uint)processId)
                {
                    throw new InvalidDataException("The intended launcher is not this process's parent.");
                }

                var ownPath = observation.OwnPath;
                if (string.IsNullOrEmpty(ownPath) ||
                    !string.Equals(Path.GetFileName(ownPath), "PowerToys.WorkspacesLauncherUI.exe", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The launcher UI image identity is invalid.");
                }

                var expectedPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ownPath), "PowerToys.WorkspacesLauncher.exe"));
                var actualPath = Path.GetFullPath(observation.ImagePath);
                if (!string.Equals(expectedPath, actualPath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("The launcher is not the expected sibling executable.");
                }

                var ownVersion = observation.OwnVersion;
                if (ownVersion == (0, 0, 0, 0) || ownVersion != observation.ImageVersion)
                {
                    throw new InvalidDataException("The launcher file version does not match the UI.");
                }

                identity.ThrowIfExited();
                return identity;
            }
            catch
            {
                identity.Dispose();
                throw;
            }
        }

        internal void AuthenticatePipe(NamedPipeClientStream pipe, int processId)
        {
            if (!NativeMethods.GetNamedPipeServerProcessId(pipe.SafePipeHandle, out var serverProcessId))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (serverProcessId != (uint)processId)
            {
                throw new InvalidDataException("The pipe server is not the intended launcher.");
            }

            ThrowIfExited();
        }

        internal async Task WaitForExitAsync(CancellationToken cancellationToken)
        {
            using var waitHandle = new ProcessWaitHandle(_handle);
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var registration = ThreadPool.RegisterWaitForSingleObject(
                waitHandle,
                (_, _) => completion.TrySetResult(),
                null,
                Timeout.Infinite,
                executeOnlyOnce: true);
            try
            {
                await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                registration.Unregister(null);
            }
        }

        public void Dispose()
        {
            _handle.Dispose();
        }

        private static (int Major, int Minor, int Build, int Revision) GetNumericVersion(string path)
        {
            var version = FileVersionInfo.GetVersionInfo(path);
            return (version.FileMajorPart, version.FileMinorPart, version.FileBuildPart, version.FilePrivatePart);
        }

        private static ProcessObservation ObserveProcess(SafeProcessHandle handle)
        {
            var imagePath = new StringBuilder(32768);
            var length = (uint)imagePath.Capacity;
            if (!NativeMethods.QueryFullProcessImageName(handle, 0, imagePath, ref length))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var ownPath = Environment.ProcessPath;
            return new ProcessObservation(
                GetParentProcessId(),
                ownPath,
                imagePath.ToString(),
                string.IsNullOrEmpty(ownPath) ? default : GetNumericVersion(ownPath),
                GetNumericVersion(imagePath.ToString()));
        }

        internal static uint GetParentProcessId()
        {
            using var snapshot = NativeMethods.CreateToolhelp32Snapshot(0x00000002, 0);
            if (snapshot.IsInvalid)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>() };
            if (!NativeMethods.Process32First(snapshot, ref entry))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            do
            {
                if (entry.ProcessId == (uint)Environment.ProcessId)
                {
                    return entry.ParentProcessId;
                }
            }
            while (NativeMethods.Process32Next(snapshot, ref entry));

            throw new InvalidDataException("This process was not found in the process snapshot.");
        }

        private void ThrowIfExited()
        {
            const uint waitTimeout = 258;
            var result = NativeMethods.WaitForSingleObject(_handle, 0);
            if (result == uint.MaxValue)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if (result != waitTimeout)
            {
                throw new InvalidDataException("The intended launcher has exited.");
            }
        }

        private sealed class ProcessWaitHandle : WaitHandle
        {
            internal ProcessWaitHandle(SafeProcessHandle handle)
            {
                // The connection retains the owning process handle until this wait has finished.
                SafeWaitHandle = new SafeWaitHandle(handle.DangerousGetHandle(), ownsHandle: false);
            }
        }

        internal sealed record ProcessObservation(
            uint ParentProcessId,
            string OwnPath,
            string ImagePath,
            (int Major, int Minor, int Build, int Revision) OwnVersion,
            (int Major, int Minor, int Build, int Revision) ImageVersion);

        private sealed class SnapshotHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public SnapshotHandle()
                : base(ownsHandle: true)
            {
            }

            protected override bool ReleaseHandle()
            {
                return NativeMethods.CloseHandle(handle);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct ProcessEntry
        {
            internal uint Size;
            internal uint Usage;
            internal uint ProcessId;
            internal UIntPtr DefaultHeapId;
            internal uint ModuleId;
            internal uint Threads;
            internal uint ParentProcessId;
            internal int BasePriority;
            internal uint Flags;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            internal string ExeFile;
        }

        private static class NativeMethods
        {
            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, StringBuilder imagePath, ref uint size);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern SnapshotHandle CreateToolhelp32Snapshot(uint flags, uint processId);

            [DllImport("kernel32.dll", EntryPoint = "Process32FirstW", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool Process32First(SnapshotHandle snapshot, ref ProcessEntry entry);

            [DllImport("kernel32.dll", EntryPoint = "Process32NextW", CharSet = CharSet.Unicode, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool Process32Next(SnapshotHandle snapshot, ref ProcessEntry entry);

            [DllImport("kernel32.dll", SetLastError = true)]
            internal static extern uint WaitForSingleObject(SafeProcessHandle handle, uint milliseconds);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CloseHandle(IntPtr handle);
        }
    }
}
