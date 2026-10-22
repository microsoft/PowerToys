// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    /// <summary>
    /// Verifies and deletes an owned file through one Windows handle so a path replacement cannot
    /// redirect deletion to a newer file.
    /// </summary>
    public static partial class HandleBoundFileDeletion
    {
        private const uint DeleteAccess = 0x00010000;
        private const uint FileReadData = 0x00000001;
        private const uint FileReadAttributes = 0x00000080;
        private const uint SynchronizeAccess = 0x00100000;
        private const uint FileShareRead = 0x00000001;
        private const uint FileShareDelete = 0x00000004;
        private const uint OpenExisting = 3;
        private const uint FileAttributeNormal = 0x00000080;
        private const uint FileFlagSequentialScan = 0x08000000;
        private const uint FileFlagOpenReparsePoint = 0x00200000;
        private const uint InvalidFileAttributes = 0xFFFFFFFF;
        private const int ErrorFileNotFound = 2;
        private const int ErrorPathNotFound = 3;

        public static HandleBoundFileDeletionResult TryDelete(
            string path,
            byte[] expectedBytes,
            Action? beforeDisposition = null)
        {
            ArgumentNullException.ThrowIfNull(expectedBytes);

            if (string.IsNullOrWhiteSpace(path))
            {
                return Failure(
                    HandleBoundFileDeletionStatus.VerificationFailed,
                    path ?? string.Empty,
                    "The owned file path is empty.");
            }

            if (!OperatingSystem.IsWindows())
            {
                return Failure(
                    HandleBoundFileDeletionStatus.HandleAcquisitionFailed,
                    path,
                    $"Handle-bound cleanup is only supported on Windows for '{path}'.",
                    new PlatformNotSupportedException());
            }

            string nativePath;
            try
            {
                nativePath = ToExtendedLengthPath(path);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return Failure(
                    HandleBoundFileDeletionStatus.HandleAcquisitionFailed,
                    path,
                    $"The owned file path '{path}' could not be resolved.",
                    ex);
            }

            using (SafeFileHandle handle = CreateFile(
                       nativePath,
                       FileReadData | FileReadAttributes | DeleteAccess | SynchronizeAccess,
                       FileShareRead | FileShareDelete,
                       IntPtr.Zero,
                       OpenExisting,
                       FileAttributeNormal | FileFlagSequentialScan | FileFlagOpenReparsePoint,
                       IntPtr.Zero))
            {
                if (handle.IsInvalid)
                {
                    int error = Marshal.GetLastPInvokeError();
                    if (IsNotFound(error))
                    {
                        return new HandleBoundFileDeletionResult(
                            HandleBoundFileDeletionStatus.NotFound,
                            path,
                            Error: null);
                    }

                    return Win32Failure(
                        HandleBoundFileDeletionStatus.HandleAcquisitionFailed,
                        path,
                        $"The owned file '{path}' could not be opened for handle-bound cleanup.",
                        error);
                }

                try
                {
                    if (!MatchesExpectedBytes(handle, expectedBytes))
                    {
                        return Failure(
                            HandleBoundFileDeletionStatus.IdentityMismatch,
                            path,
                            $"The file at '{path}' is not the owned cleanup artifact.");
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
                {
                    return Failure(
                        HandleBoundFileDeletionStatus.VerificationFailed,
                        path,
                        $"The owned file '{path}' could not be verified through its cleanup handle.",
                        ex);
                }

                try
                {
                    beforeDisposition?.Invoke();
                }
                catch (Exception ex)
                {
                    return Failure(
                        HandleBoundFileDeletionStatus.DispositionFailed,
                        path,
                        $"Cleanup of the owned file '{path}' was interrupted before disposition.",
                        ex);
                }

                var disposition = new FileDispositionInfo { DeleteFile = 1 };
                if (!SetFileInformationByHandle(
                        handle,
                        FileInfoByHandleClass.FileDispositionInfo,
                        ref disposition,
                        (uint)Marshal.SizeOf<FileDispositionInfo>()))
                {
                    return Win32Failure(
                        HandleBoundFileDeletionStatus.DispositionFailed,
                        path,
                        $"The owned file '{path}' could not be marked for deletion through its verified handle.",
                        Marshal.GetLastPInvokeError());
                }
            }

            uint attributes = GetFileAttributes(nativePath);
            if (attributes != InvalidFileAttributes)
            {
                return Failure(
                    HandleBoundFileDeletionStatus.PathStillPresent,
                    path,
                    $"The owned file object was deleted, but '{path}' now refers to another or still-pending file.");
            }

            int probeError = Marshal.GetLastPInvokeError();
            if (!IsNotFound(probeError))
            {
                return Win32Failure(
                    HandleBoundFileDeletionStatus.PathVerificationFailed,
                    path,
                    $"Deletion of the owned file object succeeded, but the pathname '{path}' could not be verified absent.",
                    probeError);
            }

            return new HandleBoundFileDeletionResult(
                HandleBoundFileDeletionStatus.Deleted,
                path,
                Error: null);
        }

        private static bool MatchesExpectedBytes(SafeFileHandle handle, byte[] expectedBytes)
        {
            if (RandomAccess.GetLength(handle) != expectedBytes.LongLength)
            {
                return false;
            }

            byte[] actualBytes = new byte[expectedBytes.Length];
            int totalRead = 0;
            while (totalRead < actualBytes.Length)
            {
                int read = RandomAccess.Read(handle, actualBytes.AsSpan(totalRead), totalRead);
                if (read == 0)
                {
                    return false;
                }

                totalRead += read;
            }

            return actualBytes.AsSpan().SequenceEqual(expectedBytes);
        }

        private static string ToExtendedLengthPath(string path)
        {
            string fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(@"\\?\", StringComparison.Ordinal)
                || fullPath.StartsWith(@"\\.\", StringComparison.Ordinal))
            {
                return fullPath;
            }

            return fullPath.StartsWith(@"\\", StringComparison.Ordinal)
                ? @"\\?\UNC\" + fullPath[2..]
                : @"\\?\" + fullPath;
        }

        private static bool IsNotFound(int error)
            => error is ErrorFileNotFound or ErrorPathNotFound;

        private static HandleBoundFileDeletionResult Win32Failure(
            HandleBoundFileDeletionStatus status,
            string path,
            string message,
            int error)
            => Failure(status, path, message, new Win32Exception(error));

        private static HandleBoundFileDeletionResult Failure(
            HandleBoundFileDeletionStatus status,
            string path,
            string message,
            Exception? inner = null)
            => new(
                status,
                path,
                inner is null ? new IOException(message) : new IOException(message, inner));

        [LibraryImport(
            "kernel32.dll",
            EntryPoint = "CreateFileW",
            SetLastError = true,
            StringMarshalling = StringMarshalling.Utf16)]
        private static partial SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetFileInformationByHandle(
            SafeFileHandle fileHandle,
            FileInfoByHandleClass fileInformationClass,
            ref FileDispositionInfo fileInformation,
            uint bufferSize);

        [LibraryImport(
            "kernel32.dll",
            EntryPoint = "GetFileAttributesW",
            SetLastError = true,
            StringMarshalling = StringMarshalling.Utf16)]
        private static partial uint GetFileAttributes(string fileName);

        private enum FileInfoByHandleClass
        {
            FileDispositionInfo = 4,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileDispositionInfo
        {
            public int DeleteFile;
        }
    }

    public enum HandleBoundFileDeletionStatus
    {
        Deleted,
        NotFound,
        IdentityMismatch,
        HandleAcquisitionFailed,
        VerificationFailed,
        DispositionFailed,
        PathStillPresent,
        PathVerificationFailed,
    }

    public readonly record struct HandleBoundFileDeletionResult(
        HandleBoundFileDeletionStatus Status,
        string Path,
        Exception? Error)
    {
        public bool Succeeded => Status is HandleBoundFileDeletionStatus.Deleted or HandleBoundFileDeletionStatus.NotFound;

        public bool OwnedObjectDeleted => Status is HandleBoundFileDeletionStatus.Deleted or HandleBoundFileDeletionStatus.PathStillPresent;
    }
}
