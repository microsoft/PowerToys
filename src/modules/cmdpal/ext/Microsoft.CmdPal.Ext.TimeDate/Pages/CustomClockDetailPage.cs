// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TimeDate.Pages;

internal sealed partial class CustomClockDetailPage : DynamicListPage
{
    private readonly ISettingsInterface _settings;
    private readonly CustomClock _clock;
    private readonly TimeZoneInfo? _explicitTimeZone;

    internal CustomClockDetailPage(ISettingsInterface settings, CustomClock clock)
    {
        _settings = settings;
        _clock = clock;
        _explicitTimeZone = CustomClockDisplay.ResolveExplicitTimeZone(clock);
        Id = clock.Id == Guid.Empty ? CustomClockIds.LocalDetailPage : CustomClockIds.GetDetailPage(clock.Id);
        var timeZone = _explicitTimeZone ?? TimeZoneInfo.Local;
        Title = CustomClockDisplay.GetName(clock, timeZone, CustomClockDisplay.GetCurrentTime(timeZone));
        Name = Resources.timedate_custom_clock_show;
        Icon = Icons.TimeIcon;
        PlaceholderText = Resources.Microsoft_plugin_timedate_placeholder_text;
        ShowDetails = true;
    }

    public override IListItem[] GetItems() => [.. TimeDateCalculator.ExecuteSearch(_settings, SearchText, CustomClockDisplay.GetCurrentTime(_explicitTimeZone ?? TimeZoneInfo.Local))];

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        SetSearchNoUpdate(newSearch);
        RaiseItemsChanged(-2);
    }
}
