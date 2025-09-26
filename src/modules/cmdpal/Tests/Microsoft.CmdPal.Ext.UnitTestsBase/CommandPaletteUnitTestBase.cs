﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.UnitTestBase;

public class CommandPaletteUnitTestBase
{
    private bool MatchesFilter(string filter, IListItem item) =>
        FuzzyStringMatcher.ScoreFuzzy(filter, item.Title) > 0 ||
        FuzzyStringMatcher.ScoreFuzzy(filter, item.Subtitle) > 0;

    public IListItem[] Query(string query, IListItem[] candidates)
    {
        IListItem[] listItems = candidates
            .Where(item => MatchesFilter(query, item))
            .ToArray();

        return listItems;
    }
}
