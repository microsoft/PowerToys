// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Win32;

namespace SamplePagesExtension;

internal sealed partial class SampleShowPowerDisplayCommand : InvokableCommand
{
    public SampleShowPowerDisplayCommand()
    {
        Name = "Show PowerDisplay";
    }

    public override ICommandResult Invoke()
    {
        try
        {
            PInvoke.GetCursorPos(out var cursorPos);

            var extensionDir = AppContext.BaseDirectory;
            var exePath = Path.GetFullPath(Path.Combine(extensionDir, "..", "..", "..", "PowerToys.PowerDisplay.exe"));

            if (!File.Exists(exePath))
            {
                return CommandResult.ShowToast($"PowerDisplay.exe not found at {exePath}");
            }

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"--show-at {cursorPos.X} {cursorPos.Y}",
                UseShellExecute = false,
            });

            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to launch PowerDisplay: {ex.Message}");
        }
    }
}
