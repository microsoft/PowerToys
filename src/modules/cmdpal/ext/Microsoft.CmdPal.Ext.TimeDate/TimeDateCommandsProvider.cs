// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.CmdPal.Ext.TimeDate.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TimeDate;

public sealed partial class TimeDateCommandsProvider : CommandProvider
{
    private readonly CommandItem _command;
    private readonly CommandItem _customClocksCommand;
    private readonly SettingsManager _settingsManager;
    private readonly CustomClockManager _customClockManager;
    private readonly ClockUpdateService _clockUpdateService;
    private readonly TimeDateExtensionPage _timeDateExtensionPage;
    private readonly FallbackTimeDateItem _fallbackTimeDateItem;

    private readonly OnLoadDockBandItem _bandItem;
    private readonly WrappedDockItem _allClocksBandItem;
    private readonly WrappedDockItem _notificationCenterBandItem;

    // Keep a reference to the band so we can dispose it when the provider is disposed.
    private readonly Lock _customClockBandsLock = new();
    private readonly List<CustomClockDockBand> _customClockBands = [];
    private ICommandItem[] _customClockBandItems = [];

    private NowDockBand? _nowDockBand;
    private bool _disposed;

    public TimeDateCommandsProvider()
        : this(new SettingsManager(), new CustomClockManager(), new ClockUpdateService())
    {
    }

    internal TimeDateCommandsProvider(SettingsManager settingsManager, CustomClockManager customClockManager, ClockUpdateService clockUpdateService)
    {
        _settingsManager = settingsManager;
        _customClockManager = customClockManager;
        _clockUpdateService = clockUpdateService;
        _timeDateExtensionPage = new(_settingsManager, _customClockManager, _clockUpdateService);
        _fallbackTimeDateItem = new(_settingsManager);
        DisplayName = Resources.Microsoft_plugin_timedate_plugin_name;
        Id = "com.microsoft.cmdpal.builtin.datetime";
        _command = new CommandItem(_timeDateExtensionPage)
        {
            Icon = _timeDateExtensionPage.Icon,
            Title = Resources.Microsoft_plugin_timedate_plugin_name,
            MoreCommands = [new CommandContextItem(_settingsManager.Settings.SettingsPage)],
        };
        _customClocksCommand = new CommandItem(_timeDateExtensionPage.CustomClockListPage)
        {
            Icon = Icons.TimeIcon,
            Title = Resources.timedate_custom_clocks_manage,
        };

        Icon = _timeDateExtensionPage.Icon;
        Settings = _settingsManager.Settings;

        _nowDockBand = new NowDockBand(
            _settingsManager,
            _timeDateExtensionPage.CustomClockListPage,
            _clockUpdateService);

        _settingsManager.DockClockFormatsChanged += DockClockFormatsChanged;
        _settingsManager.Settings.SettingsChanged += SettingsChanged;

        _bandItem = new OnLoadDockBandItem(
            [_nowDockBand],
            CustomClockIds.LocalDockBand,
            CustomClockDisplay.GetDockBandTitle(Resources.timedate_custom_clock_local),
            _nowDockBand.StartUpdating,
            _nowDockBand.StopUpdating)
        {
            Icon = Icons.TimeDateExtIcon,
        };

        // Offered under the same ID as the top-level command, so pinning that
        // command resolves to this band instead of a generic wrapper around it.
        _allClocksBandItem = new WrappedDockItem(
            [new ListItem(_timeDateExtensionPage.CustomClockListPage) { Title = Resources.timedate_all_clocks, Icon = Icons.TimeIcon }],
            CustomClockListPage.PageId,
            CustomClockDisplay.GetDockBandTitle(Resources.timedate_all_clocks));

        RebuildCustomClockBands();
        _customClockManager.ClocksChanged += CustomClockManager_ClocksChanged;

        var notificationCenterBand = new NotificationCenterDockBand();
        _notificationCenterBandItem = new WrappedDockItem(
            [notificationCenterBand],
            "com.microsoft.cmdpal.timedate.notificationCenterBand",
            Resources.timedate_notification_center_band_title);
    }

    public override ICommandItem[] TopLevelCommands() => [_command, _customClocksCommand];

    public override IFallbackCommandItem[] FallbackCommands() => [_fallbackTimeDateItem];

    public override ICommandItem[] GetDockBands()
    {
        lock (_customClockBandsLock)
        {
            return [_bandItem, _allClocksBandItem, _notificationCenterBandItem, .. _customClockBandItems];
        }
    }

    // Only returns page-shaped items: this also backs pinning to the top level,
    // so a dock-shaped band must never be returned from here. Bands are declared
    // by GetDockBands instead.
    public override ICommandItem? GetCommandItem(string id)
    {
        if (id == CustomClockIds.LocalDetailPage && _nowDockBand is not null)
        {
            return CreateClockDetailItem(new CustomClock
            {
                Id = Guid.Empty,
                Title = Resources.timedate_custom_clock_local,
                TimeZoneId = CustomClock.CurrentTimeZoneId,
                TitleFormat = "t",
                SubtitleFormat = "d",
            });
        }

        foreach (var clock in _customClockManager.Clocks)
        {
            if (id == CustomClockIds.GetDetailPage(clock.Id))
            {
                return CreateClockDetailItem(clock);
            }
        }

        return null;
    }

    private void DockClockFormatsChanged(object? sender, EventArgs e) => _nowDockBand?.UpdateSettings(_settingsManager);

    private void SettingsChanged(object sender, Settings args) => _nowDockBand?.UpdateSettings(_settingsManager);

    private void CustomClockManager_ClocksChanged(object? sender, EventArgs e)
    {
        if (RebuildCustomClockBands())
        {
            RaiseItemsChanged();
        }
    }

    private bool RebuildCustomClockBands()
    {
        lock (_customClockBandsLock)
        {
            if (_disposed)
            {
                return false;
            }

            foreach (var band in _customClockBands)
            {
                band.Dispose();
            }

            _customClockBands.Clear();
            var dockItems = new List<ICommandItem>();
            foreach (var clock in _customClockManager.Clocks)
            {
                var clockBand = new CustomClockDockBand(clock, _customClockManager, _settingsManager, _clockUpdateService);
                var wrappedBand = new OnLoadDockBandItem(
                    [clockBand],
                    CustomClockIds.GetDockBand(clock.Id),
                    CustomClockDisplay.GetDockBandTitle(CustomClockDisplay.GetName(clock)),
                    clockBand.StartUpdating,
                    clockBand.StopUpdating)
                {
                    Icon = Icons.TimeDateExtIcon,
                };
                _customClockBands.Add(clockBand);
                dockItems.Add(wrappedBand);
            }

            _customClockBandItems = [.. dockItems];
            return true;
        }
    }

    private ListItem CreateClockDetailItem(CustomClock clock)
    {
        var item = new ListItem(new CustomClockDetailPage(_settingsManager, clock))
        {
            Icon = Icons.TimeIcon,
            Title = CustomClockDisplay.GetName(clock),
        };
        item.GetProperties()[WellKnownExtensionAttributes.DockCommandId] = CustomClockIds.GetDockBand(clock.Id);
        return item;
    }

    public override void Dispose()
    {
        lock (_customClockBandsLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var band in _customClockBands)
            {
                band.Dispose();
            }

            _customClockBands.Clear();
            _customClockBandItems = [];
        }

        _settingsManager.DockClockFormatsChanged -= DockClockFormatsChanged;
        _settingsManager.Settings.SettingsChanged -= SettingsChanged;
        _customClockManager.ClocksChanged -= CustomClockManager_ClocksChanged;
        _nowDockBand?.Dispose();
        _nowDockBand = null;
        _timeDateExtensionPage.Dispose();
        _clockUpdateService.Dispose();
        GC.SuppressFinalize(this);
        base.Dispose();
    }
}

#pragma warning disable SA1402 // File may only contain a single type

internal sealed partial class NotificationCenterDockBand : ListItem
{
    public NotificationCenterDockBand()
    {
        Icon = Icons.NotificationCenterIcon; // Notification bell
        Title = Resources.timedate_notification_center_band_title;
        Command = new OpenUrlCommand("ms-actioncenter:")
        {
            Id = "com.microsoft.cmdpal.timedate.notificationCenterBand",
            Name = Resources.timedate_show_notification_center_command_name,
            Result = CommandResult.Dismiss(),
        };
    }
}

#pragma warning restore SA1402 // File may only contain a single type
