// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Wox.Plugin.Common.Win32;
using Wox.Plugin.Logger;

namespace Wox.Plugin.Common.VirtualDesktop
{
    /// <summary>
    /// Helper class to work with Virtual Desktops.
    /// This helper uses only public available and documented COM-Interfaces or informations from registry.
    /// </summary>
    /// <remarks>
    /// To use this hleper you have to create an instance of it and acces the method via the helper instance.
    /// We are only allowed to use public documented com interfaces.
    /// </remarks>
    /// <seealso href="https://docs.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ivirtualdesktopmanager">Documentation of IVirtualDesktopManager interface</seealso>
    /// <seealso href="https://docs.microsoft.com/en-us/archive/blogs/winsdk/virtual-desktop-switching-in-windows-10">CSharp example code for IVirtualDesktopManager</seealso>
    public class VirtualDesktopHelper
    {
        /// <summary>
        /// Internal settings to enable automatic update of desktop list.
        /// This will be off by default to avoid to many registry queries.
        /// </summary>
        private readonly bool _desktopListAutoUpdate;

        /// <summary>
        /// List of all available Virtual Desktop in their real order
        /// </summary>
        private List<Guid> availableDesktops;

        /// <summary>
        /// Id of the current visible Desktop.
        /// </summary>
        private Guid currentDesktop;

        /// <summary>
        /// Instance of "Virtual Desktop Manager"
        /// </summary>
        private IVirtualDesktopManager virtualDesktopManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="VirtualDesktopHelper"/> class.
        /// </summary>
        /// <param name="desktopListUpdate">Setting to configure if the list of available desktops should update automatically or only when calling <see cref="UpdateDesktopList"/>. Per default this is set to manual update (false) to have less registry queries.</param>
        public VirtualDesktopHelper(bool desktopListUpdate = false)
        {
            // ToDo: Error-Handling if VDs are diabled and instance can't be creted.
            virtualDesktopManager = (IVirtualDesktopManager)new CVirtualDesktopManager();

            _desktopListAutoUpdate = desktopListUpdate;
            UpdateDesktopList();
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="VirtualDesktopHelper"/> class.
        /// </summary>
        ~VirtualDesktopHelper()
        {
            virtualDesktopManager = null;
            availableDesktops = null;
            currentDesktop = Guid.Empty;
        }

        /// <summary>
        /// Method to update the list of Virtual Desktops from Registry
        /// </summary>
        public void UpdateDesktopList()
        {
            // List of all desktops
            // Each guid has 16 bytes
            RegistryKey allDeskSubKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VirtualDesktops", false);
            byte[] allDeskValue = (byte[])allDeskSubKey.GetValue("VirtualDesktopIDs");

            int numberOfDesktops = allDeskValue.Length / 16;
            availableDesktops.Clear();

            for (int i = 0; i < numberOfDesktops; i++)
            {
                byte[] guidArray = new byte[16];
                Array.ConstrainedCopy(allDeskValue, i * 16, guidArray, 0, 16);
                availableDesktops.Add(new Guid(guidArray));
            }

            // Guid for current desktop
            RegistryKey currentDeskSubKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\SessionInfo\\1\\VirtualDesktops", false);
            var currentDeskValue = currentDeskSubKey.GetValue("CurrentVirtualDesktop");
            currentDesktop = (currentDeskValue != null) ? new Guid((byte[])currentDeskValue) : Guid.Empty;
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
            Guid currentDesktopId = currentDesktop;

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
        /// Returns a value indicating if the window is cloaked by VirtualDesktopManager.
        /// (A cloaked window is not visible to the user. But the window is still composed by DWM.)
        /// </summary>
        /// <param name="hWindow">Handle of the window.</param>
        /// <returns>A value indicating if the window is cloaked by VDM, because it is moved to an other desktop.</returns>
        public bool IsWindowCloakedByVdm(IntPtr hWindow)
        {
            // If a window is hidden because it is moved to an other desktop, then DWM returns type "CloakedShell". If DWM returns an other type the window is not cloaked by shell or VirtualDesktopManager.
            _ = NativeMethods.DwmGetWindowAttribute(hWindow, (int)DwmWindowAttributes.Cloaked, out int dwmCloakedState, sizeof(uint));
            return GetDesktopAssignmentStateFromHwnd(hWindow) == DesktopAssignmentType.OtherDesktop && dwmCloakedState == (int)DwmWindowCloakStates.CloakedShell;
        }

        public List<Guid> GetListOfAllDesktops()
        {
            return availableDesktops;
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
        /// Code used from <see href="https://docs.microsoft.com/en-us/archive/blogs/winsdk/virtual-desktop-switching-in-windows-10"./>
        /// </summary>
        [ComImport]
        [Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a")]
        private class CVirtualDesktopManager
        {
        }

        /// <summary>
        /// Interface for accessing Virtual Desktop Manager.
        /// Code used from <see href="https://docs.microsoft.com/en-us/archive/blogs/winsdk/virtual-desktop-switching-in-windows-10"./>
        /// </summary>
        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
        [System.Security.SuppressUnmanagedCodeSecurity]
        private interface IVirtualDesktopManager
        {
            [PreserveSig]
            int IsWindowOnCurrentVirtualDesktop([In] IntPtr hTopLevelWindow, [Out] out int onCurrentDesktop);

            [PreserveSig]
            int GetWindowDesktopId([In] IntPtr hTopLevelWindow, [Out] out Guid desktop);

            [PreserveSig]
            int MoveWindowToDesktop([In] IntPtr hTopLevelWindow, [MarshalAs(UnmanagedType.LPStruct)] [In]Guid desktop);
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
