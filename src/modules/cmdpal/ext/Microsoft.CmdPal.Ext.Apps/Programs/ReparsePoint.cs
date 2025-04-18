// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Win32.SafeHandles;
using Windows.Storage.Streams;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

/// <summary>
/// Provides access to NTFS reparse points in .Net.
/// </summary>
public static class ReparsePoint
{
#pragma warning disable SA1310 // Field names should not contain underscore

    private const int ERROR_NOT_A_REPARSE_POINT = 4390;

    private const int ERROR_INSUFFICIENT_BUFFER = 122;

    private const int ERROR_MORE_DATA = 234;

    private const int FSCTL_GET_REPARSE_POINT = 0x000900A8;

    private const uint IO_REPARSE_TAG_APPEXECLINK = 0x8000001B;

    private const int MAXIMUM_REPARSE_DATA_BUFFER_SIZE = 16 * 1024;

    private const int E_INVALID_PROTOCOL_FORMAT = unchecked((int)0x83760002);
#pragma warning restore SA1310 // Field names should not contain underscore

    [Flags]
    private enum FileAccessType : uint
    {
        DELETE = 0x00010000,
        READ_CONTROL = 0x00020000,
        WRITE_DAC = 0x00040000,
        WRITE_OWNER = 0x00080000,
        SYNCHRONIZE = 0x00100000,

        STANDARD_RIGHTS_REQUIRED = 0x000F0000,

        STANDARD_RIGHTS_READ = READ_CONTROL,
        STANDARD_RIGHTS_WRITE = READ_CONTROL,
        STANDARD_RIGHTS_EXECUTE = READ_CONTROL,

        STANDARD_RIGHTS_ALL = 0x001F0000,

        SPECIFIC_RIGHTS_ALL = 0x0000FFFF,

        ACCESS_SYSTEM_SECURITY = 0x01000000,

        MAXIMUM_ALLOWED = 0x02000000,

        GENERIC_READ = 0x80000000,
        GENERIC_WRITE = 0x40000000,
        GENERIC_EXECUTE = 0x20000000,
        GENERIC_ALL = 0x10000000,

        FILE_READ_DATA = 0x0001,
        FILE_WRITE_DATA = 0x0002,
        FILE_APPEND_DATA = 0x0004,
        FILE_READ_EA = 0x0008,
        FILE_WRITE_EA = 0x0010,
        FILE_EXECUTE = 0x0020,
        FILE_READ_ATTRIBUTES = 0x0080,
        FILE_WRITE_ATTRIBUTES = 0x0100,

        FILE_ALL_ACCESS =
            STANDARD_RIGHTS_REQUIRED |
            SYNCHRONIZE
            | 0x1FF,

        FILE_GENERIC_READ =
            STANDARD_RIGHTS_READ |
            FILE_READ_DATA |
            FILE_READ_ATTRIBUTES |
            FILE_READ_EA |
            SYNCHRONIZE,

        FILE_GENERIC_WRITE =
            STANDARD_RIGHTS_WRITE |
            FILE_WRITE_DATA |
            FILE_WRITE_ATTRIBUTES |
            FILE_WRITE_EA |
            FILE_APPEND_DATA |
            SYNCHRONIZE,

        FILE_GENERIC_EXECUTE =
            STANDARD_RIGHTS_EXECUTE |
            FILE_READ_ATTRIBUTES |
            FILE_EXECUTE |
            SYNCHRONIZE,
    }

    [Flags]
    private enum FileShareType : uint
    {
        None = 0x00000000,
        Read = 0x00000001,
        Write = 0x00000002,
        Delete = 0x00000004,
    }

    private enum CreationDisposition : uint
    {
        New = 1,
        CreateAlways = 2,
        OpenExisting = 3,
        OpenAlways = 4,
        TruncateExisting = 5,
    }

    [Flags]
    private enum FileAttributes : uint
    {
        Readonly = 0x00000001,
        Hidden = 0x00000002,
        System = 0x00000004,
        Directory = 0x00000010,
        Archive = 0x00000020,
        Device = 0x00000040,
        Normal = 0x00000080,
        Temporary = 0x00000100,
        SparseFile = 0x00000200,
        ReparsePoint = 0x00000400,
        Compressed = 0x00000800,
        Offline = 0x00001000,
        NotContentIndexed = 0x00002000,
        Encrypted = 0x00004000,
        Write_Through = 0x80000000,
        Overlapped = 0x40000000,
        NoBuffering = 0x20000000,
        RandomAccess = 0x10000000,
        SequentialScan = 0x08000000,
        DeleteOnClose = 0x04000000,
        BackupSemantics = 0x02000000,
        PosixSemantics = 0x01000000,
        OpenReparsePoint = 0x00200000,
        OpenNoRecall = 0x00100000,
        FirstPipeInstance = 0x00080000,
    }

    private enum AppExecutionAliasReparseTagBufferLayoutVersion : uint
    {
        Invalid = 0,

        /// <summary>
        /// Initial version used package full name, aumid, exe path
        /// </summary>
        Initial = 1,

        /// <summary>
        /// This version replaces package full name with family name, to allow
        /// optional packages to reference their main package across versions.
        /// </summary>
        PackageFamilyName = 2,

        /// <summary>
        /// This version appends a flag to the family Name version to differentiate
        /// between UWP and Centennial
        /// </summary>
        MultiAppTypeSupport = 3,

        /// <summary>
        /// Used to check version validity, where valid is (Invalid, UpperBound)
        /// </summary>
        UpperBound,
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AppExecutionAliasReparseTagHeader
    {
        /// <summary>
        /// Reparse point tag.
        /// </summary>
        public uint ReparseTag;

        /// <summary>
        /// Size, in bytes, of the data after the Reserved member.
        /// </summary>
        public ushort ReparseDataLength;

        /// <summary>
        /// Reserved; do not use.
        /// </summary>
        public ushort Reserved;

        public AppExecutionAliasReparseTagBufferLayoutVersion Version;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool DeviceIoControl(
        IntPtr hDevice,
        uint dwIoControlCode,
        IntPtr inBuffer,
        int nInBufferSize,
        IntPtr outBuffer,
        int nOutBufferSize,
        out int pBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFile(
        string lpFileName,
        FileAccessType dwDesiredAccess,
        FileShareType dwShareMode,
        IntPtr lpSecurityAttributes,
        CreationDisposition dwCreationDisposition,
        FileAttributes dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    /// <summary>
    /// Gets the target of the specified reparse point.
    /// </summary>
    /// <param name="reparsePoint">The path of the reparse point.</param>
    /// <returns>
    /// The target of the reparse point.
    /// </returns>
    /// <exception cref="IOException">
    /// Thrown when the reparse point specified is not a reparse point or is invalid.
    /// </exception>
    public static string? GetTarget(string reparsePoint)
    {
        using (SafeFileHandle reparsePointHandle = new SafeFileHandle(
            CreateFile(
                reparsePoint,
                FileAccessType.FILE_READ_ATTRIBUTES | FileAccessType.FILE_READ_EA,
                FileShareType.Delete | FileShareType.Read | FileShareType.Write,
                IntPtr.Zero,
                CreationDisposition.OpenExisting,
                FileAttributes.OpenReparsePoint,
                IntPtr.Zero),
            true))
        {
            if (Marshal.GetLastWin32Error() != 0)
            {
                ThrowLastWin32Error("Unable to open reparse point.");
            }

            var outBufferSize = 512;
            var outBuffer = Marshal.AllocHGlobal(outBufferSize);

            try
            {
                // For-loop allows an attempt with 512-bytes buffer, before retrying with a 'MAXIMUM_REPARSE_DATA_BUFFER_SIZE' bytes buffer.
                for (var i = 0; i < 2; ++i)
                {
                    int bytesReturned;
                    var result = DeviceIoControl(
                        reparsePointHandle.DangerousGetHandle(),
                        FSCTL_GET_REPARSE_POINT,
                        IntPtr.Zero,
                        0,
                        outBuffer,
                        outBufferSize,
                        out bytesReturned,
                        IntPtr.Zero);

                    if (!result)
                    {
                        var error = Marshal.GetLastWin32Error();
                        if (error == ERROR_NOT_A_REPARSE_POINT)
                        {
                            return null;
                        }

                        if ((error == ERROR_INSUFFICIENT_BUFFER) || (error == ERROR_MORE_DATA))
                        {
                            Marshal.FreeHGlobal(outBuffer);
                            outBuffer = IntPtr.Zero;

                            outBufferSize = MAXIMUM_REPARSE_DATA_BUFFER_SIZE;
                            outBuffer = Marshal.AllocHGlobal(outBufferSize);
                            continue;
                        }

                        ThrowLastWin32Error("Unable to get information about reparse point.");
                    }

                    AppExecutionAliasReparseTagHeader aliasReparseHeader = Marshal.PtrToStructure<AppExecutionAliasReparseTagHeader>(outBuffer);

                    if (aliasReparseHeader.ReparseTag == IO_REPARSE_TAG_APPEXECLINK)
                    {
                        var metadata = AppExecutionAliasMetadata.FromPersistedRepresentationIntPtr(
                            outBuffer,
                            aliasReparseHeader.Version);

                        return metadata.ExePath;
                    }

                    return null;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(outBuffer);
            }
        }

        return null;
    }

    private sealed class AppExecutionAliasMetadata
    {
        public string PackageFullName { get; init; } = string.Empty;

        public string PackageFamilyName { get; init; } = string.Empty;

        public string Aumid { get; init; } = string.Empty;

        public string ExePath { get; init; } = string.Empty;

        public static AppExecutionAliasMetadata FromPersistedRepresentationIntPtr(IntPtr reparseDataBufferPtr, AppExecutionAliasReparseTagBufferLayoutVersion version)
        {
            var dataOffset = Marshal.SizeOf(typeof(AppExecutionAliasReparseTagHeader));
            var dataBufferPtr = reparseDataBufferPtr + dataOffset;

            string? packageFullName = null;
            string? packageFamilyName = null;
            string? aumid = null;
            string? exePath = null;

            VerifyVersion(version);

            switch (version)
            {
                case AppExecutionAliasReparseTagBufferLayoutVersion.Initial:
                    packageFullName = Marshal.PtrToStringUni(dataBufferPtr);
                    if (packageFullName is not null)
                    {
                        dataBufferPtr += Encoding.Unicode.GetByteCount(packageFullName) + Encoding.Unicode.GetByteCount("\0");
                        aumid = Marshal.PtrToStringUni(dataBufferPtr);

                        if (aumid is not null)
                        {
                            dataBufferPtr += Encoding.Unicode.GetByteCount(aumid) + Encoding.Unicode.GetByteCount("\0");
                            exePath = Marshal.PtrToStringUni(dataBufferPtr);
                        }
                    }

                    break;

                case AppExecutionAliasReparseTagBufferLayoutVersion.PackageFamilyName:
                case AppExecutionAliasReparseTagBufferLayoutVersion.MultiAppTypeSupport:
                    packageFamilyName = Marshal.PtrToStringUni(dataBufferPtr);

                    if (packageFamilyName is not null)
                    {
                        dataBufferPtr += Encoding.Unicode.GetByteCount(packageFamilyName) + Encoding.Unicode.GetByteCount("\0");
                        aumid = Marshal.PtrToStringUni(dataBufferPtr);

                        if (aumid is not null)
                        {
                            dataBufferPtr += Encoding.Unicode.GetByteCount(aumid) + Encoding.Unicode.GetByteCount("\0");

                            exePath = Marshal.PtrToStringUni(dataBufferPtr);
                        }
                    }

                    break;
            }

            return new AppExecutionAliasMetadata
            {
                PackageFullName = packageFullName ?? string.Empty,
                PackageFamilyName = packageFamilyName ?? string.Empty,
                Aumid = aumid ?? string.Empty,
                ExePath = exePath ?? string.Empty,
            };
        }

        private static void VerifyVersion(AppExecutionAliasReparseTagBufferLayoutVersion version)
        {
            var uintVersion = (uint)version;

            if (uintVersion > (uint)AppExecutionAliasReparseTagBufferLayoutVersion.Invalid &&
                uintVersion < (uint)AppExecutionAliasReparseTagBufferLayoutVersion.UpperBound)
            {
                return;
            }

            throw new IOException("Invalid app execution alias reparse version.", E_INVALID_PROTOCOL_FORMAT);
        }
    }

    private static void ThrowLastWin32Error(string message)
    {
        throw new IOException(message, Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
    }
}
