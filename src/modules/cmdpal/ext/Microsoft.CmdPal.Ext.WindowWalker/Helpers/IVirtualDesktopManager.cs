// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace Microsoft.CmdPal.Ext.WindowWalker.Helpers;

/// <summary>
/// Interface for accessing Virtual Desktop Manager.
/// Code used from <see href="https://learn.microsoft.com/archive/blogs/winsdk/virtual-desktop-switching-in-windows-10"./>
/// </summary>
[GeneratedComInterface]
[Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
public partial interface IVirtualDesktopManager
{
    [PreserveSig]
    int IsWindowOnCurrentVirtualDesktop(IntPtr hTopLevelWindow, out int onCurrentDesktop);

    [PreserveSig]
    int GetWindowDesktopId(IntPtr hTopLevelWindow, out Guid desktop);

    [PreserveSig]
    int MoveWindowToDesktop(IntPtr hTopLevelWindow, ref Guid desktop);
}
