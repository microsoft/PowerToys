// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Win32.SafeHandles;
using MouseWithoutBorders.Core;

namespace MouseWithoutBorders;

internal sealed class LocalPathLease : IDisposable
{
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint GenericRead = 0x80000000;
    private const uint FileReadAttributes = 0x00000080;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint FileFlagOpenReparsePoint = 0x00200000;
    private const uint FileFlagSequentialScan = 0x08000000;

    private readonly object _lock = new();
    private List<SafeFileHandle> _handles;
    private SafeFileHandle _fileHandle;
    private int _referenceCount = 1;

    private LocalPathLease(
        string displayPath,
        bool isDirectory,
        long length,
        List<SafeFileHandle> handles,
        SafeFileHandle fileHandle)
    {
        DisplayPath = displayPath;
        IsDirectory = isDirectory;
        Length = length;
        _handles = handles;
        _fileHandle = fileHandle;
    }

    internal string DisplayPath { get; }

    internal bool IsDirectory { get; }

    internal long Length { get; }

    internal bool IsDisposed
    {
        get
        {
            lock (_lock)
            {
                return _referenceCount == 0;
            }
        }
    }

    internal LocalPathLease Acquire()
    {
        lock (_lock)
        {
            if (_referenceCount == 0)
            {
                return null;
            }

            _referenceCount++;
            return this;
        }
    }

    internal FileStream OpenReadStream(int bufferSize)
    {
        lock (_lock)
        {
            if (_referenceCount == 0 || IsDirectory || _fileHandle == null || _fileHandle.IsInvalid)
            {
                return null;
            }

            SafeFileHandle reopenedHandle = ReOpenFile(
                _fileHandle,
                GenericRead,
                FileShareRead,
                FileFlagSequentialScan);

            if (reopenedHandle.IsInvalid)
            {
                reopenedHandle.Dispose();
                return null;
            }

            try
            {
                return new FileStream(reopenedHandle, FileAccess.Read, bufferSize, false);
            }
            catch (Exception)
            {
                reopenedHandle.Dispose();
                return null;
            }
        }
    }

    public void Dispose()
    {
        List<SafeFileHandle> handles = null;

        lock (_lock)
        {
            if (_referenceCount == 0 || --_referenceCount != 0)
            {
                return;
            }

            handles = _handles;
            _handles = null;
            _fileHandle = null;
        }

        foreach (SafeFileHandle handle in handles)
        {
            handle.Dispose();
        }
    }

    internal static bool TryCreate(string path, out LocalPathLease lease)
    {
        LocalPathLease openedLease = null;
        bool impersonated = Launch.ImpersonateLoggedOnUserAndDoSomething(
            () => openedLease = TryCreateForCurrentUser(path));

        if (!impersonated)
        {
            openedLease?.Dispose();
            openedLease = null;
        }

        lease = openedLease;
        return lease != null;
    }

    internal static LocalPathLease TryCreateForCurrentUser(string path)
    {
        List<SafeFileHandle> handles = new();

        try
        {
            if (!TryGetLocalDevicePath(path, out string displayPath, out string deviceRoot, out string physicalPath))
            {
                return null;
            }

            string currentPath = deviceRoot + Path.DirectorySeparatorChar;
            if (!TryOpenComponent(
                currentPath,
                FileReadAttributes,
                FileShareRead | FileShareWrite,
                handles,
                out FileAttributes attributes,
                out _)
                || (attributes & FileAttributes.ReparsePoint) != 0)
            {
                return null;
            }

            string relativePath = physicalPath[(deviceRoot.Length + 1)..];
            string[] components = relativePath.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);

            long length = 0;
            for (int index = 0; index < components.Length; index++)
            {
                currentPath = Path.Combine(currentPath, components[index]);
                bool isLast = index == components.Length - 1;
                uint desiredAccess = isLast ? GenericRead : FileReadAttributes;
                uint shareMode = isLast
                    ? FileShareRead | FileShareWrite | FileShareDelete
                    : FileShareRead | FileShareWrite;
                if (!TryOpenComponent(
                    currentPath,
                    desiredAccess,
                    shareMode,
                    handles,
                    out attributes,
                    out long componentLength)
                    || (attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return null;
                }

                if (!isLast && (attributes & FileAttributes.Directory) == 0)
                {
                    return null;
                }

                if (isLast)
                {
                    length = componentLength;
                }
            }

            bool isDirectory = (attributes & FileAttributes.Directory) != 0;
            SafeFileHandle fileHandle = handles[^1];

            // Parent handles prevent component replacement while the final handle is
            // opened and verified. The final handle is identity-stable, so release
            // the parents afterward to preserve normal rename/delete behavior.
            for (int index = 0; index < handles.Count - 1; index++)
            {
                handles[index].Dispose();
            }

            handles = new List<SafeFileHandle> { fileHandle };
            LocalPathLease lease = new(displayPath, isDirectory, length, handles, fileHandle);
            handles = null;
            return lease;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            if (handles != null)
            {
                foreach (SafeFileHandle handle in handles)
                {
                    handle.Dispose();
                }
            }
        }
    }

    private static bool TryGetLocalDevicePath(
        string path,
        out string displayPath,
        out string deviceRoot,
        out string physicalPath)
    {
        return TryGetLocalDevicePath(
            path,
            root => new DriveInfo(root).DriveType,
            QueryDosDeviceTarget,
            out displayPath,
            out deviceRoot,
            out physicalPath);
    }

    internal static bool TryGetLocalDevicePath(
        string path,
        Func<string, DriveType> getDriveType,
        Func<string, string> queryDosDevice,
        out string displayPath,
        out string deviceRoot,
        out string physicalPath)
    {
        displayPath = null;
        deviceRoot = null;
        physicalPath = null;

        if (string.IsNullOrWhiteSpace(path)
            || path.StartsWith(@"\\", StringComparison.Ordinal)
            || path.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        string fullPath = Path.GetFullPath(path);
        string root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root)
            || root.Length != 3
            || root[1] != Path.VolumeSeparatorChar
            || getDriveType(root) is DriveType.Network or DriveType.NoRootDirectory or DriveType.Unknown)
        {
            return false;
        }

        string target = queryDosDevice(root[..2])?.TrimEnd(Path.DirectorySeparatorChar);
        if (!IsDirectLocalVolumeDeviceTarget(target))
        {
            return false;
        }

        displayPath = fullPath;
        deviceRoot = @"\\?\GLOBALROOT" + target;
        physicalPath = deviceRoot + fullPath[(root.Length - 1)..];
        return true;
    }

    private static string QueryDosDeviceTarget(string deviceName)
    {
        StringBuilder targetBuffer = new(32768);
        return QueryDosDevice(deviceName, targetBuffer, targetBuffer.Capacity) == 0
            ? null
            : targetBuffer.ToString().Split('\0')[0];
    }

    private static bool IsDirectLocalVolumeDeviceTarget(string target)
    {
        const string devicePrefix = @"\Device\";
        if (string.IsNullOrEmpty(target)
            || !target.StartsWith(devicePrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string deviceName = target[devicePrefix.Length..];

        // Only direct volume-device roots are accepted. SUBST/custom DOS mappings,
        // directory mount points, and cloud/symbolic-link/reparse-backed paths fail
        // closed because retaining only the final handle cannot stabilize path
        // components embedded inside the DOS-device target.
        return deviceName.Length > 0
            && !deviceName.Contains(Path.DirectorySeparatorChar)
            && !deviceName.Equals("Mup", StringComparison.OrdinalIgnoreCase)
            && !deviceName.Equals("LanmanRedirector", StringComparison.OrdinalIgnoreCase)
            && !deviceName.Equals("WebDavRedirector", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryOpenComponent(
        string path,
        uint desiredAccess,
        uint shareMode,
        List<SafeFileHandle> handles,
        out FileAttributes attributes,
        out long length)
    {
        attributes = default;
        length = 0;

        SafeFileHandle handle = CreateFile(
            path,
            desiredAccess,
            shareMode,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics | FileFlagOpenReparsePoint,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            handle.Dispose();
            return false;
        }

        if (!GetFileInformationByHandle(handle, out ByHandleFileInformation information))
        {
            handle.Dispose();
            return false;
        }

        handles.Add(handle);
        attributes = (FileAttributes)information.FileAttributes;
        length = ((long)information.FileSizeHigh << 32) | information.FileSizeLow;
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        internal uint FileAttributes;
        internal System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        internal System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        internal System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        internal uint VolumeSerialNumber;
        internal uint FileSizeHigh;
        internal uint FileSizeLow;
        internal uint NumberOfLinks;
        internal uint FileIndexHigh;
        internal uint FileIndexLow;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint QueryDosDevice(string deviceName, StringBuilder targetPath, int maximumLength);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeFileHandle ReOpenFile(
        SafeFileHandle originalFile,
        uint desiredAccess,
        uint shareMode,
        uint flagsAndAttributes);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation fileInformation);
}
