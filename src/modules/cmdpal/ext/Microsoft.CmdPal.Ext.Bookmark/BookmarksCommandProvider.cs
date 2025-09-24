﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CmdPal.Ext.Bookmarks.Pages;
using Microsoft.CmdPal.Ext.Bookmarks.Persistence;
using Microsoft.CmdPal.Ext.Bookmarks.Services;
using Microsoft.CommandPalette.Extensions;

namespace Microsoft.CmdPal.Ext.Bookmarks;

public sealed partial class BookmarksCommandProvider : CommandProvider
{
    private readonly IPlaceholderParser _placeholderParser = new PlaceholderParser();
    private readonly IBookmarksManager _bookmarksManager;
    private readonly IBookmarkResolver _commandResolver;
    private readonly IBookmarkIconLocator _iconLocator = new IconLocator();

    private readonly ListItem _addNewItem;
    private readonly Lock _bookmarksLock = new();

    private ICommandItem[] _commands = [];
    private List<BookmarkListItem> _bookmarks = [];
    private bool _isLoading;
    private bool _isLoaded;

    private static string StateJsonPath()
    {
        var directory = Utilities.BaseSettingsPath("Microsoft.CmdPal");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "bookmarks.json");
    }

    public static BookmarksCommandProvider CreateWithDefaultStore()
    {
        return new BookmarksCommandProvider(new BookmarksManager(new FileBookmarkDataSource(StateJsonPath())));
    }

    internal BookmarksCommandProvider(IBookmarksManager bookmarksManager)
    {
        ArgumentNullException.ThrowIfNull(bookmarksManager);
        _bookmarksManager = bookmarksManager;
        _bookmarksManager.BookmarkAdded += OnBookmarkAdded;
        _bookmarksManager.BookmarkRemoved += OnBookmarkRemoved;

        _commandResolver = new BookmarkResolver(_placeholderParser);

        Id = "Bookmarks";
        DisplayName = Resources.bookmarks_display_name;
        Icon = Icons.PinIcon;

        var addBookmarkPage = new AddBookmarkPage(null);
        addBookmarkPage.AddedCommand += (_, e) => _bookmarksManager.Add(e.Name, e.Bookmark);
        _addNewItem = new ListItem(addBookmarkPage);
    }

    private void OnBookmarkAdded(BookmarkData bookmarkData)
    {
        var newItem = new BookmarkListItem(bookmarkData, _bookmarksManager, _commandResolver, _iconLocator, _placeholderParser);
        lock (_bookmarksLock)
        {
            _bookmarks.Add(newItem);
        }

        NotifyChange();
    }

    private void OnBookmarkRemoved(BookmarkData bookmarkData)
    {
        lock (_bookmarksLock)
        {
            _bookmarks.RemoveAll(t => t.BookmarkId == bookmarkData.Id);
        }

        NotifyChange();
    }

    public override ICommandItem[] TopLevelCommands()
    {
        if (!_isLoaded && !_isLoading)
        {
            _isLoading = true;
            lock (_bookmarksLock)
            {
                _bookmarks = [.. _bookmarksManager.Bookmarks.Select(bookmark => new BookmarkListItem(bookmark, _bookmarksManager, _commandResolver, _iconLocator, _placeholderParser))];
            }

            _isLoaded = true;
            NotifyChange();
        }

        return _commands;
    }

    private void NotifyChange()
    {
        lock (_bookmarksLock)
        {
            _commands = [_addNewItem, .. _bookmarks];
        }

        RaiseItemsChanged();
    }
}
