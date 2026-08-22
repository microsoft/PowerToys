// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

using Microsoft.Win32.SafeHandles;

namespace Microsoft.PowerToys.SettingsBackupRestore.Security;

/// <summary>
/// A directory root kept open while child objects are opened relative to its handle.
/// </summary>
public sealed class SecureDirectoryRoot : IDisposable
{
    internal const uint ReadOnlyDirectoryAccess = NativeMethods.FileReadData | NativeMethods.FileReadAttributes | NativeMethods.Synchronize;
    internal const uint WritableDirectoryAccess = NativeMethods.FileReadData | NativeMethods.FileWriteData | NativeMethods.FileAppendData |
                                                  NativeMethods.FileReadAttributes | NativeMethods.FileWriteAttributes | NativeMethods.Synchronize;

    private readonly SafeFileHandle handle;
    private bool disposed;

    private SecureDirectoryRoot(SafeFileHandle handle, string finalPath)
    {
        this.handle = handle;
        FinalPath = finalPath;
    }

    /// <summary>
    /// Gets the canonical path returned for the root handle.
    /// </summary>
    public string FinalPath { get; }

    internal Action<uint>? DirectoryOpenAccessObserver { get; set; }

    internal Action<string>? DirectoryBeforeValidationObserver { get; set; }

    /// <summary>
    /// Opens a real directory as a security root and rejects a reparse point at the root path.
    /// </summary>
    public static SecureDirectoryRoot Open(string path)
    {
        return Open(path, writable: true);
    }

    /// <summary>
    /// Opens a real directory with only the rights needed to read children.
    /// </summary>
    public static SecureDirectoryRoot OpenReadOnly(string path)
    {
        return Open(path, writable: false);
    }

    /// <summary>
    /// Creates a missing directory tree through an existing ancestor handle, then opens the requested root.
    /// </summary>
    public static SecureDirectoryRoot OpenOrCreate(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            return Open(fullPath);
        }

        string? ancestor = Path.GetDirectoryName(fullPath);
        while (!string.IsNullOrEmpty(ancestor) && !Directory.Exists(ancestor))
        {
            ancestor = Path.GetDirectoryName(ancestor);
        }

        if (string.IsNullOrEmpty(ancestor))
        {
            throw new DirectoryNotFoundException($"No existing ancestor was found for {fullPath}.");
        }

        using (SecureDirectoryRoot ancestorRoot = Open(ancestor))
        {
            string relativePath = Path.GetRelativePath(ancestor, fullPath);
            ancestorRoot.CreateDirectory(relativePath);
        }

        return Open(fullPath);
    }

    private static SecureDirectoryRoot Open(string path, bool writable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        SafeFileHandle rootHandle = NativeMethods.CreateFileW(
            Path.GetFullPath(path),
            writable ? WritableDirectoryAccess : ReadOnlyDirectoryAccess,
            NativeMethods.FileShareRead | NativeMethods.FileShareWrite | NativeMethods.FileShareDelete,
            IntPtr.Zero,
            NativeMethods.OpenExisting,
            NativeMethods.FileFlagBackupSemantics | NativeMethods.FileFlagOpenReparsePoint,
            IntPtr.Zero);

        if (rootHandle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            rootHandle.Dispose();
            throw new Win32Exception(error, $"Could not open security root: {path}");
        }

        try
        {
            FileHandleMetadata metadata = GetMetadata(rootHandle);
            ValidateMetadata(metadata, expectDirectory: true, rejectHardLinks: false);

            return new SecureDirectoryRoot(rootHandle, GetFinalPath(rootHandle));
        }
        catch
        {
            rootHandle.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens a non-reparse file for same-handle reads.
    /// </summary>
    public SecureFile OpenFileForRead(string relativePath, FileShare share = FileShare.Read)
    {
        return OpenFile(relativePath, FileAccess.Read, share, createNew: false, createParents: false, rejectHardLinks: false);
    }

    /// <summary>
    /// Opens a non-reparse, single-link file for same-handle overwrite without truncating it.
    /// </summary>
    public SecureFile OpenFileForOverwrite(string relativePath, FileShare share = FileShare.None)
    {
        return OpenFile(relativePath, FileAccess.ReadWrite, share, createNew: false, createParents: false, rejectHardLinks: true);
    }

    /// <summary>
    /// Creates a new file exclusively below this root and returns the exact handle to write.
    /// </summary>
    public SecureFile CreateNewFile(string relativePath)
    {
        return OpenFile(relativePath, FileAccess.ReadWrite, FileShare.None, createNew: true, createParents: true, rejectHardLinks: false);
    }

    internal SecureFile CreateNewFileInExistingDirectory(string relativePath)
    {
        return OpenFile(relativePath, FileAccess.ReadWrite, FileShare.None, createNew: true, createParents: false, rejectHardLinks: false);
    }

    /// <summary>
    /// Creates a directory and its parents without following reparse points.
    /// </summary>
    public void CreateDirectory(string relativePath)
    {
        ThrowIfDisposed();
        string normalized = SecurePath.NormalizeRelative(relativePath);
        using SafeFileHandle directory = OpenDirectoryChain(normalized.Split('\\'), createMissing: true, exclusiveLast: false);
    }

    internal bool DirectoryExists(string relativePath)
    {
        ThrowIfDisposed();
        string normalized = SecurePath.NormalizeRelative(relativePath);
        try
        {
            using SafeFileHandle directory = OpenDirectoryChain(normalized.Split('\\'), createMissing: false, exclusiveLast: false);
            return true;
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode is 2 or 3)
        {
            return false;
        }
    }

    /// <summary>
    /// Enumerates regular files below this root while validating each traversed directory and file handle.
    /// </summary>
    public IReadOnlyList<string> EnumerateFiles(bool recursive = true, Func<string, bool>? fileFilter = null)
    {
        ThrowIfDisposed();
        List<string> files = [];
        EnumerateFiles(files, string.Empty, recursive, fileFilter);
        return files;
    }

    /// <summary>
    /// Removes all validated entries below this app-owned root and marks the root itself for deletion.
    /// </summary>
    public void DeleteTree()
    {
        ThrowIfDisposed();
        DeleteTreeContents();
        DeleteSelf();
    }

    internal void CreateNewDirectory(string relativePath)
    {
        ThrowIfDisposed();
        string normalized = SecurePath.NormalizeRelative(relativePath);
        string[] components = normalized.Split('\\');
        SafeFileHandle? parentHandle = null;

        try
        {
            SafeFileHandle effectiveParent = handle;
            if (components.Length > 1)
            {
                parentHandle = OpenDirectoryChain(components[..^1], createMissing: false, exclusiveLast: false);
                effectiveParent = parentHandle;
            }

            SafeFileHandle directoryHandle = OpenRelative(
                effectiveParent,
                components[^1],
                WritableDirectoryAccess | NativeMethods.Delete,
                NativeMethods.FileShareRead | NativeMethods.FileShareWrite | NativeMethods.FileShareDelete,
                NativeMethods.FileCreate,
                NativeMethods.FileDirectoryFile | NativeMethods.FileOpenReparsePoint | NativeMethods.FileSynchronousIoNonAlert);
            try
            {
                DirectoryBeforeValidationObserver?.Invoke(normalized);
                ValidateOpenedHandle(directoryHandle, expectDirectory: true, rejectHardLinks: false);
            }
            catch (Exception validationException)
            {
                try
                {
                    MarkForDeletion(directoryHandle);
                }
                catch (Exception cleanupException)
                {
                    validationException.Data["CreatedDirectoryCleanupError"] = cleanupException;
                }
                finally
                {
                    directoryHandle.Dispose();
                }

                throw;
            }

            directoryHandle.Dispose();
        }
        finally
        {
            parentHandle?.Dispose();
        }
    }

    /// <summary>
    /// Creates an unpredictable child directory with create-new semantics and keeps its handle open.
    /// </summary>
    public SecureDirectoryRoot CreateExclusiveStagingDirectory(string prefix = "PowerToysRestore-")
    {
        return CreateExclusiveStagingDirectory(prefix, static () => RandomNumberGenerator.GetHexString(32).ToLowerInvariant(), beforeValidation: null);
    }

    internal SecureDirectoryRoot CreateExclusiveStagingDirectory(
        string prefix,
        Func<string> suffixFactory,
        Action? beforeValidation = null)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(suffixFactory);
        if (string.IsNullOrWhiteSpace(prefix) || prefix.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("The staging prefix is not a valid file-name prefix.", nameof(prefix));
        }

        for (int attempt = 0; attempt < 32; attempt++)
        {
            string childName = prefix + suffixFactory();
            SecurePath.NormalizeRelative(childName);
            SafeFileHandle childHandle;
            try
            {
                childHandle = OpenRelative(
                    handle,
                    childName,
                    WritableDirectoryAccess | NativeMethods.Delete,
                    NativeMethods.FileShareRead | NativeMethods.FileShareWrite | NativeMethods.FileShareDelete,
                    NativeMethods.FileCreate,
                    NativeMethods.FileDirectoryFile | NativeMethods.FileOpenReparsePoint | NativeMethods.FileSynchronousIoNonAlert);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode is 80 or 183)
            {
                // A cryptographically random collision is retried without opening an existing directory.
                continue;
            }

            try
            {
                beforeValidation?.Invoke();
                string childFinalPath = ValidateOpenedHandle(childHandle, expectDirectory: true, rejectHardLinks: false);
                return new SecureDirectoryRoot(childHandle, childFinalPath);
            }
            catch (Exception validationException)
            {
                try
                {
                    MarkForDeletion(childHandle);
                }
                catch (Exception cleanupException)
                {
                    validationException.Data["StagingCleanupError"] = cleanupException;
                }
                finally
                {
                    childHandle.Dispose();
                }

                throw;
            }
        }

        throw new IOException("Could not create an exclusive staging directory.");
    }

    internal void DeleteEntry(string relativePath, bool isDirectory)
    {
        ThrowIfDisposed();
        string normalized = SecurePath.NormalizeRelative(relativePath);
        string[] components = normalized.Split('\\');
        SafeFileHandle? parentHandle = null;

        try
        {
            SafeFileHandle effectiveParent = handle;
            if (components.Length > 1)
            {
                parentHandle = OpenDirectoryChain(components[..^1], createMissing: false, exclusiveLast: false);
                effectiveParent = parentHandle;
            }

            uint createOptions = (isDirectory ? NativeMethods.FileDirectoryFile : NativeMethods.FileNonDirectoryFile) |
                                 NativeMethods.FileOpenReparsePoint |
                                 NativeMethods.FileSynchronousIoNonAlert;
            using SafeFileHandle entryHandle = OpenRelative(
                effectiveParent,
                components[^1],
                NativeMethods.Delete | NativeMethods.FileReadAttributes | NativeMethods.Synchronize,
                NativeMethods.FileShareRead | NativeMethods.FileShareWrite | NativeMethods.FileShareDelete,
                NativeMethods.FileOpen,
                createOptions);
            ValidateOpenedHandle(entryHandle, expectDirectory: isDirectory, rejectHardLinks: !isDirectory);
            MarkForDeletion(entryHandle);
        }
        finally
        {
            parentHandle?.Dispose();
        }
    }

    internal void DeleteSelf()
    {
        ThrowIfDisposed();
        MarkForDeletion(handle);
    }

    private void EnumerateFiles(List<string> files, string relativeDirectory, bool recursive, Func<string, bool>? fileFilter)
    {
        string directoryPath = string.IsNullOrEmpty(relativeDirectory) ? FinalPath : Path.Combine(FinalPath, relativeDirectory);
        foreach (string entryPath in Directory.EnumerateFileSystemEntries(directoryPath, "*", SearchOption.TopDirectoryOnly))
        {
            string name = Path.GetFileName(entryPath);
            string relativePath = string.IsNullOrEmpty(relativeDirectory) ? name : Path.Combine(relativeDirectory, name);
            FileAttributes attributes = File.GetAttributes(entryPath);
            if ((attributes & FileAttributes.Directory) != 0)
            {
                if (!recursive)
                {
                    continue;
                }

                using SecureDirectoryRoot child = OpenSubdirectory(relativePath);
                foreach (string childFile in child.EnumerateFiles(fileFilter: childPath => fileFilter?.Invoke(Path.Combine(relativePath, childPath)) ?? true))
                {
                    files.Add(Path.Combine(relativePath, childFile));
                }
            }
            else
            {
                if (fileFilter?.Invoke(relativePath) == false)
                {
                    continue;
                }

                using SecureFile file = OpenFileForRead(relativePath);
                files.Add(relativePath);
            }
        }
    }

    private SecureDirectoryRoot OpenSubdirectory(string relativePath)
    {
        string normalized = SecurePath.NormalizeRelative(relativePath);
        SafeFileHandle directoryHandle = OpenDirectoryChain(normalized.Split('\\'), createMissing: false, exclusiveLast: false);
        try
        {
            string finalPath = ValidateOpenedHandle(directoryHandle, expectDirectory: true, rejectHardLinks: false);
            return new SecureDirectoryRoot(directoryHandle, finalPath);
        }
        catch
        {
            directoryHandle.Dispose();
            throw;
        }
    }

    private void DeleteTreeContents()
    {
        foreach (string entryPath in Directory.EnumerateFileSystemEntries(FinalPath, "*", SearchOption.TopDirectoryOnly).ToArray())
        {
            string name = Path.GetFileName(entryPath);
            FileAttributes attributes = File.GetAttributes(entryPath);
            if ((attributes & FileAttributes.Directory) != 0)
            {
                using (SecureDirectoryRoot child = OpenSubdirectory(name))
                {
                    child.DeleteTreeContents();
                }

                DeleteEntry(name, isDirectory: true);
            }
            else
            {
                DeleteEntry(name, isDirectory: false);
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (!disposed)
        {
            handle.Dispose();
            disposed = true;
        }
    }

    internal static FileHandleMetadata GetMetadata(SafeFileHandle fileHandle)
    {
        if (!NativeMethods.GetFileInformationByHandleEx(
                fileHandle,
                NativeMethods.FileInfoByHandleClass.FileAttributeTagInfo,
                out NativeMethods.FileAttributeTagInfo attributeInfo,
                (uint)Marshal.SizeOf<NativeMethods.FileAttributeTagInfo>()))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not query reparse metadata.");
        }

        if (!NativeMethods.GetFileInformationByHandleEx(
                fileHandle,
                NativeMethods.FileInfoByHandleClass.FileStandardInfo,
                out NativeMethods.FileStandardInfo standardInfo,
                (uint)Marshal.SizeOf<NativeMethods.FileStandardInfo>()))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not query standard file metadata.");
        }

        bool isReparsePoint = (((FileAttributes)attributeInfo.FileAttributes) & FileAttributes.ReparsePoint) != 0;
        return new FileHandleMetadata(
            standardInfo.Directory,
            isReparsePoint,
            attributeInfo.ReparseTag,
            standardInfo.NumberOfLinks,
            standardInfo.EndOfFile);
    }

    internal static string GetFinalPath(SafeFileHandle fileHandle)
    {
        char[] buffer = new char[512];
        while (true)
        {
            uint length = NativeMethods.GetFinalPathNameByHandleW(fileHandle, buffer, (uint)buffer.Length, 0);
            if (length == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not query final path by handle.");
            }

            if (length < buffer.Length)
            {
                return SecurePath.NormalizeFinalPath(new string(buffer, 0, checked((int)length)));
            }

            buffer = new char[checked((int)length + 1)];
        }
    }

    private SecureFile OpenFile(
        string relativePath,
        FileAccess access,
        FileShare share,
        bool createNew,
        bool createParents,
        bool rejectHardLinks)
    {
        ThrowIfDisposed();
        string normalized = SecurePath.NormalizeRelative(relativePath);
        string[] components = normalized.Split('\\');
        SafeFileHandle? parentHandle = null;
        List<string> createdDirectories = [];

        try
        {
            SafeFileHandle effectiveParent = handle;
            if (components.Length > 1)
            {
                parentHandle = OpenDirectoryChain(
                    components[..^1],
                    createMissing: createParents,
                    exclusiveLast: false,
                    createdDirectories);
                effectiveParent = parentHandle;
            }

            uint desiredAccess = AccessMask(access) | (createNew ? NativeMethods.Delete : 0);
            uint shareAccess = ShareMask(share);
            SafeFileHandle fileHandle = OpenRelative(
                effectiveParent,
                components[^1],
                desiredAccess,
                shareAccess,
                createNew ? NativeMethods.FileCreate : NativeMethods.FileOpen,
                NativeMethods.FileNonDirectoryFile | NativeMethods.FileOpenReparsePoint | NativeMethods.FileSynchronousIoNonAlert);

            try
            {
                string finalPath = ValidateOpenedHandle(fileHandle, expectDirectory: false, rejectHardLinks);
                return new SecureFile(fileHandle, access, finalPath);
            }
            catch (Exception validationException)
            {
                if (createNew)
                {
                    try
                    {
                        MarkForDeletion(fileHandle);
                    }
                    catch (Exception cleanupException)
                    {
                        validationException.Data["CreatedFileCleanupError"] = cleanupException;
                    }
                }

                fileHandle.Dispose();
                throw;
            }
        }
        catch (Exception exception)
        {
            RollbackCreatedDirectories(createdDirectories, exception);
            throw;
        }
        finally
        {
            parentHandle?.Dispose();
        }
    }

    private SafeFileHandle OpenDirectoryChain(
        string[] components,
        bool createMissing,
        bool exclusiveLast,
        List<string>? createdDirectories = null)
    {
        SafeFileHandle current = handle;
        SafeFileHandle? ownedCurrent = null;
        List<string> created = createdDirectories ?? [];
        int initialCreatedCount = created.Count;

        try
        {
            for (int index = 0; index < components.Length; index++)
            {
                bool createExclusively = createMissing && exclusiveLast && index == components.Length - 1;
                uint desiredAccess;
                SafeFileHandle next;
                bool wasCreated;
                if (!createMissing)
                {
                    desiredAccess = ReadOnlyDirectoryAccess;
                    next = OpenRelative(
                        current,
                        components[index],
                        desiredAccess,
                        NativeMethods.FileShareRead | NativeMethods.FileShareWrite | NativeMethods.FileShareDelete,
                        NativeMethods.FileOpen,
                        NativeMethods.FileDirectoryFile | NativeMethods.FileOpenReparsePoint | NativeMethods.FileSynchronousIoNonAlert,
                        out wasCreated);
                }
                else if (createExclusively)
                {
                    desiredAccess = WritableDirectoryAccess | NativeMethods.Delete;
                    next = OpenRelative(
                        current,
                        components[index],
                        desiredAccess,
                        NativeMethods.FileShareRead | NativeMethods.FileShareWrite | NativeMethods.FileShareDelete,
                        NativeMethods.FileCreate,
                        NativeMethods.FileDirectoryFile | NativeMethods.FileOpenReparsePoint | NativeMethods.FileSynchronousIoNonAlert,
                        out wasCreated);
                }
                else
                {
                    try
                    {
                        desiredAccess = ReadOnlyDirectoryAccess;
                        next = OpenRelative(
                            current,
                            components[index],
                            desiredAccess,
                            NativeMethods.FileShareRead | NativeMethods.FileShareWrite | NativeMethods.FileShareDelete,
                            NativeMethods.FileOpen,
                            NativeMethods.FileDirectoryFile | NativeMethods.FileOpenReparsePoint | NativeMethods.FileSynchronousIoNonAlert,
                            out wasCreated);
                    }
                    catch (Win32Exception exception) when (exception.NativeErrorCode is 2 or 3)
                    {
                        desiredAccess = WritableDirectoryAccess | NativeMethods.Delete;
                        next = OpenRelative(
                            current,
                            components[index],
                            desiredAccess,
                            NativeMethods.FileShareRead | NativeMethods.FileShareWrite | NativeMethods.FileShareDelete,
                            NativeMethods.FileCreate,
                            NativeMethods.FileDirectoryFile | NativeMethods.FileOpenReparsePoint | NativeMethods.FileSynchronousIoNonAlert,
                            out wasCreated);
                    }
                }

                DirectoryOpenAccessObserver?.Invoke(desiredAccess);

                try
                {
                    DirectoryBeforeValidationObserver?.Invoke(string.Join('\\', components[..(index + 1)]));
                    ValidateOpenedHandle(next, expectDirectory: true, rejectHardLinks: false);
                }
                catch (Exception validationException)
                {
                    if (wasCreated)
                    {
                        try
                        {
                            MarkForDeletion(next);
                        }
                        catch (Exception cleanupException)
                        {
                            validationException.Data["CreatedDirectoryCleanupError"] = cleanupException;
                        }
                    }

                    next.Dispose();
                    throw;
                }

                if (wasCreated)
                {
                    created.Add(string.Join('\\', components[..(index + 1)]));
                }

                ownedCurrent?.Dispose();
                ownedCurrent = next;
                current = next;
            }

            SafeFileHandle result = ownedCurrent ?? throw new InvalidOperationException("No directory component was opened.");
            ownedCurrent = null;
            return result;
        }
        catch (Exception exception)
        {
            ownedCurrent?.Dispose();
            ownedCurrent = null;
            if (created.Count > initialCreatedCount)
            {
                List<string> currentAttempt = created.GetRange(initialCreatedCount, created.Count - initialCreatedCount);
                created.RemoveRange(initialCreatedCount, created.Count - initialCreatedCount);
                RollbackCreatedDirectories(currentAttempt, exception);
            }

            throw;
        }
        finally
        {
            ownedCurrent?.Dispose();
        }
    }

    private string ValidateOpenedHandle(SafeFileHandle openedHandle, bool expectDirectory, bool rejectHardLinks)
    {
        FileHandleMetadata metadata = GetMetadata(openedHandle);
        ValidateMetadata(metadata, expectDirectory, rejectHardLinks);

        string finalPath = GetFinalPath(openedHandle);
        string currentRootPath = GetFinalPath(handle);
        if (!SecurePath.IsContained(currentRootPath, finalPath))
        {
            throw new IOException($"Opened handle escaped its root: {finalPath}");
        }

        return finalPath;
    }

    internal static void ValidateMetadata(FileHandleMetadata metadata, bool expectDirectory, bool rejectHardLinks)
    {
        if (metadata.IsDirectory != expectDirectory)
        {
            throw new IOException(expectDirectory ? "Expected a directory." : "Expected a file.");
        }

        if (metadata.IsReparsePoint)
        {
            throw new IOException($"Reparse point rejected (tag 0x{metadata.ReparseTag:X8}).");
        }

        if (rejectHardLinks && metadata.LinkCount != 1)
        {
            throw new IOException($"Existing target has {metadata.LinkCount} hard links; overwrite rejected before truncation.");
        }
    }

    private static SafeFileHandle OpenRelative(
        SafeFileHandle parent,
        string name,
        uint desiredAccess,
        uint shareAccess,
        uint disposition,
        uint createOptions)
    {
        return OpenRelative(parent, name, desiredAccess, shareAccess, disposition, createOptions, out _);
    }

    private static SafeFileHandle OpenRelative(
        SafeFileHandle parent,
        string name,
        uint desiredAccess,
        uint shareAccess,
        uint disposition,
        uint createOptions,
        out bool wasCreated)
    {
        IntPtr nameBuffer = IntPtr.Zero;
        IntPtr unicodeStringPointer = IntPtr.Zero;
        bool parentReferenceAdded = false;

        try
        {
            nameBuffer = Marshal.StringToHGlobalUni(name);
            NativeMethods.UnicodeString unicodeString = new()
            {
                Length = checked((ushort)(name.Length * sizeof(char))),
                MaximumLength = checked((ushort)((name.Length + 1) * sizeof(char))),
                Buffer = nameBuffer,
            };

            unicodeStringPointer = Marshal.AllocHGlobal(Marshal.SizeOf<NativeMethods.UnicodeString>());
            Marshal.StructureToPtr(unicodeString, unicodeStringPointer, fDeleteOld: false);

            parent.DangerousAddRef(ref parentReferenceAdded);
            NativeMethods.ObjectAttributes objectAttributes = new()
            {
                Length = Marshal.SizeOf<NativeMethods.ObjectAttributes>(),
                RootDirectory = parent.DangerousGetHandle(),
                ObjectName = unicodeStringPointer,
                Attributes = NativeMethods.ObjCaseInsensitive,
            };

            uint status = NativeMethods.NtCreateFile(
                out SafeFileHandle fileHandle,
                desiredAccess,
                ref objectAttributes,
                out NativeMethods.IoStatusBlock ioStatusBlock,
                IntPtr.Zero,
                (uint)FileAttributes.Normal,
                shareAccess,
                disposition,
                createOptions,
                IntPtr.Zero,
                0);

            if (unchecked((int)status) < 0)
            {
                fileHandle?.Dispose();
                int error = checked((int)NativeMethods.RtlNtStatusToDosError(status));
                throw new Win32Exception(error, $"Handle-relative open failed for '{name}'.");
            }

            wasCreated = ioStatusBlock.Information == new IntPtr(2);
            return fileHandle;
        }
        finally
        {
            if (parentReferenceAdded)
            {
                parent.DangerousRelease();
            }

            if (unicodeStringPointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(unicodeStringPointer);
            }

            if (nameBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(nameBuffer);
            }
        }
    }

    private static uint AccessMask(FileAccess access)
    {
        return access switch
        {
            FileAccess.Read => NativeMethods.GenericRead | NativeMethods.Synchronize,
            FileAccess.Write => NativeMethods.GenericWrite | NativeMethods.Synchronize,
            FileAccess.ReadWrite => NativeMethods.GenericRead | NativeMethods.GenericWrite | NativeMethods.Synchronize,
            _ => throw new ArgumentOutOfRangeException(nameof(access)),
        };
    }

    private static uint ShareMask(FileShare share)
    {
        uint result = 0;
        if ((share & FileShare.Read) != 0)
        {
            result |= NativeMethods.FileShareRead;
        }

        if ((share & FileShare.Write) != 0)
        {
            result |= NativeMethods.FileShareWrite;
        }

        if ((share & FileShare.Delete) != 0)
        {
            result |= NativeMethods.FileShareDelete;
        }

        return result;
    }

    private static void MarkForDeletion(SafeFileHandle fileHandle)
    {
        NativeMethods.FileDispositionInfo disposition = new() { DeleteFile = true };
        if (!NativeMethods.SetFileInformationByHandle(
                fileHandle,
                NativeMethods.FileInfoByHandleClass.FileDispositionInfo,
                ref disposition,
                (uint)Marshal.SizeOf<NativeMethods.FileDispositionInfo>()))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not delete app-owned staging entry by handle.");
        }
    }

    private void RollbackCreatedDirectories(List<string> createdDirectories, Exception originalException)
    {
        List<Exception> cleanupErrors = [];
        for (int index = createdDirectories.Count - 1; index >= 0; index--)
        {
            try
            {
                DeleteEntry(createdDirectories[index], isDirectory: true);
            }
            catch (Exception cleanupException)
            {
                cleanupErrors.Add(cleanupException);
            }
        }

        if (cleanupErrors.Count > 0)
        {
            originalException.Data["CreatedDirectoryCleanupErrors"] = cleanupErrors.ToArray();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
