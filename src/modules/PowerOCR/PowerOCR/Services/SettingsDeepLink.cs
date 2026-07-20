// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;

using ManagedCommon;

namespace PowerOCR.Services;

internal sealed class SettingsDeepLink
{
    public void Open()
    {
        string executable = Path.Combine(
            PowerToysPathResolver.GetPowerToysInstallPath(),
            "PowerToys.exe");

        if (!File.Exists(executable))
        {
            Logger.LogError($"Failed to find PowerToys.exe at '{executable}'.");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "--open-settings=PowerOcr",
                UseShellExecute = false,
            });
        }
        catch (Win32Exception ex)
        {
            Logger.LogError("Failed to open Text Extractor settings.", ex);
        }
        catch (InvalidOperationException ex)
        {
            Logger.LogError("Failed to open Text Extractor settings.", ex);
        }
    }
}
