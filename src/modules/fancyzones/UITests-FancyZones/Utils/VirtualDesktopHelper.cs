// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Runtime.InteropServices;

namespace Microsoft.FancyZones.UITests.Utils
{
    [ComImport]
    [Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(true)]
    public partial interface IVirtualDesktopManager
    {
        int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, out bool onCurrentDesktop);

        int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);

        int MoveWindowToDesktop(IntPtr topLevelWindow, [MarshalAs(UnmanagedType.LPStruct)] Guid desktopId);
    }

    [ComImport]
    [Guid("c5e0cdca-7b6e-41b2-9fc4-d93975cc467b")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class VirtualDesktopManagerCom
    {
    }

    public static class VirtualDesktopHelper
    {
        private static readonly IVirtualDesktopManager Vdm = (IVirtualDesktopManager)new VirtualDesktopManagerCom();

        public static Guid GetWindowDesktopId(IntPtr hwnd)
        {
            Vdm.GetWindowDesktopId(hwnd, out Guid desktopId);
            return desktopId;
        }

        public static bool IsWindowOnCurrentDesktop(IntPtr hwnd)
        {
            Vdm.IsWindowOnCurrentVirtualDesktop(hwnd, out bool onCurrent);
            return onCurrent;
        }

        public static void MoveWindowToDesktop(IntPtr hwnd, Guid targetDesktopId)
        {
            Vdm.MoveWindowToDesktop(hwnd, targetDesktopId);
        }
    }
}
