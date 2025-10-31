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
using Windows.Foundation.Collections;

namespace Microsoft.CmdPal.Ext.TimeDate;

public partial class TimeDateCommandsProvider : CommandProvider, IExtendedAttributesProvider
{
    private readonly CommandItem _command;
    private static readonly SettingsManager _settingsManager = new SettingsManager();
    private static readonly CompositeFormat MicrosoftPluginTimedatePluginDescription = System.Text.CompositeFormat.Parse(Resources.Microsoft_plugin_timedate_plugin_description);
    private static readonly TimeDateExtensionPage _timeDateExtensionPage = new(_settingsManager);
    private readonly FallbackTimeDateItem _fallbackTimeDateItem = new(_settingsManager);

    private readonly CommandItem _bandItem;

    public TimeDateCommandsProvider()
    {
        DisplayName = Resources.Microsoft_plugin_timedate_plugin_name;
        Id = "DateTime";
        _command = new CommandItem(_timeDateExtensionPage)
        {
            Icon = _timeDateExtensionPage.Icon,
            Title = Resources.Microsoft_plugin_timedate_plugin_name,
            Subtitle = GetTranslatedPluginDescription(),
            MoreCommands = [new CommandContextItem(_settingsManager.Settings.SettingsPage)],
        };

        Icon = _timeDateExtensionPage.Icon;
        Settings = _settingsManager.Settings;

        _bandItem = new NowDockBand();
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

    public IDictionary<string, object> GetProperties()
    {
        return new PropertySet()
        {
            { "DockBands", new ICommandItem[] { _bandItem } },
        };
    }
}
#pragma warning disable SA1402 // File may only contain a single type

internal sealed partial class NowDockBand : CommandItem
{
    private CopyTextCommand _copyTimeCommand;
    private CopyTextCommand _copyDateCommand;

    public NowDockBand()
    {
        Command = new NoOpCommand() { Id = "com.microsoft.cmdpal.timedate.dockband" };
        _copyTimeCommand = new CopyTextCommand(string.Empty) { Name = "Copy Time" };
        _copyDateCommand = new CopyTextCommand(string.Empty) { Name = "Copy Date" };
        MoreCommands = [
            new CommandContextItem(_copyTimeCommand),
            new CommandContextItem(_copyDateCommand)
        ];
        UpdateText();

        // Create a timer to update the time every minute
        System.Timers.Timer timer = new(60000); // 60000 ms = 1 minute

        // but we want it to tick on the minute, so calculate the initial delay
        var now = DateTime.Now;
        timer.Interval = 60000 - ((now.Second * 1000) + now.Millisecond);

        // then after the first tick, set it to 60 seconds
        timer.Elapsed += Timer_ElapsedFirst;
        timer.Start();
    }

    private void Timer_ElapsedFirst(object sender, System.Timers.ElapsedEventArgs e)
    {
        // After the first tick, set the interval to 60 seconds
        if (sender is System.Timers.Timer timer)
        {
            timer.Interval = 60000;
            timer.Elapsed -= Timer_ElapsedFirst;
            timer.Elapsed += Timer_Elapsed;

            // Still call the callback, so that we update the clock
            Timer_Elapsed(sender, e);
        }
    }

    private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
        UpdateText();
    }

    private void UpdateText()
    {
        var timeExtended = false; // timeLongFormat ?? settings.TimeWithSecond;
        var dateExtended = false; // dateLongFormat ?? settings.DateWithWeekday;
        var dateTimeNow = DateTime.Now;

        var timeString = dateTimeNow.ToString(
            TimeAndDateHelper.GetStringFormat(FormatStringType.Time, timeExtended, dateExtended),
            CultureInfo.CurrentCulture);
        var dateString = dateTimeNow.ToString(
            TimeAndDateHelper.GetStringFormat(FormatStringType.Date, timeExtended, dateExtended),
            CultureInfo.CurrentCulture);

        Title = timeString;
        Subtitle = dateString;

        //// TODO! This is a hack - we shouldn't need to set a Name on band items to get them to appear. We should be able to figure it out if there's a Icon OR HasText
        // ((NoOpCommand)Command).Name = $"{Title}\n{Subtitle}";
        _copyDateCommand.Text = dateString;
        _copyTimeCommand.Text = timeString;
    }
}
#pragma warning restore SA1402 // File may only contain a single type
