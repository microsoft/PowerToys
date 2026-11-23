// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace WorkspacesEditor.Services
{
    internal static class WorkspaceShortcutFile
    {
        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint DeleteAccess = 0x10000;
        private const uint OpenReparsePoint = 0x00200000;

        internal static (bool Owned, string IconPath) Inspect(string path, Guid workspaceId, SharingRetryBudget retryBudget)
        {
            return Inspect(path, workspaceId, retryBudget, out _);
        }

        internal static (bool Owned, string IconPath) Inspect(string path, Guid workspaceId, SharingRetryBudget retryBudget, out byte[] ownedContents)
        {
            ownedContents = null;
            using FileStream stream = OpenFile(path, FileAccess.Read, forChange: false, retryBudget);
            byte[] contents = ReadContents(stream);
            var result = WorkspaceShellLink.Inspect(contents, workspaceId);
            if (result.Owned)
            {
                ownedContents = contents;
            }

            return result;
        }

        internal static void Create(string path, byte[] contents, Action commitIcon)
        {
            // Publish a complete file, and let the filesystem arbitrate a race for a desktop name.
            string temporary = Path.Combine(Path.GetDirectoryName(path), Path.GetRandomFileName());
            using FileStream stream = OpenFile(temporary, FileAccess.ReadWrite, forChange: true, retryBudget: null, mode: FileMode.CreateNew);
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

        internal static string Update(string path, string launcherPath, Guid workspaceId, string iconPath, Action commitIcon, SharingRetryBudget retryBudget = null)
        {
            using FileStream stream = OpenFile(path, FileAccess.ReadWrite, forChange: true, retryBudget);
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

        internal static void Remove(string path, Guid workspaceId, string keepName, SharingRetryBudget retryBudget = null)
        {
            using FileStream stream = OpenFile(path, FileAccess.Read, forChange: true, retryBudget);
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

        // Check and unlink the same directory entry while this handle excludes writers and replacement.
        internal static void DeleteIf(string path, Func<FileStream, bool> ownsFile)
        {
            using FileStream stream = OpenFile(path, FileAccess.Read, forChange: true, retryBudget: null);
            if (ownsFile(stream))
            {
                Delete(stream.SafeFileHandle);
            }
        }

        // Recheck the record with writers and replacement excluded, and move that same file.
        // A valid journal installed between the original read and this open is left in place.
        internal static void MoveIf(string path, string destination, Func<FileStream, bool> shouldMove)
        {
            using FileStream stream = OpenFile(path, FileAccess.Read, forChange: true, retryBudget: null);
            if (shouldMove(stream))
            {
                Rename(stream.SafeFileHandle, destination);
            }
        }

        private static void Delete(SafeFileHandle handle)
        {
            // Unlink the name when our handle closes, even if another reader is still using
            // the old file. Omitting IGNORE_READONLY_ATTRIBUTE preserves a read-only link.
            const uint DeleteWithPosixSemantics = 0x1 | 0x2;
            if (SetFileInformationByHandle(handle, FileInfoClass.DispositionEx, BitConverter.GetBytes(DeleteWithPosixSemantics), sizeof(uint)))
            {
                return;
            }

            var error = (FileError)Marshal.GetLastWin32Error();
            if (error is not (FileError.InvalidFunction or FileError.NotSupported or FileError.InvalidParameter))
            {
                throw new Win32Exception((int)error);
            }

            // Filesystems without POSIX disposition use ordinary delete-on-close semantics.
            if (!SetFileInformationByHandle(handle, FileInfoClass.Disposition, new byte[] { 1 }, 1))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        private static FileStream OpenFile(string path, FileAccess fileAccess, bool forChange, SharingRetryBudget retryBudget, FileMode mode = FileMode.Open)
        {
            uint access = forChange ? GenericRead | DeleteAccess : GenericRead;
            if (fileAccess == FileAccess.ReadWrite)
            {
                access |= GenericWrite;
            }

            uint flags = forChange ? OpenReparsePoint : 0;
            SafeFileHandle handle;
            while (true)
            {
                handle = CreateFileW(path, access, FileShare.Read, 0, mode, flags, 0);
                if (!handle.IsInvalid)
                {
                    break;
                }

                var error = (FileError)Marshal.GetLastWin32Error();
                handle.Dispose();
                if (error == FileError.FileNotFound)
                {
                    throw new FileNotFoundException("The workspace shortcut could not be found.", path);
                }

                if (mode != FileMode.Open || error is not (FileError.SharingViolation or FileError.LockViolation))
                {
                    throw new Win32Exception((int)error);
                }

                // A short-lived desktop reader can deny write/delete sharing while inspecting
                // a link. Ownership is checked on the acquired handle before any mutation.
                retryBudget ??= new SharingRetryBudget();
                if (!retryBudget.TryWait())
                {
                    throw new Win32Exception((int)error);
                }
            }

            try
            {
                if (forChange)
                {
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
                }

                return new FileStream(handle, fileAccess);
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

            if (length < 76)
            {
                // A Shell Link must start with the 76-byte ShellLinkHeader.
                throw new InvalidDataException("The desktop file has no complete Shell Link header.");
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

        // Inspect only the managed icon directory. A foreign shortcut can name an arbitrary
        // network location, and its icon must never be resolved merely to clean up our files.
        internal static bool MayReferenceIcon(string linkIcon, string iconPath)
        {
            if (string.IsNullOrWhiteSpace(linkIcon))
            {
                return false;
            }

            if (!TryGetIconPathSpelling(iconPath, out string managedPath, out _))
            {
                return false;
            }

            string candidateInput = linkIcon.Trim('"');
            if (string.Equals(candidateInput, iconPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            candidateInput = Environment.ExpandEnvironmentVariables(candidateInput);
            if (!TryGetIconPathSpelling(candidateInput, out string candidate, out bool verbatim) ||
                !string.Equals(Path.GetPathRoot(candidate), Path.GetPathRoot(managedPath), StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetExtension(candidate), ".ico", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (string.Equals(candidate, managedPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                // Only ordinary Win32 paths interpret dot segments and strip trailing periods.
                string path = verbatim ? candidate : ReadWindowsPath(buffer => GetFullPathNameW(candidate, (uint)buffer.Capacity, buffer, 0));
                if (string.Equals(path, managedPath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                string directory = Path.GetDirectoryName(managedPath);
                string trustedDirectory = Path.GetDirectoryName(iconPath);
                string candidateDirectory = Path.GetDirectoryName(path);
                if (!string.Equals(directory, candidateDirectory, StringComparison.OrdinalIgnoreCase) &&
                    !IsShortDirectorySpelling(directory, candidateDirectory, trustedDirectory))
                {
                    return false;
                }

                string leaf = Path.GetFileName(path);
                if (!WorkspaceShortcutJournal.IsValidName(Path.GetFileNameWithoutExtension(leaf)))
                {
                    return false;
                }

                // Use the trusted directory even when the link used a short name for an ancestor.
                using var managed = OpenIconMetadata(iconPath);
                using var referenced = OpenIconMetadata(Path.Combine(trustedDirectory, leaf));
                if (!TryReadFileId(managed, out var first) || !TryReadFileId(referenced, out var second))
                {
                    return true;
                }

                return first.Volume == second.Volume && first.Low == second.Low && first.High == second.High;
            }
            catch (Win32Exception ex)
            {
                // A missing file cannot reference the ICO. Preserve it when identity was inaccessible.
                return ex.NativeErrorCode is not (2 or 3);
            }
        }

        private static bool TryGetIconPathSpelling(string path, out string spelling, out bool verbatim)
        {
            const string ExtendedPrefix = @"\\?\";
            const string ExtendedUncPrefix = @"\\?\UNC\";
            verbatim = path.StartsWith(ExtendedPrefix, StringComparison.Ordinal);
            spelling = path.Replace('/', '\\');
            if (verbatim)
            {
                // The extended namespace takes separators and directory names literally. Removing
                // its prefix must not turn a literal path into a different file on the desktop.
                if (path.Contains('/'))
                {
                    return false;
                }

                bool unc = path.StartsWith(ExtendedUncPrefix, StringComparison.OrdinalIgnoreCase);
                string suffix = path[(unc ? ExtendedUncPrefix.Length : ExtendedPrefix.Length)..];
                if (!unc && (suffix.Length < 3 || !char.IsAsciiLetter(suffix[0]) || suffix[1] != ':' || suffix[2] != '\\'))
                {
                    return false;
                }

                string[] components = (unc ? suffix : suffix[3..]).Split('\\');
                if ((unc && components.Length < 2) ||
                    Array.Exists(components, component => component.Length == 0 || component is "." or ".."))
                {
                    return false;
                }

                spelling = unc ? @"\\" + suffix : suffix;
            }

            // Also reject device prefixes presented with mixed/forward separators.
            return Path.IsPathFullyQualified(spelling) &&
                !spelling.StartsWith(ExtendedPrefix, StringComparison.Ordinal) &&
                !spelling.StartsWith(@"\\.\", StringComparison.Ordinal) &&
                !spelling.StartsWith(@"\??\", StringComparison.Ordinal);
        }

        private static bool IsShortDirectorySpelling(string directory, string candidate, string trustedDirectory)
        {
            string[] longParts = directory.Split('\\');
            string[] candidateParts = candidate.Split('\\');
            if (candidateParts.Length != longParts.Length)
            {
                return false;
            }

            for (int i = 0; i < longParts.Length; i++)
            {
                // A differing component longer than an 8.3 name is not a DOS alias.
                if (candidateParts[i].Length > 12 && !string.Equals(candidateParts[i], longParts[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            string shortPath = ReadWindowsPath(buffer => GetShortPathNameW(trustedDirectory, buffer, (uint)buffer.Capacity));
            if (!TryGetIconPathSpelling(shortPath, out string shortDirectory, out _))
            {
                return false;
            }

            string[] shortParts = shortDirectory.Split('\\');
            for (int i = 0; i < longParts.Length; i++)
            {
                if (!string.Equals(candidateParts[i], longParts[i], StringComparison.OrdinalIgnoreCase) &&
                    (shortParts.Length != longParts.Length || !string.Equals(candidateParts[i], shortParts[i], StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }

            return true;
        }

        private static SafeFileHandle OpenIconMetadata(string path)
        {
            const uint ReadAttributes = 0x80;
            var handle = CreateFileW(path, ReadAttributes, FileShare.ReadWrite | FileShare.Delete, 0, FileMode.Open, OpenReparsePoint, 0);
            if (!handle.IsInvalid)
            {
                return handle;
            }

            int error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new Win32Exception(error);
        }

        private static bool TryReadFileId(SafeFileHandle handle, out FileIdentity id)
        {
            id = default;
            const uint NameSurrogate = 0x20000000;
            return GetFileInformationByHandleEx(handle, FileInfoClass.AttributeTag, out AttributeTagInfo info, (uint)Marshal.SizeOf<AttributeTagInfo>()) &&
                ((info.Attributes & FileAttributes.ReparsePoint) == 0 || (info.ReparseTag & NameSurrogate) == 0) &&
                GetFileInformationByHandleEx(handle, FileInfoClass.FileId, out id, (uint)Marshal.SizeOf<FileIdentity>()) &&
                (id.Low != 0 || id.High != 0);
        }

        private static string GetNameOnDisk(SafeFileHandle handle) =>
            Path.GetFileName(ReadWindowsPath(buffer => GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, 0)));

        private static string ReadWindowsPath(Func<StringBuilder, uint> read)
        {
            var name = new StringBuilder(260);
            uint length = read(name);
            if (length >= name.Capacity)
            {
                name.EnsureCapacity(checked((int)length + 1));
                length = read(name);
            }

            if (length == 0 || length >= name.Capacity)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            return name.ToString();
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
            FileId = 18,
            DispositionEx = 21,
        }

        private enum FileError
        {
            InvalidFunction = 1,
            FileNotFound = 2,
            SharingViolation = 32,
            LockViolation = 33,
            NotSupported = 50,
            InvalidParameter = 87,
        }

        internal sealed class SharingRetryBudget
        {
            private readonly Action<int> _wait;

            // Share the allowance across every name inspected or changed by one user action.
            private int _remaining = 6;

            internal SharingRetryBudget(Action<int> wait = null)
            {
                _wait = wait ?? Thread.Sleep;
            }

            internal bool TryWait()
            {
                if (_remaining == 0)
                {
                    return false;
                }

                _remaining--;
                _wait(25);
                return true;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AttributeTagInfo
        {
            public FileAttributes Attributes;
            public uint ReparseTag;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileIdentity
        {
            public ulong Volume;
            public ulong Low;
            public ulong High;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern SafeFileHandle CreateFileW(string path, uint access, FileShare share, nint security, FileMode disposition, uint flags, nint template);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, StringBuilder name, uint size, uint flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern uint GetFullPathNameW(string path, uint size, StringBuilder name, nint filePart);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static extern uint GetShortPathNameW(string path, StringBuilder name, uint size);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, FileInfoClass infoClass, out AttributeTagInfo info, uint size);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, FileInfoClass infoClass, out FileIdentity id, uint size);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetFileInformationByHandle(SafeFileHandle handle, FileInfoClass infoClass, byte[] info, uint size);
    }
}
