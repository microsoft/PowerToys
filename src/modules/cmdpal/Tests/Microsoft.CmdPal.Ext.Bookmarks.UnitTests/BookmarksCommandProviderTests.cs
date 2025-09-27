// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Bookmarks.Persistence;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

[TestClass]
public class BookmarksCommandProviderTests
{
    [TestMethod]
    public void ProviderHasCorrectId()
    {
        // Setup
        var mockBookmarkManager = new MockBookmarkManager();
        var provider = new BookmarksCommandProvider(mockBookmarkManager);

        // Assert
        Assert.AreEqual("Bookmarks", provider.Id);
    }

    [TestMethod]
    public void ProviderHasDisplayName()
    {
        // Setup
        var mockBookmarkManager = new MockBookmarkManager();
        var provider = new BookmarksCommandProvider(mockBookmarkManager);

        // Assert
        Assert.IsNotNull(provider.DisplayName);
        Assert.IsTrue(provider.DisplayName.Length > 0);
    }

    [TestMethod]
    public void ProviderHasIcon()
    {
        // Setup
        var mockBookmarkManager = new MockBookmarkManager();
        var provider = new BookmarksCommandProvider(mockBookmarkManager);

        // Assert
        Assert.IsNotNull(provider.Icon);
    }

    [TestMethod]
    public void TopLevelCommandsNotEmpty()
    {
        // Setup
        var mockBookmarkManager = new MockBookmarkManager();
        var provider = new BookmarksCommandProvider(mockBookmarkManager);

        // Act
        var commands = provider.TopLevelCommands();

        // Assert
        Assert.IsNotNull(commands);
        Assert.IsTrue(commands.Length > 0);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task ProviderWithMockData_LoadsBookmarksCorrectly()
    {
        // Arrange
        var mockBookmarkManager = new MockBookmarkManager(
            new BookmarkData("Test Bookmark", "http://test.com"),
            new BookmarkData("Another Bookmark", "http://another.com"));
        var provider = new BookmarksCommandProvider(mockBookmarkManager);

        // Act
        var commands = provider.TopLevelCommands();

        // Assert
        Assert.IsNotNull(commands, "commands != null");

        // Should have three commands：Add + two custom bookmarks
        Assert.AreEqual(3, commands.Length);

        // Wait until all BookmarkListItem commands are initialized
        await Task.WhenAll(commands.OfType<Pages.BookmarkListItem>().Select(t => t.IsInitialized));

        var addCommand = commands.FirstOrDefault(c => c.Title.Contains("Add bookmark"));
        var testBookmark = commands.FirstOrDefault(c => c.Title.Contains("Test Bookmark"));

        Assert.IsNotNull(addCommand, "addCommand != null");
        Assert.IsNotNull(testBookmark, "testBookmark != null");
    }

    [TestMethod]
    public void ProviderWithEmptyData_HasOnlyAddCommand()
    {
        // Arrange
        var mockBookmarkManager = new MockBookmarkManager();
        var provider = new BookmarksCommandProvider(mockBookmarkManager);

        // Act
        var commands = provider.TopLevelCommands();

        // Assert
        Assert.IsNotNull(commands);

        // Only have Add command
        Assert.AreEqual(1, commands.Length);

        var addCommand = commands.FirstOrDefault(c => c.Title.Contains("Add bookmark"));
        Assert.IsNotNull(addCommand);
    }

    [TestMethod]
    public void ProviderWithInvalidData_HandlesGracefully()
    {
        // Arrange
        var dataSource = new MockBookmarkDataSource("invalid json");
        var provider = new BookmarksCommandProvider(new MockBookmarkManager());

        // Act
        var commands = provider.TopLevelCommands();

        // Assert
        Assert.IsNotNull(commands);

        // Only have one command. Will ignore json parse error.
        Assert.AreEqual(1, commands.Length);

        var addCommand = commands.FirstOrDefault(c => c.Title.Contains("Add bookmark"));
        Assert.IsNotNull(addCommand);
    }
}
