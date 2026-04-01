// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Bookmarks.Persistence;

namespace Microsoft.CmdPal.Ext.Bookmarks.Commands;

internal sealed partial class DeleteBookmarkCommand : InvokableCommand
{
    private readonly BookmarkData _bookmark;
    private readonly IBookmarksManager _bookmarksManager;

    public DeleteBookmarkCommand(BookmarkData bookmark, IBookmarksManager bookmarksManager)
    {
        ArgumentNullException.ThrowIfNull(bookmark);
        ArgumentNullException.ThrowIfNull(bookmarksManager);

        _bookmark = bookmark;
        _bookmarksManager = bookmarksManager;
        Name = Resources.bookmarks_delete_name;
        Icon = Icons.DeleteIcon;
    }

    public override CommandResult Invoke()
    {
        _bookmarksManager.Remove(_bookmark.Id);
        return CommandResult.GoHome();
    }
}
