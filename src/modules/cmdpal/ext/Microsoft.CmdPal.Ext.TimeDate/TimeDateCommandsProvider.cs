// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.CmdPal.Ext.TimeDate.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TimeDate;

public sealed partial class TimeDateCommandsProvider : CommandProvider
{
    private readonly CommandItem _command;
    private static readonly SettingsManager _settingsManager = new SettingsManager();
    private static readonly CompositeFormat MicrosoftPluginTimedatePluginDescription = System.Text.CompositeFormat.Parse(Resources.Microsoft_plugin_timedate_plugin_description);
    private static readonly TimeDateExtensionPage _timeDateExtensionPage = new(_settingsManager);
    private readonly FallbackTimeDateItem _fallbackTimeDateItem = new(_settingsManager);

    private readonly WrappedDockItem _bandItem;
    private readonly WrappedDockItem _notificationCenterBandItem;

    // Keep a reference to the band so we can dispose it when the provider is disposed.
    private NowDockBand? _nowDockBand;

    public TimeDateCommandsProvider()
    {
        DisplayName = Resources.Microsoft_plugin_timedate_plugin_name;
        Id = "com.microsoft.cmdpal.builtin.datetime";
        _command = new CommandItem(_timeDateExtensionPage)
        {
            Icon = _timeDateExtensionPage.Icon,
            Title = Resources.Microsoft_plugin_timedate_plugin_name,
            MoreCommands = [new CommandContextItem(_settingsManager.Settings.SettingsPage)],
        };

        Icon = _timeDateExtensionPage.Icon;
        Settings = _settingsManager.Settings;

        WrappedDockItem? wrappedBand = null;

        // During NowDockBand construction, UpdateText() runs synchronously.
        // At that point wrappedBand is still null so the callback is a no-op.
        // On subsequent timer ticks, wrappedBand is non-null and SetItems fires
        // RaiseItemsChanged - the framework marshals to the UI thread in
        // DockBandViewModel.InitializeFromList via DoOnUiThread.
        _nowDockBand = new NowDockBand(_settingsManager.DockClockWithSecond, onUpdated: () =>
        {
            if (wrappedBand is not null)
            {
                wrappedBand.Items = [_nowDockBand!];
            }
        });

        // Re-read the dock clock preference whenever settings change so the band updates
        // live (no app restart required). The band ignores no-op changes internally.
        _settingsManager.Settings.SettingsChanged += OnSettingsChanged;

        wrappedBand = new WrappedDockItem(
            [_nowDockBand],
            "com.microsoft.cmdpal.timedate.dockBand",
            Resources.Microsoft_plugin_timedate_dock_band_title)
        {
            Icon = Icons.TimeDateExtIcon,
        };

        _bandItem = wrappedBand;

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

    public override ICommandItem[] TopLevelCommands() => [_command];

    public override IFallbackCommandItem[] FallbackCommands() => [_fallbackTimeDateItem];

    public override ICommandItem[] GetDockBands()
    {
        return [_bandItem, _notificationCenterBandItem];
    }

    private void OnSettingsChanged(object sender, Settings args)
    {
        _nowDockBand?.UpdateSettings(_settingsManager.DockClockWithSecond);
    }

    public override void Dispose()
    {
        _settingsManager.Settings.SettingsChanged -= OnSettingsChanged;
        _nowDockBand?.Dispose();
        _nowDockBand = null;
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
