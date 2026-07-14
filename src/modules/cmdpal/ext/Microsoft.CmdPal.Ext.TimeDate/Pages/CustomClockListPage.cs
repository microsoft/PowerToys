// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
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
    private readonly Lock _stateLock = new();
    private CustomClockOverviewItem[] _clockItems = [];
    private bool _isLoaded;
    private bool _disposed;

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

        _clockManager.ClocksChanged += ClockManager_ClocksChanged;
        lock (_stateLock)
        {
            RebuildItems();
        }

        Id = PageId;
        Title = Resources.timedate_custom_clocks;
        Name = Title;
        Icon = Icons.TimeIcon;
        PlaceholderText = Resources.timedate_custom_clocks_search;
    }

    public override IListItem[] GetItems()
    {
        CustomClockOverviewItem[] clockItems;
        lock (_stateLock)
        {
            clockItems = _clockItems;
        }

        var items = new List<IListItem>
        {
            new Separator(Resources.timedate_custom_clock_local),
            _localClockItem,
        };
        items.Add(new Separator(Resources.timedate_custom_clocks));
        items.AddRange(clockItems.Where(clock => string.IsNullOrWhiteSpace(SearchText) || CustomClockDisplay.GetName(clock.Clock).Contains(SearchText, StringComparison.CurrentCultureIgnoreCase)));
        items.Add(new ListItem(new EditCustomClockPage(_clockManager, _settings, null)) { Title = Resources.timedate_custom_clock_add, Icon = Icons.AddIcon });
        return [.. items];
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        RaiseItemsChanged(-2);
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _isLoaded = false;
            _clockManager.ClocksChanged -= ClockManager_ClocksChanged;
            foreach (var item in _clockItems)
            {
                item.StopUpdating();
            }

            _localClockItem.StopUpdating();
        }
    }

    private void ClockManager_ClocksChanged(object? sender, EventArgs e)
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            foreach (var item in _clockItems)
            {
                item.StopUpdating();
            }

            RebuildItems();
            if (_isLoaded)
            {
                StartUpdatingItems();
            }
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

    protected override void Loaded()
    {
        lock (_stateLock)
        {
            if (_disposed || _isLoaded)
            {
                return;
            }

            _isLoaded = true;
            _localClockItem.StartUpdating();
            StartUpdatingItems();
        }
    }

    protected override void Unloaded()
    {
        lock (_stateLock)
        {
            if (!_isLoaded)
            {
                return;
            }

            _isLoaded = false;
            foreach (var item in _clockItems)
            {
                item.StopUpdating();
            }

            _localClockItem.StopUpdating();
        }
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
    private readonly CompiledClockFormat? _copyFormat;
    private readonly TimeZoneInfo? _explicitTimeZone;
    private readonly CopyTextCommand _copyTitleCommand;
    private readonly CopyTextCommand _copySubtitleCommand;
    private readonly CopyCurrentClockFormatCommand? _copyCustomFormatCommand;

    internal CustomClockOverviewItem(CustomClock clock, ISettingsInterface settings, ClockUpdateService clockUpdateService)
    {
        Clock = clock;
        _settings = settings;
        _clockUpdateService = clockUpdateService;
        _titleFormat = CustomClockDisplay.CompileFormat(clock.TitleFormat);
        _subtitleFormat = CustomClockDisplay.CompileFormat(clock.SubtitleFormat);
        _copyFormat = string.IsNullOrEmpty(clock.CopyFormat) ? null : CustomClockDisplay.CompileFormat(clock.CopyFormat);
        _explicitTimeZone = CustomClockDisplay.ResolveExplicitTimeZone(clock);
        _copyTitleCommand = new CopyTextCommand(string.Empty)
        {
            Name = CustomClockFormatOptions.GetCopyCommandName(settings, clock.TitleFormat),
        };
        _copySubtitleCommand = new CopyTextCommand(string.Empty)
        {
            Name = CustomClockFormatOptions.GetCopyCommandName(settings, clock.SubtitleFormat),
        };
        _copyCustomFormatCommand = _copyFormat is null
            ? null
            : new CopyCurrentClockFormatCommand(CustomClockFormatOptions.GetCopyCommandName(settings, clock.CopyFormat), GetCustomCopyText);
        Icon = Icons.TimeIcon;
        Command = new CustomClockDetailPage(settings, clock);
        MoreCommands =
        [
            new CommandContextItem(_copyTitleCommand),
            new CommandContextItem(_copySubtitleCommand),
            .. _copyCustomFormatCommand is null ? [] : new CommandContextItem[] { new(_copyCustomFormatCommand) },
        ];
        UpdateText();
    }

    internal CustomClock Clock { get; }

    internal CopyTextCommand CopyTitleCommand => _copyTitleCommand;

    internal CopyTextCommand CopySubtitleCommand => _copySubtitleCommand;

    internal CopyCurrentClockFormatCommand? CopyCustomFormatCommand => _copyCustomFormatCommand;

    internal void AddManagementCommands(CustomClockManager clockManager)
    {
        var editPage = new EditCustomClockPage(clockManager, _settings, Clock);
        MoreCommands =
        [
            .. MoreCommands,
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
            .. MoreCommands,
            new CommandContextItem(new EditDefaultDockClockPage(dockClockSettings)),
        ];
    }

    internal void StartUpdating()
    {
        UpdateText();
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
        _copyTitleCommand.Text = title;
        _copySubtitleCommand.Text = subtitle;
    }

    private string GetCustomCopyText()
    {
        var format = _copyFormat;
        if (format is null)
        {
            return string.Empty;
        }

        var timeZone = _explicitTimeZone ?? TimeZoneInfo.Local;
        var now = CustomClockDisplay.GetCurrentTime(timeZone);
        return CustomClockDisplay.Format(now, format, _settings);
    }
}

#pragma warning restore SA1402 // File may only contain a single type
