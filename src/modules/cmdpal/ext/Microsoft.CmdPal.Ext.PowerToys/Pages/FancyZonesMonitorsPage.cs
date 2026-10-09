// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;
using PowerToysExtension.Properties;

namespace PowerToysExtension.Pages;

internal sealed partial class FancyZonesMonitorsPage : DynamicListPage
{
    private static readonly CompositeFormat CurrentLayoutFormat = CompositeFormat.Parse(Resources.FancyZones_CurrentLayout_Format);

    private readonly CommandItem _emptyMessage;

    public FancyZonesMonitorsPage()
    {
        Icon = PowerToysResourcesHelper.IconFromSettingsIcon("FancyZones.png");
        Name = Title = Resources.FancyZones_Monitors_Title;
        Id = "com.microsoft.cmdpal.powertoys.fancyzones.monitors";

        _emptyMessage = new CommandItem()
        {
            Title = Resources.FancyZones_NoMonitorsFound_Title,
            Subtitle = Resources.FancyZones_NoMonitorsFound_Subtitle,
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

            var layoutDescription = FancyZonesDataService.TryGetAppliedLayoutForMonitor(monitor.Data, out var applied) && applied is not null
                ? string.Format(CultureInfo.CurrentCulture, CurrentLayoutFormat, applied.Value.Type)
                : Resources.FancyZones_CurrentLayout_Unknown;

            var item = new FancyZonesMonitorListItem(monitor, layoutDescription, monitorIcon);
            items.Add(item);
        }

        return [.. items];
    }
}
