// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Properties;

namespace PowerToysExtension.Helpers;

internal static class FancyZonesContextHelper
{
    private static readonly CompositeFormat ApplyToMonitorFormat = CompositeFormat.Parse(Resources.FancyZones_ApplyTo_Format);

    public static string FormatApplyToMonitorTitle(FancyZonesMonitorDescriptor monitor)
    {
        return string.Format(CultureInfo.CurrentCulture, ApplyToMonitorFormat, monitor.Title);
    }

    public static IContextItem[] BuildLayoutContext(FancyZonesLayoutDescriptor layout, IReadOnlyList<FancyZonesMonitorDescriptor> monitors)
    {
        var commands = new List<IContextItem>(monitors.Count);

        foreach (var monitor in monitors)
        {
            commands.Add(new CommandContextItem(new ApplyFancyZonesLayoutCommand(layout, monitor))
            {
                Title = FormatApplyToMonitorTitle(monitor),
                Subtitle = monitor.Subtitle,
            });
        }

        return commands.ToArray();
    }
}
