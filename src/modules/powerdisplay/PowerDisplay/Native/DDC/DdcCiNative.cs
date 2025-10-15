// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ManagedCommon;
using static PowerDisplay.Native.NativeConstants;
using static PowerDisplay.Native.NativeDelegates;

// 类型别名，兼容 Windows API 命名约定
using PHYSICAL_MONITOR = PowerDisplay.Native.PhysicalMonitor;
using RECT = PowerDisplay.Native.Rect;
using MONITORINFOEX = PowerDisplay.Native.MonitorInfoEx;
using DISPLAY_DEVICE = PowerDisplay.Native.DisplayDevice;
using LUID = PowerDisplay.Native.Luid;
using DISPLAYCONFIG_TARGET_DEVICE_NAME = PowerDisplay.Native.DISPLAYCONFIG_TARGET_DEVICE_NAME;
using DISPLAYCONFIG_DEVICE_INFO_HEADER = PowerDisplay.Native.DISPLAYCONFIG_DEVICE_INFO_HEADER;
using DISPLAYCONFIG_PATH_INFO = PowerDisplay.Native.DISPLAYCONFIG_PATH_INFO;
using DISPLAYCONFIG_MODE_INFO = PowerDisplay.Native.DISPLAYCONFIG_MODE_INFO;

namespace PowerDisplay.Native.DDC
{
    /// <summary>
    /// DDC/CI 原生 API 封装
    /// </summary>
    public static class DdcCiNative
    {
        // DLL Imports
        private const string Dxva2Dll = "Dxva2.dll";
        private const string User32Dll = "User32.dll";

        // Physical Monitor API

        /// <summary>
        /// 从 HMONITOR 获取物理显示器数量
        /// </summary>
        [DllImport(Dxva2Dll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor,
            ref uint pdwNumberOfPhysicalMonitors);

        /// <summary>
        /// 从 HMONITOR 获取物理显示器数组
        /// </summary>
        [DllImport(Dxva2Dll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor,
            uint dwPhysicalMonitorArraySize,
            [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        /// <summary>
        /// 销毁物理显示器句柄
        /// </summary>
        [DllImport(Dxva2Dll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DestroyPhysicalMonitor(IntPtr hPhysicalMonitor);

        /// <summary>
        /// 销毁物理显示器数组
        /// </summary>
        [DllImport(Dxva2Dll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyPhysicalMonitors(
            uint dwPhysicalMonitorArraySize,
            PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        /// <summary>
        /// 获取 VCP 功能和回复
        /// </summary>
        [DllImport(Dxva2Dll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetVCPFeatureAndVCPFeatureReply(
            IntPtr hPhysicalMonitor,
            byte bVCPCode,
            IntPtr pvct,
            out uint pdwCurrentValue,
            out uint pdwMaximumValue);

        /// <summary>
        /// 设置 VCP 功能
        /// </summary>
        [DllImport(Dxva2Dll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetVCPFeature(
            IntPtr hPhysicalMonitor,
            byte bVCPCode,
            uint dwNewValue);

        /// <summary>
        /// 保存当前设置
        /// </summary>
        [DllImport(Dxva2Dll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SaveCurrentSettings(IntPtr hPhysicalMonitor);

        /// <summary>
        /// 获取功能字符串长度
        /// </summary>
        [DllImport(Dxva2Dll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCapabilitiesStringLength(
            IntPtr hPhysicalMonitor,
            out uint pdwCapabilitiesStringLengthInCharacters);

        /// <summary>
        /// 功能请求和功能回复
        /// </summary>
        [DllImport(Dxva2Dll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CapabilitiesRequestAndCapabilitiesReply(
            IntPtr hPhysicalMonitor,
            [Out] IntPtr pszASCIICapabilitiesString,
            uint dwCapabilitiesStringLengthInCharacters);

        /// <summary>
        /// 获取显示器亮度
        /// </summary>
        [DllImport(Dxva2Dll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorBrightness(
            IntPtr hPhysicalMonitor,
            out uint pdwMinimumBrightness,
            out uint pdwCurrentBrightness,
            out uint pdwMaximumBrightness);

        /// <summary>
        /// 设置显示器亮度
        /// </summary>
        [DllImport(Dxva2Dll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetMonitorBrightness(
            IntPtr hPhysicalMonitor,
            uint dwNewBrightness);

        /// <summary>
        /// 获取显示器对比度
        /// </summary>
        [DllImport(Dxva2Dll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorContrast(
            IntPtr hPhysicalMonitor,
            out uint pdwMinimumContrast,
            out uint pdwCurrentContrast,
            out uint pdwMaximumContrast);

        /// <summary>
        /// 设置显示器对比度
        /// </summary>
        [DllImport(Dxva2Dll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetMonitorContrast(
            IntPtr hPhysicalMonitor,
            uint dwNewContrast);

        /// <summary>
        /// 获取显示器功能
        /// </summary>
        [DllImport(Dxva2Dll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorCapabilities(
            IntPtr hPhysicalMonitor,
            out uint pdwMonitorCapabilities,
            out uint pdwSupportedColorTemperatures);

        // Display Configuration API

        /// <summary>
        /// 获取显示配置缓冲区大小
        /// </summary>
        [DllImport(User32Dll, SetLastError = true)]
        private static extern int GetDisplayConfigBufferSizes(
            uint flags,
            out uint numPathArrayElements,
            out uint numModeInfoArrayElements);

        /// <summary>
        /// 查询显示配置
        /// </summary>
        [DllImport(User32Dll, SetLastError = true)]
        private static extern int QueryDisplayConfig(
            uint flags,
            ref uint numPathArrayElements,
            [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
            ref uint numModeInfoArrayElements,
            [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
            IntPtr currentTopologyId);

        /// <summary>
        /// 获取显示配置设备信息
        /// </summary>
        [DllImport(User32Dll, SetLastError = true)]
        private static extern int DisplayConfigGetDeviceInfo(
            ref DISPLAYCONFIG_TARGET_DEVICE_NAME deviceName);

        // Display Configuration 常量
        public const uint QdcAllPaths = 0x00000001;
        public const uint QdcOnlyActivePaths = 0x00000002;
        public const uint DisplayconfigDeviceInfoGetTargetName = 2;

        /// <summary>
        /// 枚举显示监视器
        /// </summary>
        [DllImport(User32Dll, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumDisplayMonitors(
            IntPtr hdc,
            IntPtr lprcClip,
            MonitorEnumProc lpfnEnum,
            IntPtr dwData);

        /// <summary>
        /// 获取显示器信息
        /// </summary>
        [DllImport(User32Dll, SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(
            IntPtr hMonitor,
            ref MONITORINFOEX lpmi);

        /// <summary>
        /// 枚举显示设备
        /// </summary>
        [DllImport(User32Dll, SetLastError = true, CharSet = CharSet.Ansi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplayDevices(
            string? lpDevice,
            uint iDevNum,
            ref DISPLAY_DEVICE lpDisplayDevice,
            uint dwFlags);

        /// <summary>
        /// 从窗口获取显示器句柄
        /// </summary>
        [DllImport(User32Dll, SetLastError = true)]
        public static extern IntPtr MonitorFromWindow(
            IntPtr hwnd,
            uint dwFlags);

        /// <summary>
        /// 从点获取显示器句柄
        /// </summary>
        [DllImport(User32Dll, SetLastError = true)]
        public static extern IntPtr MonitorFromPoint(
            POINT pt,
            uint dwFlags);

        /// <summary>
        /// 获取最后错误
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint GetLastError();

        // Helper Methods

        /// <summary>
        /// 获取 VCP 功能值的安全包装
        /// </summary>
        /// <param name="hPhysicalMonitor">物理显示器句柄</param>
        /// <param name="vcpCode">VCP 代码</param>
        /// <param name="currentValue">当前值</param>
        /// <param name="maxValue">最大值</param>
        /// <returns>是否成功</returns>
        public static bool TryGetVCPFeature(IntPtr hPhysicalMonitor, byte vcpCode, out uint currentValue, out uint maxValue)
        {
            currentValue = 0;
            maxValue = 0;

            if (hPhysicalMonitor == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                return GetVCPFeatureAndVCPFeatureReply(hPhysicalMonitor, vcpCode, IntPtr.Zero, out currentValue, out maxValue);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 设置 VCP 功能值的安全包装
        /// </summary>
        /// <param name="hPhysicalMonitor">物理显示器句柄</param>
        /// <param name="vcpCode">VCP 代码</param>
        /// <param name="value">新值</param>
        /// <returns>是否成功</returns>
        public static bool TrySetVCPFeature(IntPtr hPhysicalMonitor, byte vcpCode, uint value)
        {
            if (hPhysicalMonitor == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                return SetVCPFeature(hPhysicalMonitor, vcpCode, value);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 获取高级亮度信息的安全包装
        /// </summary>
        /// <param name="hPhysicalMonitor">物理显示器句柄</param>
        /// <param name="minBrightness">最小亮度</param>
        /// <param name="currentBrightness">当前亮度</param>
        /// <param name="maxBrightness">最大亮度</param>
        /// <returns>是否成功</returns>
        public static bool TryGetMonitorBrightness(IntPtr hPhysicalMonitor, out uint minBrightness, out uint currentBrightness, out uint maxBrightness)
        {
            minBrightness = 0;
            currentBrightness = 0;
            maxBrightness = 0;

            if (hPhysicalMonitor == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                return GetMonitorBrightness(hPhysicalMonitor, out minBrightness, out currentBrightness, out maxBrightness);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 设置高级亮度的安全包装
        /// </summary>
        /// <param name="hPhysicalMonitor">物理显示器句柄</param>
        /// <param name="brightness">亮度值</param>
        /// <returns>是否成功</returns>
        public static bool TrySetMonitorBrightness(IntPtr hPhysicalMonitor, uint brightness)
        {
            if (hPhysicalMonitor == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                return SetMonitorBrightness(hPhysicalMonitor, brightness);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 检查 DDC/CI 连接的有效性
        /// </summary>
        /// <param name="hPhysicalMonitor">物理显示器句柄</param>
        /// <returns>是否连接有效</returns>
        public static bool ValidateDdcCiConnection(IntPtr hPhysicalMonitor)
        {
            if (hPhysicalMonitor == IntPtr.Zero)
            {
                return false;
            }

            // 尝试读取基本的 VCP 代码来验证连接
            var testCodes = new byte[] { NativeConstants.VcpCodeBrightness, NativeConstants.VcpCodeNewControlValue, NativeConstants.VcpCodeVcpVersion };

            foreach (var code in testCodes)
            {
                if (TryGetVCPFeature(hPhysicalMonitor, code, out _, out _))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取显示器友好名称
        /// </summary>
        /// <param name="adapterId">适配器 ID</param>
        /// <param name="targetId">目标 ID</param>
        /// <returns>显示器友好名称，如果获取失败返回 null</returns>
        public static string? GetMonitorFriendlyName(LUID adapterId, uint targetId)
        {
            try
            {
                var deviceName = new DISPLAYCONFIG_TARGET_DEVICE_NAME
                {
                    Header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        Type = DisplayconfigDeviceInfoGetTargetName,
                        Size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
                        AdapterId = adapterId,
                        Id = targetId,
                    },
                };

                var result = DisplayConfigGetDeviceInfo(ref deviceName);
                if (result == 0) // ERROR_SUCCESS
                {
                    return deviceName.MonitorFriendlyDeviceName;
                }
            }
            catch
            {
                // 忽略错误
            }

            return null;
        }

        /// <summary>
        /// 通过枚举显示配置获取所有显示器友好名称
        /// </summary>
        /// <returns>设备路径到友好名称的映射</returns>
        public static Dictionary<string, string> GetAllMonitorFriendlyNames()
        {
            var friendlyNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // 获取缓冲区大小
                var result = GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out uint pathCount, out uint modeCount);
                if (result != 0) // ERROR_SUCCESS
                {
                    return friendlyNames;
                }

                // 分配缓冲区
                var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

                // 查询显示配置
                result = QueryDisplayConfig(QdcOnlyActivePaths, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
                if (result != 0)
                {
                    return friendlyNames;
                }

                // 获取每个路径的友好名称
                for (int i = 0; i < pathCount; i++)
                {
                    var path = paths[i];
                    var friendlyName = GetMonitorFriendlyName(path.TargetInfo.AdapterId, path.TargetInfo.Id);

                    if (!string.IsNullOrEmpty(friendlyName))
                    {
                        // 使用适配器和目标 ID 作为键
                        var key = $"{path.TargetInfo.AdapterId}_{path.TargetInfo.Id}";
                        friendlyNames[key] = friendlyName;
                    }
                }
            }
            catch
            {
                // 忽略错误
            }

            return friendlyNames;
        }

        /// <summary>
        /// 获取显示器的EDID硬件ID信息
        /// </summary>
        /// <param name="adapterId">适配器ID</param>
        /// <param name="targetId">目标ID</param>
        /// <returns>硬件ID字符串，格式为制造商代码+产品代码</returns>
        public static string? GetMonitorHardwareId(LUID adapterId, uint targetId)
        {
            try
            {
                var deviceName = new DISPLAYCONFIG_TARGET_DEVICE_NAME
                {
                    Header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        Type = DisplayconfigDeviceInfoGetTargetName,
                        Size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
                        AdapterId = adapterId,
                        Id = targetId,
                    },
                };

                var result = DisplayConfigGetDeviceInfo(ref deviceName);
                if (result == 0) // ERROR_SUCCESS
                {
                    // 将制造商ID转换为3字符字符串
                    var manufacturerId = deviceName.EdidManufactureId;
                    var manufactureCode = ConvertManufactureIdToString(manufacturerId);

                    // 将产品ID转换为4位十六进制字符串
                    var productCode = deviceName.EdidProductCodeId.ToString("X4");

                    var hardwareId = $"{manufactureCode}{productCode}";
                    Logger.LogDebug($"GetMonitorHardwareId - ManufacturerId: 0x{manufacturerId:X4}, Code: '{manufactureCode}', ProductCode: '{productCode}', Result: '{hardwareId}'");

                    return hardwareId;
                }
                else
                {
                    Logger.LogError($"GetMonitorHardwareId - DisplayConfigGetDeviceInfo failed with result: {result}");
                }
            }
            catch
            {
                // 忽略错误
            }

            return null;
        }

        /// <summary>
        /// 将制造商ID转换为3字符制造商代码
        /// </summary>
        /// <param name="manufacturerId">制造商ID</param>
        /// <returns>3字符制造商代码</returns>
        private static string ConvertManufactureIdToString(ushort manufacturerId)
        {
            // EDID制造商ID需要先进行字节序交换
            manufacturerId = (ushort)(((manufacturerId & 0xff00) >> 8) | ((manufacturerId & 0x00ff) << 8));

            // 提取3个5位字符（每个字符是A-Z，其中A=1, B=2, ..., Z=26）
            var char1 = (char)('A' - 1 + ((manufacturerId >> 0) & 0x1f));
            var char2 = (char)('A' - 1 + ((manufacturerId >> 5) & 0x1f));
            var char3 = (char)('A' - 1 + ((manufacturerId >> 10) & 0x1f));

            // 按正确顺序组合字符
            return $"{char3}{char2}{char1}";
        }

        /// <summary>
        /// 获取所有显示器的完整信息，包括友好名称和硬件ID
        /// </summary>
        /// <returns>包含显示器信息的字典</returns>
        public static Dictionary<string, MonitorDisplayInfo> GetAllMonitorDisplayInfo()
        {
            var monitorInfo = new Dictionary<string, MonitorDisplayInfo>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // 获取缓冲区大小
                var result = GetDisplayConfigBufferSizes(QdcOnlyActivePaths, out uint pathCount, out uint modeCount);
                if (result != 0) // ERROR_SUCCESS
                {
                    return monitorInfo;
                }

                // 分配缓冲区
                var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

                // 查询显示配置
                result = QueryDisplayConfig(QdcOnlyActivePaths, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero);
                if (result != 0)
                {
                    return monitorInfo;
                }

                // 获取每个路径的信息
                for (int i = 0; i < pathCount; i++)
                {
                    var path = paths[i];
                    var friendlyName = GetMonitorFriendlyName(path.TargetInfo.AdapterId, path.TargetInfo.Id);
                    var hardwareId = GetMonitorHardwareId(path.TargetInfo.AdapterId, path.TargetInfo.Id);

                    if (!string.IsNullOrEmpty(friendlyName) || !string.IsNullOrEmpty(hardwareId))
                    {
                        var key = $"{path.TargetInfo.AdapterId}_{path.TargetInfo.Id}";
                        monitorInfo[key] = new MonitorDisplayInfo
                        {
                            FriendlyName = friendlyName ?? string.Empty,
                            HardwareId = hardwareId ?? string.Empty,
                            AdapterId = path.TargetInfo.AdapterId,
                            TargetId = path.TargetInfo.Id
                        };
                    }
                }
            }
            catch
            {
                // 忽略错误
            }

            return monitorInfo;
        }
    }

    /// <summary>
    /// 显示器显示信息结构
    /// </summary>
    public struct MonitorDisplayInfo
    {
        public string FriendlyName { get; set; }
        public string HardwareId { get; set; }
        public LUID AdapterId { get; set; }
        public uint TargetId { get; set; }
    }
}

