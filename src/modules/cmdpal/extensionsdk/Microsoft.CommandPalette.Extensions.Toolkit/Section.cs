// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public sealed partial class Section : IEnumerable<IListItem>
{
    public IListItem[] Items { get; set; } = [];

    public string SectionTitle { get; set; } = string.Empty;

    private Separator CreateSectionListItem()
    {
        return new Separator(SectionTitle);
    }

    public Section(string sectionName, IListItem[] items)
    {
        SectionTitle = sectionName;
        var listItems = items.ToList();

        if (listItems.Count > 0)
        {
            listItems.Insert(0, CreateSectionListItem());
            Items = [.. listItems];
        }
    }

    public Section()
    {
    }

    public IEnumerator<IListItem> GetEnumerator() => Items.ToList().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
