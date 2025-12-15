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

internal sealed partial class FancyZonesMonitorsPage : DynamicListPage
{
    private readonly CommandItem _emptyMessage;

    public FancyZonesMonitorsPage()
    {
        Icon = PowerToysResourcesHelper.IconFromSettingsIcon("FancyZones.png");
        Name = Title = "FancyZones Monitors";
        Id = "com.microsoft.cmdpal.powertoys.fancyzones.monitors";

        _emptyMessage = new CommandItem()
        {
            Title = "No monitors found",
            Subtitle = "Open FancyZones Editor once to initialize monitor data.",
            Icon = PowerToysResourcesHelper.IconFromSettingsIcon("FancyZones.png"),
        };
        EmptyContent = _emptyMessage;
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        RaiseItemsChanged(0);
    }

    public override IListItem[] GetItems()
    {
        if (!FancyZonesDataService.TryGetMonitors(out var monitors, out var error))
        {
            _emptyMessage.Subtitle = error;
            return Array.Empty<IListItem>();
        }

        var icon = PowerToysResourcesHelper.IconFromSettingsIcon("FancyZones.png");
        var items = new List<IListItem>(monitors.Count);

        foreach (var monitor in monitors)
        {
            if (!string.IsNullOrWhiteSpace(SearchText) &&
                !monitor.Title.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) &&
                !monitor.Subtitle.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            var monitorPage = new FancyZonesMonitorPage(monitor.Index);
            var item = new ListItem(new CommandItem(monitorPage))
            {
                Title = monitor.Title,
                Subtitle = monitor.Subtitle,
                Icon = icon,
                Details = FancyZonesMonitorPage.BuildMonitorDetails(monitor),
                MoreCommands =
                [
                    new CommandContextItem(new IdentifyFancyZonesMonitorCommand(monitor.Index))
                    {
                        Title = "Identify monitor",
                        Subtitle = "Show an on-screen label on this monitor",
                    },
                    new CommandContextItem(new FancyZonesMonitorLayoutPickerPage(monitor.Index))
                    {
                        Title = "Apply layout…",
                        Subtitle = "Pick a layout for this monitor",
                    },
                ],
            };

            items.Add(item);
        }

        return items.ToArray();
    }
}
