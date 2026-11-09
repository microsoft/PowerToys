// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Common.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Persistence;

namespace Microsoft.CmdPal.Ext.Bookmarks;

internal sealed partial class BookmarksManager : IDisposable, IBookmarksManager
{
    private readonly IBookmarkDataSource _dataSource;
    private readonly BookmarkJsonParser _parser = new();
    private readonly SupersedingAsyncGate _savingGate;
    private readonly Lock _lock = new();

    // Keep mutations and their notifications ordered while allowing reads during callbacks.
    private readonly Lock _mutationLock = new();
    private BookmarksData _bookmarksData = new();

    public event Action<IReadOnlyList<BookmarkData>>? BookmarksAdded;

    public event Action<BookmarkData, BookmarkData>? BookmarkUpdated; // old, new

    public event Action<BookmarkData>? BookmarkRemoved;

    public IReadOnlyCollection<BookmarkData> Bookmarks
    {
        get
        {
            lock (_lock)
            {
                return _bookmarksData.Data.ToList().AsReadOnly();
            }
        }
    }

    public BookmarksManager(IBookmarkDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        _dataSource = dataSource;
        _savingGate = new SupersedingAsyncGate(WriteData);
        LoadBookmarksFromFile();
    }

    public BookmarkData Add(string name, string bookmark)
    {
        var newBookmark = new BookmarkData(name, bookmark);
        Logger.LogDebug($"BookmarksManager.Add: created bookmark id={newBookmark.Id} name='{name}' value='{bookmark}'");

        using var mutation = _mutationLock.EnterScope();
        lock (_lock)
        {
            _bookmarksData.Data.Add(newBookmark);
            _ = SaveChangesAsync();
        }

        BookmarksAdded?.Invoke([newBookmark]);
        return newBookmark;
    }

    public int AddRange(IReadOnlyCollection<(string Name, string Bookmark)> bookmarks)
    {
        ArgumentNullException.ThrowIfNull(bookmarks);
        using var mutation = _mutationLock.EnterScope();
        List<BookmarkData> added = [];
        lock (_lock)
        {
            var existing = _bookmarksData.Data.Select(bookmark => (bookmark.Name, bookmark.Bookmark)).ToHashSet();
            foreach (var bookmark in bookmarks)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(bookmark.Bookmark);
                if (existing.Add((bookmark.Name, bookmark.Bookmark)))
                {
                    added.Add(new BookmarkData(bookmark.Name, bookmark.Bookmark));
                }
            }

            if (added.Count == 0)
            {
                return 0;
            }

            var updated = new BookmarksData { Data = [.. _bookmarksData.Data, .. added] };
            _dataSource.SaveBookmarkData(_parser.SerializeBookmarks(updated));
            _bookmarksData = updated;
        }

        BookmarksAdded?.Invoke(added);
        return added.Count;
    }

    public bool Remove(Guid id)
    {
        using var mutation = _mutationLock.EnterScope();
        BookmarkData? bookmark;
        lock (_lock)
        {
            bookmark = _bookmarksData.Data.FirstOrDefault(b => b.Id == id);
            if (bookmark is null || !_bookmarksData.Data.Remove(bookmark))
            {
                return false;
            }

            _ = SaveChangesAsync();
        }

        BookmarkRemoved?.Invoke(bookmark);
        return true;
    }

    public BookmarkData? Update(Guid id, string name, string bookmark)
    {
        using var mutation = _mutationLock.EnterScope();
        BookmarkData? existingBookmark;
        BookmarkData updatedBookmark;
        lock (_lock)
        {
            existingBookmark = _bookmarksData.Data.FirstOrDefault(b => b.Id == id);
            if (existingBookmark is null)
            {
                return null;
            }

            updatedBookmark = existingBookmark with
            {
                Name = name,
                Bookmark = bookmark,
            };

            var index = _bookmarksData.Data.IndexOf(existingBookmark);
            _bookmarksData.Data[index] = updatedBookmark;

            _ = SaveChangesAsync();
        }

        BookmarkUpdated?.Invoke(existingBookmark, updatedBookmark);
        return updatedBookmark;
    }

    private void LoadBookmarksFromFile()
    {
        try
        {
            var jsonData = _dataSource.GetBookmarkData();
            var bookmarksData = _parser.ParseBookmarks(jsonData);

            // Upgrade old bookmarks if necessary
            // Pre .95 versions did not assign IDs to bookmarks
            var upgraded = false;
            for (var index = 0; index < bookmarksData.Data.Count; index++)
            {
                var bookmark = bookmarksData.Data[index];
                if (bookmark.Id == Guid.Empty)
                {
                    bookmarksData.Data[index] = bookmark with { Id = Guid.NewGuid() };
                    upgraded = true;
                }
            }

            lock (_lock)
            {
                _bookmarksData = bookmarksData;
            }

            // LOAD BEARING: Save upgraded data back to file
            // This ensures that old bookmarks are not repeatedly upgraded on each load,
            // as the hotkeys and aliases are tied to the generated bookmark IDs.
            if (upgraded)
            {
                _ = SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message);
        }
    }

    private Task WriteData(CancellationToken arg)
    {
        lock (_lock)
        {
            // Serialize all writes so a pending save cannot overwrite a completed batch.
            try
            {
                _dataSource.SaveBookmarkData(_parser.SerializeBookmarks(_bookmarksData));
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to save bookmarks", ex);
            }
        }

        return Task.CompletedTask;
    }

    private async Task SaveChangesAsync()
    {
        await _savingGate.ExecuteAsync(CancellationToken.None);
    }

    public void Dispose() => _savingGate.Dispose();
}
