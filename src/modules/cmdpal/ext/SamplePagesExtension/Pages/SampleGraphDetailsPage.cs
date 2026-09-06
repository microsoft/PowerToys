// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

internal sealed partial class SampleGraphDetailsPage : ListPage
{
    private readonly IListItem[] _items;

    public SampleGraphDetailsPage()
    {
        Name = "Open";
        Title = "Graphs in details";
        Icon = new IconInfo("\uE9D9");
        ShowDetails = true;
        using var samples = new SampleGraphsPage();
        var names = new[] { "Line graph", "Scalar usage bar", "Stacked usage bar", "Doughnut graph" };
        _items = samples.Graphs.Select((graph, index) => new ListItem(new NoOpCommand())
        {
            Title = names[index],
            Subtitle = "Snapshot displayed as native details content",
            Details = new Details { Title = names[index], Content = [graph] },
        }).ToArray();
    }

    public override IListItem[] GetItems() => _items;
}
