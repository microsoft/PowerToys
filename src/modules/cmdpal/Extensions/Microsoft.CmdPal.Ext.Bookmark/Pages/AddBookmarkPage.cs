// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Bookmarks.Persistence;
using Microsoft.CommandPalette.Extensions;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.Bookmarks.Pages;

internal sealed partial class AddBookmarkPage : ContentPage
{
    internal event TypedEventHandler<object, BookmarkData>? AddedCommand
    {
        add => _addBookmarkForm.AddedCommand += value;
        remove => _addBookmarkForm.AddedCommand -= value;
    }

    private readonly AddBookmarkForm _addBookmarkForm;

    public AddBookmarkPage(BookmarkData? bookmark)
    {
        var name = bookmark?.Name ?? string.Empty;
        var url = bookmark?.Bookmark ?? string.Empty;

        Icon = Icons.BookmarkIcon;
        var isAdd = string.IsNullOrEmpty(name) && string.IsNullOrEmpty(url);
        Title = isAdd ? Resources.bookmarks_add_title : Resources.bookmarks_edit_name;
        Name = isAdd ? Resources.bookmarks_add_name : Resources.bookmarks_edit_name;
        _addBookmarkForm = new AddBookmarkForm(bookmark);
    }

    public override IContent[] GetContent() => [_addBookmarkForm];
}
