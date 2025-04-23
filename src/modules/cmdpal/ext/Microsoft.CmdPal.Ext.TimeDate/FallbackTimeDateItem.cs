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
    private readonly HashSet<string> _validOptions;
    private SettingsManager _settingsManager;

    public FallbackTimeDateItem(SettingsManager settings)
         : base(new NoOpCommand(), Resources.Microsoft_plugin_timedate_fallback_display_title)
    {
        Title = string.Empty;
        Subtitle = string.Empty;
        _settingsManager = settings;
        _validOptions = new(StringComparer.OrdinalIgnoreCase)
        {
            ResultHelper.SelectStringFromResources(true, string.Empty, "Microsoft_plugin_timedate_SearchTagWeek"),
            ResultHelper.SelectStringFromResources(true, string.Empty, "Microsoft_plugin_timedate_SearchTagDate"),
            ResultHelper.SelectStringFromResources(true, string.Empty, "Microsoft_plugin_timedate_Year"),
            ResultHelper.SelectStringFromResources(true, string.Empty, "Microsoft_plugin_timedate_Now"),
            ResultHelper.SelectStringFromResources(true, string.Empty, "Microsoft_plugin_timedate_Time"),
        };
    }

    public override void UpdateQuery(string query)
    {
        if (!_settingsManager.EnableFallbackItems || string.IsNullOrWhiteSpace(query) || !_validOptions.Contains(query))
        {
            Title = string.Empty;
            Subtitle = string.Empty;
            Command = new NoOpCommand();
            return;
        }

        var availableResults = AvailableResultsList.GetList(false, _settingsManager);
        ListItem result = null;
        var maxScore = 0;

        foreach (var f in availableResults)
        {
            var score = f.Score(query, f.Label, f.AlternativeSearchTag);
            if (score > maxScore)
            {
                maxScore = score;
                result = f.ToListItem();
            }
        }

        if (result != null)
        {
            Title = result.Title;
            Subtitle = result.Subtitle;
            Icon = result.Icon;
        }
        else
        {
            Title = string.Empty;
            Subtitle = string.Empty;
            Command = new NoOpCommand();
        }
    }
}
