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
        /// The oder and list in the registry is always up to date
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
        /// The data in the registry are always up to date
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
        /// Retruns a odered list of all existing desktops. The list is odered in the same way as the existing desktops.
        /// </summary>
        /// <returns>List of desktops or an empty list on failure.</returns>
        public List<Guid> GetDesktopList()
        {
            if (_desktopListAutoUpdate)
            {
                UpdateDesktopList();
            }

            return availableDesktops;
        }

        /// <summary>
        /// Returns the count of existing desktops
        /// </summary>
        /// <returns>Number of existing desktops or zero on failure.</returns>
        public int GetDesktopCount()
        {
            if (_desktopListAutoUpdate)
            {
                UpdateDesktopList();
            }

            return availableDesktops.Count;
        }

        /// <summary>
        /// Returns the id of the generic desktop (view) "All Desktops".
        /// (At the moment we can't implement this because the necessary com interfaces are private.)
        /// </summary>
        /// <exception cref="NotImplementedException">Because this mehtod isn't implemented.</exception>
        private void GetAllDesktopsId()
        {
            // Not available via public com interfaces.
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the id of the desktop taht is currently visible to the user.
        /// </summary>
        /// <returns>Guid of the current desktop or an empty guid on failure.</returns>
        public Guid GetCurrentDesktopId()
        {
            if (_desktopListAutoUpdate)
            {
                UpdateDesktopList();
            }

            return currentDesktop;
        }

        /// <summary>
        /// Returns the number (position) of a desktop.
        /// </summary>
        /// <returns>Number of the desktop, if found, otherwise -1.</returns>
        /// <param name="desktop">The guid of the desktop.</param>
        public int GetDesktopNumber(Guid desktop)
        {
            if (_desktopListAutoUpdate)
            {
                UpdateDesktopList();
            }

            return availableDesktops.IndexOf(desktop);
        }

        /// <summary>
        /// Returns the name of a desktop
        /// </summary>
        /// <param name="desktop">Guid of the desktop</param>
        /// <returns>Returns the name of the desktop, or "All Desktops" if guid belongs to the generic desktop "All Desktops", or an empty string on failur.</returns>
        public string GetNameOfDesktop(Guid desktop)
        {
            if (desktop == Guid.Empty)
            {
                return string.Empty;
            }

            // Here we expect that the guid in the parameter is valid.
            // So if we can't find the id in the desktop list, it must be the generic id for "All Desktops".
            if (!GetDesktopList().Contains(desktop))
            {
                return Properties.Resources.VirtualDesktopHelper_AllDesktops;
            }

            string registryPath = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Explorer\\VirtualDesktops\\Desktops\\{" + desktop.ToString().ToUpper() + "}";
            RegistryKey deskSubKey = Registry.CurrentUser.OpenSubKey(registryPath, false);
            if (deskSubKey == null)
            {
                int number = GetDesktopNumber(desktop);
                return $"{Properties.Resources.VirtualDesktopHelper_Desktop} {number}";
            }
            else
            {
                var name = deskSubKey.GetValue("Name");
                return (name != null) ? (string)name : string.Empty;
            }
        }

        /// <summary>
        /// Returns the desktop id for a window.
        /// </summary>
        /// <param name="hWindow">Handle of the window.</param>
        /// <param name="desktopId">The guid of the desktop, where the window is shown.</param>
        /// <returns>HResult of the called method.</returns>
        public int GetWindowDesktopId(IntPtr hWindow, out Guid desktopId)
        {
            return virtualDesktopManager.GetWindowDesktopId(hWindow, out desktopId);
        }

        /// <summary>
        /// Returns the desktop assignment type for a window.
        /// </summary>
        /// <param name="hWindow">Handle of the window.</param>
        /// <returns>Type of <see cref="DesktopAssignment"/>.</returns>
        public DesktopAssignmentType GetWindowDesktopAssignmentType(IntPtr hWindow)
        {
            _ = virtualDesktopManager.IsWindowOnCurrentVirtualDesktop(hWindow, out int isCurrentDesktop);
            int hResult = GetWindowDesktopId(hWindow, out Guid windowDesktopId);
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
        /// Returns a value indicating if the window is assigned to a currently visible dekstop.
        /// </summary>
        /// <param name="hWindow">Handle to the top level window.</param>
        /// <returns>True if the desktop with the window is visible or if the window is assigned to all desktops. False if the desktop is not visible and on failure,</returns>
        public bool IsWindowOnVisibleDesktop(IntPtr hWindow)
        {
            return GetWindowDesktopAssignmentType(hWindow) == DesktopAssignmentType.CurrentDesktop || GetWindowDesktopAssignmentType(hWindow) == DesktopAssignmentType.AllDesktops;
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
            return GetWindowDesktopAssignmentType(hWindow) == DesktopAssignmentType.OtherDesktop && dwmCloakedState == (int)DwmWindowCloakStates.CloakedShell;
        }

        /// <summary>
        /// Moves the window to a specific desktop.
        /// </summary>
        /// <param name="hWindow">Handle of the top level window.</param>
        /// <param name="desktopId">Guid of the target desktop.</param>
        /// <returns>HResult of non-zero on faile or zero on success.</returns>
        public int MoveWindowToDesktop(IntPtr hWindow, in Guid desktopId)
        {
            return virtualDesktopManager.MoveWindowToDesktop(hWindow, desktopId);
        }

        public bool MoveWindowOneDesktopLeft(IntPtr hWindow)
        {
            if (GetWindowDesktopAssignmentType(hWindow) == DesktopAssignmentType.Unknown || GetWindowDesktopAssignmentType(hWindow) == DesktopAssignmentType.NotAssigned)
            {
                return false;
            }

            _ = GetWindowDesktopId(hWindow, out Guid currentDesktop);
            int currentWindowDesktopNumber = GetDesktopNumber(currentDesktop);

            if (currentWindowDesktopNumber > 1)
            {
                Guid newDesktop = availableDesktops[currentWindowDesktopNumber - 1];
                int hr = MoveWindowToDesktop(hWindow, newDesktop);
                if (hr != (int)HRESULT.S_OK)
                {
                    Log.Exception("Failed to move the window to an other desktop.", Marshal.GetExceptionForHR(hr), typeof(VirtualDesktopHelper));
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Move a window one desktop right.
        /// </summary>
        /// <param name="hWindow">Handle of the top level window.</param>
        /// <returns>True on success and false on failure.</returns>
        public bool MoveWindowOneDesktopRight(IntPtr hWindow)
        {
            if (GetWindowDesktopAssignmentType(hWindow) == DesktopAssignmentType.Unknown || GetWindowDesktopAssignmentType(hWindow) == DesktopAssignmentType.NotAssigned)
            {
                return false;
            }

            _ = GetWindowDesktopId(hWindow, out Guid currentDesktop);
            int currentWindowDesktopNumber = GetDesktopNumber(currentDesktop);

            if (currentWindowDesktopNumber < GetDesktopCount())
            {
                Guid newDesktop = availableDesktops[currentWindowDesktopNumber + 1];
                int hr = MoveWindowToDesktop(hWindow, newDesktop);
                if (hr != (int)HRESULT.S_OK)
                {
                    Log.Exception("Failed to move the window to an other desktop.", Marshal.GetExceptionForHR(hr), typeof(VirtualDesktopHelper));
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns the id of the generic desktop (view) "All Desktops".
        /// (At the moment we can't implement this because the necessary com interfaces are private.)
        /// </summary>
        /// <exception cref="NotImplementedException">Because this mehtod isn't implemented.</exception>
        private void ShowWindowOnAllDesktops()
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
