// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;

using ManagedCommon;

namespace PowerOCR.Helpers
{
    public static class SettingsDeepLink
    {
        public static void OpenSettings()
        {
            try
            {
                // WinUI 3 apps are in a subfolder; PowerToys.exe is in the parent directory.
                var directoryPath = AppContext.BaseDirectory;
                var exePath = Path.Combine(directoryPath, "..", "PowerToys.exe");

                if (!File.Exists(exePath))
                {
                    // Fallback: same directory
                    exePath = Path.Combine(directoryPath, "PowerToys.exe");
                }

                if (!File.Exists(exePath))
                {
                    Logger.LogError($"Failed to find PowerToys.exe at {exePath}");
                    return;
                }

                Process.Start(new ProcessStartInfo(exePath) { Arguments = "--open-settings=PowerOcr", UseShellExecute = false });
            }
            catch (Exception ex)
            {
                Logger.LogError($"SettingsDeepLink.OpenSettings exception: {ex.Message}");
            }
        }
    }
}
