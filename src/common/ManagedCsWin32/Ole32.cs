// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace ManagedCsWin32;

public static partial class Ole32
{
    [LibraryImport("ole32.dll")]
    public static partial int CoCreateInstance(
        ref Guid rclsid,
        IntPtr pUnkOuter,
        CLSCTX dwClsContext,
        ref Guid riid,
        out IntPtr rReturnedComObject);

    [LibraryImport("ole32.dll")]
    internal static partial int CoInitializeEx(nint pvReserved, uint dwCoInit);

    [LibraryImport("ole32.dll")]
    internal static partial void CoUninitialize();
}

[Flags]
public enum CLSCTX : uint
{
    InProcServer = 0x1,
    InProcHandler = 0x2,
    LocalServer = 0x4,
    InProcServer16 = 0x8,
    RemoteServer = 0x10,
    InProcHandler16 = 0x20,
    Reserved1 = 0x40,
    Reserved2 = 0x80,
    Reserved3 = 0x100,
    Reserved4 = 0x200,
    NoCodeDownload = 0x400,
    Reserved5 = 0x800,
    NoCustomMarshal = 0x1000,
    EnableCodeDownload = 0x2000,
    NoFailureLog = 0x4000,
    DisableAAA = 0x8000,
    EnableAAA = 0x10000,
    FromDefaultContext = 0x20000,
    ActivateX86Server = 0x40000,
#pragma warning disable CA1069 // Keep the original defines for compatibility
    Activate32BitServer = 0x40000, // Same as ActivateX86Server
#pragma warning restore CA1069 // Keep the original defines for compatibility
    Activate64BitServer = 0x80000,
    EnableCloaking = 0x100000,
    AppContainer = 0x400000,
    ActivateAAAAsIU = 0x800000,
    Reserved6 = 0x1000000,
    ActivateARM32Server = 0x2000000,
    AllowLowerTrustRegistration = 0x4000000,
    PSDll = 0x80000000,

    INPROC = InProcServer | InProcHandler,
    SERVER = InProcServer | LocalServer | RemoteServer,
    ALL = InProcHandler | SERVER,
}
