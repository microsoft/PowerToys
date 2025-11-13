// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using ManagedCommon;
using PowerDisplay.Core.Models;
using PowerDisplay.Core.Utils;

namespace PowerDisplay.Native.DDC
{
    /// <summary>
    /// Resolves and caches VCP codes for monitor controls
    /// Handles brightness, color temperature, and other VCP feature codes
    /// </summary>
    public class VcpCodeResolver
    {
        private readonly Dictionary<string, byte> _cachedCodes = new();

        // VCP code priority order (for brightness control)
        private static readonly byte[] BrightnessVcpCodes =
        {
            NativeConstants.VcpCodeBrightness,           // 0x10 - Standard brightness
            NativeConstants.VcpCodeBacklightControl,    // 0x13 - Backlight control
            NativeConstants.VcpCodeBacklightLevelWhite, // 0x6B - White backlight level
            NativeConstants.VcpCodeContrast,             // 0x12 - Contrast as last resort
        };

        // VCP code priority order (for color temperature control)
        // Per MCCS specification:
        // - 0x0C (Color Temperature Request): Set specific color temperature preset
        // - 0x0B (Color Temperature Increment): Increment color temperature value
        private static readonly byte[] ColorTemperatureVcpCodes =
        {
            NativeConstants.VcpCodeColorTemperature,           // 0x0C - Standard color temperature (primary)
            NativeConstants.VcpCodeColorTemperatureIncrement,  // 0x0B - Color temperature increment (fallback)
        };

        /// <summary>
        /// Get best VCP code for brightness control
        /// </summary>
        public byte? GetBrightnessVcpCode(string monitorId, IntPtr physicalHandle)
        {
            // Return cached best code if available
            if (_cachedCodes.TryGetValue(monitorId, out var cachedCode))
            {
                return cachedCode;
            }

            // Find first working VCP code (highest priority)
            foreach (var code in BrightnessVcpCodes)
            {
                if (DdcCiNative.TryGetVCPFeature(physicalHandle, code, out _, out _))
                {
                    // Cache and return the best (first working) code
                    _cachedCodes[monitorId] = code;
                    return code;
                }
            }

            return null;
        }

        /// <summary>
        /// Get best VCP code for color temperature control
        /// </summary>
        public byte? GetColorTemperatureVcpCode(string monitorId, IntPtr physicalHandle)
        {
            var cacheKey = $"{monitorId}_colortemp";

            // Return cached best code if available
            if (_cachedCodes.TryGetValue(cacheKey, out var cachedCode))
            {
                return cachedCode;
            }

            // Find first working VCP code (highest priority)
            foreach (var code in ColorTemperatureVcpCodes)
            {
                if (DdcCiNative.TryGetVCPFeature(physicalHandle, code, out _, out _))
                {
                    // Cache and return the best (first working) code
                    _cachedCodes[cacheKey] = code;
                    return code;
                }
            }

            return null;
        }

        /// <summary>
        /// Convert Kelvin temperature to VCP value (uses unified converter)
        /// </summary>
        public uint ConvertKelvinToVcpValue(int kelvin, BrightnessInfo vcpRange)
        {
            return (uint)ColorTemperatureConverter.KelvinToVcp(kelvin, vcpRange.Maximum);
        }

        /// <summary>
        /// Get current color temperature information
        /// </summary>
        public BrightnessInfo GetCurrentColorTemperature(IntPtr physicalHandle)
        {
            // Try different VCP codes to get color temperature
            foreach (var code in ColorTemperatureVcpCodes)
            {
                if (DdcCiNative.TryGetVCPFeature(physicalHandle, code, out uint current, out uint max))
                {
                    return new BrightnessInfo((int)current, 0, (int)max);
                }
            }

            return BrightnessInfo.Invalid;
        }

        /// <summary>
        /// Clear all cached codes
        /// </summary>
        public void ClearCache()
        {
            _cachedCodes.Clear();
        }
    }
}
