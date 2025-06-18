// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using ManagedCsWin32;
using Microsoft.Win32.SafeHandles;

namespace Microsoft.CmdPal.Ext.Apps.Programs;

/// <summary>
/// Provides access to NTFS reparse points in .Net.
/// </summary>
public static partial class ReparsePoint
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
            Kernel32.CreateFile(
                reparsePoint,
                FileAccessType.FILE_READ_ATTRIBUTES | FileAccessType.FILE_READ_EA,
                FileShareType.Delete | FileShareType.Read | FileShareType.Write,
                IntPtr.Zero,
                CreationDisposition.OpenExisting,
                ManagedCsWin32.FileAttributes.OpenReparsePoint,
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
                    var result = Kernel32.DeviceIoControl(
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

                    unsafe
                    {
                        var aliasReparseHeader = Unsafe.Read<AppExecutionAliasReparseTagHeader>((void*)outBuffer);

                        if (aliasReparseHeader.ReparseTag == IO_REPARSE_TAG_APPEXECLINK)
                        {
                            var metadata = AppExecutionAliasMetadata.FromPersistedRepresentationIntPtr(
                                outBuffer,
                                aliasReparseHeader.Version);

                            return metadata.ExePath;
                        }
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
            unsafe
            {
                var dataOffset = Unsafe.SizeOf<AppExecutionAliasReparseTagHeader>();

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
