// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
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

        var monitorIcon = new IconInfo("\uE7F4");
        var items = new List<IListItem>(monitors.Count);

        foreach (var monitor in monitors)
        {
            if (!string.IsNullOrWhiteSpace(SearchText) &&
                !monitor.Title.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) &&
                !monitor.Subtitle.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            var appliedText = FancyZonesDataService.TryGetAppliedLayoutForMonitor(monitor.Data, out var applied) && applied is not null
                ? $"Current layout: {applied.Type} ({applied.ZoneCount} zones)"
                : "Current layout: unknown";

            var item = new FancyZonesMonitorListItem(monitor, appliedText, monitorIcon);
            items.Add(item);
        }

        return [.. items];
    }
}
