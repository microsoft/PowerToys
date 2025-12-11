// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

[TestClass]
public class BookmarkManagerTests
{
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
        var bookmarkManager = new BookmarksManager(new MockBookmarkDataSource());
        var bookmarkAddedEventFired = false;
        bookmarkManager.BookmarkAdded += (bookmark) =>
        {
            bookmarkAddedEventFired = true;
            Assert.AreEqual("TestBookmark", bookmark.Name);
            Assert.AreEqual("C:\\TestPath", bookmark.Bookmark);
        };

        // Act
        var addedBookmark = bookmarkManager.Add("TestBookmark", "C:\\TestPath");

        // Assert
        var bookmarks = bookmarkManager.Bookmarks;
        Assert.AreEqual(1, bookmarks.Count);
        Assert.AreEqual(addedBookmark, bookmarks.First());
        Assert.IsTrue(bookmarkAddedEventFired);
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
