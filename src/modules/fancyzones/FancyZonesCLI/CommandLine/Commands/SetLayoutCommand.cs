// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Globalization;

using FancyZonesCLI.Utils;
using FancyZonesEditorCommon.Data;
using FancyZonesEditorCommon.Utils;

namespace FancyZonesCLI.CommandLine.Commands;

internal sealed partial class SetLayoutCommand : FancyZonesBaseCommand
{
    private static readonly string[] AliasesMonitor = ["--monitor", "-m"];
    private static readonly string[] AliasesAll = ["--all", "-a"];

    private const string DefaultLayoutUuid = "{00000000-0000-0000-0000-000000000000}";

    private readonly Argument<string> _layoutId;
    private readonly Option<int?> _monitor;
    private readonly Option<bool> _all;

    public SetLayoutCommand()
        : base("set-layout", "Set layout by UUID or template name")
    {
        AddAlias("s");

        _layoutId = new Argument<string>("layout", "Layout UUID or template type (e.g. focus, columns)");
        AddArgument(_layoutId);

        _monitor = new Option<int?>(AliasesMonitor, "Apply to monitor N (1-based)");
        _monitor.AddValidator(result =>
        {
            if (result.Tokens.Count == 0)
            {
                return;
            }

            int? monitor = result.GetValueOrDefault<int?>();
            if (monitor.HasValue && monitor.Value < 1)
            {
                result.ErrorMessage = "Monitor index must be >= 1.";
            }
        });

        _all = new Option<bool>(AliasesAll, "Apply to all monitors");

        AddOption(_monitor);
        AddOption(_all);

        AddValidator(commandResult =>
        {
            int? monitor = commandResult.GetValueForOption(_monitor);
            bool all = commandResult.GetValueForOption(_all);

            if (monitor.HasValue && all)
            {
                commandResult.ErrorMessage = "Cannot specify both --monitor and --all.";
            }
        });
    }

    protected override string Execute(InvocationContext context)
    {
        // FancyZones running guard is handled by FancyZonesBaseCommand.
        string layout = context.ParseResult.GetValueForArgument(_layoutId);
        int? monitor = context.ParseResult.GetValueForOption(_monitor);
        bool all = context.ParseResult.GetValueForOption(_all);
        Logger.LogInfo($"SetLayout called with layout: '{layout}', monitor: {(monitor.HasValue ? monitor.Value.ToString(CultureInfo.InvariantCulture) : "<default>")}, all: {all}");

        var (targetCustomLayout, targetTemplate) = ResolveTargetLayout(layout);

        var editorParams = ReadEditorParametersWithRefresh();
        var appliedLayouts = FancyZonesDataIO.ReadAppliedLayouts();
        appliedLayouts.AppliedLayouts ??= new List<AppliedLayouts.AppliedLayoutWrapper>();

        List<int> monitorsToUpdate = GetMonitorsToUpdate(editorParams, monitor, all);
        List<AppliedLayouts.AppliedLayoutWrapper> newLayouts = BuildNewLayouts(editorParams, monitorsToUpdate, targetCustomLayout, targetTemplate);
        var updatedLayouts = MergeWithHistoricalLayouts(appliedLayouts, newLayouts);

        Logger.LogInfo($"Writing {updatedLayouts.AppliedLayouts?.Count ?? 0} layouts to file");
        FancyZonesDataIO.WriteAppliedLayouts(updatedLayouts);
        Logger.LogInfo($"Applied layouts file updated for {monitorsToUpdate.Count} monitor(s)");

        NativeMethods.NotifyFancyZones(NativeMethods.WM_PRIV_APPLIED_LAYOUTS_FILE_UPDATE);
        Logger.LogInfo("FancyZones notified of layout change");

        return BuildSuccessMessage(layout, monitor, all);
    }

    private static string BuildSuccessMessage(string layout, int? monitor, bool all)
    {
        if (all)
        {
            return string.Format(CultureInfo.InvariantCulture, "Layout '{0}' applied to all monitors.", layout);
        }

        if (monitor.HasValue)
        {
            return string.Format(CultureInfo.InvariantCulture, "Layout '{0}' applied to monitor {1}.", layout, monitor.Value);
        }

        return string.Format(CultureInfo.InvariantCulture, "Layout '{0}' applied to monitor 1.", layout);
    }

    private static (CustomLayouts.CustomLayoutWrapper? TargetCustomLayout, LayoutTemplates.TemplateLayoutWrapper? TargetTemplate) ResolveTargetLayout(string layout)
    {
        var customLayouts = FancyZonesDataIO.ReadCustomLayouts();
        CustomLayouts.CustomLayoutWrapper? targetCustomLayout = FindCustomLayout(customLayouts, layout);

        LayoutTemplates.TemplateLayoutWrapper? targetTemplate = null;
        if (!targetCustomLayout.HasValue || string.IsNullOrEmpty(targetCustomLayout.Value.Uuid))
        {
            var templates = FancyZonesDataIO.ReadLayoutTemplates();
            targetTemplate = FindTemplate(templates, layout);

            if (targetCustomLayout.HasValue && string.IsNullOrEmpty(targetCustomLayout.Value.Uuid))
            {
                targetCustomLayout = null;
            }
        }

        if (!targetCustomLayout.HasValue && !targetTemplate.HasValue)
        {
            throw new InvalidOperationException(
                $"Layout '{layout}' not found{Environment.NewLine}" +
                "Tip: For templates, use the type name (e.g., 'focus', 'columns', 'rows', 'grid', 'priority-grid')" +
                $"{Environment.NewLine}     For custom layouts, use the UUID from 'get-layouts'");
        }

        return (targetCustomLayout, targetTemplate);
    }

    private static CustomLayouts.CustomLayoutWrapper? FindCustomLayout(CustomLayouts.CustomLayoutListWrapper customLayouts, string layout)
    {
        if (customLayouts.CustomLayouts == null)
        {
            return null;
        }

        foreach (var customLayout in customLayouts.CustomLayouts)
        {
            if (customLayout.Uuid.Equals(layout, StringComparison.OrdinalIgnoreCase))
            {
                return customLayout;
            }
        }

        return null;
    }

    private static LayoutTemplates.TemplateLayoutWrapper? FindTemplate(LayoutTemplates.TemplateLayoutsListWrapper templates, string layout)
    {
        if (templates.LayoutTemplates == null)
        {
            return null;
        }

        foreach (var template in templates.LayoutTemplates)
        {
            if (template.Type.Equals(layout, StringComparison.OrdinalIgnoreCase))
            {
                return template;
            }
        }

        return null;
    }

    private static EditorParameters.ParamsWrapper ReadEditorParametersWithRefresh()
    {
        NativeMethods.NotifyFancyZones(NativeMethods.WM_PRIV_SAVE_EDITOR_PARAMETERS);
        System.Threading.Thread.Sleep(200);

        var editorParams = FancyZonesDataIO.ReadEditorParameters();
        if (editorParams.Monitors == null || editorParams.Monitors.Count == 0)
        {
            throw new InvalidOperationException("Could not get current monitor information.");
        }

        return editorParams;
    }

    private static List<int> GetMonitorsToUpdate(EditorParameters.ParamsWrapper editorParams, int? monitor, bool all)
    {
        var result = new List<int>();

        if (all)
        {
            for (int i = 0; i < editorParams.Monitors.Count; i++)
            {
                result.Add(i);
            }

            return result;
        }

        if (monitor.HasValue)
        {
            int monitorIndex = monitor.Value - 1; // Convert to 0-based.
            if (monitorIndex < 0 || monitorIndex >= editorParams.Monitors.Count)
            {
                throw new InvalidOperationException($"Monitor {monitor.Value} not found. Available monitors: 1-{editorParams.Monitors.Count}");
            }

            result.Add(monitorIndex);
            return result;
        }

        // Default: first monitor.
        result.Add(0);
        return result;
    }

    private static List<AppliedLayouts.AppliedLayoutWrapper> BuildNewLayouts(
        EditorParameters.ParamsWrapper editorParams,
        List<int> monitorsToUpdate,
        CustomLayouts.CustomLayoutWrapper? targetCustomLayout,
        LayoutTemplates.TemplateLayoutWrapper? targetTemplate)
    {
        var newLayouts = new List<AppliedLayouts.AppliedLayoutWrapper>();

        foreach (int monitorIndex in monitorsToUpdate)
        {
            var currentMonitor = editorParams.Monitors[monitorIndex];

            var (layoutUuid, layoutType, showSpacing, spacing, zoneCount, sensitivityRadius) =
                GetLayoutSettings(targetCustomLayout, targetTemplate);

            var deviceId = new AppliedLayouts.AppliedLayoutWrapper.DeviceIdWrapper
            {
                Monitor = currentMonitor.Monitor,
                MonitorInstance = currentMonitor.MonitorInstanceId,
                MonitorNumber = currentMonitor.MonitorNumber,
                SerialNumber = currentMonitor.MonitorSerialNumber,
                VirtualDesktop = currentMonitor.VirtualDesktop,
            };

            newLayouts.Add(new AppliedLayouts.AppliedLayoutWrapper
            {
                Device = deviceId,
                AppliedLayout = new AppliedLayouts.AppliedLayoutWrapper.LayoutWrapper
                {
                    Uuid = layoutUuid,
                    Type = layoutType,
                    ShowSpacing = showSpacing,
                    Spacing = spacing,
                    ZoneCount = zoneCount,
                    SensitivityRadius = sensitivityRadius,
                },
            });
        }

        if (newLayouts.Count == 0)
        {
            throw new InvalidOperationException("Internal error - no monitors to update.");
        }

        return newLayouts;
    }

    private static (string LayoutUuid, string LayoutType, bool ShowSpacing, int Spacing, int ZoneCount, int SensitivityRadius) GetLayoutSettings(
        CustomLayouts.CustomLayoutWrapper? targetCustomLayout,
        LayoutTemplates.TemplateLayoutWrapper? targetTemplate)
    {
        if (targetCustomLayout.HasValue)
        {
            var customLayoutsSerializer = new CustomLayouts();
            string type = targetCustomLayout.Value.Type?.ToLowerInvariant() ?? string.Empty;

            bool showSpacing = false;
            int spacing = 0;
            int zoneCount = 0;
            int sensitivityRadius = 20;

            if (type == "canvas")
            {
                var info = customLayoutsSerializer.CanvasFromJsonElement(targetCustomLayout.Value.Info.GetRawText());
                zoneCount = info.Zones?.Count ?? 0;
                sensitivityRadius = info.SensitivityRadius;
            }
            else if (type == "grid")
            {
                var info = customLayoutsSerializer.GridFromJsonElement(targetCustomLayout.Value.Info.GetRawText());
                showSpacing = info.ShowSpacing;
                spacing = info.Spacing;
                sensitivityRadius = info.SensitivityRadius;

                if (info.CellChildMap != null)
                {
                    var uniqueZoneIds = new HashSet<int>();

                    for (int r = 0; r < info.CellChildMap.Length; r++)
                    {
                        int[] row = info.CellChildMap[r];
                        if (row == null)
                        {
                            continue;
                        }

                        for (int c = 0; c < row.Length; c++)
                        {
                            uniqueZoneIds.Add(row[c]);
                        }
                    }

                    zoneCount = uniqueZoneIds.Count;
                }
            }
            else
            {
                throw new InvalidOperationException($"Unsupported custom layout type '{targetCustomLayout.Value.Type}'.");
            }

            return (
                targetCustomLayout.Value.Uuid,
                Constants.CustomLayoutJsonTag,
                ShowSpacing: showSpacing,
                Spacing: spacing,
                ZoneCount: zoneCount,
                SensitivityRadius: sensitivityRadius);
        }

        if (targetTemplate.HasValue)
        {
            return (
                DefaultLayoutUuid,
                targetTemplate.Value.Type,
                targetTemplate.Value.ShowSpacing,
                targetTemplate.Value.Spacing,
                targetTemplate.Value.ZoneCount,
                targetTemplate.Value.SensitivityRadius);
        }

        throw new InvalidOperationException("Internal error - no layout selected.");
    }

    private static AppliedLayouts.AppliedLayoutsListWrapper MergeWithHistoricalLayouts(
        AppliedLayouts.AppliedLayoutsListWrapper existingLayouts,
        List<AppliedLayouts.AppliedLayoutWrapper> newLayouts)
    {
        var mergedLayoutsList = new List<AppliedLayouts.AppliedLayoutWrapper>();
        mergedLayoutsList.AddRange(newLayouts);

        if (existingLayouts.AppliedLayouts != null)
        {
            foreach (var existingLayout in existingLayouts.AppliedLayouts)
            {
                bool isUpdated = false;

                foreach (var newLayout in newLayouts)
                {
                    if (AppliedLayoutsHelper.MatchesDevice(
                        existingLayout.Device,
                        newLayout.Device.Monitor,
                        newLayout.Device.SerialNumber,
                        newLayout.Device.MonitorNumber,
                        newLayout.Device.VirtualDesktop))
                    {
                        isUpdated = true;
                        break;
                    }
                }

                if (!isUpdated)
                {
                    mergedLayoutsList.Add(existingLayout);
                }
            }
        }

        return new AppliedLayouts.AppliedLayoutsListWrapper
        {
            AppliedLayouts = mergedLayoutsList,
        };
    }
}
