// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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

    public BookmarkPlaceholderPage(string name, string url, string type)
    {
        Name = name;
        Icon = new IconInfo(UrlCommand.IconFromUrl(url, type));
        _bookmarkPlaceholder = new BookmarkPlaceholderForm(name, url, type);
    }
}
