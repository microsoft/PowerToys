// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.CmdPal.Core.Common.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Persistence;

namespace Microsoft.CmdPal.Ext.Bookmarks;

internal sealed partial class BookmarksManager : IDisposable, IBookmarksManager
{
    private readonly IBookmarkDataSource _dataSource;
    private readonly BookmarkJsonParser _parser = new();
    private readonly SupersedingAsyncGate _savingGate;
    private readonly Lock _lock = new();
    private BookmarksData _bookmarksData = new();

    public event Action<BookmarkData>? BookmarkAdded;

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

        lock (_lock)
        {
            _bookmarksData.Data.Add(newBookmark);
            _ = SaveChangesAsync();
            BookmarkAdded?.Invoke(newBookmark);
            return newBookmark;
        }
    }

    public bool Remove(Guid id)
    {
        lock (_lock)
        {
            var bookmark = _bookmarksData.Data.FirstOrDefault(b => b.Id == id);
            if (bookmark != null && _bookmarksData.Data.Remove(bookmark))
            {
                _ = SaveChangesAsync();
                BookmarkRemoved?.Invoke(bookmark);
                return true;
            }

            return false;
        }
    }

    public BookmarkData? Update(Guid id, string name, string bookmark)
    {
        lock (_lock)
        {
            var existingBookmark = _bookmarksData.Data.FirstOrDefault(b => b.Id == id);
            if (existingBookmark != null)
            {
                var updatedBookmark = existingBookmark with
                {
                    Name = name,
                    Bookmark = bookmark,
                };

                var index = _bookmarksData.Data.IndexOf(existingBookmark);
                _bookmarksData.Data[index] = updatedBookmark;

                _ = SaveChangesAsync();
                BookmarkUpdated?.Invoke(existingBookmark, updatedBookmark);
                return updatedBookmark;
            }

            return null;
        }
    }

    private void LoadBookmarksFromFile()
    {
        try
        {
            var jsonData = _dataSource.GetBookmarkData();
            _bookmarksData = _parser.ParseBookmarks(jsonData);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message);
        }
    }

    private Task WriteData(CancellationToken arg)
    {
        List<BookmarkData> dataToSave;
        lock (_lock)
        {
            dataToSave = _bookmarksData.Data.ToList();
        }

        try
        {
            var jsonData = _parser.SerializeBookmarks(new BookmarksData { Data = dataToSave });
            _dataSource.SaveBookmarkData(jsonData);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to save bookmarks: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private async Task SaveChangesAsync()
    {
        await _savingGate.ExecuteAsync(CancellationToken.None);
    }

    public void Dispose() => _savingGate.Dispose();
}
