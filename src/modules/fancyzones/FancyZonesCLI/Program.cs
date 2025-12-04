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
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static int Main(string[] args)
    {
        // Initialize Windows messages
        InitializeWindowMessages();

        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        var command = args[0].ToLowerInvariant();

        return command switch
        {
            "open-editor" or "editor" or "e" => OpenEditor(),
            "get-monitors" or "monitors" or "m" => GetMonitors(),
            "get-layouts" or "layouts" or "ls" => GetLayouts(),
            "get-active-layout" or "active" or "get-active" or "a" => GetActiveLayout(),
            "set-layout" or "set" or "s" => args.Length >= 2 ? SetLayout(args.Skip(1).ToArray()) : PrintErrorAndReturn("Error: set-layout requires a UUID parameter"),
            "open-settings" or "settings" => OpenSettings(),
            "get-hotkeys" or "hotkeys" or "hk" => GetHotkeys(),
            "set-hotkey" or "shk" => args.Length >= 3 ? SetHotkey(int.Parse(args[1], CultureInfo.InvariantCulture), args[2]) : PrintErrorAndReturn("Error: set-hotkey requires <key> <uuid>"),
            "remove-hotkey" or "rhk" => args.Length >= 2 ? RemoveHotkey(int.Parse(args[1], CultureInfo.InvariantCulture)) : PrintErrorAndReturn("Error: remove-hotkey requires <key>"),
            "help" or "--help" or "-h" => PrintUsageAndReturn(),
            _ => PrintUnknownCommandAndReturn(command),
        };
    }

    private static int PrintErrorAndReturn(string message)
    {
        Console.WriteLine(message);
        return 1;
    }

    private static int PrintUsageAndReturn()
    {
        PrintUsage();
        return 0;
    }

    private static int PrintUnknownCommandAndReturn(string command)
    {
        Console.WriteLine($"Error: Unknown command: {command}\n");
        PrintUsage();
        return 1;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("FancyZones CLI - Command line interface for FancyZones");
        Console.WriteLine("======================================================");
        Console.WriteLine();
        Console.WriteLine("Usage: FancyZonesCLI.exe <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  open-editor (editor, e)          Launch FancyZones layout editor");
        Console.WriteLine("  get-monitors (monitors, m)       List all monitors and their properties");
        Console.WriteLine("  get-layouts (layouts, ls)        List all available layouts");
        Console.WriteLine("  get-active-layout (active, a)    Show currently active layout");
        Console.WriteLine("  set-layout (set, s) <uuid> [options]");
        Console.WriteLine("                                   Set layout by UUID");
        Console.WriteLine("    --monitor <n>                  Apply to monitor N (1-based)");
        Console.WriteLine("    --all                          Apply to all monitors");
        Console.WriteLine("  open-settings (settings)         Open FancyZones settings page");
        Console.WriteLine("  get-hotkeys (hotkeys, hk)        List all layout hotkeys");
        Console.WriteLine("  set-hotkey (shk) <key> <uuid>    Assign hotkey (0-9) to CUSTOM layout");
        Console.WriteLine("                                   Note: Only custom layouts work with hotkeys");
        Console.WriteLine("  remove-hotkey (rhk) <key>        Remove hotkey assignment");
        Console.WriteLine("  help                             Show this help message");
        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  FancyZonesCLI.exe e                   # Open editor (short)");
        Console.WriteLine("  FancyZonesCLI.exe m                   # List monitors (short)");
        Console.WriteLine("  FancyZonesCLI.exe ls                  # List layouts (short)");
        Console.WriteLine("  FancyZonesCLI.exe a                   # Get active layout (short)");
        Console.WriteLine("  FancyZonesCLI.exe s focus --all       # Set layout (short)");
        Console.WriteLine("  FancyZonesCLI.exe open-editor         # Open editor (long)");
        Console.WriteLine("  FancyZonesCLI.exe get-monitors");
        Console.WriteLine("  FancyZonesCLI.exe get-layouts");
        Console.WriteLine("  FancyZonesCLI.exe set-layout {12345678-1234-1234-1234-123456789012}");
        Console.WriteLine("  FancyZonesCLI.exe set-layout focus --monitor 2");
        Console.WriteLine("  FancyZonesCLI.exe set-layout columns --all");
        Console.WriteLine("  FancyZonesCLI.exe set-hotkey 3 {12345678-1234-1234-1234-123456789012}");
    }

    private static int OpenEditor()
    {
        var editorExe = "PowerToys.FancyZonesEditor.exe";

        // Check if editor-parameters.json exists
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var editorParamsPath = Path.Combine(localAppData, "Microsoft", "PowerToys", "FancyZones", "editor-parameters.json");

        if (!File.Exists(editorParamsPath))
        {
            Console.WriteLine("Error: editor-parameters.json not found.");
            Console.WriteLine("Please launch FancyZones Editor using Win+` (Win+Backtick) hotkey first.");
            return 1;
        }

        // Check if editor is already running
        var existingProcess = Process.GetProcessesByName("PowerToys.FancyZonesEditor").FirstOrDefault();
        if (existingProcess != null)
        {
            NativeMethods.SetForegroundWindow(existingProcess.MainWindowHandle);
            Console.WriteLine("FancyZones Editor is already running. Brought window to foreground.");
            return 0;
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
                Console.WriteLine("FancyZones Editor launched successfully.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to launch: {ex.Message}");
                return 1;
            }
        }

        Console.WriteLine($"Error: Could not find {editorExe} in {AppContext.BaseDirectory}");
        return 1;
    }

    private static int OpenSettings()
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
                Console.WriteLine("Error: PowerToys.exe not found. Please ensure PowerToys is installed.");
                return 1;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = powertoysExe,
                Arguments = "--open-settings=FancyZones",
                UseShellExecute = false,
            });
            Console.WriteLine("FancyZones Settings opened successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: Failed to open FancyZones Settings. {ex.Message}");
            return 1;
        }
    }

    private static int GetMonitors()
    {
        var dataPath = GetFancyZonesDataPath();
        var appliedLayoutsPath = Path.Combine(dataPath, "applied-layouts.json");

        if (!File.Exists(appliedLayoutsPath))
        {
            Console.WriteLine("Error: applied-layouts.json not found.");
            return 1;
        }

        try
        {
            var json = File.ReadAllText(appliedLayoutsPath);
            var appliedLayouts = JsonSerializer.Deserialize<AppliedLayouts>(json, FancyZonesJsonContext.Default.AppliedLayouts);

            if (appliedLayouts?.Layouts == null || appliedLayouts.Layouts.Count == 0)
            {
                Console.WriteLine("No monitors found.");
                return 0;
            }

            Console.WriteLine($"=== Monitors ({appliedLayouts.Layouts.Count} total) ===");
            Console.WriteLine();

            for (int i = 0; i < appliedLayouts.Layouts.Count; i++)
            {
                var layout = appliedLayouts.Layouts[i];
                var monitorNum = i + 1;

                Console.WriteLine($"Monitor {monitorNum}:");
                Console.WriteLine($"  Monitor: {layout.Device.Monitor}");
                Console.WriteLine($"  Monitor Instance: {layout.Device.MonitorInstance}");
                Console.WriteLine($"  Monitor Number: {layout.Device.MonitorNumber}");
                Console.WriteLine($"  Serial Number: {layout.Device.SerialNumber}");
                Console.WriteLine($"  Virtual Desktop: {layout.Device.VirtualDesktop}");
                Console.WriteLine($"  Sensitivity Radius: {layout.AppliedLayout.SensitivityRadius}px");
                Console.WriteLine($"  Active Layout: {layout.AppliedLayout.Type}");
                Console.WriteLine($"  Zone Count: {layout.AppliedLayout.ZoneCount}");
                Console.WriteLine();
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: Failed to read monitor information. {ex.Message}");
            return 1;
        }
    }

    private static int GetLayouts()
    {
        var dataPath = GetFancyZonesDataPath();
        var templatesPath = Path.Combine(dataPath, "layout-templates.json");
        var customLayoutsPath = Path.Combine(dataPath, "custom-layouts.json");

        // Print template layouts
        if (File.Exists(templatesPath))
        {
            try
            {
                var templatesJson = JsonSerializer.Deserialize(File.ReadAllText(templatesPath), FancyZonesJsonContext.Default.LayoutTemplates);
                if (templatesJson?.Templates != null)
                {
                    Console.WriteLine($"=== Built-in Template Layouts ({templatesJson.Templates.Count} total) ===\n");

                    for (int i = 0; i < templatesJson.Templates.Count; i++)
                    {
                        var template = templatesJson.Templates[i];
                        Console.WriteLine($"[T{i + 1}] {template.Type}");
                        Console.WriteLine($"    Zones: {template.ZoneCount}");
                        if (template.ShowSpacing && template.Spacing > 0)
                        {
                            Console.WriteLine($", Spacing: {template.Spacing}px");
                        }

                        Console.WriteLine();

                        // Draw visual preview
                        LayoutVisualizer.DrawTemplateLayout(template);

                        if (i < templatesJson.Templates.Count - 1)
                        {
                            Console.WriteLine();
                        }
                    }

                    Console.WriteLine("\n");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing templates: {ex.Message}");
            }
        }

        // Print custom layouts
        if (File.Exists(customLayoutsPath))
        {
            try
            {
                var customLayouts = JsonSerializer.Deserialize(File.ReadAllText(customLayoutsPath), FancyZonesJsonContext.Default.CustomLayouts);
                if (customLayouts?.Layouts != null)
                {
                    Console.WriteLine($"=== Custom Layouts ({customLayouts.Layouts.Count} total) ===");

                    for (int i = 0; i < customLayouts.Layouts.Count; i++)
                    {
                        var layout = customLayouts.Layouts[i];
                        Console.WriteLine($"[{i + 1}] {layout.Name}");
                        Console.WriteLine($"    UUID: {layout.Uuid}");
                        Console.Write($"    Type: {layout.Type}");

                        bool isCanvasLayout = false;
                        if (layout.Info.ValueKind != JsonValueKind.Undefined && layout.Info.ValueKind != JsonValueKind.Null)
                        {
                            if (layout.Type == "grid" && layout.Info.TryGetProperty("rows", out var rows) && layout.Info.TryGetProperty("columns", out var cols))
                            {
                                Console.Write($" ({rows.GetInt32()}x{cols.GetInt32()} grid)");
                            }
                            else if (layout.Type == "canvas" && layout.Info.TryGetProperty("zones", out var zones))
                            {
                                Console.Write($" ({zones.GetArrayLength()} zones)");
                                isCanvasLayout = true;
                            }
                        }

                        Console.WriteLine("\n");

                        // Draw visual preview
                        LayoutVisualizer.DrawCustomLayout(layout);

                        // Add note for canvas layouts
                        if (isCanvasLayout)
                        {
                            Console.WriteLine("\n    Note: Canvas layout preview is approximate.");
                            Console.WriteLine("          Open FancyZones Editor for precise zone boundaries.");
                        }

                        if (i < customLayouts.Layouts.Count - 1)
                        {
                            Console.WriteLine();
                        }
                    }

                    Console.WriteLine("\nUse 'FancyZonesCLI.exe set-layout <UUID>' to apply a layout.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing custom layouts: {ex.Message}");
            }
        }

        return 0;
    }

    private static int GetActiveLayout()
    {
        var dataPath = GetFancyZonesDataPath();
        var appliedLayoutsPath = Path.Combine(dataPath, "applied-layouts.json");

        if (!File.Exists(appliedLayoutsPath))
        {
            Console.WriteLine($"Error: Could not find applied-layouts.json");
            return 1;
        }

        try
        {
            var appliedLayouts = JsonSerializer.Deserialize(File.ReadAllText(appliedLayoutsPath), FancyZonesJsonContext.Default.AppliedLayouts);
            if (appliedLayouts?.Layouts == null || appliedLayouts.Layouts.Count == 0)
            {
                Console.WriteLine("No active layouts found.");
                return 0;
            }

            Console.WriteLine("\n=== Active FancyZones Layout(s) ===\n");

            for (int i = 0; i < appliedLayouts.Layouts.Count; i++)
            {
                var layout = appliedLayouts.Layouts[i];
                Console.WriteLine($"Monitor {i + 1}:");
                Console.WriteLine($"  Name: {layout.AppliedLayout.Type}");
                Console.WriteLine($"  UUID: {layout.AppliedLayout.Uuid}");
                Console.WriteLine($"  Type: {layout.AppliedLayout.Type} ({layout.AppliedLayout.ZoneCount} zones)");

                if (layout.AppliedLayout.ShowSpacing)
                {
                    Console.WriteLine($"  Spacing: {layout.AppliedLayout.Spacing}px");
                }

                Console.WriteLine($"  Sensitivity Radius: {layout.AppliedLayout.SensitivityRadius}px");

                if (i < appliedLayouts.Layouts.Count - 1)
                {
                    Console.WriteLine();
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int SetLayout(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Error: set-layout requires a UUID parameter");
            return 1;
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
                    Console.WriteLine($"Error: Invalid monitor number: {args[i + 1]}");
                    return 1;
                }
            }
            else if (args[i] == "--all")
            {
                applyToAll = true;
            }
        }

        if (targetMonitor.HasValue && applyToAll)
        {
            Console.WriteLine("Error: Cannot specify both --monitor and --all");
            return 1;
        }

        var dataPath = GetFancyZonesDataPath();
        var appliedLayoutsPath = Path.Combine(dataPath, "applied-layouts.json");
        var customLayoutsPath = Path.Combine(dataPath, "custom-layouts.json");
        var templatesPath = Path.Combine(dataPath, "layout-templates.json");

        if (!File.Exists(appliedLayoutsPath))
        {
            Console.WriteLine("Error: applied-layouts.json not found");
            return 1;
        }

        try
        {
            // Try to find layout in custom layouts first (by UUID)
            CustomLayout targetCustomLayout = null;
            TemplateLayout targetTemplate = null;

            if (File.Exists(customLayoutsPath))
            {
                var customLayouts = JsonSerializer.Deserialize(File.ReadAllText(customLayoutsPath), FancyZonesJsonContext.Default.CustomLayouts);
                targetCustomLayout = customLayouts?.Layouts?.FirstOrDefault(l => l.Uuid.Equals(uuid, StringComparison.OrdinalIgnoreCase));
            }

            // If not found in custom layouts, try template layouts (by type name or UUID)
            if (targetCustomLayout == null && File.Exists(templatesPath))
            {
                var templates = JsonSerializer.Deserialize(File.ReadAllText(templatesPath), FancyZonesJsonContext.Default.LayoutTemplates);

                // Try matching by type name (case-insensitive)
                targetTemplate = templates?.Templates?.FirstOrDefault(t => t.Type.Equals(uuid, StringComparison.OrdinalIgnoreCase));
            }

            if (targetCustomLayout == null && targetTemplate == null)
            {
                Console.WriteLine($"Error: Layout '{uuid}' not found");
                Console.WriteLine("Tip: For templates, use the type name (e.g., 'focus', 'columns', 'rows', 'grid', 'priority-grid')");
                Console.WriteLine("     For custom layouts, use the UUID from 'get-layouts'");
                return 1;
            }

            // Read current applied layouts
            var appliedLayouts = JsonSerializer.Deserialize(File.ReadAllText(appliedLayoutsPath), FancyZonesJsonContext.Default.AppliedLayouts);
            if (appliedLayouts?.Layouts == null || appliedLayouts.Layouts.Count == 0)
            {
                Console.WriteLine("Error: No monitors configured");
                return 1;
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
                    Console.WriteLine($"Error: Monitor {targetMonitor.Value} not found. Available monitors: 1-{appliedLayouts.Layouts.Count}");
                    return 1;
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
            File.WriteAllText(appliedLayoutsPath, JsonSerializer.Serialize(appliedLayouts, FancyZonesJsonContext.Default.AppliedLayouts));

            // Notify FancyZones to reload
            NotifyFancyZones(wmPrivAppliedLayoutsFileUpdate);

            string layoutName = targetCustomLayout?.Name ?? targetTemplate?.Type ?? uuid;
            if (applyToAll)
            {
                Console.WriteLine($"Layout '{layoutName}' applied to all {monitorsToUpdate.Count} monitors");
            }
            else if (targetMonitor.HasValue)
            {
                Console.WriteLine($"Layout '{layoutName}' applied to monitor {targetMonitor.Value}");
            }
            else
            {
                Console.WriteLine($"Layout '{layoutName}' applied to monitor 1");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int GetHotkeys()
    {
        var dataPath = GetFancyZonesDataPath();
        var hotkeysPath = Path.Combine(dataPath, "layout-hotkeys.json");

        if (!File.Exists(hotkeysPath))
        {
            Console.WriteLine("No hotkeys configured.");
            return 0;
        }

        try
        {
            var hotkeys = JsonSerializer.Deserialize(File.ReadAllText(hotkeysPath), FancyZonesJsonContext.Default.LayoutHotkeys);
            if (hotkeys?.Hotkeys == null || hotkeys.Hotkeys.Count == 0)
            {
                Console.WriteLine("No hotkeys configured.");
                return 0;
            }

            Console.WriteLine("=== Layout Hotkeys ===\n");
            Console.WriteLine("Press Win + Ctrl + Alt + <number> to switch layouts:\n");

            foreach (var hotkey in hotkeys.Hotkeys.OrderBy(h => h.Key))
            {
                Console.WriteLine($"  [{hotkey.Key}] → {hotkey.LayoutId}");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int SetHotkey(int key, string layoutUuid)
    {
        if (key < 0 || key > 9)
        {
            Console.WriteLine("Error: Key must be between 0 and 9");
            return 1;
        }

        // Check if this is a custom layout UUID
        var dataPath = GetFancyZonesDataPath();
        var customLayoutsPath = Path.Combine(dataPath, "custom-layouts.json");
        bool isCustomLayout = false;
        string layoutName = layoutUuid;

        if (File.Exists(customLayoutsPath))
        {
            try
            {
                var customLayoutsJson = File.ReadAllText(customLayoutsPath);
                var customLayouts = JsonSerializer.Deserialize<CustomLayouts>(customLayoutsJson, FancyZonesJsonContext.Default.CustomLayouts);
                var layout = customLayouts?.Layouts?.FirstOrDefault(l => l.Uuid.Equals(layoutUuid, StringComparison.OrdinalIgnoreCase));
                if (layout != null)
                {
                    isCustomLayout = true;
                    layoutName = layout.Name;
                }
            }
            catch
            {
                // Ignore parse errors
            }
        }

        var hotkeysPath = Path.Combine(dataPath, "layout-hotkeys.json");

        try
        {
            LayoutHotkeys hotkeys;
            if (File.Exists(hotkeysPath))
            {
                hotkeys = JsonSerializer.Deserialize(File.ReadAllText(hotkeysPath), FancyZonesJsonContext.Default.LayoutHotkeys) ?? new LayoutHotkeys();
            }
            else
            {
                hotkeys = new LayoutHotkeys();
            }

            hotkeys.Hotkeys ??= new List<LayoutHotkey>();

            // Remove existing hotkey for this key
            hotkeys.Hotkeys.RemoveAll(h => h.Key == key);

            // Add new hotkey
            hotkeys.Hotkeys.Add(new LayoutHotkey { Key = key, LayoutId = layoutUuid });

            // Save
            File.WriteAllText(hotkeysPath, JsonSerializer.Serialize(hotkeys, FancyZonesJsonContext.Default.LayoutHotkeys));

            // Notify FancyZones
            NotifyFancyZones(wmPrivLayoutHotkeysFileUpdate);

            if (isCustomLayout)
            {
                Console.WriteLine($"✓ Hotkey {key} assigned to custom layout '{layoutName}'");
                Console.WriteLine($"  Press Win + Ctrl + Alt + {key} to switch to this layout");
            }
            else
            {
                Console.WriteLine($"⚠ Warning: Hotkey {key} assigned to '{layoutUuid}'");
                Console.WriteLine($"  Note: FancyZones hotkeys only work with CUSTOM layouts.");
                Console.WriteLine($"  Template layouts (focus, columns, rows, etc.) cannot be used with hotkeys.");
                Console.WriteLine($"  Create a custom layout in the FancyZones Editor to use this hotkey.");
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int RemoveHotkey(int key)
    {
        var dataPath = GetFancyZonesDataPath();
        var hotkeysPath = Path.Combine(dataPath, "layout-hotkeys.json");

        if (!File.Exists(hotkeysPath))
        {
            Console.WriteLine($"No hotkey assigned to key {key}");
            return 0;
        }

        try
        {
            var hotkeys = JsonSerializer.Deserialize(File.ReadAllText(hotkeysPath), FancyZonesJsonContext.Default.LayoutHotkeys);
            if (hotkeys?.Hotkeys == null)
            {
                Console.WriteLine($"No hotkey assigned to key {key}");
                return 0;
            }

            var removed = hotkeys.Hotkeys.RemoveAll(h => h.Key == key);
            if (removed == 0)
            {
                Console.WriteLine($"No hotkey assigned to key {key}");
                return 0;
            }

            // Save
            File.WriteAllText(hotkeysPath, JsonSerializer.Serialize(hotkeys, FancyZonesJsonContext.Default.LayoutHotkeys));

            // Notify FancyZones
            NotifyFancyZones(wmPrivLayoutHotkeysFileUpdate);

            Console.WriteLine($"Hotkey {key} removed");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static string GetFancyZonesDataPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "Microsoft", "PowerToys", "FancyZones");
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
