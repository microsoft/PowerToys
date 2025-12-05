// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public sealed partial class LiveSectionBuilder
{
    public IListItem[] Items { get; set; } = [];

    public string SectionTitle { get; set; } = string.Empty;

    private ListItem CreateSectionListItem()
    {
        return new ListItem
        {
            Section = SectionTitle,
            Command = null,
        };
    }

    public LiveSectionBuilder(string sectionName, IListItem[] items)
    {
        Items = items;
        SectionTitle = sectionName;
    }

    public LiveSectionBuilder()
    {
    }

    public IListItem[] ToListItems()
    {
        var listItems = Items.ToList();
        if (listItems.Count > 0)
        {
            listItems.Insert(0, CreateSectionListItem());
        }

        return [.. listItems];
    }
}
