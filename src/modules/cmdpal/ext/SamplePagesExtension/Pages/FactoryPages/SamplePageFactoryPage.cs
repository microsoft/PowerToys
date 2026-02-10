// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension.Pages;

public sealed partial class SamplePageFactoryPage : ListPage
{
    private readonly List<IListItem> _items;

    public SamplePageFactoryPage()
    {
        _items = [
            new ListItem(new SamplePageFactoryCommand(this)) { Title = "Capture world state" },
            new ListItem(new EvilSamplePageFactoryCommand(this)) { Title = "Capture world state", Subtitle = "...but take a sweet time creating that" },
        ];
    }

    public override IListItem[] GetItems() => [.. _items];

    internal void AddPage(Page page)
    {
        _items.Add(new ListItem(page));
        RaiseItemsChanged();
    }
}
