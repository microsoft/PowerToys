// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.CommandLine.Invocation;
using System.Globalization;
using System.Text.Json;

using FancyZonesEditorCommon.Data;
using FancyZonesEditorCommon.Utils;

namespace FancyZonesCLI.CommandLine.Commands;

internal sealed partial class GetLayoutsCommand : FancyZonesBaseCommand
{
    public GetLayoutsCommand()
        : base("get-layouts", "List available layouts")
    {
        AddAlias("ls");
    }

    protected override string Execute(InvocationContext context)
    {
        var sb = new System.Text.StringBuilder();

        // Print template layouts.
        var templatesJson = FancyZonesDataIO.ReadLayoutTemplates();

        if (templatesJson.LayoutTemplates != null)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"=== Built-in Template Layouts ({templatesJson.LayoutTemplates.Count} total) ===\n");

            for (int i = 0; i < templatesJson.LayoutTemplates.Count; i++)
            {
                var template = templatesJson.LayoutTemplates[i];
                sb.AppendLine(CultureInfo.InvariantCulture, $"[T{i + 1}] {template.Type}");
                sb.Append(CultureInfo.InvariantCulture, $"    Zones: {template.ZoneCount}");
                if (template.ShowSpacing && template.Spacing > 0)
                {
                    sb.Append(CultureInfo.InvariantCulture, $", Spacing: {template.Spacing}px");
                }

                sb.AppendLine();
                sb.AppendLine();

                // Draw visual preview.
                sb.Append(LayoutVisualizer.DrawTemplateLayout(template));

                if (i < templatesJson.LayoutTemplates.Count - 1)
                {
                    sb.AppendLine();
                }
            }

            sb.AppendLine("\n");
        }

        // Print custom layouts.
        var customLayouts = FancyZonesDataIO.ReadCustomLayouts();

        if (customLayouts.CustomLayouts != null)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"=== Custom Layouts ({customLayouts.CustomLayouts.Count} total) ===");

            for (int i = 0; i < customLayouts.CustomLayouts.Count; i++)
            {
                var layout = customLayouts.CustomLayouts[i];
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

                // Draw visual preview.
                sb.Append(LayoutVisualizer.DrawCustomLayout(layout));

                // Add note for canvas layouts.
                if (isCanvasLayout)
                {
                    sb.AppendLine("\n    Note: Canvas layout preview is approximate.");
                    sb.AppendLine("          Open FancyZones Editor for precise zone boundaries.");
                }

                if (i < customLayouts.CustomLayouts.Count - 1)
                {
                    sb.AppendLine();
                }
            }

            sb.AppendLine("\nUse 'FancyZonesCLI.exe set-layout <UUID>' to apply a layout.");
        }

        return sb.ToString().TrimEnd();
    }
}
