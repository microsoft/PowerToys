// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using Microsoft.CmdPal.Ext.TimeDate.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.UI.Dispatching;

namespace Microsoft.CmdPal.Ext.TimeDate;

public sealed partial class TimeDateCommandsProvider : CommandProvider
{
    private readonly CommandItem _command;
    private static readonly SettingsManager _settingsManager = new SettingsManager();
    private static readonly CompositeFormat MicrosoftPluginTimedatePluginDescription = System.Text.CompositeFormat.Parse(Resources.Microsoft_plugin_timedate_plugin_description);
    private static readonly TimeDateExtensionPage _timeDateExtensionPage = new(_settingsManager);
    private readonly FallbackTimeDateItem _fallbackTimeDateItem = new(_settingsManager);

    private readonly WrappedDockItem _bandItem;

    // Keep a reference to the band so we can dispose it when the provider is disposed.
    private NowDockBand? _nowDockBand;

    // Capture the UI dispatcher queue for the thread that constructs the provider. It may be
    // null in out-of-process extension hosts, so callers must handle that.
    private readonly DispatcherQueue? _uiDispatcherQueue;

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

        // Capture dispatcher for the current thread. This may be null in some extension hosting scenarios.
        _uiDispatcherQueue = DispatcherQueue.GetForCurrentThread();

        // NOTE: During NowDockBand construction, UpdateText() runs synchronously.
        // At that point wrappedBand is null → the callback is a no-op (intended).
        // The dock framework reads the initial Title/Subtitle via GetItems() on first render.
        // On every subsequent timer tick, wrappedBand is non-null → SetItems fires.
        // RaiseItemsChanged fires unconditionally even for same-instance updates — load-bearing.
        _nowDockBand = new NowDockBand(onUpdated: () =>
        {
            if (wrappedBand is not null)
            {
                if (_uiDispatcherQueue is not null)
                {
                    // Marshal the Items update back to the UI thread to avoid cross-thread updates.
                    _uiDispatcherQueue.TryEnqueue(() => wrappedBand.Items = [_nowDockBand!]);
                }
                else
                {
                    // If there's no dispatcher available (out-of-process), assign directly.
                    wrappedBand.Items = [_nowDockBand!];
                }
            }
        });

        wrappedBand = new WrappedDockItem(
            [_nowDockBand],
            "com.microsoft.cmdpal.timedate.dockBand",
            Resources.Microsoft_plugin_timedate_dock_band_title)
        {
            Icon = Icons.TimeDateExtIcon,
        };

        _bandItem = wrappedBand;
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
        return [_bandItem];
    }

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
    public override void Dispose()
    {
        if (_nowDockBand is not null)
        {
            try
            {
                _nowDockBand.Dispose();
            }
            catch
            {
            }

            _nowDockBand = null;
        }

        base.Dispose();
    }
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
}
