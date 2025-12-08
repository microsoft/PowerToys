// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using FancyZonesCLI.Commands;

namespace FancyZonesCLI;

internal sealed class Program
{
    private static int Main(string[] args)
    {
        // Initialize logger
        Logger.InitializeLogger();
        Logger.LogInfo($"CLI invoked with args: [{string.Join(", ", args)}]");

        // Initialize Windows messages
        NativeMethods.InitializeWindowMessages();

        (int ExitCode, string Output) result;

        if (args.Length == 0)
        {
            result = (1, GetUsageText());
        }
        else
        {
            var command = args[0].ToLowerInvariant();

            result = command switch
            {
                "open-editor" or "editor" or "e" => EditorCommands.OpenEditor(),
                "get-monitors" or "monitors" or "m" => MonitorCommands.GetMonitors(),
                "get-layouts" or "layouts" or "ls" => LayoutCommands.GetLayouts(),
                "get-active-layout" or "active" or "get-active" or "a" => LayoutCommands.GetActiveLayout(),
                "set-layout" or "set" or "s" => args.Length >= 2
                    ? LayoutCommands.SetLayout(args.Skip(1).ToArray(), NativeMethods.NotifyFancyZones, NativeMethods.WM_PRIV_APPLIED_LAYOUTS_FILE_UPDATE)
                    : (1, "Error: set-layout requires a UUID parameter"),
                "open-settings" or "settings" => EditorCommands.OpenSettings(),
                "get-hotkeys" or "hotkeys" or "hk" => HotkeyCommands.GetHotkeys(),
                "set-hotkey" or "shk" => args.Length >= 3
                    ? HotkeyCommands.SetHotkey(int.Parse(args[1], CultureInfo.InvariantCulture), args[2], NativeMethods.NotifyFancyZones, NativeMethods.WM_PRIV_LAYOUT_HOTKEYS_FILE_UPDATE)
                    : (1, "Error: set-hotkey requires <key> <uuid>"),
                "remove-hotkey" or "rhk" => args.Length >= 2
                    ? HotkeyCommands.RemoveHotkey(int.Parse(args[1], CultureInfo.InvariantCulture), NativeMethods.NotifyFancyZones, NativeMethods.WM_PRIV_LAYOUT_HOTKEYS_FILE_UPDATE)
                    : (1, "Error: remove-hotkey requires <key>"),
                "help" or "--help" or "-h" => (0, GetUsageText()),
                _ => (1, $"Error: Unknown command: {command}\n\n{GetUsageText()}"),
            };
        }

        // Log result
        if (result.ExitCode == 0)
        {
            Logger.LogInfo($"Command completed successfully");
        }
        else
        {
            Logger.LogWarning($"Command failed with exit code {result.ExitCode}: {result.Output}");
        }

        // Output result
        if (!string.IsNullOrEmpty(result.Output))
        {
            Console.WriteLine(result.Output);
        }

        return result.ExitCode;
    }

    private static string GetUsageText()
    {
        return """
            FancyZones CLI - Command line interface for FancyZones
            ======================================================

            Usage: FancyZonesCLI.exe <command> [options]

            Commands:
              open-editor (editor, e)          Launch FancyZones layout editor
              get-monitors (monitors, m)       List all monitors and their properties
              get-layouts (layouts, ls)        List all available layouts
              get-active-layout (get-active, active, a)
                                               Show currently active layout
              set-layout (set, s) <uuid> [options]
                                               Set layout by UUID
                --monitor <n>                  Apply to monitor N (1-based)
                --all                          Apply to all monitors
              open-settings (settings)         Open FancyZones settings page
              get-hotkeys (hotkeys, hk)        List all layout hotkeys
              set-hotkey (shk) <key> <uuid>    Assign hotkey (0-9) to CUSTOM layout
                                               Note: Only custom layouts work with hotkeys
              remove-hotkey (rhk) <key>        Remove hotkey assignment
              help                             Show this help message


            Examples:
              FancyZonesCLI.exe e                   # Open editor (short)
              FancyZonesCLI.exe m                   # List monitors (short)
              FancyZonesCLI.exe ls                  # List layouts (short)
              FancyZonesCLI.exe a                   # Get active layout (short)
              FancyZonesCLI.exe s focus --all       # Set layout (short)
              FancyZonesCLI.exe open-editor         # Open editor (long)
              FancyZonesCLI.exe get-monitors
              FancyZonesCLI.exe get-layouts
              FancyZonesCLI.exe set-layout {12345678-1234-1234-1234-123456789012}
              FancyZonesCLI.exe set-layout focus --monitor 2
              FancyZonesCLI.exe set-layout columns --all
              FancyZonesCLI.exe set-hotkey 3 {12345678-1234-1234-1234-123456789012}
            """;
    }
}
