// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Pages;

#pragma warning disable SA1402 // File may only contain a single type

public sealed partial class EmojiPage : ListPage
{
    private readonly Dictionary<string, List<ListItem>> _emojiListItems;

    public EmojiPage()
    {
        Icon = new IconInfo("\uE899");
        Name = "Emoji"; // Properties.Resources.emoji_page_name;
        Id = "com.microsoft.cmdpal.emoji";
        ShowDetails = false;
        GridProperties = new SmallGridLayout();

        _emojiListItems = new Dictionary<string, List<ListItem>>();
        foreach (var group in EmojiDict.Data)
        {
            var listItems = group.Value.Select(s => new EmojiListItem(s.Emoji) { Title = s.Name }).Cast<ListItem>().ToList();
            _emojiListItems.Add(group.Key, listItems);
        }

        var filters = new EmojiFilters();
        filters.PropChanged += Filters_PropChanged;
        Filters = filters;
    }

    public override IListItem[] GetItems()
    {
        if (Filters is null)
        {
            return [];
        }

        if (Filters.CurrentFilterId == "all")
        {
            return _emojiListItems.Values.SelectMany(x => x).ToArray();
        }

        if (_emojiListItems.TryGetValue(Filters.CurrentFilterId, out var items))
        {
            return items.ToArray();
        }

        return [];
    }

    private void Filters_PropChanged(object sender, IPropChangedEventArgs args) => RaiseItemsChanged();
}

public partial class EmojiFilters : Filters
{
    private List<IFilterItem> _allFilters = new()
    {
        new Filter() { Id = "all", Name = "All Emoji" },
        new Separator(),
    };

    public EmojiFilters()
    {
        CurrentFilterId = EmojiDict.Data.Keys.First();

        foreach (var group in EmojiDict.Data)
        {
            _allFilters.Add(new Filter() { Id = group.Key, Name = group.Key });
        }
    }

    public override IFilterItem[] GetFilters()
    {
        return _allFilters.ToArray();
    }
}
#pragma warning restore SA1402 // File may only contain a single type
