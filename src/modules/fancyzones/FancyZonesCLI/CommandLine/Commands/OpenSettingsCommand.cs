// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.IO;

namespace FancyZonesCLI.CommandLine.Commands;

internal sealed partial class OpenSettingsCommand : FancyZonesBaseCommand
{
    public OpenSettingsCommand()
        : base("open-settings", "Open FancyZones settings page")
    {
        AddAlias("settings");
    }

    protected override string Execute(InvocationContext context)
    {
        // Check in the same directory as the CLI (typical for dev builds)
        var powertoysExe = Path.Combine(AppContext.BaseDirectory, "PowerToys.exe");
        if (!File.Exists(powertoysExe))
        {
            throw new FileNotFoundException("PowerToys.exe not found. Ensure PowerToys is installed, or run the CLI from the same folder as PowerToys.exe.", powertoysExe);
        }

        try
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = powertoysExe,
                Arguments = "--open-settings=FancyZones",
                UseShellExecute = false,
            });

            if (process == null)
            {
                throw new InvalidOperationException("PowerToys.exe failed to start.");
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to open FancyZones Settings. {ex.Message}", ex);
        }
    }
}
