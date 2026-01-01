// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Pages;

internal sealed partial class FancyZonesMonitorLayoutPickerPage : DynamicListPage
{
    private readonly FancyZonesMonitorDescriptor _monitor;
    private readonly CommandItem _emptyMessage;

    public FancyZonesMonitorLayoutPickerPage(FancyZonesMonitorDescriptor monitor)
    {
        _monitor = monitor;
        Icon = PowerToysResourcesHelper.IconFromSettingsIcon("FancyZones.png");
        Name = Title = $"Set active layout for {_monitor.Title}";
        Id = $"com.microsoft.cmdpal.powertoys.fancyzones.monitor.{_monitor.Index}.layouts";

        _emptyMessage = new CommandItem()
        {
            Title = "No layouts found",
            Subtitle = "Open FancyZones Editor once to initialize layouts.",
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
        var layouts = FancyZonesDataService.GetLayouts();
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            layouts = layouts
                .Where(l => l.Title.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase) ||
                            l.Subtitle.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase))
                .ToArray();
        }

        if (layouts.Count == 0)
        {
            return Array.Empty<IListItem>();
        }

        var fallbackIcon = PowerToysResourcesHelper.IconFromSettingsIcon("FancyZones.png");
        var items = new List<IListItem>(layouts.Count);
        foreach (var layout in layouts)
        {
            var command = new ApplyFancyZonesLayoutCommand(layout, _monitor);
            var item = new FancyZonesLayoutListItem(command, layout, fallbackIcon);
            items.Add(item);
        }

        return [.. items];
    }
}
