// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.UnitTestBase;

public class CommandPaletteUnitTestBase
{
    private bool MatchesFilter(string filter, IListItem item) => StringMatcher.FuzzySearch(filter, item.Title).Success || StringMatcher.FuzzySearch(filter, item.Subtitle).Success;

    public IListItem[] Query(string query, IListItem[] candidates)
    {
        IListItem[] listItems = candidates
            .Where(item => MatchesFilter(query, item))
            .ToArray();

        return listItems;
    }
}
