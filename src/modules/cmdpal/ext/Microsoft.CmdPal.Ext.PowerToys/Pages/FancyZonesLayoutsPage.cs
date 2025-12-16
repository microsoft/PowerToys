// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Commands;
using PowerToysExtension.Helpers;

namespace PowerToysExtension.Pages;

internal sealed partial class FancyZonesLayoutsPage : DynamicListPage
{
    private readonly CommandItem _emptyMessage;

    public FancyZonesLayoutsPage()
    {
        Icon = PowerToysResourcesHelper.IconFromSettingsIcon("FancyZones.png");
        Name = Title = "FancyZones Layouts";
        Id = "com.microsoft.cmdpal.powertoys.fancyzones.layouts";

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
        try
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

            _ = FancyZonesDataService.TryGetMonitors(out var monitors, out _);
            var fallbackIcon = PowerToysResourcesHelper.IconFromSettingsIcon("FancyZones.png");

            var items = new List<IListItem>(layouts.Count);
            foreach (var layout in layouts)
            {
                var defaultCommand = new ApplyFancyZonesLayoutCommand(layout, monitor: null);

                var item = new FancyZonesLayoutListItem(defaultCommand, layout, fallbackIcon)
                {
                    MoreCommands = BuildLayoutContext(layout, monitors),
                };

                items.Add(item);
            }

            return items.ToArray();
        }
        catch (Exception ex)
        {
            _emptyMessage.Subtitle = ex.Message;
            return Array.Empty<IListItem>();
        }
    }

    private static IContextItem[] BuildLayoutContext(FancyZonesLayoutDescriptor layout, IReadOnlyList<FancyZonesMonitorDescriptor> monitors)
    {
        var commands = new List<IContextItem>(monitors.Count);

        for (var i = 0; i < monitors.Count; i++)
        {
            var monitor = monitors[i];
            commands.Add(new CommandContextItem(new ApplyFancyZonesLayoutCommand(layout, monitor))
            {
                Title = string.Format(CultureInfo.CurrentCulture, "Apply to {0}", monitor.Title),
                Subtitle = monitor.Subtitle,
            });
        }

        return commands.ToArray();
    }
}
