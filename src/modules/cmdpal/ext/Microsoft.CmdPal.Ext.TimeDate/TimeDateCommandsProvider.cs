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

public partial class TimeDateCommandsProvider : CommandProvider
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

    public override ICommandItem[] TopLevelCommands() => [_command, _bandItem];

    public override IFallbackCommandItem[] FallbackCommands() => [_fallbackTimeDateItem];
}
#pragma warning disable SA1402 // File may only contain a single type

internal sealed partial class NowDockBand : CommandItem
{
    public NowDockBand()
    {
        Command = new NoOpCommand() { Id = "com.microsoft.cmdpal.timedate.dockband" };
        UpdateText();
    }

    private void UpdateText()
    {
        var timeExtended = false; // timeLongFormat ?? settings.TimeWithSecond;
        var dateExtended = false; // dateLongFormat ?? settings.DateWithWeekday;
        var dateTimeNow = DateTime.Now;

        // var isSystemDateTime = true;
        // var result = new AvailableResult()
        // {
        //    Value = dateTimeNow.ToString(TimeAndDateHelper.GetStringFormat(FormatStringType.DateTime, timeExtended, dateExtended), CultureInfo.CurrentCulture),
        //    Label = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_DateAndTime", "Microsoft_plugin_timedate_Now"),
        //    AlternativeSearchTag = ResultHelper.SelectStringFromResources(isSystemDateTime, "Microsoft_plugin_timedate_SearchTagFormat"),
        //    IconType = ResultIconType.DateTime,
        // };
        Title = dateTimeNow.ToString(
            TimeAndDateHelper.GetStringFormat(FormatStringType.Time, timeExtended, dateExtended),
            CultureInfo.CurrentCulture);
        Subtitle = dateTimeNow.ToString(
            TimeAndDateHelper.GetStringFormat(FormatStringType.Date, timeExtended, dateExtended),
            CultureInfo.CurrentCulture);

        // TODO! This is a hack - we shouldn't need to set a Name on band items to get them to appear. We should be able to figure it out if there's a Icon OR HasText
        ((NoOpCommand)Command).Name = $"{Title}\n{Subtitle}";
    }
}
#pragma warning restore SA1402 // File may only contain a single type
