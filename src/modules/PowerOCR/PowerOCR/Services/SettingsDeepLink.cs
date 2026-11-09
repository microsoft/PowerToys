// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;

using ManagedCommon;

namespace PowerOCR.Services;

public sealed class SettingsDeepLink
{
    public void Open()
    {
        try
        {
            string? installPath = PowerToysPathResolver.GetPowerToysInstallPath();
            if (string.IsNullOrWhiteSpace(installPath))
            {
                Logger.LogError("Failed to resolve the PowerToys installation path.");
                return;
            }

            string executable = Path.Combine(installPath, "PowerToys.exe");
            if (!File.Exists(executable))
            {
                Logger.LogError($"Failed to find PowerToys.exe at '{executable}'.");
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "--open-settings=PowerOcr",
                UseShellExecute = false,
            });
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to open Text Extractor settings.", ex);
        }
    }
}
