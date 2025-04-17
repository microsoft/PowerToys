// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using Microsoft.CmdPal.Ext.TimeDate.Helpers;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TimeDate;

internal sealed partial class FallbackTimeDateItem : FallbackCommandItem
{
    private SettingsManager _settingsManager;

    public FallbackTimeDateItem(SettingsManager settings)
         : base(new NoOpCommand(), Resources.Microsoft_plugin_timedate_fallback_display_title)
    {
        Title = string.Empty;
        Subtitle = string.Empty;
        _settingsManager = settings;
    }

    public override void UpdateQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            Title = string.Empty;
            Subtitle = string.Empty;
            Command = new NoOpCommand();
            return;
        }

        var items = TimeDateCalculator.ExecuteSearch(_settingsManager, query);

        if (items.Count > 0 &&
            items[0].Title != Resources.Microsoft_plugin_timedate_InvalidInput_ErrorMessageTitle &&
            items[0].Title != Resources.Microsoft_plugin_timedate_ErrorResultTitle)
        {
            Title = items[0].Title;
            Subtitle = items[0].Subtitle;
            Icon = items[0].Icon;
        }
        else
        {
            Title = string.Empty;
            Subtitle = string.Empty;
            Command = new NoOpCommand();
        }
    }
}
