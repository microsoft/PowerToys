// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.ClipboardHistory.Pages;

public sealed partial class EmojiPage : ListPage
{
    public EmojiPage()
    {
        Icon = new IconInfo("\uE899");
        Name = "Emoji"; // Properties.Resources.emoji_page_name;
        Id = "com.microsoft.cmdpal.emoji";
        ShowDetails = false;
        GridProperties = new SmallGridLayout();
    }

    public override IListItem[] GetItems()
    {
        var items = EmojiDict.Data["SmileysAndEmotion"].Select(s => new EmojiListItem(s.Emoji) { Title = s.Name });
        return items.ToArray();
    }
}
