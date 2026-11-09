// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Bookmarks.Persistence;

namespace Microsoft.CmdPal.Ext.Bookmarks;

public interface IBookmarksManager
{
    event Action<IReadOnlyList<BookmarkData>>? BookmarksAdded;

    event Action<BookmarkData, BookmarkData>? BookmarkUpdated;

    event Action<BookmarkData>? BookmarkRemoved;

    IReadOnlyCollection<BookmarkData> Bookmarks { get; }

    BookmarkData Add(string name, string bookmark);

    /// <summary>Adds a batch with fresh IDs, skipping exact name/target duplicates, and persists it before notifying listeners.</summary>
    int AddRange(IReadOnlyCollection<(string Name, string Bookmark)> bookmarks);

    bool Remove(Guid id);

    BookmarkData? Update(Guid id, string name, string bookmark);
}
