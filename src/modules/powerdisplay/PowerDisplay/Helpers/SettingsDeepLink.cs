// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using ManagedCommon;

namespace PowerDisplay.Helpers
{
    /// <summary>
    /// Helper class to open PowerToys Settings application.
    /// Simplified version for PowerDisplay module (AOT compatible).
    /// </summary>
    internal static class SettingsDeepLink
    {
        /// <summary>
        /// Opens PowerToys Settings to PowerDisplay page
        /// </summary>
        public static void OpenPowerDisplaySettings()
        {
            try
            {
                // PowerDisplay is a WinUI3 app, PowerToys.exe is in parent directory
                var directoryPath = Path.Combine(AppContext.BaseDirectory, "..", "PowerToys.exe");

                var startInfo = new ProcessStartInfo(directoryPath)
                {
                    Arguments = "--open-settings=PowerDisplay",
                    UseShellExecute = true
                };

                Process.Start(startInfo);
                Logger.LogInfo("Opened PowerToys Settings to PowerDisplay page");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to open PowerToys Settings: {ex.Message}");
            }
        }
    }
}
