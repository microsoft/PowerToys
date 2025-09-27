// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Bookmarks.Persistence;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

#pragma warning disable CS0067

internal sealed class MockBookmarkManager : IBookmarksManager
{
    private readonly List<BookmarkData> _bookmarks;

    public event Action<BookmarkData> BookmarkAdded;

    public event Action<BookmarkData, BookmarkData> BookmarkUpdated;

    public event Action<BookmarkData> BookmarkRemoved;

    public IReadOnlyCollection<BookmarkData> Bookmarks => _bookmarks;

    public BookmarkData Add(string name, string bookmark) => throw new NotImplementedException();

    public bool Remove(Guid id) => throw new NotImplementedException();

    public BookmarkData Update(Guid id, string name, string bookmark) => throw new NotImplementedException();

    public MockBookmarkManager(params IEnumerable<BookmarkData> bookmarks)
    {
        _bookmarks = [.. bookmarks];
    }
}
