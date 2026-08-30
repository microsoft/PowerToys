// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.AlwaysOnTop.UITests;

internal static class VirtualDesktopHelper
{
    private static readonly Guid VirtualDesktopManagerClassId = new("AA509086-5CA9-4C25-8F95-589D3C07B48A");

    internal static bool IsWindowOnCurrentDesktop(IntPtr window)
    {
        var managerType = Type.GetTypeFromCLSID(VirtualDesktopManagerClassId, throwOnError: true)
            ?? throw new InvalidOperationException("The virtual desktop manager COM type is unavailable.");
        var managerObject = Activator.CreateInstance(managerType)
            ?? throw new InvalidOperationException("The virtual desktop manager could not be created.");

        try
        {
            var manager = (IVirtualDesktopManager)managerObject;
            var result = manager.IsWindowOnCurrentVirtualDesktop(window, out var isCurrent);
            if (result < 0)
            {
                Marshal.ThrowExceptionForHR(result);
            }

            return isCurrent;
        }
        finally
        {
            Marshal.FinalReleaseComObject(managerObject);
        }
    }

    [ComImport]
    [Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IVirtualDesktopManager
    {
        [PreserveSig]
        int IsWindowOnCurrentVirtualDesktop(
            IntPtr topLevelWindow,
            [MarshalAs(UnmanagedType.Bool)] out bool onCurrentDesktop);

        [PreserveSig]
        int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);

        [PreserveSig]
        int MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
    }
}
