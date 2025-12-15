// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PowerToysExtension.Helpers;

internal static class FancyZonesDataService
{
    private const string ZeroUuid = "{00000000-0000-0000-0000-000000000000}";

    public static bool TryGetMonitors(out IReadOnlyList<FancyZonesMonitorDescriptor> monitors, out string error)
    {
        monitors = Array.Empty<FancyZonesMonitorDescriptor>();
        error = string.Empty;

        FancyZonesEditorParametersFile? editorParams;
        try
        {
            if (!File.Exists(FancyZonesPaths.EditorParameters))
            {
                error = "FancyZones monitor data not found. Open FancyZones Editor once to initialize.";
                return false;
            }

            var json = File.ReadAllText(FancyZonesPaths.EditorParameters);
            editorParams = JsonSerializer.Deserialize(json, FancyZonesJsonContext.Default.FancyZonesEditorParametersFile);
        }
        catch (Exception ex)
        {
            error = $"Failed to read FancyZones monitor data: {ex.Message}";
            return false;
        }

        var editorMonitors = editorParams?.Monitors;
        if (editorMonitors is null || editorMonitors.Count == 0)
        {
            error = "No FancyZones monitors found.";
            return false;
        }

        var currentVirtualDesktop = FancyZonesVirtualDesktop.GetCurrentVirtualDesktopIdString();
        foreach (var monitor in editorMonitors)
        {
            monitor.VirtualDesktop = currentVirtualDesktop;
        }

        monitors = editorMonitors
            .Select((monitor, i) => new FancyZonesMonitorDescriptor(i + 1, monitor))
            .ToArray();
        return true;
    }

    public static IReadOnlyList<FancyZonesLayoutDescriptor> GetLayouts()
    {
        var layouts = new List<FancyZonesLayoutDescriptor>();
        layouts.AddRange(GetTemplateLayouts());
        layouts.AddRange(GetCustomLayouts());
        return layouts;
    }

    public static bool TryGetAppliedLayoutForMonitor(FancyZonesEditorMonitor monitor, out FancyZonesAppliedLayout? appliedLayout)
    {
        appliedLayout = null;

        if (!TryReadAppliedLayouts(out var file))
        {
            return false;
        }

        var match = FindAppliedLayoutEntry(file, monitor);
        appliedLayout = match?.AppliedLayout;
        return appliedLayout is not null;
    }

    public static (bool Success, string Message) ApplyLayoutToAllMonitors(FancyZonesLayoutDescriptor layout)
    {
        if (!TryGetMonitors(out var monitors, out var error))
        {
            return (false, error);
        }

        return ApplyLayoutToMonitors(layout, monitors.Select(m => m.Data));
    }

    public static (bool Success, string Message) ApplyLayoutToMonitorIndex(FancyZonesLayoutDescriptor layout, int monitorIndex)
    {
        if (!TryGetMonitors(out var monitors, out var error))
        {
            return (false, error);
        }

        if (monitorIndex < 1 || monitorIndex > monitors.Count)
        {
            return (false, $"Monitor {monitorIndex} not found.");
        }

        return ApplyLayoutToMonitors(layout, [monitors[monitorIndex - 1].Data]);
    }

    private static (bool Success, string Message) ApplyLayoutToMonitors(FancyZonesLayoutDescriptor layout, IEnumerable<FancyZonesEditorMonitor> monitors)
    {
        if (!TryReadAppliedLayouts(out var appliedFile) || appliedFile is null)
        {
            appliedFile = new FancyZonesAppliedLayoutsFile { AppliedLayouts = new List<FancyZonesAppliedLayoutEntry>() };
        }

        appliedFile.AppliedLayouts ??= new List<FancyZonesAppliedLayoutEntry>();

        foreach (var monitor in monitors)
        {
            var entry = FindAppliedLayoutEntry(appliedFile, monitor);
            if (entry is null)
            {
                entry = new FancyZonesAppliedLayoutEntry
                {
                    Device = new FancyZonesAppliedDevice(),
                    AppliedLayout = new FancyZonesAppliedLayout(),
                };

                appliedFile.AppliedLayouts.Add(entry);
            }

            entry.Device.Monitor = monitor.Monitor;
            entry.Device.MonitorInstance = monitor.MonitorInstanceId ?? string.Empty;
            entry.Device.SerialNumber = monitor.MonitorSerialNumber ?? string.Empty;
            entry.Device.MonitorNumber = monitor.MonitorNumber;
            entry.Device.VirtualDesktop = monitor.VirtualDesktop ?? string.Empty;

            entry.AppliedLayout.Uuid = layout.ApplyLayout.Uuid;
            entry.AppliedLayout.Type = layout.ApplyLayout.Type;
            entry.AppliedLayout.ZoneCount = layout.ApplyLayout.ZoneCount;
            entry.AppliedLayout.ShowSpacing = layout.ApplyLayout.ShowSpacing;
            entry.AppliedLayout.Spacing = layout.ApplyLayout.Spacing;
            entry.AppliedLayout.SensitivityRadius = layout.ApplyLayout.SensitivityRadius;
        }

        try
        {
            var json = JsonSerializer.Serialize(appliedFile, FancyZonesJsonContext.Default.FancyZonesAppliedLayoutsFile);
            var directory = Path.GetDirectoryName(FancyZonesPaths.AppliedLayouts);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return (false, "Failed to write applied layouts: invalid applied-layouts.json path.");
            }

            Directory.CreateDirectory(directory);
            File.WriteAllText(FancyZonesPaths.AppliedLayouts, json);
        }
        catch (Exception ex)
        {
            return (false, $"Failed to write applied layouts: {ex.Message}");
        }

        try
        {
            FancyZonesNotifier.NotifyAppliedLayoutsChanged();
        }
        catch (Exception ex)
        {
            return (true, $"Layout applied, but FancyZones could not be notified: {ex.Message}");
        }

        return (true, "Layout applied.");
    }

    private static FancyZonesAppliedLayoutEntry? FindAppliedLayoutEntry(FancyZonesAppliedLayoutsFile? file, FancyZonesEditorMonitor monitor)
    {
        if (file?.AppliedLayouts is null)
        {
            return null;
        }

        return file.AppliedLayouts.FirstOrDefault(e =>
            string.Equals(e.Device.Monitor, monitor.Monitor, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.Device.MonitorInstance ?? string.Empty, monitor.MonitorInstanceId ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.Device.SerialNumber ?? string.Empty, monitor.MonitorSerialNumber ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
            e.Device.MonitorNumber == monitor.MonitorNumber &&
            string.Equals(e.Device.VirtualDesktop, monitor.VirtualDesktop, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryReadAppliedLayouts(out FancyZonesAppliedLayoutsFile? file)
    {
        file = null;
        try
        {
            if (!File.Exists(FancyZonesPaths.AppliedLayouts))
            {
                return false;
            }

            var json = File.ReadAllText(FancyZonesPaths.AppliedLayouts);
            file = JsonSerializer.Deserialize(json, FancyZonesJsonContext.Default.FancyZonesAppliedLayoutsFile);
            return file is not null;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<FancyZonesLayoutDescriptor> GetTemplateLayouts()
    {
        FancyZonesLayoutTemplatesFile? templates;
        try
        {
            if (!File.Exists(FancyZonesPaths.LayoutTemplates))
            {
                yield break;
            }

            var json = File.ReadAllText(FancyZonesPaths.LayoutTemplates);
            templates = JsonSerializer.Deserialize(json, FancyZonesJsonContext.Default.FancyZonesLayoutTemplatesFile);
        }
        catch
        {
            yield break;
        }

        var templateLayouts = templates?.LayoutTemplates;
        if (templateLayouts is null)
        {
            yield break;
        }

        foreach (var template in templateLayouts)
        {
            if (string.IsNullOrWhiteSpace(template.Type))
            {
                continue;
            }

            var type = template.Type.Trim();
            var zoneCount = type.Equals("blank", StringComparison.OrdinalIgnoreCase)
                ? 0
                : template.ZoneCount > 0 ? template.ZoneCount : 3;
            var title = $"Template: {type}";
            var subtitle = $"{zoneCount} zones";

            yield return new FancyZonesLayoutDescriptor
            {
                Id = $"template:{type.ToLowerInvariant()}",
                Source = FancyZonesLayoutSource.Template,
                Title = title,
                Subtitle = subtitle,
                Template = template,
                ApplyLayout = new FancyZonesAppliedLayout
                {
                    Type = type.ToLowerInvariant(),
                    Uuid = ZeroUuid,
                    ZoneCount = zoneCount,
                    ShowSpacing = template.ShowSpacing,
                    Spacing = template.Spacing,
                    SensitivityRadius = template.SensitivityRadius,
                },
            };
        }
    }

    private static IEnumerable<FancyZonesLayoutDescriptor> GetCustomLayouts()
    {
        FancyZonesCustomLayoutsFile? customLayouts;
        try
        {
            if (!File.Exists(FancyZonesPaths.CustomLayouts))
            {
                yield break;
            }

            var json = File.ReadAllText(FancyZonesPaths.CustomLayouts);
            customLayouts = JsonSerializer.Deserialize(json, FancyZonesJsonContext.Default.FancyZonesCustomLayoutsFile);
        }
        catch
        {
            yield break;
        }

        var layouts = customLayouts?.CustomLayouts;
        if (layouts is null)
        {
            yield break;
        }

        foreach (var custom in layouts)
        {
            if (string.IsNullOrWhiteSpace(custom.Uuid) || string.IsNullOrWhiteSpace(custom.Name))
            {
                continue;
            }

            var uuid = custom.Uuid.Trim();
            var customType = custom.Type?.Trim().ToLowerInvariant() ?? string.Empty;

            if (!TryBuildAppliedLayoutForCustom(custom, out var applied))
            {
                continue;
            }

            var title = custom.Name.Trim();
            var subtitle = customType switch
            {
                "grid" => $"Custom grid  {applied.ZoneCount} zones",
                "canvas" => $"Custom canvas  {applied.ZoneCount} zones",
                _ => $"Custom  {applied.ZoneCount} zones",
            };

            yield return new FancyZonesLayoutDescriptor
            {
                Id = $"custom:{uuid}",
                Source = FancyZonesLayoutSource.Custom,
                Title = title,
                Subtitle = subtitle,
                Custom = custom,
                ApplyLayout = applied,
            };
        }
    }

    private static bool TryBuildAppliedLayoutForCustom(FancyZonesCustomLayout custom, out FancyZonesAppliedLayout applied)
    {
        applied = new FancyZonesAppliedLayout
        {
            Type = "custom",
            Uuid = custom.Uuid.Trim(),
            ShowSpacing = false,
            Spacing = 0,
            ZoneCount = 0,
            SensitivityRadius = 20,
        };

        if (custom.Info.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return false;
        }

        var customType = custom.Type?.Trim().ToLowerInvariant() ?? string.Empty;
        if (customType == "grid")
        {
            if (!TryParseCustomGridInfo(custom.Info, out var zoneCount, out var showSpacing, out var spacing, out var sensitivity))
            {
                return false;
            }

            applied.ZoneCount = zoneCount;
            applied.ShowSpacing = showSpacing;
            applied.Spacing = spacing;
            applied.SensitivityRadius = sensitivity;
            return true;
        }

        if (customType == "canvas")
        {
            if (!TryParseCustomCanvasInfo(custom.Info, out var zoneCount, out var sensitivity))
            {
                return false;
            }

            applied.ZoneCount = zoneCount;
            applied.SensitivityRadius = sensitivity;
            applied.ShowSpacing = false;
            applied.Spacing = 0;
            return true;
        }

        return false;
    }

    internal static bool TryParseCustomGridInfo(JsonElement info, out int zoneCount, out bool showSpacing, out int spacing, out int sensitivityRadius)
    {
        zoneCount = 0;
        showSpacing = false;
        spacing = 0;
        sensitivityRadius = 20;

        if (!info.TryGetProperty("rows", out var _) ||
            !info.TryGetProperty("columns", out var _))
        {
            return false;
        }

        if (!info.TryGetProperty("cell-child-map", out var cellMap) || cellMap.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        int max = -1;
        foreach (var row in cellMap.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var cell in row.EnumerateArray())
            {
                if (cell.ValueKind == JsonValueKind.Number && cell.TryGetInt32(out var value))
                {
                    max = Math.Max(max, value);
                }
            }
        }

        zoneCount = max + 1;

        if (info.TryGetProperty("show-spacing", out var showSpacingProp) &&
            (showSpacingProp.ValueKind == JsonValueKind.True || showSpacingProp.ValueKind == JsonValueKind.False))
        {
            showSpacing = showSpacingProp.GetBoolean();
        }

        if (info.TryGetProperty("spacing", out var spacingProp) && spacingProp.ValueKind == JsonValueKind.Number)
        {
            spacing = spacingProp.GetInt32();
        }

        if (info.TryGetProperty("sensitivity-radius", out var sensitivityProp) && sensitivityProp.ValueKind == JsonValueKind.Number)
        {
            sensitivityRadius = sensitivityProp.GetInt32();
        }

        return zoneCount > 0;
    }

    internal static bool TryParseCustomCanvasInfo(JsonElement info, out int zoneCount, out int sensitivityRadius)
    {
        zoneCount = 0;
        sensitivityRadius = 20;

        if (!info.TryGetProperty("zones", out var zones) || zones.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        zoneCount = zones.GetArrayLength();

        if (info.TryGetProperty("sensitivity-radius", out var sensitivityProp) && sensitivityProp.ValueKind == JsonValueKind.Number)
        {
            sensitivityRadius = sensitivityProp.GetInt32();
        }

        return zoneCount >= 0;
    }
}
