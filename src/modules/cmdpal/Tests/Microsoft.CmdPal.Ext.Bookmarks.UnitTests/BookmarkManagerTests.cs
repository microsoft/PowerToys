// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Bookmarks.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

[TestClass]
public class BookmarkManagerTests
{
    [TestMethod]
    [DataRow(nameof(IBookmarksManager.Add))]
    [DataRow(nameof(IBookmarksManager.AddRange))]
    [DataRow(nameof(IBookmarksManager.Remove))]
    [DataRow(nameof(IBookmarksManager.Update))]
    [Timeout(15000)]
    public void Mutations_AllowConcurrentReadsDuringNotification(string operation)
    {
        using var manager = new BookmarksManager(new MockBookmarkDataSource());
        var existing = manager.Add("Existing", "existing");
        Task<BookmarkData[]> read = null;
        var readCompletedDuringNotification = false;
        void ReadDuringNotification()
        {
            read = Task.Run(() => manager.Bookmarks.ToArray());
            readCompletedDuringNotification = read.Wait(3000);
        }

        manager.BookmarksAdded += _ => ReadDuringNotification();
        manager.BookmarkRemoved += _ => ReadDuringNotification();
        manager.BookmarkUpdated += (_, _) => ReadDuringNotification();

        switch (operation)
        {
            case nameof(IBookmarksManager.Add):
                manager.Add("Added", "added");
                break;
            case nameof(IBookmarksManager.AddRange):
                manager.AddRange([("Imported", "imported")]);
                break;
            case nameof(IBookmarksManager.Remove):
                manager.Remove(existing.Id);
                break;
            case nameof(IBookmarksManager.Update):
                manager.Update(existing.Id, "Updated", "updated");
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }

        Assert.IsNotNull(read);
        Assert.IsTrue(read.Wait(3000), "The reader must finish after the notification returns.");
        Assert.IsTrue(readCompletedDuringNotification, "Notifications must not hold the data lock needed by provider reads.");
        CollectionAssert.AreEqual(manager.Bookmarks.ToArray(), read.Result);
    }

    [TestMethod]
    [Timeout(15000)]
    public void AddRange_CannotBeOverwrittenByAnEarlierPendingSave()
    {
        using var saveStarted = new ManualResetEventSlim();
        using var finishSave = new ManualResetEventSlim();
        var attempts = 0;
        var saveWasReleased = false;
        var savedCounts = new ConcurrentQueue<int>();
        var parser = new BookmarkJsonParser();
        var dataSource = new MockBookmarkDataSource
        {
            BeforeSave = () =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                {
                    saveStarted.Set();
                    saveWasReleased = finishSave.Wait(10000);
                }
            },
            AfterSave = json => savedCounts.Enqueue(parser.ParseBookmarks(json).Data.Count),
        };
        using var manager = new BookmarksManager(dataSource);
        manager.Add("Existing", "existing");
        Assert.IsTrue(saveStarted.Wait(5000));

        Exception batchError = null;
        var imported = 0;
        var batch = new Thread(() =>
        {
            try
            {
                imported = manager.AddRange([("Imported", "imported")]);
            }
            catch (Exception ex)
            {
                batchError = ex;
            }
        }) { IsBackground = true };
        batch.Start();
        try
        {
            // Wait for lock contention or completion, not merely for the worker to start.
            Assert.IsTrue(SpinWait.SpinUntil(() => !batch.IsAlive || (batch.ThreadState & ThreadState.WaitSleepJoin) != 0, 5000));
            Assert.IsTrue(batch.IsAlive, "The batch must wait for the pending save.");
            Assert.AreEqual(1, Volatile.Read(ref attempts));
        }
        finally
        {
            finishSave.Set();
            Assert.IsTrue(batch.Join(5000));
        }

        Assert.IsTrue(saveWasReleased, "The pending save must be released before its wait times out.");
        Assert.IsNull(batchError);
        Assert.AreEqual(1, imported);
        int[] expectedCounts = [1, 2];
        CollectionAssert.AreEqual(expectedCounts, savedCounts.ToArray());
        using var reloaded = new BookmarksManager(dataSource);
        CollectionAssert.AreEqual(manager.Bookmarks.ToArray(), reloaded.Bookmarks.ToArray());
    }

    [TestMethod]
    public void AddRange_RejectsInvalidEntriesBeforeChangingTheCollection()
    {
        var dataSource = new MockBookmarkDataSource();
        using var manager = new BookmarksManager(dataSource);
        manager.BookmarksAdded += _ => Assert.Fail("An invalid batch must not notify.");

        Assert.ThrowsException<ArgumentException>(() => manager.AddRange(
            [("Valid", "https://example.com"), ("Invalid", string.Empty)]));

        Assert.AreEqual(0, manager.Bookmarks.Count);
        Assert.AreEqual(0, dataSource.SaveCount);
    }

    [TestMethod]
    public void BookmarkManager_CanBeInstantiated()
    {
        // Arrange & Act
        var bookmarkManager = new BookmarksManager(new MockBookmarkDataSource());

        // Assert
        Assert.IsNotNull(bookmarkManager);
    }

    [TestMethod]
    public void BookmarkManager_InitialBookmarksEmpty()
    {
        // Arrange
        var bookmarkManager = new BookmarksManager(new MockBookmarkDataSource());

        // Act
        var bookmarks = bookmarkManager.Bookmarks;

        // Assert
        Assert.IsNotNull(bookmarks);
        Assert.AreEqual(0, bookmarks.Count);
    }

    [TestMethod]
    public void BookmarkManager_InitialBookmarksCorruptedData()
    {
        // Arrange
        var json = "@*>$ß Corrupted data. Hey, this is not JSON!";
        var bookmarkManager = new BookmarksManager(new MockBookmarkDataSource(json));

        // Act
        var bookmarks = bookmarkManager.Bookmarks;

        // Assert
        Assert.IsNotNull(bookmarks);
        Assert.AreEqual(0, bookmarks.Count);
    }

    [TestMethod]
    public void BookmarkManager_InitializeWithExistingData()
    {
        // Arrange
        const string json = """
                            {
                                "Data":[
                                    {"Id":"d290f1ee-6c54-4b01-90e6-d701748f0851","Name":"Bookmark1","Bookmark":"C:\\Path1"},
                                    {"Id":"c4a760a4-5b63-4c9e-b8b3-2c3f5f3e6f7a","Name":"Bookmark2","Bookmark":"D:\\Path2"}
                                ]
                            }
                            """;
        var bookmarkManager = new BookmarksManager(new MockBookmarkDataSource(json));

        // Act
        var bookmarks = bookmarkManager.Bookmarks?.ToList();

        // Assert
        Assert.IsNotNull(bookmarks);
        Assert.AreEqual(2, bookmarks.Count);

        Assert.AreEqual("Bookmark1", bookmarks[0].Name);
        Assert.AreEqual("C:\\Path1", bookmarks[0].Bookmark);
        Assert.AreEqual(Guid.Parse("d290f1ee-6c54-4b01-90e6-d701748f0851"), bookmarks[0].Id);

        Assert.AreEqual("Bookmark2", bookmarks[1].Name);
        Assert.AreEqual("D:\\Path2", bookmarks[1].Bookmark);
        Assert.AreEqual(Guid.Parse("c4a760a4-5b63-4c9e-b8b3-2c3f5f3e6f7a"), bookmarks[1].Id);
    }

    [TestMethod]
    public void BookmarkManager_InitializeWithLegacyData_GeneratesIds()
    {
        // Arrange
        const string json = """
                            {
                                "Data":
                                [
                                    { "Name":"Bookmark1", "Bookmark":"C:\\Path1" },
                                    { "Name":"Bookmark2", "Bookmark":"D:\\Path2" }
                                ]
                            }
                            """;
        var bookmarkManager = new BookmarksManager(new MockBookmarkDataSource(json));

        // Act
        var bookmarks = bookmarkManager.Bookmarks?.ToList();

        // Assert
        Assert.IsNotNull(bookmarks);
        Assert.AreEqual(2, bookmarks.Count);

        Assert.AreEqual("Bookmark1", bookmarks[0].Name);
        Assert.AreEqual("C:\\Path1", bookmarks[0].Bookmark);
        Assert.AreNotEqual(Guid.Empty, bookmarks[0].Id);

        Assert.AreEqual("Bookmark2", bookmarks[1].Name);
        Assert.AreEqual("D:\\Path2", bookmarks[1].Bookmark);
        Assert.AreNotEqual(Guid.Empty, bookmarks[1].Id);

        Assert.AreNotEqual(bookmarks[0].Id, bookmarks[1].Id);
    }

    [TestMethod]
    public void BookmarkManager_AddBookmark_WorksCorrectly()
    {
        // Arrange
        using var bookmarkManager = new BookmarksManager(new MockBookmarkDataSource());
        var addedEventCount = 0;
        bookmarkManager.BookmarksAdded += (added) =>
        {
            addedEventCount++;
            Assert.AreEqual(1, added.Count);
            var bookmark = added[0];
            Assert.AreEqual("TestBookmark", bookmark.Name);
            Assert.AreEqual("C:\\TestPath", bookmark.Bookmark);
        };

        // Act
        var addedBookmark = bookmarkManager.Add("TestBookmark", "C:\\TestPath");

        // Assert
        var bookmarks = bookmarkManager.Bookmarks;
        Assert.AreEqual(1, bookmarks.Count);
        Assert.AreEqual(addedBookmark, bookmarks.First());
        Assert.AreEqual(1, addedEventCount);
    }

    [TestMethod]
    public void BookmarkManager_RemoveBookmark_WorksCorrectly()
    {
        // Arrange
        var bookmarkManager = new BookmarksManager(new MockBookmarkDataSource());
        var addedBookmark = bookmarkManager.Add("TestBookmark", "C:\\TestPath");
        var bookmarkRemovedEventFired = false;
        bookmarkManager.BookmarkRemoved += (bookmark) =>
        {
            bookmarkRemovedEventFired = true;
            Assert.AreEqual(addedBookmark, bookmark);
        };

        // Act
        var removeResult = bookmarkManager.Remove(addedBookmark.Id);

        // Assert
        var bookmarks = bookmarkManager.Bookmarks;
        Assert.IsTrue(removeResult);
        Assert.AreEqual(0, bookmarks.Count);
        Assert.IsTrue(bookmarkRemovedEventFired);
    }

    [TestMethod]
    public void BookmarkManager_UpdateBookmark_WorksCorrectly()
    {
        // Arrange
        var bookmarkManager = new BookmarksManager(new MockBookmarkDataSource());
        var addedBookmark = bookmarkManager.Add("TestBookmark", "C:\\TestPath");
        var bookmarkUpdatedEventFired = false;
        bookmarkManager.BookmarkUpdated += (data, bookmarkData) =>
        {
            bookmarkUpdatedEventFired = true;
            Assert.AreEqual(addedBookmark, data);
            Assert.AreEqual("UpdatedBookmark", bookmarkData.Name);
            Assert.AreEqual("D:\\UpdatedPath", bookmarkData.Bookmark);
        };

        // Act
        var updatedBookmark = bookmarkManager.Update(addedBookmark.Id, "UpdatedBookmark", "D:\\UpdatedPath");

        // Assert
        var bookmarks = bookmarkManager.Bookmarks;
        Assert.IsNotNull(updatedBookmark);
        Assert.AreEqual(1, bookmarks.Count);
        Assert.AreEqual(updatedBookmark, bookmarks.First());
        Assert.AreEqual("UpdatedBookmark", updatedBookmark.Name);
        Assert.AreEqual("D:\\UpdatedPath", updatedBookmark.Bookmark);
        Assert.IsTrue(bookmarkUpdatedEventFired);
    }

    [TestMethod]
    public void BookmarkManager_LegacyData_IdsArePersistedAcrossLoads()
    {
        // Arrange
        const string json = """
                            {
                                "Data": 
                                [
                                    { "Name": "C:\\","Bookmark": "C:\\" },
                                    { "Name": "Bing.com","Bookmark": "https://bing.com" }
                                ]
                            }
                            """;

        var dataSource = new MockBookmarkDataSource(json);

        // First load: IDs should be generated for legacy entries
        var manager1 = new BookmarksManager(dataSource);
        var firstLoad = manager1.Bookmarks.ToList();
        Assert.AreEqual(2, firstLoad.Count);
        Assert.AreNotEqual(Guid.Empty, firstLoad[0].Id);
        Assert.AreNotEqual(Guid.Empty, firstLoad[1].Id);

        // Keep a name->id map to be insensitive to ordering
        var firstIdsByName = firstLoad.ToDictionary(b => b.Name, b => b.Id);

        // Wait deterministically for async persistence to complete
        var wasSaved = dataSource.WaitForSave(1, 5000);
        Assert.IsTrue(wasSaved, "Data was not saved within the expected time.");

        // Second load: should read back the same IDs from persisted data
        var manager2 = new BookmarksManager(dataSource);
        var secondLoad = manager2.Bookmarks.ToList();
        Assert.AreEqual(2, secondLoad.Count);

        var secondIdsByName = secondLoad.ToDictionary(b => b.Name, b => b.Id);

        foreach (var kvp in firstIdsByName)
        {
            Assert.IsTrue(secondIdsByName.ContainsKey(kvp.Key), $"Missing bookmark '{kvp.Key}' after reload.");
            Assert.AreEqual(kvp.Value, secondIdsByName[kvp.Key], $"Bookmark '{kvp.Key}' upgraded ID was not persisted across loads.");
        }
    }
}
