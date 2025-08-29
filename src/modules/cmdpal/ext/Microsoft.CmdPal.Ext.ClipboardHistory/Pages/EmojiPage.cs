// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Pages;

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
    }

    public override IListItem[] GetItems()
    {
        var items = _emojiListItems["SmileysAndEmotion"];
        return items.ToArray();
    }
}
