// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Bookmarks;

internal sealed partial class BookmarkPlaceholderPage : ContentPage
{
    private readonly Lazy<IconInfo> _icon;
    private readonly FormContent _bookmarkPlaceholder;

    public override IContent[] GetContent() => [_bookmarkPlaceholder];

    public override IconInfo Icon { get => _icon.Value; set => base.Icon = value; }

    public BookmarkPlaceholderPage(BookmarkData data)
        : this(data.Name, data.Bookmark)
    {
    }

    public BookmarkPlaceholderPage(string name, string url)
    {
        Name = Properties.Resources.bookmarks_command_name_open;

        _bookmarkPlaceholder = new BookmarkPlaceholderForm(name, url);

        _icon = new Lazy<IconInfo>(() =>
        {
            ShellHelpers.ParseExecutableAndArgs(url, out var exe, out var args);
            var t = UrlCommand.GetIconForPath(exe);
            t.Wait();
            return t.Result;
        });
    }
}
