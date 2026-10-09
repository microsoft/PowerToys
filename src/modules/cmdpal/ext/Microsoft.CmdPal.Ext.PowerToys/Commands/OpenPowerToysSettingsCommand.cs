// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Commands;

/// <summary>
/// Opens the PowerToys settings application deep linked to a specific module.
/// </summary>
internal sealed partial class OpenPowerToysSettingsCommand : InvokableCommand
{
    private readonly string _moduleName;
    private readonly string _settingsKey;

    internal OpenPowerToysSettingsCommand(string moduleName, string settingsKey)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            throw new ArgumentException("Module name is required", nameof(moduleName));
        }

        if (string.IsNullOrWhiteSpace(settingsKey))
        {
            throw new ArgumentException("Settings key is required", nameof(settingsKey));
        }

        _moduleName = moduleName;
        _settingsKey = settingsKey;
        Name = $"Open {_moduleName} settings";
    }

    public override CommandResult Invoke()
    {
        try
        {
            var powerToysPath = PowerToysPathResolver.TryResolveExecutable("PowerToys.exe");
            if (string.IsNullOrEmpty(powerToysPath))
            {
                return CommandResult.ShowToast("Unable to locate PowerToys.");
            }

            var startInfo = new ProcessStartInfo(powerToysPath)
            {
                Arguments = $"--open-settings={_settingsKey}",
                UseShellExecute = false,
            };

            Process.Start(startInfo);
            return CommandResult.Hide();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Opening {_moduleName} settings failed: {ex.Message}");
        }
    }
}
