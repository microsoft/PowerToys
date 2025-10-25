// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;

namespace PowerDisplay.Native
{
    /// <summary>
    /// P/Invoke declarations using LibraryImport source generator
    /// </summary>
    internal static partial class PInvoke
    {
        // ==================== User32.dll - Window Management ====================
        // GetWindowLong: On 64-bit use GetWindowLongPtrW, on 32-bit use GetWindowLongW
#if WIN64
        [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        internal static partial IntPtr GetWindowLong(IntPtr hWnd, int nIndex);
#else
        [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW")]
        internal static partial int GetWindowLong(IntPtr hWnd, int nIndex);
#endif

        // SetWindowLong: On 64-bit use SetWindowLongPtrW, on 32-bit use SetWindowLongW
#if WIN64
        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        internal static partial IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
#else
        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW")]
        internal static partial int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
#endif

        // SetWindowLongPtr: Always uses the Ptr variant (64-bit)
        [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
        internal static partial IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint uFlags);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetForegroundWindow(IntPtr hWnd);

        // ==================== User32.dll - Window Creation and Messaging ====================
        [LibraryImport("user32.dll", EntryPoint = "CreateWindowExW", StringMarshalling = StringMarshalling.Utf16)]
        internal static partial IntPtr CreateWindowEx(
            uint dwExStyle,
            [MarshalAs(UnmanagedType.LPWStr)] string lpClassName,
            [MarshalAs(UnmanagedType.LPWStr)] string lpWindowName,
            uint dwStyle,
            int x,
            int y,
            int nWidth,
            int nHeight,
            IntPtr hWndParent,
            IntPtr hMenu,
            IntPtr hInstance,
            IntPtr lpParam);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DestroyWindow(IntPtr hWnd);

        [LibraryImport("user32.dll", EntryPoint = "DefWindowProcW")]
        internal static partial IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

        [LibraryImport("user32.dll")]
        internal static partial IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

        [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
        internal static partial uint RegisterWindowMessage([MarshalAs(UnmanagedType.LPWStr)] string lpString);

        // ==================== User32.dll - Menu Functions ====================
        [LibraryImport("user32.dll")]
        internal static partial IntPtr CreatePopupMenu();

        [LibraryImport("user32.dll", EntryPoint = "AppendMenuW", StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool AppendMenu(
            IntPtr hMenu,
            uint uFlags,
            uint uIDNewItem,
            [MarshalAs(UnmanagedType.LPWStr)] string lpNewItem);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DestroyMenu(IntPtr hMenu);

        [LibraryImport("user32.dll")]
        internal static partial int TrackPopupMenu(
            IntPtr hMenu,
            uint uFlags,
            int x,
            int y,
            int nReserved,
            IntPtr hWnd,
            IntPtr prcRect);

        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetCursorPos(out POINT lpPoint);

        // ==================== User32.dll - Display Configuration ====================
        [LibraryImport("user32.dll")]
        internal static partial int GetDisplayConfigBufferSizes(
            uint flags,
            out uint numPathArrayElements,
            out uint numModeInfoArrayElements);

        // With DisableRuntimeMarshalling, LibraryImport can handle struct arrays
        [LibraryImport("user32.dll")]
        internal static partial int QueryDisplayConfig(
            uint flags,
            ref uint numPathArrayElements,
            [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
            ref uint numModeInfoArrayElements,
            [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
            IntPtr currentTopologyId);

        [LibraryImport("user32.dll")]
        internal static partial int DisplayConfigGetDeviceInfo(
            ref DISPLAYCONFIG_TARGET_DEVICE_NAME deviceName);

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
        internal static partial bool GetMonitorInfo(
            IntPtr hMonitor,
            ref MonitorInfoEx lpmi);

        [LibraryImport("user32.dll", EntryPoint = "EnumDisplayDevicesW", StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool EnumDisplayDevices(
            [MarshalAs(UnmanagedType.LPWStr)] string? lpDevice,
            uint iDevNum,
            ref DisplayDevice lpDisplayDevice,
            uint dwFlags);

        [LibraryImport("user32.dll")]
        internal static partial IntPtr MonitorFromWindow(
            IntPtr hwnd,
            uint dwFlags);

        [LibraryImport("user32.dll")]
        internal static partial IntPtr MonitorFromPoint(
            POINT pt,
            uint dwFlags);

        // ==================== Shell32.dll - Tray Icon ====================
        [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW", StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool Shell_NotifyIcon(
            uint dwMessage,
            ref NOTIFYICONDATA lpData);

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

        [LibraryImport("Dxva2.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetMonitorBrightness(
            IntPtr hPhysicalMonitor,
            out uint pdwMinimumBrightness,
            out uint pdwCurrentBrightness,
            out uint pdwMaximumBrightness);

        [LibraryImport("Dxva2.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetMonitorBrightness(
            IntPtr hPhysicalMonitor,
            uint dwNewBrightness);

        [LibraryImport("Dxva2.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetMonitorContrast(
            IntPtr hPhysicalMonitor,
            out uint pdwMinimumContrast,
            out uint pdwCurrentContrast,
            out uint pdwMaximumContrast);

        [LibraryImport("Dxva2.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool SetMonitorContrast(
            IntPtr hPhysicalMonitor,
            uint dwNewContrast);

        [LibraryImport("Dxva2.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool GetMonitorCapabilities(
            IntPtr hPhysicalMonitor,
            out uint pdwMonitorCapabilities,
            out uint pdwSupportedColorTemperatures);

        // ==================== Kernel32.dll ====================
        [LibraryImport("kernel32.dll")]
        internal static partial uint GetLastError();
    }
}
