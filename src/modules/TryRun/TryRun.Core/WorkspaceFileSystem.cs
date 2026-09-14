// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PowerToys.TryRun.Core;

internal static partial class WorkspaceFileSystem
{
    private const uint FileReadAttributes = 0x80;
    private const uint GenericRead = 0x80000000;
    private const uint OpenExisting = 3;
    private const uint BackupSemantics = 0x02000000;
    private const uint OpenReparsePoint = 0x00200000;

    internal sealed class DirectoryLease : IDisposable
    {
        private readonly List<SafeFileHandle> handles = [];

        public DirectoryLease(string path)
        {
            var ancestors = new Stack<string>();
            for (var directory = new DirectoryInfo(WorkspacePath.LocalPath(path)); directory is not null; directory = directory.Parent)
            {
                ancestors.Push(directory.FullName);
            }

            try
            {
                while (ancestors.TryPop(out var ancestor))
                {
                    handles.Add(OpenDirectory(ancestor));
                }

                Path = PhysicalDirectory.Resolve(handles[^1]);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public string Path { get; }

        public void Dispose()
        {
            for (var index = handles.Count - 1; index >= 0; index--)
            {
                handles[index].Dispose();
            }

            handles.Clear();
        }
    }

    internal static SafeFileHandle OpenDirectory(string path)
    {
        return Open(path, directory: true);
    }

    internal static FileStream OpenRead(string path)
    {
        return new FileStream(Open(path, directory: false), FileAccess.Read);
    }

    internal static string CreateDirectory(string path)
    {
        // Unlike Directory.CreateDirectory, this fails if any entry already
        // occupies the name. Export never merges with an existing directory.
        if (!CreateDirectoryW(path, IntPtr.Zero))
        {
            throw Error("Could not create a new result directory.");
        }

        return PhysicalDirectory.Resolve(path);
    }

    private static SafeFileHandle Open(string path, bool directory)
    {
        // Deny write/delete sharing while inspecting or copying. Do not follow
        // the final reparse point; directory leases also hold its ancestors.
        var handle = CreateFileW(path, directory ? FileReadAttributes : GenericRead, (uint)FileShare.Read, IntPtr.Zero, OpenExisting, BackupSemantics | OpenReparsePoint, IntPtr.Zero);
        try
        {
            if (handle.IsInvalid || !GetFileInformationByHandle(handle, out var info))
            {
                throw Error("Could not safely open a workspace entry.");
            }

            var attributes = (FileAttributes)info.Attributes;
            if ((attributes & FileAttributes.ReparsePoint) != 0 || ((attributes & FileAttributes.Directory) != 0) != directory || (!directory && info.NumberOfLinks != 1))
            {
                throw new IOException("Choose regular files and folders. Links, junctions, and hard-linked files are not supported.");
            }

            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private static IOException Error(string message)
    {
        return new IOException(message, new Win32Exception(Marshal.GetLastPInvokeError()));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public uint CreationTimeLow;
        public uint CreationTimeHigh;
        public uint LastAccessTimeLow;
        public uint LastAccessTimeHigh;
        public uint LastWriteTimeLow;
        public uint LastWriteTimeHigh;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    private static partial SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr securityAttributes, uint disposition, uint flags, IntPtr template);

    [LibraryImport("kernel32.dll", EntryPoint = "CreateDirectoryW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CreateDirectoryW(string path, IntPtr securityAttributes);

    [LibraryImport("kernel32.dll", EntryPoint = "GetFileInformationByHandle", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetFileInformationByHandle(SafeFileHandle handle, out FileInformation information);
}
