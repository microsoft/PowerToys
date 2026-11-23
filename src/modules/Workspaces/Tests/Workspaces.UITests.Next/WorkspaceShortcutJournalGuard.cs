// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.Workspaces.UITests;

internal sealed class WorkspaceShortcutJournalGuard : IDisposable
{
    private readonly List<SafeFileHandle> files = [];

    private WorkspaceShortcutJournalGuard()
    {
    }

    public void Dispose()
    {
        foreach (var file in this.files)
        {
            file.Dispose();
        }

        this.files.Clear();
    }

    internal static WorkspaceShortcutJournalGuard AcquireForProfile(string workspaceDirectory)
    {
        Directory.CreateDirectory(workspaceDirectory);
        var profile = Open(workspaceDirectory, isDirectory: true) ?? throw new DirectoryNotFoundException(workspaceDirectory);

        try
        {
            // Keep the profile directory from being replaced between preflight and restoration.
            var guard = Acquire(Path.Combine(workspaceDirectory, "WorkspacesIcons"));
            guard.files.Add(profile);
            return guard;
        }
        catch
        {
            profile.Dispose();
            throw;
        }
    }

    internal static WorkspaceShortcutJournalGuard Acquire(string directory)
    {
        const string suffix = ".links.json";
        const int bracedGuidLength = 38;
        var guard = new WorkspaceShortcutJournalGuard();
        try
        {
            using var folder = Open(directory, isDirectory: true);
            if (folder is not null)
            {
                foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos("*" + suffix).OrderBy(entry => entry.Name, StringComparer.Ordinal))
                {
                    string name = entry.Name;
                    if (name.Length != bracedGuidLength + suffix.Length || !name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ||
                        !Guid.TryParseExact(name.AsSpan(0, bracedGuidLength), "B", out _))
                    {
                        continue;
                    }

                    // The editor only reads files. A reparse point still needs its tag checked.
                    if ((entry.Attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != FileAttributes.Directory &&
                        Open(entry.FullName, isDirectory: false) is { } file)
                    {
                        guard.files.Add(file);
                    }
                }
            }

            return guard;
        }
        catch
        {
            guard.Dispose();
            throw;
        }
    }

    private static SafeFileHandle? Open(string path, bool isDirectory)
    {
        const uint genericRead = 0x80000000;
        const uint openReparsePoint = 0x00200000;
        const uint backupSemantics = 0x02000000;
        const uint nameSurrogate = 0x20000000;
        const int fileAttributeTagInfo = 9;

        // Keep the directory name stable during enumeration. Deny reads of the existing journals
        // while the editor sees the test workspace list. Neither handle follows a redirected name.
        var handle = CreateFileW(path, genericRead, isDirectory ? FileShare.ReadWrite : FileShare.None, 0, FileMode.Open, openReparsePoint | backupSemantics, 0);
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            handle.Dispose();
            return error is 2 or 3 ? null : throw new Win32Exception(error, $"Could not protect the Workspaces test profile entry '{path}'.");
        }

        try
        {
            if (!GetFileInformationByHandleEx(handle, fileAttributeTagInfo, out var info, (uint)Marshal.SizeOf<AttributeTagInfo>()))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }

            if ((info.ReparseTag & nameSurrogate) != 0 || info.Attributes.HasFlag(FileAttributes.Directory) != isDirectory)
            {
                throw new IOException($"Unexpected or redirected Workspaces test profile entry '{path}'.");
            }

            return handle;
        }
        catch
        {
            handle.Dispose();
            throw;
        }
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

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int informationClass, out AttributeTagInfo info, uint size);
}
