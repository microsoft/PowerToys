// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;

using ManagedCommon;

namespace Hosts.Helpers
{
    public static class SettingsDeepLink
    {
        public static void OpenSettings()
        {
            try
            {
                var exePath = Path.Combine(PowerToysPathResolver.GetPowerToysInstallPath(), "PowerToys.exe");

                if (!File.Exists(exePath))
                {
                    Logger.LogError($"Failed to find powertoys exe path, {exePath}");
                    return;
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = "--open-settings=Hosts",
                    UseShellExecute = false,
                });
            }
            catch (Exception ex)
            {
                Logger.LogError(ex.Message);
            }
        }
    }
}
