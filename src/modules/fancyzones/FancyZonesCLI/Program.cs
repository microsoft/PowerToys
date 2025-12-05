// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace FancyZonesCLI;

internal sealed class Program
{
    private static int Main(string[] args)
    {
        // Initialize Windows messages
        InitializeWindowMessages();

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
                "open-editor" or "editor" or "e" => OpenEditor(),
                "get-monitors" or "monitors" or "m" => GetMonitors(),
                "get-layouts" or "layouts" or "ls" => GetLayouts(),
                "get-active-layout" or "active" or "get-active" or "a" => GetActiveLayout(),
                "set-layout" or "set" or "s" => args.Length >= 2 ? SetLayout(args.Skip(1).ToArray()) : (1, "Error: set-layout requires a UUID parameter"),
                "open-settings" or "settings" => OpenSettings(),
                "get-hotkeys" or "hotkeys" or "hk" => GetHotkeys(),
                "set-hotkey" or "shk" => args.Length >= 3 ? SetHotkey(int.Parse(args[1], CultureInfo.InvariantCulture), args[2]) : (1, "Error: set-hotkey requires <key> <uuid>"),
                "remove-hotkey" or "rhk" => args.Length >= 2 ? RemoveHotkey(int.Parse(args[1], CultureInfo.InvariantCulture)) : (1, "Error: remove-hotkey requires <key>"),
                "help" or "--help" or "-h" => (0, GetUsageText()),
                _ => (1, $"Error: Unknown command: {command}\n\n{GetUsageText()}"),
            };
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

    private static (int ExitCode, string Output) OpenEditor()
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

    private static (int ExitCode, string Output) OpenSettings()
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

    private static (int ExitCode, string Output) GetMonitors()
    {
        if (!FancyZonesData.TryReadAppliedLayouts(out var appliedLayouts, out var error))
        {
            return (1, $"Error: {error}");
        }

        if (appliedLayouts.Layouts == null || appliedLayouts.Layouts.Count == 0)
        {
            return (0, "No monitors found.");
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"=== Monitors ({appliedLayouts.Layouts.Count} total) ===");
        sb.AppendLine();

        for (int i = 0; i < appliedLayouts.Layouts.Count; i++)
        {
            var layout = appliedLayouts.Layouts[i];
            var monitorNum = i + 1;

            sb.AppendLine(CultureInfo.InvariantCulture, $"Monitor {monitorNum}:");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Monitor: {layout.Device.Monitor}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Monitor Instance: {layout.Device.MonitorInstance}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Monitor Number: {layout.Device.MonitorNumber}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Serial Number: {layout.Device.SerialNumber}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Virtual Desktop: {layout.Device.VirtualDesktop}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Sensitivity Radius: {layout.AppliedLayout.SensitivityRadius}px");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Active Layout: {layout.AppliedLayout.Type}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Zone Count: {layout.AppliedLayout.ZoneCount}");
            sb.AppendLine();
        }

        return (0, sb.ToString().TrimEnd());
    }

    private static (int ExitCode, string Output) GetLayouts()
    {
        var sb = new System.Text.StringBuilder();

        // Print template layouts
        var templatesJson = FancyZonesData.ReadLayoutTemplates();
        if (templatesJson?.Templates != null)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"=== Built-in Template Layouts ({templatesJson.Templates.Count} total) ===\n");

            for (int i = 0; i < templatesJson.Templates.Count; i++)
            {
                var template = templatesJson.Templates[i];
                sb.AppendLine(CultureInfo.InvariantCulture, $"[T{i + 1}] {template.Type}");
                sb.Append(CultureInfo.InvariantCulture, $"    Zones: {template.ZoneCount}");
                if (template.ShowSpacing && template.Spacing > 0)
                {
                    sb.Append(CultureInfo.InvariantCulture, $", Spacing: {template.Spacing}px");
                }

                sb.AppendLine();
                sb.AppendLine();

                // Draw visual preview
                sb.Append(LayoutVisualizer.DrawTemplateLayout(template));

                if (i < templatesJson.Templates.Count - 1)
                {
                    sb.AppendLine();
                }
            }

            sb.AppendLine("\n");
        }

        // Print custom layouts
        var customLayouts = FancyZonesData.ReadCustomLayouts();
        if (customLayouts?.Layouts != null)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"=== Custom Layouts ({customLayouts.Layouts.Count} total) ===");

            for (int i = 0; i < customLayouts.Layouts.Count; i++)
            {
                var layout = customLayouts.Layouts[i];
                sb.AppendLine(CultureInfo.InvariantCulture, $"[{i + 1}] {layout.Name}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"    UUID: {layout.Uuid}");
                sb.Append(CultureInfo.InvariantCulture, $"    Type: {layout.Type}");

                bool isCanvasLayout = false;
                if (layout.Info.ValueKind != JsonValueKind.Undefined && layout.Info.ValueKind != JsonValueKind.Null)
                {
                    if (layout.Type == "grid" && layout.Info.TryGetProperty("rows", out var rows) && layout.Info.TryGetProperty("columns", out var cols))
                    {
                        sb.Append(CultureInfo.InvariantCulture, $" ({rows.GetInt32()}x{cols.GetInt32()} grid)");
                    }
                    else if (layout.Type == "canvas" && layout.Info.TryGetProperty("zones", out var zones))
                    {
                        sb.Append(CultureInfo.InvariantCulture, $" ({zones.GetArrayLength()} zones)");
                        isCanvasLayout = true;
                    }
                }

                sb.AppendLine("\n");

                // Draw visual preview
                sb.Append(LayoutVisualizer.DrawCustomLayout(layout));

                // Add note for canvas layouts
                if (isCanvasLayout)
                {
                    sb.AppendLine("\n    Note: Canvas layout preview is approximate.");
                    sb.AppendLine("          Open FancyZones Editor for precise zone boundaries.");
                }

                if (i < customLayouts.Layouts.Count - 1)
                {
                    sb.AppendLine();
                }
            }

            sb.AppendLine("\nUse 'FancyZonesCLI.exe set-layout <UUID>' to apply a layout.");
        }

        return (0, sb.ToString().TrimEnd());
    }

    private static (int ExitCode, string Output) GetActiveLayout()
    {
        if (!FancyZonesData.TryReadAppliedLayouts(out var appliedLayouts, out var error))
        {
            return (1, $"Error: {error}");
        }

        if (appliedLayouts.Layouts == null || appliedLayouts.Layouts.Count == 0)
        {
            return (0, "No active layouts found.");
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("\n=== Active FancyZones Layout(s) ===\n");

        for (int i = 0; i < appliedLayouts.Layouts.Count; i++)
        {
            var layout = appliedLayouts.Layouts[i];
            sb.AppendLine(CultureInfo.InvariantCulture, $"Monitor {i + 1}:");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Name: {layout.AppliedLayout.Type}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  UUID: {layout.AppliedLayout.Uuid}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Type: {layout.AppliedLayout.Type} ({layout.AppliedLayout.ZoneCount} zones)");

            if (layout.AppliedLayout.ShowSpacing)
            {
                sb.AppendLine(CultureInfo.InvariantCulture, $"  Spacing: {layout.AppliedLayout.Spacing}px");
            }

            sb.AppendLine(CultureInfo.InvariantCulture, $"  Sensitivity Radius: {layout.AppliedLayout.SensitivityRadius}px");

            if (i < appliedLayouts.Layouts.Count - 1)
            {
                sb.AppendLine();
            }
        }

        return (0, sb.ToString().TrimEnd());
    }

    private static (int ExitCode, string Output) SetLayout(string[] args)
    {
        if (args.Length == 0)
        {
            return (1, "Error: set-layout requires a UUID parameter");
        }

        string uuid = args[0];
        int? targetMonitor = null;
        bool applyToAll = false;

        // Parse options
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--monitor" && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out int monitorNum))
                {
                    targetMonitor = monitorNum;
                    i++; // Skip next arg
                }
                else
                {
                    return (1, $"Error: Invalid monitor number: {args[i + 1]}");
                }
            }
            else if (args[i] == "--all")
            {
                applyToAll = true;
            }
        }

        if (targetMonitor.HasValue && applyToAll)
        {
            return (1, "Error: Cannot specify both --monitor and --all");
        }

        // Try to find layout in custom layouts first (by UUID)
        var customLayouts = FancyZonesData.ReadCustomLayouts();
        var targetCustomLayout = customLayouts?.Layouts?.FirstOrDefault(l => l.Uuid.Equals(uuid, StringComparison.OrdinalIgnoreCase));

        // If not found in custom layouts, try template layouts (by type name)
        TemplateLayout targetTemplate = null;
        if (targetCustomLayout == null)
        {
            var templates = FancyZonesData.ReadLayoutTemplates();
            targetTemplate = templates?.Templates?.FirstOrDefault(t => t.Type.Equals(uuid, StringComparison.OrdinalIgnoreCase));
        }

        if (targetCustomLayout == null && targetTemplate == null)
        {
            return (1, $"Error: Layout '{uuid}' not found\nTip: For templates, use the type name (e.g., 'focus', 'columns', 'rows', 'grid', 'priority-grid')\n     For custom layouts, use the UUID from 'get-layouts'");
        }

        // Read current applied layouts
        if (!FancyZonesData.TryReadAppliedLayouts(out var appliedLayouts, out var error))
        {
            return (1, $"Error: {error}");
        }

        if (appliedLayouts.Layouts == null || appliedLayouts.Layouts.Count == 0)
        {
            return (1, "Error: No monitors configured");
        }

        // Determine which monitors to update
        List<int> monitorsToUpdate = new List<int>();
        if (applyToAll)
        {
            for (int i = 0; i < appliedLayouts.Layouts.Count; i++)
            {
                monitorsToUpdate.Add(i);
            }
        }
        else if (targetMonitor.HasValue)
        {
            int monitorIndex = targetMonitor.Value - 1; // Convert to 0-based
            if (monitorIndex < 0 || monitorIndex >= appliedLayouts.Layouts.Count)
            {
                return (1, $"Error: Monitor {targetMonitor.Value} not found. Available monitors: 1-{appliedLayouts.Layouts.Count}");
            }

            monitorsToUpdate.Add(monitorIndex);
        }
        else
        {
            // Default: first monitor
            monitorsToUpdate.Add(0);
        }

        // Update selected monitors
        foreach (int monitorIndex in monitorsToUpdate)
        {
            if (targetCustomLayout != null)
            {
                appliedLayouts.Layouts[monitorIndex].AppliedLayout.Uuid = targetCustomLayout.Uuid;
                appliedLayouts.Layouts[monitorIndex].AppliedLayout.Type = targetCustomLayout.Type;
            }
            else if (targetTemplate != null)
            {
                // For templates, use all-zeros UUID and the template type
                appliedLayouts.Layouts[monitorIndex].AppliedLayout.Uuid = "{00000000-0000-0000-0000-000000000000}";
                appliedLayouts.Layouts[monitorIndex].AppliedLayout.Type = targetTemplate.Type;
                appliedLayouts.Layouts[monitorIndex].AppliedLayout.ZoneCount = targetTemplate.ZoneCount;
                appliedLayouts.Layouts[monitorIndex].AppliedLayout.ShowSpacing = targetTemplate.ShowSpacing;
                appliedLayouts.Layouts[monitorIndex].AppliedLayout.Spacing = targetTemplate.Spacing;
            }
        }

        // Write back to file
        FancyZonesData.WriteAppliedLayouts(appliedLayouts);

        // Notify FancyZones to reload
        NotifyFancyZones(wmPrivAppliedLayoutsFileUpdate);

        string layoutName = targetCustomLayout?.Name ?? targetTemplate?.Type ?? uuid;
        if (applyToAll)
        {
            return (0, $"Layout '{layoutName}' applied to all {monitorsToUpdate.Count} monitors");
        }
        else if (targetMonitor.HasValue)
        {
            return (0, $"Layout '{layoutName}' applied to monitor {targetMonitor.Value}");
        }
        else
        {
            return (0, $"Layout '{layoutName}' applied to monitor 1");
        }
    }

    private static (int ExitCode, string Output) GetHotkeys()
    {
        var hotkeys = FancyZonesData.ReadLayoutHotkeys();
        if (hotkeys?.Hotkeys == null || hotkeys.Hotkeys.Count == 0)
        {
            return (0, "No hotkeys configured.");
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Layout Hotkeys ===\n");
        sb.AppendLine("Press Win + Ctrl + Alt + <number> to switch layouts:\n");

        foreach (var hotkey in hotkeys.Hotkeys.OrderBy(h => h.Key))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  [{hotkey.Key}] → {hotkey.LayoutId}");
        }

        return (0, sb.ToString().TrimEnd());
    }

    private static (int ExitCode, string Output) SetHotkey(int key, string layoutUuid)
    {
        if (key < 0 || key > 9)
        {
            return (1, "Error: Key must be between 0 and 9");
        }

        // Check if this is a custom layout UUID
        var customLayouts = FancyZonesData.ReadCustomLayouts();
        var matchedLayout = customLayouts?.Layouts?.FirstOrDefault(l => l.Uuid.Equals(layoutUuid, StringComparison.OrdinalIgnoreCase));
        bool isCustomLayout = matchedLayout != null;
        string layoutName = matchedLayout?.Name ?? layoutUuid;

        var hotkeys = FancyZonesData.ReadLayoutHotkeys() ?? new LayoutHotkeys();

        hotkeys.Hotkeys ??= new List<LayoutHotkey>();

        // Remove existing hotkey for this key
        hotkeys.Hotkeys.RemoveAll(h => h.Key == key);

        // Add new hotkey
        hotkeys.Hotkeys.Add(new LayoutHotkey { Key = key, LayoutId = layoutUuid });

        // Save
        File.WriteAllText(FancyZonesPaths.LayoutHotkeys, JsonSerializer.Serialize(hotkeys, FancyZonesJsonContext.Default.LayoutHotkeys));

        // Notify FancyZones
        NotifyFancyZones(wmPrivLayoutHotkeysFileUpdate);

        if (isCustomLayout)
        {
            return (0, $"✓ Hotkey {key} assigned to custom layout '{layoutName}'\n  Press Win + Ctrl + Alt + {key} to switch to this layout");
        }
        else
        {
            return (0, $"⚠ Warning: Hotkey {key} assigned to '{layoutUuid}'\n  Note: FancyZones hotkeys only work with CUSTOM layouts.\n  Template layouts (focus, columns, rows, etc.) cannot be used with hotkeys.\n  Create a custom layout in the FancyZones Editor to use this hotkey.");
        }
    }

    private static (int ExitCode, string Output) RemoveHotkey(int key)
    {
        var hotkeys = FancyZonesData.ReadLayoutHotkeys();
        if (hotkeys?.Hotkeys == null)
        {
            return (0, $"No hotkey assigned to key {key}");
        }

        var removed = hotkeys.Hotkeys.RemoveAll(h => h.Key == key);
        if (removed == 0)
        {
            return (0, $"No hotkey assigned to key {key}");
        }

        // Save
        FancyZonesData.WriteLayoutHotkeys(hotkeys);

        // Notify FancyZones
        NotifyFancyZones(wmPrivLayoutHotkeysFileUpdate);

        return (0, $"Hotkey {key} removed");
    }

    // Windows Messages for notifying FancyZones
    private static uint wmPrivAppliedLayoutsFileUpdate;
    private static uint wmPrivLayoutHotkeysFileUpdate;

    private static void NotifyFancyZones(uint message)
    {
        // Broadcast message to all windows
        NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, message, IntPtr.Zero, IntPtr.Zero);
    }

    private static void InitializeWindowMessages()
    {
        wmPrivAppliedLayoutsFileUpdate = NativeMethods.RegisterWindowMessage("{2ef2c8a7-e0d5-4f31-9ede-52aade2d284d}");
        wmPrivLayoutHotkeysFileUpdate = NativeMethods.RegisterWindowMessage("{07229b7e-4f22-4357-b136-33c289be2295}");
    }
}

internal static class NativeMethods
{
    public static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern uint RegisterWindowMessage(string lpString);
}
