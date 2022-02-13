// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Microsoft.Plugin.Common.VirtualDesktop
{
    /// <summary>
    /// Helper class to work with Virtual Desktops.
    /// This class grants access to public documented interfaces of Virtual Desktop Manager.
    /// </summary>
    /// <remarks> We are only allowed to use public documented com interfaces.</remarks>
    /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ivirtualdesktopmanager">Documentation of IVirtualDesktopManager interface</seealso>
    /// <seealso href="https://docs.microsoft.com/en-us/archive/blogs/winsdk/virtual-desktop-switching-in-windows-10">CSharp example code for IVirtualDesktopManager</seealso>
    public class VirtualDesktopHelper
    {
        /// <summary>
        /// Instance of "Virtual Desktop Manager"
        /// </summary>
        private IVirtualDesktopManager virtualDesktopManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualDesktopHelper"/> class.
        /// </summary>
        public VirtualDesktopHelper()
        {
            // ToDo: Error-Handling if VDs are diabled and instance can't be creted.
            virtualDesktopManager = (IVirtualDesktopManager)new CVirtualDesktopManager();
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="VirtualDesktopHelper"/> class.
        /// </summary>
        ~VirtualDesktopHelper()
        {
            virtualDesktopManager = null;
        }

        /// <summary>
        /// Returns the desktop assignment type for a window.
        /// </summary>
        /// <param name="hWindow">Handle of the window.</param>
        /// <returns>Type of <see cref="DesktopAssignment"/>.</returns>
        public DesktopAssignmentType GetDesktopAssignmentStateFromHwnd(IntPtr hWindow)
        {
            _ = virtualDesktopManager.IsWindowOnCurrentVirtualDesktop(hWindow, out int isCurrentDesktop);
            int hResult = GetDesktopIdFromHwnd(hWindow, out Guid windowDesktopId);
            Guid currentDesktopId = GetCurrentDesktopIdFromRegistry();

            if (hResult != 0)
            {
                return DesktopAssignmentType.Unknown;
            }
            else if (windowDesktopId == Guid.Empty)
            {
                return DesktopAssignmentType.NotAssigned;
            }
            else if (isCurrentDesktop == 1 && currentDesktopId != Guid.Empty && windowDesktopId != currentDesktopId)
            {
                // These windows are marked as visible on the current desktop, but their desktop is not the one from the current desktop.
                return DesktopAssignmentType.AllDesktops;
            }
            else if (isCurrentDesktop == 1)
            {
                return DesktopAssignmentType.CurrentDesktop;
            }
            else
            {
                return DesktopAssignmentType.OtherDesktop;
            }
        }

        /// <summary>
        /// Returns the desktop id for a window.
        /// </summary>
        /// <param name="hWindow">Handle of the window.</param>
        /// <param name="desktopId">The guid of the desktop, where the window is shown.</param>
        /// <returns>HResult of the called method.</returns>
        public int GetDesktopIdFromHwnd(IntPtr hWindow, out Guid desktopId)
        {
            return virtualDesktopManager.GetWindowDesktopId(hWindow, out desktopId);
        }

        /// <summary>
        /// Returns a value indicating if the window is cloaked by VDM.
        /// </summary>
        /// <param name="hWindow">Handle of the window.</param>
        /// <returns>True if the window is cloaked by VDM because it is moved to an other desktop.</returns>
        public bool IsWindowCloakedByVdm(IntPtr hWindow)
        {
            _ = NativeMethods.DwmGetWindowAttribute(hWindow, (int)NativeMethods.DwmWindowAttribute.Cloaked, out int dwmCloakedState, sizeof(uint));

            // If a window is hidden because it is moved to an other desktop, then DWM returns type "CloakedShell".
            return GetDesktopAssignmentStateFromHwnd(hWindow) == DesktopAssignmentType.OtherDesktop && dwmCloakedState == (int)NativeMethods.DwmWindowCloakState.CloakedShell;
        }

        /// <summary>
        /// Reads the guid of the current desktop from the registry.
        /// </summary>
        /// <returns>The guid of the current desktop from regedit as string. An empty guid is return on failure.</returns>
        private static Guid GetCurrentDesktopIdFromRegistry()
        {
            string regKeyPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\SessionInfo\\1\\VirtualDesktops";
            string regValueName = "CurrentVirtualDesktop";

            RegistryKey regSubKey = Registry.CurrentUser.OpenSubKey(regKeyPath, false);
            var regValueData = regSubKey.GetValue(regValueName);

            return (regValueData != null) ? new Guid((byte[])regValueData) : Guid.Empty;
        }

        public bool GetAllDesktopsId(out Guid deskGuid)
        {
            // Not available via public com interfaces.
            throw new NotImplementedException();
        }

        public bool ShowWindowOnAllDesktops(out Guid deskGuid)
        {
            // Not available via public com interfaces.
            throw new NotImplementedException();
        }

        /// <summary>
        /// Virtual Desktop Manager class
        /// </summary>
        [ComImport]
        [Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a")]
        private class CVirtualDesktopManager
        {
        }

        /// <summary>
        /// Interface for accessing Virtual Desktop Manager.
        /// </summary>
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
        [System.Security.SuppressUnmanagedCodeSecurity]
        private interface IVirtualDesktopManager
        {
            /// <summary>
            /// Checks if the window is shown on the current desktop.
            /// </summary>
            /// <param name="hWindow">Handle of the top level window.</param>
            /// <param name="onCurrentDesktop">Vlaue indicating whether the window is on the current desktop.</param>
            /// <returns>Hresult of the method.</returns>
            [PreserveSig]
            int IsWindowOnCurrentVirtualDesktop([In] IntPtr hWindow, [Out] out int onCurrentDesktop);

            /// <summary>
            /// Gets the desktop guid where the window is shown.
            /// </summary>
            /// <param name="hWindow">Handle of the top level window.</param>
            /// <param name="desktop">Guid of the assigned desktop.</param>
            /// <returns>Hresult of the method.</returns>
            [PreserveSig]
            int GetWindowDesktopId([In] IntPtr hWindow, [Out] out Guid desktop);

            /// <summary>
            /// Move the window to a specific desktop.
            /// </summary>
            /// <param name="hWindow">Handle of the top level window.</param>
            /// <param name="desktop">Guid of the desktop where the window should be moved to.</param>
            /// <returns>Hresult of the method.</returns>
            [PreserveSig]
            int MoveWindowToDesktop([In] IntPtr hWindow, [MarshalAs(UnmanagedType.LPStruct)] [In]Guid desktop);
        }
    }
	
	        /// <summary>
        /// Enum to show in which way a window is assigned to a desktop
        /// </summary>
        public enum DesktopAssignmentType
        {
            Unknown = -1,
            NotAssigned = 0,
            AllDesktops = 1,
            CurrentDesktop = 2,
            OtherDesktop = 3,
        }
}
