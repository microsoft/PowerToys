// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace PowerDisplay.Common.Drivers
{
    /// <summary>
    /// P/Invoke declarations using LibraryImport source generator
    /// </summary>
    internal static partial class PInvoke
    {
        // ==================== User32.dll - Display Configuration ====================
        [LibraryImport("user32.dll")]
        internal static partial int GetDisplayConfigBufferSizes(
            uint flags,
            out uint numPathArrayElements,
            out uint numModeInfoArrayElements);

        // Use unsafe pointer to avoid runtime marshalling
        [LibraryImport("user32.dll")]
        internal static unsafe partial int QueryDisplayConfig(
            uint flags,
            ref uint numPathArrayElements,
            DISPLAYCONFIG_PATH_INFO* pathArray,
            ref uint numModeInfoArrayElements,
            DISPLAYCONFIG_MODE_INFO* modeInfoArray,
            IntPtr currentTopologyId);

        [LibraryImport("user32.dll")]
        internal static unsafe partial int DisplayConfigGetDeviceInfo(
            DISPLAYCONFIG_TARGET_DEVICE_NAME* deviceName);

        [LibraryImport("user32.dll")]
        internal static unsafe partial int DisplayConfigGetDeviceInfo(
            DISPLAYCONFIG_SOURCE_DEVICE_NAME* sourceName);

        // ==================== User32.dll - Monitor Enumeration ====================
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool EnumDisplayMonitors(
            IntPtr hdc,
            IntPtr lprcClip,
            NativeDelegates.MonitorEnumProc lpfnEnum,
            IntPtr dwData);

        [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static unsafe partial bool GetMonitorInfo(
            IntPtr hMonitor,
            MonitorInfoEx* lpmi);

        [LibraryImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static unsafe partial bool EnumDisplayDevices(
            [MarshalAs(UnmanagedType.LPWStr)] string? lpDevice,
            uint iDevNum,
            DisplayDevice* lpDisplayDevice,
            uint dwFlags);

        [LibraryImport("user32.dll", EntryPoint = "EnumDisplaySettingsW", StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static unsafe partial bool EnumDisplaySettings(
            [MarshalAs(UnmanagedType.LPWStr)] string? lpszDeviceName,
            int iModeNum,
            DevMode* lpDevMode);

        [LibraryImport("user32.dll", EntryPoint = "ChangeDisplaySettingsExW", StringMarshalling = StringMarshalling.Utf16)]
        internal static unsafe partial int ChangeDisplaySettingsEx(
            [MarshalAs(UnmanagedType.LPWStr)] string? lpszDeviceName,
            DevMode* lpDevMode,
            IntPtr hwnd,
            uint dwflags,
            IntPtr lParam);

        [LibraryImport("user32.dll")]
        internal static partial IntPtr MonitorFromWindow(
            IntPtr hwnd,
            uint dwFlags);

        [LibraryImport("user32.dll")]
        internal static partial IntPtr MonitorFromPoint(
            POINT pt,
            uint dwFlags);

        // ==================== Dxva2.dll - DDC/CI Monitor Control ====================
        [LibraryImport("Dxva2.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetNumberOfPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor,
            out uint pdwNumberOfPhysicalMonitors);

        // Use unsafe pointer to avoid ArraySubType limitation
        [LibraryImport("Dxva2.dll", StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static unsafe partial bool GetPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor,
            uint dwPhysicalMonitorArraySize,
            PhysicalMonitor* pPhysicalMonitorArray);

        [LibraryImport("Dxva2.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DestroyPhysicalMonitor(IntPtr hMonitor);

        // Use unsafe pointer to avoid LPArray limitation
        [LibraryImport("Dxva2.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static unsafe partial bool DestroyPhysicalMonitors(
            uint dwPhysicalMonitorArraySize,
            PhysicalMonitor* pPhysicalMonitorArray);

        [LibraryImport("Dxva2.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetVCPFeatureAndVCPFeatureReply(
            IntPtr hPhysicalMonitor,
            byte bVCPCode,
            IntPtr pvct,
            out uint pdwCurrentValue,
            out uint pdwMaximumValue);

        [LibraryImport("Dxva2.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetVCPFeature(
            IntPtr hPhysicalMonitor,
            byte bVCPCode,
            uint dwNewValue);

        [LibraryImport("Dxva2.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SaveCurrentSettings(IntPtr hPhysicalMonitor);

        [LibraryImport("Dxva2.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetCapabilitiesStringLength(
            IntPtr hPhysicalMonitor,
            out uint pdwCapabilitiesStringLengthInCharacters);

        [LibraryImport("Dxva2.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool CapabilitiesRequestAndCapabilitiesReply(
            IntPtr hPhysicalMonitor,
            IntPtr pszASCIICapabilitiesString,
            uint dwCapabilitiesStringLengthInCharacters);

        // ==================== Kernel32.dll ====================
        [LibraryImport("kernel32.dll")]
        internal static partial uint GetLastError();
    }
}
