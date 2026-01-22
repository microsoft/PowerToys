// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;

namespace Microsoft.CmdPal.Ext.Bookmarks.Persistence;

public class BookmarkJsonParser
{
    public BookmarkJsonParser()
    {
    }

    public BookmarksData ParseBookmarks(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new BookmarksData();
        }

        try
        {
            var bookmarks = JsonSerializer.Deserialize<BookmarksData>(json, BookmarkSerializationContext.Default.BookmarksData);
            return bookmarks ?? new BookmarksData();
        }
        catch (JsonException ex)
        {
            ExtensionHost.LogMessage($"parse bookmark data failed. ex: {ex.Message}");
            return new BookmarksData();
        }
    }

    public string SerializeBookmarks(BookmarksData? bookmarks)
    {
        if (bookmarks == null)
        {
            return string.Empty;
        }

        return JsonSerializer.Serialize(bookmarks, BookmarkSerializationContext.Default.BookmarksData);
    }
}
