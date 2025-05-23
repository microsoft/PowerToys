// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace Microsoft.CmdPal.Ext.WindowWalker.Helpers;

/// <summary>
/// Interface for accessing Virtual Desktop Manager.
/// Code used from <see href="https://learn.microsoft.com/archive/blogs/winsdk/virtual-desktop-switching-in-windows-10"./>
/// </summary>
[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
[System.Security.SuppressUnmanagedCodeSecurity]
internal interface IVirtualDesktopManager
{
    [PreserveSig]
    int IsWindowOnCurrentVirtualDesktop([In] IntPtr hTopLevelWindow, [Out] out int onCurrentDesktop);

    [PreserveSig]
    int GetWindowDesktopId([In] IntPtr hTopLevelWindow, [Out] out Guid desktop);

    [PreserveSig]
    int MoveWindowToDesktop([In] IntPtr hTopLevelWindow, [MarshalAs(UnmanagedType.LPStruct)][In] Guid desktop);
}
