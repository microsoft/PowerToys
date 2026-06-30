// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Power.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Power.Helpers;

internal static class PowerFallbackQueryMatcher
{
    private static readonly string[] SupplementalTerms =
    [
        "powercfg",
        "power plan",
        "power mode",
        "energy saver",
        "battery saver",
    ];

    private static readonly Lazy<IReadOnlyList<string>> SearchTerms = new(BuildSearchTerms);

    internal static bool Matches(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        foreach (var term in SearchTerms.Value)
        {
            if (FuzzyStringMatcher.ScoreFuzzy(query, term) > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> BuildSearchTerms()
    {
        var terms = new List<string>
        {
            Resources.power_fallback_title,
            Resources.power_fallback_subtitle,
        };

        terms.AddRange(SupplementalTerms);

        foreach (var definition in PowerModeCatalog.All)
        {
            terms.Add(definition.Label);
            terms.Add(definition.ShortLabel);
        }

        terms.Add(Resources.power_section_power_plan);
        terms.Add(Resources.power_mode_energy_saver_title);

        return terms;
    }
}
