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

namespace PowerToysExtension.Pages;

internal sealed partial class FancyZonesMonitorPage : ListPage
{
    private readonly int _monitorIndex;

    public FancyZonesMonitorPage(int monitorIndex)
    {
        _monitorIndex = monitorIndex;
        Icon = PowerToysResourcesHelper.IconFromSettingsIcon("FancyZones.png");
        Name = Title = $"Monitor {monitorIndex}";
        Id = $"com.microsoft.cmdpal.powertoys.fancyzones.monitor.{monitorIndex}";
    }

    public override IListItem[] GetItems()
    {
        if (!FancyZonesDataService.TryGetMonitors(out var monitors, out var error) ||
            _monitorIndex < 1 ||
            _monitorIndex > monitors.Count)
        {
            return
            [
                new ListItem(new CommandItem(new NoOpCommand()))
                {
                    Title = "Monitor not found",
                    Subtitle = error,
                    Icon = PowerToysResourcesHelper.IconFromSettingsIcon("FancyZones.png"),
                },
            ];
        }

        var monitor = monitors[_monitorIndex - 1];
        var details = BuildMonitorDetails(monitor);

        var appliedText = FancyZonesDataService.TryGetAppliedLayoutForMonitor(monitor.Data, out var applied) && applied is not null
            ? $"Current layout: {applied.Type} ({applied.ZoneCount} zones)"
            : "Current layout: unknown";

        return
        [
            new ListItem(new CommandItem(new NoOpCommand()))
            {
                Title = monitor.Title,
                Subtitle = appliedText,
                Icon = PowerToysResourcesHelper.IconFromSettingsIcon("FancyZones.png"),
                Details = details,
            },
            new ListItem(new IdentifyFancyZonesMonitorCommand(_monitorIndex))
            {
                Title = "Identify monitor",
                Subtitle = "Show an on-screen label on this monitor",
                Icon = PowerToysResourcesHelper.IconFromSettingsIcon("FancyZones.png"),
            },
            new ListItem(new CommandItem(new FancyZonesMonitorLayoutPickerPage(_monitorIndex)))
            {
                Title = "Apply layout…",
                Subtitle = "Pick a layout for this monitor",
                Icon = PowerToysResourcesHelper.IconFromSettingsIcon("FancyZones.png"),
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
            DetailTag("Work area", $"{monitor.Data.LeftCoordinate},{monitor.Data.TopCoordinate}  {monitor.Data.WorkAreaWidth}×{monitor.Data.WorkAreaHeight}"),
            DetailTag("Resolution", $"{monitor.Data.MonitorWidth}×{monitor.Data.MonitorHeight}"),
            DetailTag("DPI", monitor.Data.Dpi.ToString(CultureInfo.InvariantCulture)),
        };

        return new Details
        {
            Title = monitor.Title,
            HeroImage = PowerToysResourcesHelper.IconFromSettingsIcon("FancyZones.png"),
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
