// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using ManagedCommon;
using PowerDisplay.Common.Models;
using static PowerDisplay.Common.Drivers.NativeConstants;
using static PowerDisplay.Common.Drivers.PInvoke;

using DevMode = PowerDisplay.Common.Drivers.DevMode;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// Enumerates and changes refresh rates using the Windows display settings API.
    /// </summary>
    public sealed class DisplayRefreshRateService
    {
        /// <returns>
        /// The rates in ascending order, or null if they could not be read. Empty is a valid
        /// result: the current resolution offers only the hardware-default rate.
        /// </returns>
        public unsafe IReadOnlyList<int>? GetAvailableRefreshRates(string gdiDeviceName)
        {
            if (string.IsNullOrEmpty(gdiDeviceName))
            {
                return null;
            }

            try
            {
                if (!TryGetCurrentMode(gdiDeviceName, out var currentMode))
                {
                    return null;
                }

                var rates = new HashSet<int>();
                for (var modeIndex = 0; ; modeIndex++)
                {
                    DevMode mode = default;
                    mode.DmSize = (short)sizeof(DevMode);
                    if (!EnumDisplaySettings(gdiDeviceName, modeIndex, &mode))
                    {
                        break;
                    }

                    if (mode.DmPelsWidth == currentMode.DmPelsWidth &&
                        mode.DmPelsHeight == currentMode.DmPelsHeight &&
                        IsSelectableRefreshRate(mode.DmDisplayFrequency))
                    {
                        rates.Add(mode.DmDisplayFrequency);
                    }
                }

                return rates.OrderBy(rate => rate).ToArray();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"GetAvailableRefreshRates: Exception for {gdiDeviceName}: {ex.Message}");
                return null;
            }
        }

        public int GetCurrentRefreshRate(string gdiDeviceName)
        {
            try
            {
                return TryGetCurrentMode(gdiDeviceName, out var mode) ? mode.DmDisplayFrequency : -1;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"GetCurrentRefreshRate: Exception for {gdiDeviceName}: {ex.Message}");
                return -1;
            }
        }

        public unsafe MonitorOperationResult SetRefreshRate(Monitor monitor, int refreshRate)
        {
            ArgumentNullException.ThrowIfNull(monitor);

            if (string.IsNullOrEmpty(monitor.GdiDeviceName))
            {
                return MonitorOperationResult.Failure("Monitor has no GdiDeviceName");
            }

            var availableRates = GetAvailableRefreshRates(monitor.GdiDeviceName);
            if (availableRates == null || !availableRates.Contains(refreshRate))
            {
                return MonitorOperationResult.Failure($"Refresh rate {refreshRate} Hz is not supported");
            }

            try
            {
                if (!TryGetCurrentMode(monitor.GdiDeviceName, out var mode))
                {
                    return MonitorOperationResult.Failure("Failed to get current display settings");
                }

                if (mode.DmDisplayFrequency == refreshRate)
                {
                    return MonitorOperationResult.Success();
                }

                // Keep the current orientation in the field mask: DEVMODE requires members
                // without a DM_* flag to be zero, and this change must not alter rotation.
                // The same goes for a non-default fixed-output (centered/stretched) scaling.
                mode.DmDisplayFrequency = refreshRate;
                mode.DmFields = DmPelsWidth | DmPelsHeight | DmDisplayFrequency | DmDisplayOrientation;
                if (mode.DmDisplayFixedOutput != 0)
                {
                    mode.DmFields |= DmDisplayFixedOutput;
                }

                var testResult = ChangeDisplaySettingsEx(
                    monitor.GdiDeviceName,
                    &mode,
                    IntPtr.Zero,
                    CdsTest,
                    IntPtr.Zero);
                if (testResult != DispChangeSuccessful)
                {
                    return MonitorOperationResult.Failure($"Display settings test failed: {testResult}", testResult);
                }

                var result = ChangeDisplaySettingsEx(
                    monitor.GdiDeviceName,
                    &mode,
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero);
                return result == DispChangeSuccessful
                    ? MonitorOperationResult.Success()
                    : MonitorOperationResult.Failure($"Failed to apply refresh rate: {result}", result);
            }
            catch (Exception ex)
            {
                Logger.LogError($"SetRefreshRate: Exception for {monitor.GdiDeviceName}: {ex.Message}");
                return MonitorOperationResult.Failure($"Exception while setting refresh rate: {ex.Message}");
            }
        }

        /// <summary>
        /// EnumDisplaySettings reports 0 or 1 for the hardware-default rate. That is not a
        /// numeric rate, so it is left out of the choices.
        /// </summary>
        internal static bool IsSelectableRefreshRate(int frequency) => frequency > 1;

        private static unsafe bool TryGetCurrentMode(string gdiDeviceName, out DevMode mode)
        {
            var currentMode = default(DevMode);
            currentMode.DmSize = (short)sizeof(DevMode);

            var result = EnumDisplaySettings(gdiDeviceName, EnumCurrentSettings, &currentMode);
            mode = currentMode;
            return result;
        }
    }
}
