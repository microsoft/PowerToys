// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.UnitTestBase;

public class CommandPaletteUnitTestBase
{
    private bool MatchesFilter(string filter, IListItem item) =>
        FuzzyStringMatcher.ScoreFuzzy(filter, item.Title) > 0 ||
        FuzzyStringMatcher.ScoreFuzzy(filter, item.Subtitle) > 0;

    public IListItem[] Query(string query, IListItem[] candidates)
    {
        var listItems = candidates
            .Where(item => MatchesFilter(query, item))
            .ToArray();

        return listItems;
    }

    public async Task UpdatePageAndWaitForItems(IDynamicListPage page, Action modification)
    {
        // Add an event handler for the ItemsChanged event,
        // Then call the modification action,
        // and wait for the event to be raised.
        var tcs = new TaskCompletionSource<object>();

        TypedEventHandler<object, IItemsChangedEventArgs> handleItemsChanged = (object s, IItemsChangedEventArgs e) =>
        {
            tcs.TrySetResult(e);
        };

        page.ItemsChanged += handleItemsChanged;
        modification();

        await tcs.Task;
    }
}
