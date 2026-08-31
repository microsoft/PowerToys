// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;

namespace Microsoft.AlwaysOnTop.UITests;

internal static class VirtualDesktopHelper
{
    private static readonly Guid VirtualDesktopManagerClassId = new("AA509086-5CA9-4C25-8F95-589D3C07B48A");
    private static readonly Lazy<IVirtualDesktopManager> Manager = new(CreateManager);

    internal static bool IsWindowOnCurrentDesktop(IntPtr window)
    {
        var result = Manager.Value.IsWindowOnCurrentVirtualDesktop(window, out var isCurrent);
        if (result < 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }

        return isCurrent;
    }

    private static IVirtualDesktopManager CreateManager()
    {
        var managerType = Type.GetTypeFromCLSID(VirtualDesktopManagerClassId, throwOnError: true)
            ?? throw new InvalidOperationException("The virtual desktop manager COM type is unavailable.");
        return (IVirtualDesktopManager)(Activator.CreateInstance(managerType)
            ?? throw new InvalidOperationException("The virtual desktop manager could not be created."));
    }

    // Keep the complete public IVirtualDesktopManager vtable in declaration order.
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
