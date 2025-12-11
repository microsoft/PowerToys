// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace FancyZonesCLI.Commands;

/// <summary>
/// Monitor-related commands.
/// </summary>
internal static class MonitorCommands
{
    public static (int ExitCode, string Output) GetMonitors()
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
}
