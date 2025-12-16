// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Globalization;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;

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
            Name = "Set active layout",
        };

        MoreCommands =
        [
            new CommandContextItem(pickerPage)
            {
                Title = "Set active layout",
                Subtitle = "Pick a layout for this monitor",
            },
        ];
    }

    public static Details BuildMonitorDetails(FancyZonesMonitorDescriptor monitor)
    {
        var tags = new List<IDetailsElement>
        {
            DetailTag("Monitor", monitor.Data.Monitor),
            DetailTag("Instance", monitor.Data.MonitorInstanceId),
            DetailTag("Serial", monitor.Data.MonitorSerialNumber),
            DetailTag("Number", monitor.Data.MonitorNumber.ToString(CultureInfo.InvariantCulture)),
            DetailTag("Virtual desktop", monitor.Data.VirtualDesktop),
            DetailTag("Work area", $"{monitor.Data.LeftCoordinate},{monitor.Data.TopCoordinate}  {monitor.Data.WorkAreaWidth}�{monitor.Data.WorkAreaHeight}"),
            DetailTag("Resolution", $"{monitor.Data.MonitorWidth}�{monitor.Data.MonitorHeight}"),
            DetailTag("DPI", monitor.Data.Dpi.ToString(CultureInfo.InvariantCulture)),
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
                Tags = [new Tag(string.IsNullOrWhiteSpace(value) ? "n/a" : value)],
            },
        };
    }
}
