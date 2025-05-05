﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
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
            Resources.ResourceManager.GetString("Microsoft_plugin_timedate_SearchTagDate", CultureInfo.CurrentCulture),
            Resources.ResourceManager.GetString("Microsoft_plugin_timedate_SearchTagDateNow", CultureInfo.CurrentCulture),

            Resources.ResourceManager.GetString("Microsoft_plugin_timedate_SearchTagTime", CultureInfo.CurrentCulture),
            Resources.ResourceManager.GetString("Microsoft_plugin_timedate_SearchTagTimeNow", CultureInfo.CurrentCulture),

            Resources.ResourceManager.GetString("Microsoft_plugin_timedate_SearchTagFormat", CultureInfo.CurrentCulture),
            Resources.ResourceManager.GetString("Microsoft_plugin_timedate_SearchTagFormatNow", CultureInfo.CurrentCulture),

            Resources.ResourceManager.GetString("Microsoft_plugin_timedate_SearchTagWeek", CultureInfo.CurrentCulture),
        };
    }

    public override void UpdateQuery(string query)
    {
        if (!_settingsManager.EnableFallbackItems || string.IsNullOrWhiteSpace(query) || !IsValidQuery(query))
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

    private bool IsValidQuery(string query)
    {
        if (_validOptions.Contains(query))
        {
            return true;
        }

        foreach (var option in _validOptions)
        {
            if (option == null)
            {
                continue;
            }

            var parts = option.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            if (parts.Any(part => string.Equals(part, query, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}
