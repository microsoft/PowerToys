// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Wox.Plugin.Common.Win32;

namespace Microsoft.Plugin.Program.Programs
{
    [Guid("03FAF64D-F26F-4B2C-AAF7-8FE7789B8BCA")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAppxManifestProperties
    {
        [PreserveSig]
        HRESULT GetBoolValue([MarshalAs(UnmanagedType.LPWStr)] string name, out bool value);

        [PreserveSig]
        HRESULT GetStringValue([MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] out string value);
    }
}
