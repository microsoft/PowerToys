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
        /// </summary>
        /// <param name="monitorNumber">Monitor number (1, 2, 3...)</param>
        /// <param name="newOrientation">New orientation: 0=normal, 1=90°, 2=180°, 3=270°</param>
        /// <returns>Operation result</returns>
        public MonitorOperationResult SetRotation(int monitorNumber, int newOrientation)
        {
            if (monitorNumber <= 0)
            {
                return MonitorOperationResult.Failure("Invalid monitor number");
            }

            if (newOrientation < 0 || newOrientation > 3)
            {
                return MonitorOperationResult.Failure($"Invalid orientation value: {newOrientation}. Must be 0-3.");
            }

            // Construct adapter name from monitor number (e.g., 1 -> "\\.\DISPLAY1")
            string adapterName = $"\\\\.\\DISPLAY{monitorNumber}";

            return SetRotationByAdapterName(adapterName, newOrientation);
        }

        /// <summary>
        /// Set display rotation by adapter name.
        /// </summary>
        /// <param name="adapterName">Adapter name (e.g., "\\.\DISPLAY1")</param>
        /// <param name="newOrientation">New orientation: 0=normal, 1=90°, 2=180°, 3=270°</param>
        /// <returns>Operation result</returns>
        public unsafe MonitorOperationResult SetRotationByAdapterName(string adapterName, int newOrientation)
        {
            try
            {
                Logger.LogInfo($"SetRotation: Setting {adapterName} to orientation {newOrientation}");

                // 1. Get current display settings
                DevMode devMode = default;
                devMode.DmSize = (short)sizeof(DevMode);

                if (!EnumDisplaySettings(adapterName, EnumCurrentSettings, &devMode))
                {
                    var error = GetLastError();
                    Logger.LogError($"SetRotation: EnumDisplaySettings failed for {adapterName}, error: {error}");
                    return MonitorOperationResult.Failure($"Failed to get current display settings for {adapterName}", (int)error);
                }

                int currentOrientation = devMode.DmDisplayOrientation;
                Logger.LogDebug($"SetRotation: Current orientation={currentOrientation}, target={newOrientation}");

                // If already at target orientation, return success
                if (currentOrientation == newOrientation)
                {
                    Logger.LogDebug($"SetRotation: Already at target orientation {newOrientation}");
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
                    Logger.LogDebug($"SetRotation: Swapped dimensions to {devMode.DmPelsWidth}x{devMode.DmPelsHeight}");
                }

                // 3. Set new orientation
                devMode.DmDisplayOrientation = newOrientation;
                devMode.DmFields = DmDisplayOrientation | DmPelsWidth | DmPelsHeight;

                // 4. Test the settings first using CDS_TEST flag
                int testResult = ChangeDisplaySettingsEx(adapterName, &devMode, IntPtr.Zero, CdsTest, IntPtr.Zero);
                if (testResult != DispChangeSuccessful)
                {
                    string errorMsg = GetChangeDisplaySettingsErrorMessage(testResult);
                    Logger.LogError($"SetRotation: Test failed for {adapterName}: {errorMsg}");
                    return MonitorOperationResult.Failure($"Display settings test failed: {errorMsg}", testResult);
                }

                Logger.LogDebug($"SetRotation: Test passed, applying settings...");

                // 5. Apply the settings (without CDS_UPDATEREGISTRY to make it temporary)
                int result = ChangeDisplaySettingsEx(adapterName, &devMode, IntPtr.Zero, 0, IntPtr.Zero);
                if (result != DispChangeSuccessful)
                {
                    string errorMsg = GetChangeDisplaySettingsErrorMessage(result);
                    Logger.LogError($"SetRotation: Apply failed for {adapterName}: {errorMsg}");
                    return MonitorOperationResult.Failure($"Failed to apply display settings: {errorMsg}", result);
                }

                Logger.LogInfo($"SetRotation: Successfully set {adapterName} to orientation {newOrientation}");
                return MonitorOperationResult.Success();
            }
            catch (Exception ex)
            {
                Logger.LogError($"SetRotation: Exception for {adapterName}: {ex.Message}");
                return MonitorOperationResult.Failure($"Exception while setting rotation: {ex.Message}");
            }
        }

        /// <summary>
        /// Get current rotation for a specific monitor.
        /// </summary>
        /// <param name="monitorNumber">Monitor number (1, 2, 3...)</param>
        /// <returns>Current orientation (0-3), or -1 if failed</returns>
        public int GetCurrentRotation(int monitorNumber)
        {
            if (monitorNumber <= 0)
            {
                return -1;
            }

            string adapterName = $"\\\\.\\DISPLAY{monitorNumber}";
            return GetCurrentRotationByAdapterName(adapterName);
        }

        /// <summary>
        /// Get current rotation by adapter name.
        /// </summary>
        /// <param name="adapterName">Adapter name (e.g., "\\.\DISPLAY1")</param>
        /// <returns>Current orientation (0-3), or -1 if failed</returns>
        public unsafe int GetCurrentRotationByAdapterName(string adapterName)
        {
            try
            {
                DevMode devMode = default;
                devMode.DmSize = (short)sizeof(DevMode);

                if (EnumDisplaySettings(adapterName, EnumCurrentSettings, &devMode))
                {
                    return devMode.DmDisplayOrientation;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"GetCurrentRotation: Exception for {adapterName}: {ex.Message}");
            }

            return -1;
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
