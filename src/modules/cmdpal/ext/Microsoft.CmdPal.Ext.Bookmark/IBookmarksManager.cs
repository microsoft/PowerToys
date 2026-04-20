// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Bookmarks.Persistence;

namespace Microsoft.CmdPal.Ext.Bookmarks;

public interface IBookmarksManager
{
    event Action<BookmarkData>? BookmarkAdded;

    event Action<BookmarkData, BookmarkData>? BookmarkUpdated;

    event Action<BookmarkData>? BookmarkRemoved;

    IReadOnlyCollection<BookmarkData> Bookmarks { get; }

    BookmarkData Add(string name, string bookmark);

    BookmarkAddResult AddAsync(string name, string bookmark);

    bool Remove(Guid id);

    BookmarkData? Update(Guid id, string name, string bookmark);
}

public record BookmarkAddResult(
    BookmarkData Bookmark,
    Task SaveTask,
    Action ReloadCallback);
