// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class ListSection
{
    public virtual IListItem[] Items { get; set; } = [];

    public virtual string SectionTitle { get; set; } = string.Empty;

    private ListItem SectionListItem => new ListItem
    {
        Section = SectionTitle,
        Command = null,
    };

    public ListSection(string sectionName, IListItem[] items)
    {
        this.Items = items;
        this.SectionTitle = sectionName;
    }

    public ListSection()
    {
    }

    public IListItem[] ToListItems()
    {
        var listItems = Items.ToList();
        if (listItems.Count > 0)
        {
            listItems.Insert(0, SectionListItem);
        }

        return [.. listItems];
    }
}
