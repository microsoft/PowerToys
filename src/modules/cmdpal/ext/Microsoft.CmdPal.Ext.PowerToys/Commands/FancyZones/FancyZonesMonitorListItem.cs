// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;
using PowerToysExtension.Properties;

namespace PowerToysExtension.Pages;

internal sealed partial class FancyZonesMonitorListItem : ListItem
{
    public FancyZonesMonitorListItem(FancyZonesMonitorDescriptor monitor, string subtitle, IconInfo icon)
        : base(new IdentifyFancyZonesMonitorCommand(monitor))
    {
        Title = monitor.Title;
        Subtitle = subtitle;
        Icon = icon;

        Details = BuildMonitorDetails(monitor);

        var pickerPage = new FancyZonesMonitorLayoutPickerPage(monitor)
        {
            Name = Resources.FancyZones_SetActiveLayout,
        };

        MoreCommands =
        [
            new CommandContextItem(pickerPage)
            {
                Title = Resources.FancyZones_SetActiveLayout,
                Subtitle = Resources.FancyZones_PickLayoutForMonitor,
            },
        ];
    }

    public static Details BuildMonitorDetails(FancyZonesMonitorDescriptor monitor)
    {
        var currentVirtualDesktop = FancyZonesVirtualDesktop.GetCurrentVirtualDesktopIdString();

        // Calculate physical resolution from logical pixels and DPI
        var scaleFactor = monitor.Data.Dpi > 0 ? monitor.Data.Dpi / 96.0 : 1.0;
        var physicalWidth = (int)Math.Round(monitor.Data.MonitorWidth * scaleFactor);
        var physicalHeight = (int)Math.Round(monitor.Data.MonitorHeight * scaleFactor);
        var resolution = $"{physicalWidth}\u00D7{physicalHeight}";

        var tags = new List<IDetailsElement>
        {
            DetailTag(Resources.FancyZones_Monitor, monitor.Data.Monitor),
            DetailTag(Resources.FancyZones_Number, monitor.Data.MonitorNumber.ToString(CultureInfo.InvariantCulture)),
            DetailTag(Resources.FancyZones_VirtualDesktop, currentVirtualDesktop),
            DetailTag(Resources.FancyZones_Resolution, resolution),
            DetailTag(Resources.FancyZones_DPI, monitor.Data.Dpi.ToString(CultureInfo.InvariantCulture)),
        };

        return new Details
        {
            Title = monitor.Title,
            HeroImage = FancyZonesMonitorPreviewRenderer.TryRenderMonitorHeroImage(monitor) ??
                        PowerToysResourcesHelper.IconFromSettingsIcon("FancyZones.png"),
            Metadata = tags.ToArray(),
        };
    }

    private static DetailsElement DetailTag(string key, string? value)
    {
        return new DetailsElement
        {
            Key = key,
            Data = new DetailsTags
            {
                Tags = [new Tag(string.IsNullOrWhiteSpace(value) ? Resources.Common_NotAvailable : value)],
            },
        };
    }
}
