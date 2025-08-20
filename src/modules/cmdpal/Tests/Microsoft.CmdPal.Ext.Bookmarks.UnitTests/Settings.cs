// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

public static class Settings
{
    public static Bookmarks CreateDefaultBookmarks()
    {
        var bookmarks = new Bookmarks();

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
