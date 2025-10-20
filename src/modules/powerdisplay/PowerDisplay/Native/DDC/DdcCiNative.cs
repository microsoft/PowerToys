// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ManagedCommon;
using static PowerDisplay.Native.NativeConstants;
using static PowerDisplay.Native.NativeDelegates;
using static PowerDisplay.Native.PInvoke;

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
    /// 显示设备信息类
    /// </summary>
    public class DisplayDeviceInfo
    {
        public string DeviceName { get; set; } = string.Empty;
        public string AdapterName { get; set; } = string.Empty;
        public string DeviceID { get; set; } = string.Empty;
        public string DeviceKey { get; set; } = string.Empty;
    }

    /// <summary>
    /// DDC/CI 原生 API 封装
    /// </summary>
    public static class DdcCiNative
    {
        // Display Configuration 常量
        public const uint QdcAllPaths = 0x00000001;

        public const uint QdcOnlyActivePaths = 0x00000002;

        public const uint DisplayconfigDeviceInfoGetTargetName = 2;

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
        public static unsafe string? GetMonitorFriendlyName(LUID adapterId, uint targetId)
        {
            try
            {
                var deviceName = new DISPLAYCONFIG_TARGET_DEVICE_NAME
                {
                    Header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        Type = DisplayconfigDeviceInfoGetTargetName,
                        Size = (uint)sizeof(DISPLAYCONFIG_TARGET_DEVICE_NAME),
                        AdapterId = adapterId,
                        Id = targetId,
                    },
                };

                var result = DisplayConfigGetDeviceInfo(ref deviceName);
                if (result == 0) // ERROR_SUCCESS
                {
                    return deviceName.GetMonitorFriendlyDeviceName();
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
        public static unsafe string? GetMonitorHardwareId(LUID adapterId, uint targetId)
        {
            try
            {
                var deviceName = new DISPLAYCONFIG_TARGET_DEVICE_NAME
                {
                    Header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        Type = DisplayconfigDeviceInfoGetTargetName,
                        Size = (uint)sizeof(DISPLAYCONFIG_TARGET_DEVICE_NAME),
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

        /// <summary>
        /// 获取所有显示设备信息（使用 EnumDisplayDevices API）
        /// 与 Twinkle Tray 实现保持一致
        /// </summary>
        /// <returns>显示设备信息列表</returns>
        public static unsafe List<DisplayDeviceInfo> GetAllDisplayDevices()
        {
            var devices = new List<DisplayDeviceInfo>();

            try
            {
                // 枚举所有适配器
                uint adapterIndex = 0;
                var adapter = new DISPLAY_DEVICE();
                adapter.Cb = (uint)sizeof(DisplayDevice);

                while (EnumDisplayDevices(null, adapterIndex, ref adapter, EddGetDeviceInterfaceName))
                {
                    // 跳过镜像驱动程序
                    if ((adapter.StateFlags & DisplayDeviceMirroringDriver) != 0)
                    {
                        adapterIndex++;
                        adapter = new DISPLAY_DEVICE();
                        adapter.Cb = (uint)sizeof(DisplayDevice);
                        continue;
                    }

                    // 只处理已连接到桌面的适配器
                    if ((adapter.StateFlags & DisplayDeviceAttachedToDesktop) != 0)
                    {
                        // 枚举该适配器上的所有显示器
                        uint displayIndex = 0;
                        var display = new DISPLAY_DEVICE();
                        display.Cb = (uint)sizeof(DisplayDevice);

                        string adapterDeviceName = adapter.GetDeviceName();
                        while (EnumDisplayDevices(adapterDeviceName, displayIndex, ref display, EddGetDeviceInterfaceName))
                        {
                            string displayDeviceID = display.GetDeviceID();
                            // 只处理活动的显示器
                            if ((display.StateFlags & DisplayDeviceAttachedToDesktop) != 0 &&
                                !string.IsNullOrEmpty(displayDeviceID))
                            {
                                var deviceInfo = new DisplayDeviceInfo
                                {
                                    DeviceName = display.GetDeviceName(),
                                    AdapterName = adapterDeviceName,
                                    DeviceID = displayDeviceID,
                                };

                                // 提取 DeviceKey：移除 GUID 部分（#{...} 及之后的内容）
                                // 例如：\\?\DISPLAY#GSM5C6D#5&1234&0&UID#{GUID} -> \\?\DISPLAY#GSM5C6D#5&1234&0&UID
                                int guidIndex = deviceInfo.DeviceID.IndexOf("#{", StringComparison.Ordinal);
                                if (guidIndex >= 0)
                                {
                                    deviceInfo.DeviceKey = deviceInfo.DeviceID.Substring(0, guidIndex);
                                }
                                else
                                {
                                    deviceInfo.DeviceKey = deviceInfo.DeviceID;
                                }

                                devices.Add(deviceInfo);

                                Logger.LogDebug($"Found display device - Name: {deviceInfo.DeviceName}, Adapter: {deviceInfo.AdapterName}, DeviceKey: {deviceInfo.DeviceKey}");
                            }

                            displayIndex++;
                            display = new DISPLAY_DEVICE();
                            display.Cb = (uint)sizeof(DisplayDevice);
                        }
                    }

                    adapterIndex++;
                    adapter = new DISPLAY_DEVICE();
                    adapter.Cb = (uint)sizeof(DisplayDevice);
                }

                Logger.LogInfo($"GetAllDisplayDevices found {devices.Count} display devices");
            }
            catch (Exception ex)
            {
                Logger.LogError($"GetAllDisplayDevices exception: {ex.Message}");
            }

            return devices;
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


