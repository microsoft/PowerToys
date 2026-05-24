// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

using FancyZonesEditorCommon.Data;
using FancyZonesEditorCommon.Utils;
using ManagedCommon;
using PowerToysExtension.Properties;

using FZPaths = FancyZonesEditorCommon.Data.FancyZonesPaths;

namespace PowerToysExtension.Helpers;

internal static class FancyZonesDataService
{
    private const string ZeroUuid = "{00000000-0000-0000-0000-000000000000}";

    private static readonly CompositeFormat ReadMonitorDataFailedFormat = CompositeFormat.Parse(Resources.FancyZones_ReadMonitorDataFailed_Format);
    private static readonly CompositeFormat WriteAppliedLayoutsFailedFormat = CompositeFormat.Parse(Resources.FancyZones_WriteAppliedLayoutsFailed_Format);
    private static readonly CompositeFormat LayoutAppliedNotifyFailedFormat = CompositeFormat.Parse(Resources.FancyZones_LayoutAppliedNotifyFailed_Format);
    private static readonly CompositeFormat TemplateFormat = CompositeFormat.Parse(Resources.FancyZones_Template_Format);
    private static readonly CompositeFormat ZonesFormat = CompositeFormat.Parse(Resources.FancyZones_Zones_Format);
    private static readonly CompositeFormat CustomGridZonesFormat = CompositeFormat.Parse(Resources.FancyZones_CustomGrid_Zones_Format);
    private static readonly CompositeFormat CustomCanvasZonesFormat = CompositeFormat.Parse(Resources.FancyZones_CustomCanvas_Zones_Format);
    private static readonly CompositeFormat CustomZonesFormat = CompositeFormat.Parse(Resources.FancyZones_Custom_Zones_Format);

    public static bool TryGetMonitors(out IReadOnlyList<FancyZonesMonitorDescriptor> monitors, out string error)
    {
        monitors = Array.Empty<FancyZonesMonitorDescriptor>();
        error = string.Empty;

        Logger.LogInfo($"TryGetMonitors: Starting. EditorParametersPath={FZPaths.EditorParameters}");

        try
        {
            // Request FancyZones to save current monitor configuration.
            // The editor-parameters.json file is only written when:
            // 1. Opening the FancyZones Editor
            // 2. Receiving the WM_PRIV_SAVE_EDITOR_PARAMETERS message
            // Without this, monitor changes (plug/unplug) won't be reflected in the file.
            var editorParams = ReadEditorParametersWithRefresh();
            Logger.LogInfo($"TryGetMonitors: ReadEditorParametersWithRefreshWithRefresh returned. Monitors={editorParams.Monitors?.Count ?? -1}");

            var editorMonitors = editorParams.Monitors;
            if (editorMonitors is null || editorMonitors.Count == 0)
            {
                error = Resources.FancyZones_NoFancyZonesMonitorsFound;
                Logger.LogWarning($"TryGetMonitors: No monitors in file.");
                return false;
            }

            monitors = editorMonitors
                .Select((monitor, i) => new FancyZonesMonitorDescriptor(i + 1, monitor))
                .ToArray();
            Logger.LogInfo($"TryGetMonitors: Succeeded. MonitorCount={monitors.Count}");
            return true;
        }
        catch (Exception ex)
        {
            error = string.Format(CultureInfo.CurrentCulture, ReadMonitorDataFailedFormat, ex.Message);
            Logger.LogError($"TryGetMonitors: Exception. Message={ex.Message} Stack={ex.StackTrace}");
            return false;
        }
    }

    /// <summary>
    /// Requests FancyZones to save the current monitor configuration and reads the file.
    /// This is a best-effort approach for performance: we send the save request and immediately
    /// read the file without waiting. If the file hasn't been updated yet, the next call will
    /// see the updated data since FancyZones processes the message asynchronously.
    /// </summary>
    private static EditorParameters.ParamsWrapper ReadEditorParametersWithRefresh()
    {
        // Request FancyZones to save the current monitor configuration.
        // This is fire-and-forget for performance - we don't wait for the save to complete.
        // If this is the first call after a monitor change, we may read stale data, but the
        // next call will see the updated file since FancyZones will have processed the message.
        FancyZonesNotifier.NotifySaveEditorParameters();

        return FancyZonesDataIO.ReadEditorParameters();
    }

    public static IReadOnlyList<FancyZonesLayoutDescriptor> GetLayouts()
    {
        Logger.LogInfo($"GetLayouts: Starting. LayoutTemplatesPath={FZPaths.LayoutTemplates} CustomLayoutsPath={FZPaths.CustomLayouts}");
        var layouts = new List<FancyZonesLayoutDescriptor>();
        try
        {
            var templates = GetTemplateLayouts().ToArray();
            Logger.LogInfo($"GetLayouts: GetTemplateLayouts returned {templates.Length} layouts");
            layouts.AddRange(templates);
        }
        catch (Exception ex)
        {
            Logger.LogError($"GetLayouts: GetTemplateLayouts failed. Message={ex.Message} Stack={ex.StackTrace}");
        }

        try
        {
            var customLayouts = GetCustomLayouts().ToArray();
            Logger.LogInfo($"GetLayouts: GetCustomLayouts returned {customLayouts.Length} layouts");
            layouts.AddRange(customLayouts);
        }
        catch (Exception ex)
        {
            Logger.LogError($"GetLayouts: GetCustomLayouts failed. Message={ex.Message} Stack={ex.StackTrace}");
        }

        Logger.LogInfo($"GetLayouts: Total layouts={layouts.Count}");
        return layouts;
    }

    public static bool TryGetAppliedLayoutForMonitor(EditorParameters.NativeMonitorDataWrapper monitor, out AppliedLayouts.AppliedLayoutWrapper.LayoutWrapper? appliedLayout)
        => TryGetAppliedLayoutForMonitor(monitor, FancyZonesVirtualDesktop.GetCurrentVirtualDesktopIdString(), out appliedLayout);

    public static bool TryGetAppliedLayoutForMonitor(EditorParameters.NativeMonitorDataWrapper monitor, string virtualDesktopId, out AppliedLayouts.AppliedLayoutWrapper.LayoutWrapper? appliedLayout)
    {
        appliedLayout = null;

        if (!TryReadAppliedLayouts(out var file))
        {
            return false;
        }

        var match = FindAppliedLayoutEntry(file, monitor, virtualDesktopId);
        if (match is not null)
        {
            appliedLayout = match.Value.AppliedLayout;
            return true;
        }

        return false;
    }

    public static (bool Success, string Message) ApplyLayoutToAllMonitors(FancyZonesLayoutDescriptor layout)
    {
        if (!TryGetMonitors(out var monitors, out var error))
        {
            return (false, error);
        }

        return ApplyLayoutToMonitors(layout, monitors.Select(m => m.Data));
    }

    public static (bool Success, string Message) ApplyLayoutToMonitor(FancyZonesLayoutDescriptor layout, FancyZonesMonitorDescriptor monitor)
    {
        if (!TryGetMonitors(out var monitors, out var error))
        {
            return (false, error);
        }

        EditorParameters.NativeMonitorDataWrapper? monitorData = null;
        foreach (var candidate in monitors)
        {
            if (candidate.Data.MonitorInstanceId == monitor.Data.MonitorInstanceId)
            {
                monitorData = candidate.Data;
                break;
            }
        }

        if (monitorData is null)
        {
            return (false, "Monitor not found.");
        }

        return ApplyLayoutToMonitors(layout, [monitorData.Value]);
    }

    private static (bool Success, string Message) ApplyLayoutToMonitors(FancyZonesLayoutDescriptor layout, IEnumerable<EditorParameters.NativeMonitorDataWrapper> monitors)
    {
        AppliedLayouts.AppliedLayoutsListWrapper appliedFile;
        if (!TryReadAppliedLayouts(out var existingFile))
        {
            appliedFile = new AppliedLayouts.AppliedLayoutsListWrapper { AppliedLayouts = new List<AppliedLayouts.AppliedLayoutWrapper>() };
        }
        else
        {
            appliedFile = existingFile;
        }

        appliedFile.AppliedLayouts ??= new List<AppliedLayouts.AppliedLayoutWrapper>();

        var currentVirtualDesktop = FancyZonesVirtualDesktop.GetCurrentVirtualDesktopIdString();

        foreach (var monitor in monitors)
        {
            var existingEntry = FindAppliedLayoutEntry(appliedFile, monitor, currentVirtualDesktop);
            if (existingEntry is not null)
            {
                // Remove the existing entry so we can add a new one
                appliedFile.AppliedLayouts.Remove(existingEntry.Value);
            }

            var newEntry = new AppliedLayouts.AppliedLayoutWrapper
            {
                Device = new AppliedLayouts.AppliedLayoutWrapper.DeviceIdWrapper
                {
                    Monitor = monitor.Monitor,
                    MonitorInstance = monitor.MonitorInstanceId ?? string.Empty,
                    SerialNumber = monitor.MonitorSerialNumber ?? string.Empty,
                    MonitorNumber = monitor.MonitorNumber,
                    VirtualDesktop = currentVirtualDesktop,
                },
                AppliedLayout = new AppliedLayouts.AppliedLayoutWrapper.LayoutWrapper
                {
                    Uuid = layout.ApplyLayout.Uuid,
                    Type = layout.ApplyLayout.Type,
                    ZoneCount = layout.ApplyLayout.ZoneCount,
                    ShowSpacing = layout.ApplyLayout.ShowSpacing,
                    Spacing = layout.ApplyLayout.Spacing,
                    SensitivityRadius = layout.ApplyLayout.SensitivityRadius,
                },
            };

            appliedFile.AppliedLayouts.Add(newEntry);
        }

        try
        {
            FancyZonesDataIO.WriteAppliedLayouts(appliedFile);
        }
        catch (Exception ex)
        {
            return (false, string.Format(CultureInfo.CurrentCulture, WriteAppliedLayoutsFailedFormat, ex.Message));
        }

        try
        {
            FancyZonesNotifier.NotifyAppliedLayoutsChanged();
        }
        catch (Exception ex)
        {
            return (true, string.Format(CultureInfo.CurrentCulture, LayoutAppliedNotifyFailedFormat, ex.Message));
        }

        return (true, Resources.FancyZones_LayoutApplied);
    }

    private static AppliedLayouts.AppliedLayoutWrapper? FindAppliedLayoutEntry(AppliedLayouts.AppliedLayoutsListWrapper file, EditorParameters.NativeMonitorDataWrapper monitor, string virtualDesktopId)
    {
        if (file.AppliedLayouts is null)
        {
            return null;
        }

        return file.AppliedLayouts.FirstOrDefault(e =>
            string.Equals(e.Device.Monitor, monitor.Monitor, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.Device.MonitorInstance ?? string.Empty, monitor.MonitorInstanceId ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.Device.SerialNumber ?? string.Empty, monitor.MonitorSerialNumber ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
            e.Device.MonitorNumber == monitor.MonitorNumber &&
            string.Equals(e.Device.VirtualDesktop, virtualDesktopId, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryReadAppliedLayouts(out AppliedLayouts.AppliedLayoutsListWrapper file)
    {
        file = default;
        try
        {
            if (!File.Exists(FZPaths.AppliedLayouts))
            {
                return false;
            }

            file = FancyZonesDataIO.ReadAppliedLayouts();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<FancyZonesLayoutDescriptor> GetTemplateLayouts()
    {
        Logger.LogInfo($"GetTemplateLayouts: Starting. Path={FZPaths.LayoutTemplates} Exists={File.Exists(FZPaths.LayoutTemplates)}");

        LayoutTemplates.TemplateLayoutsListWrapper templates;
        try
        {
            if (!File.Exists(FZPaths.LayoutTemplates))
            {
                Logger.LogWarning($"GetTemplateLayouts: File not found.");
                yield break;
            }

            templates = FancyZonesDataIO.ReadLayoutTemplates();
            Logger.LogInfo($"GetTemplateLayouts: ReadLayoutTemplates succeeded. Count={templates.LayoutTemplates?.Count ?? -1}");
        }
        catch (Exception ex)
        {
            Logger.LogError($"GetTemplateLayouts: ReadLayoutTemplates failed. Message={ex.Message} Stack={ex.StackTrace}");
            yield break;
        }

        var templateLayouts = templates.LayoutTemplates;
        if (templateLayouts is null)
        {
            Logger.LogWarning($"GetTemplateLayouts: LayoutTemplates is null.");
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
            var title = string.Format(CultureInfo.CurrentCulture, TemplateFormat, type);
            var subtitle = string.Format(CultureInfo.CurrentCulture, ZonesFormat, zoneCount);

            yield return new FancyZonesLayoutDescriptor
            {
                Id = $"template:{type.ToLowerInvariant()}",
                Source = FancyZonesLayoutSource.Template,
                Title = title,
                Subtitle = subtitle,
                Template = template,
                ApplyLayout = new AppliedLayouts.AppliedLayoutWrapper.LayoutWrapper
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
        CustomLayouts.CustomLayoutListWrapper customLayouts;
        try
        {
            if (!File.Exists(FZPaths.CustomLayouts))
            {
                yield break;
            }

            customLayouts = FancyZonesDataIO.ReadCustomLayouts();
        }
        catch
        {
            yield break;
        }

        var layouts = customLayouts.CustomLayouts;
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
                "grid" => string.Format(CultureInfo.CurrentCulture, CustomGridZonesFormat, applied.ZoneCount),
                "canvas" => string.Format(CultureInfo.CurrentCulture, CustomCanvasZonesFormat, applied.ZoneCount),
                _ => string.Format(CultureInfo.CurrentCulture, CustomZonesFormat, applied.ZoneCount),
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

    private static bool TryBuildAppliedLayoutForCustom(CustomLayouts.CustomLayoutWrapper custom, out AppliedLayouts.AppliedLayoutWrapper.LayoutWrapper applied)
    {
        applied = new AppliedLayouts.AppliedLayoutWrapper.LayoutWrapper
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

        if (!info.TryGetProperty("rows", out var rowsProp) ||
            !info.TryGetProperty("columns", out var columnsProp) ||
            rowsProp.ValueKind != JsonValueKind.Number ||
            columnsProp.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        var rows = rowsProp.GetInt32();
        var columns = columnsProp.GetInt32();
        if (rows <= 0 || columns <= 0)
        {
            return false;
        }

        if (info.TryGetProperty("cell-child-map", out var cellMap) && cellMap.ValueKind == JsonValueKind.Array)
        {
            var max = -1;
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
        }
        else
        {
            zoneCount = rows * columns;
        }

        if (zoneCount <= 0)
        {
            return false;
        }

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

        return true;
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
