// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleQueryPage : ListPage
{
    private readonly IListItem[] _items;

    internal SampleQueryPage(string query)
    {
        Id = "com.microsoft.cmdpal.sample.fallback.page.result";
        Name = "Sample query page";
        Title = $"Sample query: {query}";
        SearchText = query;
        _items =
        [
            new ListItem(new ShowToastCommand(query)
            {
                Id = "com.microsoft.cmdpal.sample.fallback.page.toast",
                Name = "Show query",
            })
            {
                Title = query,
                Subtitle = "This text is the main query.",
            },
        ];
    }

    public override IListItem[] GetItems() => _items;
}
