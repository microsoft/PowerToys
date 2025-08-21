// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Bookmarks;

public class BookmarkJsonParser
{
    public BookmarkJsonParser()
    {
    }

    public Bookmarks ParseBookmarks(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Bookmarks();
        }

        try
        {
            var bookmarks = JsonSerializer.Deserialize<Bookmarks>(json, BookmarkSerializationContext.Default.Bookmarks);
            return bookmarks ?? new Bookmarks();
        }
        catch (JsonException ex)
        {
            ExtensionHost.LogMessage($"parse bookmark data failed. ex: {ex.Message}");
            return new Bookmarks();
        }
    }

    public string SerializeBookmarks(Bookmarks? bookmarks)
    {
        if (bookmarks == null)
        {
            return string.Empty;
        }

        return JsonSerializer.Serialize(bookmarks, BookmarkSerializationContext.Default.Bookmarks);
    }
}
