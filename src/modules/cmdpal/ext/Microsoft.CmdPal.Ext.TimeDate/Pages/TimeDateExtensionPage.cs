// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TimeDate.Pages;

internal sealed partial class TimeDateExtensionPage : DynamicListPage, IDisposable
{
    private readonly ISettingsInterface _settingsManager;
    private readonly CustomClockListPage _customClockListPage;

    internal CustomClockListPage CustomClockListPage => _customClockListPage;

    public TimeDateExtensionPage(ISettingsInterface settingsManager)
        : this(settingsManager, new CustomClockManager(), new ClockUpdateService())
    {
    }

    public TimeDateExtensionPage(ISettingsInterface settingsManager, CustomClockManager customClockManager, ClockUpdateService clockUpdateService)
    {
        Icon = Icons.TimeDateExtIcon;
        Title = Resources.Microsoft_plugin_timedate_main_page_title;
        Name = Resources.Microsoft_plugin_timedate_main_page_name;
        PlaceholderText = Resources.Microsoft_plugin_timedate_placeholder_text;
        Id = "com.microsoft.cmdpal.timedate";
        _settingsManager = settingsManager;
        _customClockListPage = new CustomClockListPage(customClockManager, settingsManager, clockUpdateService);
        ShowDetails = true;
    }

    public override IListItem[] GetItems()
    {
        try
        {
            var results = TimeDateCalculator.ExecuteSearch(_settingsManager, SearchText);
            return [.. results];
        }
        catch (Exception)
        {
            // sometimes, user's input may not correct.
            // In most of the time, user may not have completed their input.
            // So, we need to clean the result.
            // But in that time, empty result may cause exception.
            // So, we need to add at least on item to user.
            return [ResultHelper.CreateInvalidInputErrorResult()];
        }
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        SetSearchNoUpdate(newSearch);
        RaiseItemsChanged(-2);
    }

    public void Dispose() => _customClockListPage.Dispose();
}
