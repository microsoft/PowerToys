// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace AllApps.Programs;

[Guid("5DA89BF4-3773-46BE-B650-7E744863B7E8")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IAppxManifestApplication
{
    [PreserveSig]
    int GetStringValue([MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] out string value);

    [PreserveSig]
    int GetAppUserModelId([MarshalAs(UnmanagedType.LPWStr)] out string value);
}
