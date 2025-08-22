// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.CmdPal.Ext.TimeDate.Pages;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TimeDate;

public partial class TimeDateCommandsProvider : CommandProvider, IRecipient<UpdatePinyinSettingsMessage>
{
    private readonly CommandItem _command;
    private static readonly SettingsManager _settingsManager = new SettingsManager();
    private static readonly CompositeFormat MicrosoftPluginTimedatePluginDescription = System.Text.CompositeFormat.Parse(Resources.Microsoft_plugin_timedate_plugin_description);
    private readonly TimeDateExtensionPage _timeDateExtensionPage;
    private readonly FallbackTimeDateItem _fallbackTimeDateItem;
    private bool _isPinYinInput;

    public TimeDateCommandsProvider()
        : this(false)
    {
    }

    public TimeDateCommandsProvider(bool isPinYinInput)
    {
        DisplayName = Resources.Microsoft_plugin_timedate_plugin_name;
        Id = "DateTime";
        _timeDateExtensionPage = new TimeDateExtensionPage(_settingsManager, isPinYinInput);

        _command = new CommandItem(_timeDateExtensionPage)
        {
            Icon = _timeDateExtensionPage.Icon,
            Title = Resources.Microsoft_plugin_timedate_plugin_name,
            Subtitle = GetTranslatedPluginDescription(),
            MoreCommands = [new CommandContextItem(_settingsManager.Settings.SettingsPage)],
        };

        Icon = _timeDateExtensionPage.Icon;
        Settings = _settingsManager.Settings;
        _isPinYinInput = isPinYinInput;
        _fallbackTimeDateItem = new FallbackTimeDateItem(_settingsManager, isPinYinInput);

        // Register for PinYin settings updates
        WeakReferenceMessenger.Default.Register<UpdatePinyinSettingsMessage>(this);
    }

    public void UpdatePinYinInput(bool isPinYinInput)
    {
        _isPinYinInput = isPinYinInput;
        _fallbackTimeDateItem.UpdatePinYinInput(isPinYinInput);
        _timeDateExtensionPage.UpdatePinyinInputSetting(isPinYinInput);
    }

    public void Receive(UpdatePinyinSettingsMessage message)
    {
        UpdatePinYinInput(message.IsPinyinInput);
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
}
