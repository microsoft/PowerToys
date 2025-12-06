// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace FancyZonesCLI.Commands;

/// <summary>
/// Editor and Settings commands.
/// </summary>
internal static class EditorCommands
{
    public static (int ExitCode, string Output) OpenEditor()
    {
        var editorExe = "PowerToys.FancyZonesEditor.exe";

        // Check if editor-parameters.json exists
        if (!FancyZonesData.EditorParametersExist())
        {
            return (1, "Error: editor-parameters.json not found.\nPlease launch FancyZones Editor using Win+` (Win+Backtick) hotkey first.");
        }

        // Check if editor is already running
        var existingProcess = Process.GetProcessesByName("PowerToys.FancyZonesEditor").FirstOrDefault();
        if (existingProcess != null)
        {
            NativeMethods.SetForegroundWindow(existingProcess.MainWindowHandle);
            return (0, "FancyZones Editor is already running. Brought window to foreground.");
        }

        // Only check same directory as CLI
        var editorPath = Path.Combine(AppContext.BaseDirectory, editorExe);

        if (File.Exists(editorPath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = editorPath,
                    UseShellExecute = true,
                });
                return (0, "FancyZones Editor launched successfully.");
            }
            catch (Exception ex)
            {
                return (1, $"Failed to launch: {ex.Message}");
            }
        }

        return (1, $"Error: Could not find {editorExe} in {AppContext.BaseDirectory}");
    }

    public static (int ExitCode, string Output) OpenSettings()
    {
        try
        {
            // Find PowerToys.exe in common locations
            string powertoysExe = null;

            // Check in the same directory as the CLI (typical for dev builds)
            var sameDirPath = Path.Combine(AppContext.BaseDirectory, "PowerToys.exe");
            if (File.Exists(sameDirPath))
            {
                powertoysExe = sameDirPath;
            }

            if (powertoysExe == null)
            {
                return (1, "Error: PowerToys.exe not found. Please ensure PowerToys is installed.");
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = powertoysExe,
                Arguments = "--open-settings=FancyZones",
                UseShellExecute = false,
            });
            return (0, "FancyZones Settings opened successfully.");
        }
        catch (Exception ex)
        {
            return (1, $"Error: Failed to open FancyZones Settings. {ex.Message}");
        }
    }
}
