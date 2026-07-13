// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.CmdPal.Common.Commands;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TimeDate.Pages;

#pragma warning disable SA1402 // Overview items are implementation details of this page.

internal sealed partial class CustomClockListPage : OnLoadDynamicListPage, IDisposable
{
    internal const string PageId = "com.microsoft.cmdpal.timedate.customClocks";
    private readonly CustomClockManager _clockManager;
    private readonly ISettingsInterface _settings;
    private readonly IDockClockSettings? _dockClockSettings;
    private readonly ClockUpdateService _clockUpdateService;
    private readonly CustomClockOverviewItem _localClockItem;
    private List<CustomClockOverviewItem> _clockItems = [];

    internal CustomClockListPage(CustomClockManager clockManager, ISettingsInterface settings, ClockUpdateService clockUpdateService)
    {
        _clockManager = clockManager;
        _settings = settings;
        _dockClockSettings = settings as IDockClockSettings;
        _clockUpdateService = clockUpdateService;
        _localClockItem = new CustomClockOverviewItem(
            new CustomClock
            {
                Id = Guid.Empty,
                Title = Resources.timedate_custom_clock_local,
                TimeZoneId = CustomClock.CurrentTimeZoneId,
                TitleFormat = "t",
                SubtitleFormat = "d",
            },
            settings,
            clockUpdateService);
        if (_dockClockSettings is not null)
        {
            _localClockItem.AddDockCustomizationCommand(_dockClockSettings);
        }

        RebuildItems();
        Id = PageId;
        Title = Resources.timedate_custom_clocks;
        Name = Title;
        Icon = Icons.TimeIcon;
        PlaceholderText = Resources.timedate_custom_clocks_search;
    }

    public override IListItem[] GetItems()
    {
        var items = new List<IListItem>
        {
            new Separator(Resources.timedate_custom_clock_local),
            _localClockItem,
        };
        items.Add(new Separator(Resources.timedate_custom_clocks));
        items.AddRange(_clockItems.Where(clock => string.IsNullOrWhiteSpace(SearchText) || CustomClockDisplay.GetName(clock.Clock).Contains(SearchText, StringComparison.CurrentCultureIgnoreCase)));
        items.Add(new ListItem(new EditCustomClockPage(_clockManager, _settings, null)) { Title = Resources.timedate_custom_clock_add, Icon = Icons.AddIcon });
        return [.. items];
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        SetSearchNoUpdate(newSearch);
        RaiseItemsChanged(-2);
    }

    public void Dispose()
    {
        if (_isLoaded)
        {
            Unloaded();
        }

        foreach (var item in _clockItems)
        {
            item.StopUpdating();
        }

        _localClockItem.StopUpdating();
    }

    private void ClockManager_ClocksChanged(object? sender, EventArgs e)
    {
        foreach (var item in _clockItems)
        {
            item.StopUpdating();
        }

        RebuildItems();
        if (_isLoaded)
        {
            StartUpdatingItems();
        }

        RaiseItemsChanged();
    }

    private void RebuildItems()
    {
        _clockItems = [.. _clockManager.Clocks.Select(clock =>
        {
            var item = new CustomClockOverviewItem(clock, _settings, _clockUpdateService);
            item.AddManagementCommands(_clockManager);
            return item;
        })];
    }

    private bool _isLoaded;

    protected override void Loaded()
    {
        _isLoaded = true;
        RebuildItems();
        _clockManager.ClocksChanged += ClockManager_ClocksChanged;
        _localClockItem.StartUpdating();
        StartUpdatingItems();
    }

    protected override void Unloaded()
    {
        _isLoaded = false;
        _clockManager.ClocksChanged -= ClockManager_ClocksChanged;
        foreach (var item in _clockItems)
        {
            item.StopUpdating();
        }

        _localClockItem.StopUpdating();
    }

    private void StartUpdatingItems()
    {
        foreach (var item in _clockItems)
        {
            item.StartUpdating();
        }
    }
}

internal sealed partial class CustomClockOverviewItem : ListItem
{
    private readonly ClockUpdateService _clockUpdateService;
    private readonly ISettingsInterface _settings;
    private readonly CompiledClockFormat _titleFormat;
    private readonly CompiledClockFormat _subtitleFormat;
    private readonly TimeZoneInfo? _explicitTimeZone;

    internal CustomClockOverviewItem(CustomClock clock, ISettingsInterface settings, ClockUpdateService clockUpdateService)
    {
        Clock = clock;
        _settings = settings;
        _clockUpdateService = clockUpdateService;
        _titleFormat = CustomClockDisplay.CompileFormat(clock.TitleFormat);
        _subtitleFormat = CustomClockDisplay.CompileFormat(clock.SubtitleFormat);
        _explicitTimeZone = CustomClockDisplay.ResolveExplicitTimeZone(clock);
        Icon = Icons.TimeIcon;
        Command = new CustomClockDetailPage(settings, clock);
        UpdateText();
    }

    internal CustomClock Clock { get; }

    internal void AddManagementCommands(CustomClockManager clockManager)
    {
        var editPage = new EditCustomClockPage(clockManager, _settings, Clock);
        MoreCommands =
        [
            new CommandContextItem(editPage),
            new CommandContextItem(new ConfirmableCommand
            {
                Command = new DeleteCustomClockCommand(clockManager, Clock.Id),
                Name = Resources.timedate_custom_clock_delete,
                Icon = Icons.DeleteIcon,
                ConfirmationTitle = Resources.timedate_custom_clock_delete_confirmation_title,
                ConfirmationMessage = Resources.timedate_custom_clock_delete_confirmation_message.Replace("{0}", CustomClockDisplay.GetName(Clock), StringComparison.Ordinal),
            }) { IsCritical = true },
        ];
    }

    internal void AddDockCustomizationCommand(IDockClockSettings dockClockSettings)
    {
        MoreCommands =
        [
            new CommandContextItem(new EditDefaultDockClockPage(dockClockSettings)),
        ];
    }

    internal void StartUpdating()
    {
        _clockUpdateService.Subscribe(this, ClockUpdateService_Tick, _titleFormat.RequiresSecondUpdates || _subtitleFormat.RequiresSecondUpdates);
    }

    internal void StopUpdating()
    {
        _clockUpdateService.Unsubscribe(this);
    }

    private void ClockUpdateService_Tick(object? sender, EventArgs e) => UpdateText();

    private void UpdateText()
    {
        var timeZone = _explicitTimeZone ?? TimeZoneInfo.Local;
        var now = CustomClockDisplay.GetCurrentTime(timeZone);
        var title = CustomClockDisplay.Format(now, _titleFormat, _settings);
        var subtitle = CustomClockDisplay.Format(now, _subtitleFormat, _settings);
        var name = CustomClockDisplay.GetName(Clock, timeZone, now);
        var offsetDifference = CustomClockDisplay.GetLocalOffsetDifference(now);

        // The overview needs to make the clock's identity visible alongside its live value.
        // When the dock title is intentionally empty, promote the subtitle so the list item
        // still leads with a current value.
        Title = string.IsNullOrEmpty(title) ? subtitle : title;
        Subtitle = string.IsNullOrEmpty(title)
            ? name
            : string.IsNullOrEmpty(subtitle)
                ? name
                : string.IsNullOrEmpty(offsetDifference)
                    ? $"{subtitle} · {name}"
                    : $"{subtitle} · {name} · {offsetDifference}";
    }
}

#pragma warning restore SA1402 // File may only contain a single type
