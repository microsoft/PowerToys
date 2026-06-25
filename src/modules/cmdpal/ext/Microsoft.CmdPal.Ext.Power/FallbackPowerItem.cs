// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.Ext.Power.Pages;
using Microsoft.CmdPal.Ext.Power.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Power;

internal sealed partial class FallbackPowerItem : FallbackCommandItem
{
    private const string FallbackId = "com.microsoft.cmdpal.builtin.power.fallback";

    private readonly PowerListPage _listPage;
    private readonly string[] _queryTerms;

    public FallbackPowerItem(PowerListPage listPage)
        : base(listPage, Resources.power_fallback_title, FallbackId)
    {
        _listPage = listPage;
        Title = string.Empty;
        Subtitle = string.Empty;
        Icon = Icons.PowerIcon;
        _queryTerms = Resources.power_fallback_search_tags
            .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    public override void UpdateQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            Command = null;
            Title = string.Empty;
            Subtitle = string.Empty;
            return;
        }

        var normalized = query.Trim();
        var score = 0;
        foreach (var term in _queryTerms)
        {
            score = Math.Max(score, FuzzyStringMatcher.ScoreFuzzy(normalized, term));
        }

        if (score <= 0)
        {
            Command = null;
            Title = string.Empty;
            Subtitle = string.Empty;
            return;
        }

        Command = _listPage;
        Title = Resources.power_fallback_title;
        Subtitle = Resources.power_fallback_subtitle;
        Icon = Icons.PowerIcon;
    }
}
