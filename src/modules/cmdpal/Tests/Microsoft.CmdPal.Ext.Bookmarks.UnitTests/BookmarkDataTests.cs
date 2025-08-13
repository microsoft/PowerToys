// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

[TestClass]
public class BookmarkDataTests
{
    [TestMethod]
    public void BookmarkDataCanBeCreated()
    {
        // Act
        var bookmark = new BookmarkData
        {
            Name = "Test Site",
            Bookmark = "https://test.com",
        };

        // Assert
        Assert.AreEqual("Test Site", bookmark.Name);
        Assert.AreEqual("https://test.com", bookmark.Bookmark);
    }

    [TestMethod]
    public void BookmarkDataHandlesEmptyValues()
    {
        // Act
        var bookmark = new BookmarkData
        {
            Name = string.Empty,
            Bookmark = string.Empty,
        };

        // Assert
        Assert.AreEqual(string.Empty, bookmark.Name);
        Assert.AreEqual(string.Empty, bookmark.Bookmark);
    }

    [TestMethod]
    public void BookmarkDataWebUrlDetection()
    {
        // Act
        var webBookmark = new BookmarkData
        {
            Name = "Test Site",
            Bookmark = "https://test.com",
        };

        var nonWebBookmark = new BookmarkData
        {
            Name = "Local File",
            Bookmark = "C:\\temp\\file.txt",
        };

        // Assert
        Assert.IsTrue(webBookmark.IsWebUrl());
        Assert.IsFalse(nonWebBookmark.IsWebUrl());
    }
}
