// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace WorkspacesEditor.Services
{
    internal static class WorkspaceShortcutFile
    {
        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint DeleteAccess = 0x10000;
        private const uint OpenReparsePoint = 0x00200000;

        internal static (bool Owned, string IconPath) Inspect(string path, Guid workspaceId)
        {
            using var stream = File.OpenRead(path);
            return WorkspaceShellLink.Inspect(ReadContents(stream), workspaceId);
        }

        internal static void Create(string path, byte[] contents, Action commitIcon)
        {
            // Publish a complete file, and let the filesystem arbitrate a race for a desktop name.
            string temporary = Path.Combine(Path.GetDirectoryName(path), Path.GetRandomFileName());
            using FileStream stream = OpenForChange(temporary, write: true, mode: FileMode.CreateNew);
            try
            {
                WriteContents(stream, contents);
                Rename(stream.SafeFileHandle, path);
                commitIcon();
            }
            catch
            {
                Delete(stream.SafeFileHandle);
                throw;
            }
        }

        internal static string Update(string path, string launcherPath, Guid workspaceId, string iconPath, Action commitIcon)
        {
            using FileStream stream = OpenForChange(path, write: true);
            byte[] original = ReadContents(stream);
            byte[] contents = WorkspaceShellLink.Create(launcherPath, workspaceId, iconPath, original);
            string originalName = GetNameOnDisk(stream.SafeFileHandle);
            bool renameSucceeded = false;
            try
            {
                WriteContents(stream, contents);
                string keptName = originalName;
                if (!string.Equals(originalName, Path.GetFileName(path), StringComparison.Ordinal))
                {
                    // This includes case-only changes and a requested name that was an 8.3 alias.
                    Rename(stream.SafeFileHandle, path);
                    renameSucceeded = true;
                    keptName = GetNameOnDisk(stream.SafeFileHandle);
                }

                commitIcon();
                return keptName;
            }
            catch
            {
                try
                {
                    WriteContents(stream, original);
                }
                finally
                {
                    if (renameSucceeded)
                    {
                        Rename(stream.SafeFileHandle, Path.Combine(Path.GetDirectoryName(path), originalName));
                    }
                }

                throw;
            }
        }

        internal static void Remove(string path, Guid workspaceId, string keepName)
        {
            using FileStream stream = OpenForChange(path, write: false);
            if (string.Equals(keepName, GetNameOnDisk(stream.SafeFileHandle), StringComparison.Ordinal))
            {
                // Both names designate the link we just published.
                return;
            }

            if (!WorkspaceShellLink.IsOwned(ReadContents(stream), workspaceId))
            {
                throw new IOException("The workspace shortcut was replaced before it could be removed.");
            }

            // Delete the file we inspected while the handle still excludes writers and replacement.
            Delete(stream.SafeFileHandle);
        }

        private static void Delete(SafeFileHandle handle)
        {
            if (!SetFileInformationByHandle(handle, FileInfoClass.Disposition, new byte[] { 1 }, 1))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        private static FileStream OpenForChange(string path, bool write, FileMode mode = FileMode.Open)
        {
            uint access = GenericRead | DeleteAccess;
            if (write)
            {
                access |= GenericWrite;
            }

            SafeFileHandle handle = CreateFileW(path, access, FileShare.Read, 0, mode, OpenReparsePoint, 0);
            try
            {
                if (handle.IsInvalid)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                if (!GetFileInformationByHandleEx(handle, FileInfoClass.AttributeTag, out AttributeTagInfo info, (uint)Marshal.SizeOf<AttributeTagInfo>()))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                // Cloud placeholders can be legitimate desktop files. A name-surrogate reparse
                // point (such as a filesystem symlink) designates a different file.
                const uint NameSurrogate = 0x20000000;
                if ((info.Attributes & FileAttributes.Directory) != 0 ||
                    ((info.Attributes & FileAttributes.ReparsePoint) != 0 && (info.ReparseTag & NameSurrogate) != 0))
                {
                    throw new IOException("The desktop entry is not a regular shortcut file.");
                }

                return new FileStream(handle, write ? FileAccess.ReadWrite : FileAccess.Read);
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }

        private static byte[] ReadContents(FileStream stream)
        {
            // Generated links are a few KiB. Leave oversized desktop files untouched.
            const int MaxShortcutBytes = 4 * 1024 * 1024;
            long length = stream.Length;
            if (length > MaxShortcutBytes)
            {
                throw new InvalidDataException("The desktop file is too large to be a workspace shortcut.");
            }

            var contents = new byte[(int)length];
            stream.ReadExactly(contents);
            return contents;
        }

        private static void WriteContents(FileStream stream, byte[] contents)
        {
            stream.Position = 0;
            stream.Write(contents);
            stream.SetLength(contents.Length);
            stream.Flush();
        }

        private static string GetNameOnDisk(SafeFileHandle handle)
        {
            var name = new StringBuilder(260);
            uint length = GetFinalPathNameByHandleW(handle, name, (uint)name.Capacity, 0);
            if (length >= name.Capacity)
            {
                name.EnsureCapacity(checked((int)length + 1));
                length = GetFinalPathNameByHandleW(handle, name, (uint)name.Capacity, 0);
            }

            if (length == 0 || length >= name.Capacity)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return Path.GetFileName(name.ToString());
        }

        private static void Rename(SafeFileHandle handle, string path)
        {
            // FILE_RENAME_INFO starts with a DWORD and an aligned HANDLE, followed by
            // the byte count and the UTF-16 name. A zero flag disallows replacement.
            byte[] name = Encoding.Unicode.GetBytes(path);
            int lengthOffset = 2 * IntPtr.Size;
            byte[] info = new byte[lengthOffset + sizeof(int) + name.Length + sizeof(char)];
            BitConverter.TryWriteBytes(info.AsSpan(lengthOffset), name.Length);
            name.CopyTo(info, lengthOffset + sizeof(int));
            if (!SetFileInformationByHandle(handle, FileInfoClass.Rename, info, (uint)info.Length))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        private enum FileInfoClass
        {
            Rename = 3,
            Disposition = 4,
            AttributeTag = 9,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AttributeTagInfo
        {
            public FileAttributes Attributes;
            public uint ReparseTag;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern SafeFileHandle CreateFileW(string path, uint access, FileShare share, nint security, FileMode disposition, uint flags, nint template);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, StringBuilder name, uint size, uint flags);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, FileInfoClass infoClass, out AttributeTagInfo info, uint size);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetFileInformationByHandle(SafeFileHandle handle, FileInfoClass infoClass, byte[] info, uint size);
    }
}
