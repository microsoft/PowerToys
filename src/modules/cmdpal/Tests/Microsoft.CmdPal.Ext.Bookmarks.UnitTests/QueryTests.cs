// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

[TestClass]
public class QueryTests : CommandPaletteUnitTestBase
{
    [TestMethod]
    public void ValidateBookmarksCreation()
    {
        // Setup
        var bookmarks = Settings.CreateDefaultBookmarks();

        // Assert
        Assert.IsNotNull(bookmarks);
        Assert.IsNotNull(bookmarks.Data);
        Assert.AreEqual(2, bookmarks.Data.Count);
    }

    [TestMethod]
    public void ValidateBookmarkData()
    {
        // Setup
        var bookmarks = Settings.CreateDefaultBookmarks();

        // Act
        var microsoftBookmark = bookmarks.Data.FirstOrDefault(b => b.Name == "Microsoft");
        var githubBookmark = bookmarks.Data.FirstOrDefault(b => b.Name == "GitHub");

        // Assert
        Assert.IsNotNull(microsoftBookmark);
        Assert.AreEqual("https://www.microsoft.com", microsoftBookmark.Bookmark);

        Assert.IsNotNull(githubBookmark);
        Assert.AreEqual("https://github.com", githubBookmark.Bookmark);
    }
}
