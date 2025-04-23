// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Bookmarks;

internal sealed partial class BookmarkPlaceholderPage : ContentPage
{
    private readonly FormContent _bookmarkPlaceholder;

    public override IContent[] GetContent() => [_bookmarkPlaceholder];

    public BookmarkPlaceholderPage(BookmarkData data)
        : this(data.Name, data.Bookmark, data.Type)
    {
    }

    public BookmarkPlaceholderPage(string name, string url, BookmarkType type)
    {
        Name = name;
        Icon = IconHelper.CreateIcon(url, type);
        _bookmarkPlaceholder = new BookmarkPlaceholderForm(name, url, type);
    }
}
