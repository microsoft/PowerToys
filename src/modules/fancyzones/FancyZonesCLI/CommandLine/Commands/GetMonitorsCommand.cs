// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine.Invocation;
using System.Globalization;

using FancyZonesCLI.Utils;
using FancyZonesEditorCommon.Data;
using FancyZonesEditorCommon.Utils;

namespace FancyZonesCLI.CommandLine.Commands;

internal sealed partial class GetMonitorsCommand : FancyZonesBaseCommand
{
    public GetMonitorsCommand()
        : base("get-monitors", "List monitors and FancyZones metadata")
    {
        AddAlias("m");
    }

    protected override string Execute(InvocationContext context)
    {
        // Request FancyZones to save current monitor configuration.
        NativeMethods.NotifyFancyZones(NativeMethods.WM_PRIV_SAVE_EDITOR_PARAMETERS);

        // Wait briefly for FancyZones to create the file.
        System.Threading.Thread.Sleep(200);

        // Try to read editor parameters for current monitor state.
        EditorParameters.ParamsWrapper editorParams;
        try
        {
            editorParams = FancyZonesDataIO.ReadEditorParameters();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to read monitor information. {ex.Message}{Environment.NewLine}Note: Ensure FancyZones is running to get current monitor information.", ex);
        }

        if (editorParams.Monitors == null || editorParams.Monitors.Count == 0)
        {
            return "No monitors found.";
        }

        // Also read applied layouts to show which layout is active on each monitor.
        var appliedLayouts = FancyZonesDataIO.ReadAppliedLayouts();

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"=== Monitors ({editorParams.Monitors.Count} total) ===");
        sb.AppendLine();

        for (int i = 0; i < editorParams.Monitors.Count; i++)
        {
            var monitor = editorParams.Monitors[i];
            var monitorNum = i + 1;

            sb.AppendLine(CultureInfo.InvariantCulture, $"Monitor {monitorNum}:");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Monitor: {monitor.Monitor}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Monitor Instance: {monitor.MonitorInstanceId}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Monitor Number: {monitor.MonitorNumber}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Serial Number: {monitor.MonitorSerialNumber}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Virtual Desktop: {monitor.VirtualDesktop}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  DPI: {monitor.Dpi}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Resolution: {monitor.MonitorWidth}x{monitor.MonitorHeight}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Work Area: {monitor.WorkAreaWidth}x{monitor.WorkAreaHeight}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Position: ({monitor.LeftCoordinate}, {monitor.TopCoordinate})");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Selected: {monitor.IsSelected}");

            // Find matching applied layout for this monitor using EditorCommon's matching logic.
            if (appliedLayouts.AppliedLayouts != null)
            {
                var matchedLayout = AppliedLayoutsHelper.FindLayoutForMonitor(
                    appliedLayouts,
                    monitor.Monitor,
                    monitor.MonitorSerialNumber,
                    monitor.MonitorNumber,
                    monitor.VirtualDesktop);

                if (matchedLayout != null && matchedLayout.Value.AppliedLayout.Type != null)
                {
                    sb.AppendLine();
                    sb.AppendLine(CultureInfo.InvariantCulture, $"  Active Layout: {matchedLayout.Value.AppliedLayout.Type}");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"  Zone Count: {matchedLayout.Value.AppliedLayout.ZoneCount}");
                    sb.AppendLine(CultureInfo.InvariantCulture, $"  Sensitivity Radius: {matchedLayout.Value.AppliedLayout.SensitivityRadius}px");
                    if (!string.IsNullOrEmpty(matchedLayout.Value.AppliedLayout.Uuid) &&
                        matchedLayout.Value.AppliedLayout.Uuid != "{00000000-0000-0000-0000-000000000000}")
                    {
                        sb.AppendLine(CultureInfo.InvariantCulture, $"  Layout UUID: {matchedLayout.Value.AppliedLayout.Uuid}");
                    }
                }
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }
}
