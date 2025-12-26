// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using PowerDisplay.Common.Models;
using static PowerDisplay.Common.Drivers.NativeConstants;
using static PowerDisplay.Common.Drivers.PInvoke;

using DevMode = PowerDisplay.Common.Drivers.DevMode;

namespace PowerDisplay.Common.Services
{
    /// <summary>
    /// Service for controlling display rotation/orientation.
    /// Uses ChangeDisplaySettingsEx API to change display orientation.
    /// </summary>
    public class DisplayRotationService
    {
        /// <summary>
        /// Set display rotation for a specific monitor.
        /// Uses GdiDeviceName from the Monitor object for accurate adapter targeting.
        /// </summary>
        /// <param name="monitor">Monitor object with GdiDeviceName</param>
        /// <param name="newOrientation">New orientation: 0=normal, 1=90°, 2=180°, 3=270°</param>
        /// <returns>Operation result</returns>
        public MonitorOperationResult SetRotation(Monitor monitor, int newOrientation)
        {
            ArgumentNullException.ThrowIfNull(monitor);

            if (newOrientation < 0 || newOrientation > 3)
            {
                return MonitorOperationResult.Failure($"Invalid orientation value: {newOrientation}. Must be 0-3.");
            }

            if (string.IsNullOrEmpty(monitor.GdiDeviceName))
            {
                return MonitorOperationResult.Failure("Monitor has no GdiDeviceName");
            }

            return SetRotationByGdiDeviceName(monitor.GdiDeviceName, newOrientation);
        }

        /// <summary>
        /// Set display rotation by GDI device name.
        /// </summary>
        /// <param name="gdiDeviceName">GDI device name (e.g., "\\.\DISPLAY1")</param>
        /// <param name="newOrientation">New orientation: 0=normal, 1=90°, 2=180°, 3=270°</param>
        /// <returns>Operation result</returns>
        public unsafe MonitorOperationResult SetRotationByGdiDeviceName(string gdiDeviceName, int newOrientation)
        {
            if (string.IsNullOrEmpty(gdiDeviceName))
            {
                return MonitorOperationResult.Failure("GDI device name is required");
            }

            try
            {
                // 1. Get current display settings
                DevMode devMode = default;
                devMode.DmSize = (short)sizeof(DevMode);

                if (!EnumDisplaySettings(gdiDeviceName, EnumCurrentSettings, &devMode))
                {
                    var error = GetLastError();
                    Logger.LogError($"SetRotation: EnumDisplaySettings failed for {gdiDeviceName}, error: {error}");
                    return MonitorOperationResult.Failure($"Failed to get current display settings for {gdiDeviceName}", (int)error);
                }

                int currentOrientation = devMode.DmDisplayOrientation;

                // If already at target orientation, return success
                if (currentOrientation == newOrientation)
                {
                    return MonitorOperationResult.Success();
                }

                // 2. Determine if we need to swap width and height
                // When switching between landscape (0°/180°) and portrait (90°/270°), swap dimensions
                bool currentIsLandscape = currentOrientation == DmdoDefault || currentOrientation == Dmdo180;
                bool newIsLandscape = newOrientation == DmdoDefault || newOrientation == Dmdo180;

                if (currentIsLandscape != newIsLandscape)
                {
                    // Swap width and height
                    int temp = devMode.DmPelsWidth;
                    devMode.DmPelsWidth = devMode.DmPelsHeight;
                    devMode.DmPelsHeight = temp;
                }

                // 3. Set new orientation
                devMode.DmDisplayOrientation = newOrientation;
                devMode.DmFields = DmDisplayOrientation | DmPelsWidth | DmPelsHeight;

                // 4. Test the settings first using CDS_TEST flag
                int testResult = ChangeDisplaySettingsEx(gdiDeviceName, &devMode, IntPtr.Zero, CdsTest, IntPtr.Zero);
                if (testResult != DispChangeSuccessful)
                {
                    string errorMsg = GetChangeDisplaySettingsErrorMessage(testResult);
                    Logger.LogError($"SetRotation: Test failed for {gdiDeviceName}: {errorMsg}");
                    return MonitorOperationResult.Failure($"Display settings test failed: {errorMsg}", testResult);
                }

                // 5. Apply the settings (without CDS_UPDATEREGISTRY to make it temporary)
                int result = ChangeDisplaySettingsEx(gdiDeviceName, &devMode, IntPtr.Zero, 0, IntPtr.Zero);
                if (result != DispChangeSuccessful)
                {
                    string errorMsg = GetChangeDisplaySettingsErrorMessage(result);
                    Logger.LogError($"SetRotation: Apply failed for {gdiDeviceName}: {errorMsg}");
                    return MonitorOperationResult.Failure($"Failed to apply display settings: {errorMsg}", result);
                }

                return MonitorOperationResult.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError($"SetRotation: Exception for {gdiDeviceName}: {ex.Message}");
                return MonitorOperationResult.Failure($"Exception while setting rotation: {ex.Message}");
            }
        }

        /// <summary>
        /// Get current orientation for a GDI device name.
        /// </summary>
        /// <param name="gdiDeviceName">GDI device name (e.g., "\\.\DISPLAY1")</param>
        /// <returns>Current orientation (0-3), or -1 if query failed</returns>
        public unsafe int GetCurrentOrientation(string gdiDeviceName)
        {
            if (string.IsNullOrEmpty(gdiDeviceName))
            {
                return -1;
            }

            try
            {
                DevMode devMode = default;
                devMode.DmSize = (short)sizeof(DevMode);

                if (!EnumDisplaySettings(gdiDeviceName, EnumCurrentSettings, &devMode))
                {
                    return -1;
                }

                return devMode.DmDisplayOrientation;
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Get human-readable error message for ChangeDisplaySettings result code.
        /// </summary>
        private static string GetChangeDisplaySettingsErrorMessage(int resultCode)
        {
            return resultCode switch
            {
                DispChangeSuccessful => "Success",
                DispChangeRestart => "Computer must be restarted",
                DispChangeFailed => "Display driver failed the specified graphics mode",
                DispChangeBadmode => "Graphics mode is not supported",
                DispChangeNotupdated => "Unable to write settings to registry",
                DispChangeBadflags => "Invalid flags",
                DispChangeBadparam => "Invalid parameter",
                _ => $"Unknown error code: {resultCode}",
            };
        }
    }
}
