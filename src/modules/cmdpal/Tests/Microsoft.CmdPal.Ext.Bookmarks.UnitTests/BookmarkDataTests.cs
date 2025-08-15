// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

[TestClass]
public class BookmarkDataTests
{
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

        var placeholderBookmark = new BookmarkData
        {
            Name = "Placeholder",
            Bookmark = "{Placeholder}",
        };

        // Assert
        Assert.IsTrue(webBookmark.IsWebUrl());
        Assert.IsFalse(webBookmark.IsPlaceholder);
        Assert.IsFalse(nonWebBookmark.IsWebUrl());
        Assert.IsFalse(nonWebBookmark.IsPlaceholder);

        Assert.IsTrue(placeholderBookmark.IsPlaceholder);
    }
}
