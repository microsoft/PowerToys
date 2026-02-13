// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Bookmarks.Persistence;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

public static class Settings
{
    public static BookmarksData CreateDefaultBookmarks()
    {
        var bookmarks = new BookmarksData();

        // Add some test bookmarks
        bookmarks.Data.Add(new BookmarkData
        {
            Name = "Microsoft",
            Bookmark = "https://www.microsoft.com",
        });

        bookmarks.Data.Add(new BookmarkData
        {
            Name = "GitHub",
            Bookmark = "https://github.com",
        });

        return bookmarks;
    }
}
