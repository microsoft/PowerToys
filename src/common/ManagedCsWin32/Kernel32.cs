// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace ManagedCsWin32;

public static partial class Kernel32
{
    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeviceIoControl(
        IntPtr hDevice,
        uint dwIoControlCode,
        IntPtr inBuffer,
        int nInBufferSize,
        IntPtr outBuffer,
        int nOutBufferSize,
        out int pBytesReturned,
        IntPtr lpOverlapped);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16, EntryPoint = "CreateFileW")]
    public static partial int CreateFile(
        string lpFileName,
        FileAccessType dwDesiredAccess,
        FileShareType dwShareMode,
        IntPtr lpSecurityAttributes,
        CreationDisposition dwCreationDisposition,
        FileAttributes dwFlagsAndAttributes,
        IntPtr hTemplateFile);
}

[Flags]
public enum FileAccessType : uint
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
public enum FileShareType : uint
{
    None = 0x00000000,
    Read = 0x00000001,
    Write = 0x00000002,
    Delete = 0x00000004,
}

public enum CreationDisposition : uint
{
    New = 1,
    CreateAlways = 2,
    OpenExisting = 3,
    OpenAlways = 4,
    TruncateExisting = 5,
}

[Flags]
public enum FileAttributes : uint
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
