// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.WinGet.Helpers;

public partial class Native
{
    [LibraryImport("ole32.dll")]
    [return: MarshalAs(UnmanagedType.U4)]
    public static partial uint CoCreateInstance(
    Guid rclsid,
    IntPtr pUnkOuter,
    uint dwClsContext,
    Guid riid,
    out IntPtr rReturnedComObject);
}
