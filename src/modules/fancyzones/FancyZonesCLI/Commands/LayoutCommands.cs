// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace FancyZonesCLI.Commands;

/// <summary>
/// Layout-related commands.
/// </summary>
internal static class LayoutCommands
{
    public static (int ExitCode, string Output) GetLayouts()
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

    public static (int ExitCode, string Output) GetActiveLayout()
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

    public static (int ExitCode, string Output) SetLayout(string[] args, Action<uint> notifyFancyZones, uint wmPrivAppliedLayoutsFileUpdate)
    {
        Logger.LogInfo($"SetLayout called with args: [{string.Join(", ", args)}]");

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
        Logger.LogInfo($"Applied layouts file updated for {monitorsToUpdate.Count} monitor(s)");

        // Notify FancyZones to reload
        notifyFancyZones(wmPrivAppliedLayoutsFileUpdate);
        Logger.LogInfo("FancyZones notified of layout change");

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
}
