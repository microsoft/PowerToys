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

internal sealed partial class GetActiveLayoutCommand : FancyZonesBaseCommand
{
    public GetActiveLayoutCommand()
        : base("get-active-layout", "Show currently active layout")
    {
        AddAlias("active");
    }

    protected override string Execute(InvocationContext context)
    {
        // Trigger FancyZones to save current monitor info and read it reliably.
        var editorParams = EditorParametersRefresh.ReadEditorParametersWithRefresh(
            () => NativeMethods.NotifyFancyZones(NativeMethods.WM_PRIV_SAVE_EDITOR_PARAMETERS));

        if (editorParams.Monitors == null || editorParams.Monitors.Count == 0)
        {
            throw new InvalidOperationException("Could not get current monitor information.");
        }

        // Read applied layouts.
        var appliedLayouts = FancyZonesDataIO.ReadAppliedLayouts();

        if (appliedLayouts.AppliedLayouts == null)
        {
            return "No layouts configured.";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("\n=== Active FancyZones Layout(s) ===\n");

        // Show only layouts for currently connected monitors.
        for (int i = 0; i < editorParams.Monitors.Count; i++)
        {
            var monitor = editorParams.Monitors[i];
            sb.AppendLine(CultureInfo.InvariantCulture, $"Monitor {i + 1}: {monitor.Monitor}");

            var matchedLayout = AppliedLayoutsHelper.FindLayoutForMonitor(
                appliedLayouts,
                monitor.Monitor,
                monitor.MonitorSerialNumber,
                monitor.MonitorNumber,
                monitor.VirtualDesktop);

            if (matchedLayout.HasValue)
            {
                var layout = matchedLayout.Value.AppliedLayout;
                sb.AppendLine(CultureInfo.InvariantCulture, $"  Layout UUID: {layout.Uuid}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"  Layout Type: {layout.Type}");
                sb.AppendLine(CultureInfo.InvariantCulture, $"  Zone Count: {layout.ZoneCount}");

                if (layout.ShowSpacing)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"  Spacing: {layout.Spacing}px");
                }

                sb.AppendLine(CultureInfo.InvariantCulture, $"  Sensitivity Radius: {layout.SensitivityRadius}px");
            }
            else
            {
                sb.AppendLine("  No layout applied");
            }

            if (i < editorParams.Monitors.Count - 1)
            {
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }
}
