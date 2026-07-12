// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.CmdPal.Ext.TimeDate.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TimeDate;

public sealed partial class TimeDateCommandsProvider : CommandProvider
{
    private static readonly CompositeFormat MicrosoftPluginTimedatePluginDescription = System.Text.CompositeFormat.Parse(Resources.Microsoft_plugin_timedate_plugin_description);
    private readonly CommandItem _command;
    private readonly CommandItem _customClocksCommand;
    private readonly SettingsManager _settingsManager = new();
    private readonly CustomClockManager _customClockManager = new();
    private readonly ClockUpdateService _clockUpdateService = new();
    private readonly TimeDateExtensionPage _timeDateExtensionPage;
    private readonly FallbackTimeDateItem _fallbackTimeDateItem;

    private readonly WrappedDockItem _bandItem;
    private readonly WrappedDockItem _notificationCenterBandItem;

    // Keep a reference to the band so we can dispose it when the provider is disposed.
    private readonly List<CustomClockDockBand> _customClockBands = [];
    private WrappedDockItem[] _customClockBandItems = [];

    private NowDockBand? _nowDockBand;

    public TimeDateCommandsProvider()
    {
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

        WrappedDockItem? wrappedBand = null;

        // During NowDockBand construction, UpdateText() runs synchronously.
        // At that point wrappedBand is still null so the callback is a no-op.
        // On subsequent timer ticks, wrappedBand is non-null and SetItems fires
        // RaiseItemsChanged - the framework marshals to the UI thread in
        // DockBandViewModel.InitializeFromList via DoOnUiThread.
        _nowDockBand = new NowDockBand(
            _settingsManager,
            _timeDateExtensionPage.CustomClockListPage,
            onUpdated: () =>
            {
                if (wrappedBand is not null)
                {
                    wrappedBand.Items = [_nowDockBand!];
                }
            },
            clockUpdateService: _clockUpdateService);

        _settingsManager.DockClockFormatsChanged += DockClockFormatsChanged;
        _settingsManager.Settings.SettingsChanged += SettingsChanged;

        wrappedBand = new WrappedDockItem(
            [_nowDockBand],
            "com.microsoft.cmdpal.timedate.dockBand",
            Resources.Microsoft_plugin_timedate_dock_band_title)
        {
            Icon = Icons.TimeDateExtIcon,
        };

        _bandItem = wrappedBand;
        RebuildCustomClockBands();
        _customClockManager.ClocksChanged += CustomClockManager_ClocksChanged;

        var notificationCenterBand = new NotificationCenterDockBand();
        _notificationCenterBandItem = new WrappedDockItem(
            [notificationCenterBand],
            "com.microsoft.cmdpal.timedate.notificationCenterBand",
            Resources.timedate_notification_center_band_title);
    }

    private string GetTranslatedPluginDescription()
    {
        // The extra strings for the examples are required for correct translations.
        var timeExample = Resources.Microsoft_plugin_timedate_plugin_description_example_time + "::" + DateTime.Now.ToString("T", CultureInfo.CurrentCulture);
        var dayExample = Resources.Microsoft_plugin_timedate_plugin_description_example_day + "::" + DateTime.Now.ToString("d", CultureInfo.CurrentCulture);
        var calendarWeekExample = Resources.Microsoft_plugin_timedate_plugin_description_example_calendarWeek + "::" + DateTime.Now.ToString("d", CultureInfo.CurrentCulture);
        return string.Format(CultureInfo.CurrentCulture, MicrosoftPluginTimedatePluginDescription, Resources.Microsoft_plugin_timedate_plugin_description_example_day, dayExample, timeExample, calendarWeekExample);
    }

    public override ICommandItem[] TopLevelCommands() => [_command, _customClocksCommand];

    public override IFallbackCommandItem[] FallbackCommands() => [_fallbackTimeDateItem];

    public override ICommandItem[] GetDockBands()
    {
        return [_bandItem, _notificationCenterBandItem, .. _customClockBandItems];
    }

    public override ICommandItem? GetCommandItem(string id) => id == CustomClockListPage.PageId
        ? new WrappedDockItem(
            [new ListItem(_timeDateExtensionPage.CustomClockListPage) { Title = Resources.timedate_all_clocks, Icon = Icons.TimeIcon }],
            CustomClockListPage.PageId,
            Resources.timedate_all_clocks)
        : null;

    private void DockClockFormatsChanged(object? sender, EventArgs e) => _nowDockBand?.UpdateSettings(_settingsManager);

    private void SettingsChanged(object sender, Settings args) => _nowDockBand?.UpdateSettings(_settingsManager);

    private void CustomClockManager_ClocksChanged(object? sender, EventArgs e)
    {
        RebuildCustomClockBands();
        RaiseItemsChanged();
    }

    private void RebuildCustomClockBands()
    {
        foreach (var band in _customClockBands)
        {
            band.Dispose();
        }

        _customClockBands.Clear();
        var dockItems = new List<WrappedDockItem>();
        foreach (var clock in _customClockManager.Clocks)
        {
            WrappedDockItem? wrappedBand = null;
            CustomClockDockBand? clockBand = null;
            clockBand = new CustomClockDockBand(clock, _customClockManager, _settingsManager, _clockUpdateService, () =>
            {
                if (wrappedBand is not null && clockBand is not null)
                {
                    wrappedBand.Items = [clockBand];
                }
            });
            wrappedBand = new WrappedDockItem([clockBand], CustomClockIds.GetDockBand(clock.Id), CustomClockDisplay.GetName(clock)) { Icon = Icons.TimeDateExtIcon };
            _customClockBands.Add(clockBand);
            dockItems.Add(wrappedBand);
        }

        _customClockBandItems = [.. dockItems];
    }

    public override void Dispose()
    {
        _settingsManager.DockClockFormatsChanged -= DockClockFormatsChanged;
        _settingsManager.Settings.SettingsChanged -= SettingsChanged;
        _customClockManager.ClocksChanged -= CustomClockManager_ClocksChanged;
        _nowDockBand?.Dispose();
        _nowDockBand = null;
        foreach (var band in _customClockBands)
        {
            band.Dispose();
        }

        _customClockBands.Clear();
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
