// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Pages;
using Microsoft.CmdPal.Ext.Bookmarks.Persistence;
using Microsoft.CmdPal.Ext.Bookmarks.Services;
using Microsoft.CommandPalette.Extensions.Toolkit;
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

    [TestMethod]
    public void GetDockBands_UsesBookmarkTitleAndFallbackIconImmediately()
    {
        // Arrange
        var bookmark = new BookmarkData("Test Bookmark", "http://test.com");
        var provider = new BookmarksCommandProvider(new MockBookmarkManager(bookmark));

        // Act
        var bands = provider.GetDockBands();

        // Assert
        Assert.IsNotNull(bands);
        Assert.AreEqual(1, bands.Length);
        Assert.AreEqual(bookmark.Name, bands[0].Title);
        Assert.IsNotNull(bands[0].Icon);
    }

    [TestMethod]
    public void BookmarkDockItem_UsesFallbackIconWhileBookmarkIsLoading()
    {
        // Arrange
        var bookmark = new BookmarkData("Test Bookmark", "http://test.com");
        var bookmarkItem = new BookmarkListItem(
            bookmark,
            new MockBookmarkManager(bookmark),
            new BlockingBookmarkResolver(),
            new IconLocator(),
            new PlaceholderParser(),
            asBand: true);
        using var dockItem = new BookmarkDockItem(bookmarkItem, "test-id");

        // Assert
        Assert.AreSame(Icons.BookmarksExtensionIcon, dockItem.Icon);
    }

    [TestMethod]
    [Timeout(5000)]
    public async Task BookmarkDockItem_TracksFinalTitleAndIcon()
    {
        // Arrange
        var bookmark = new BookmarkData("Test Bookmark", "http://test.com");
        var bookmarkItem = new BookmarkListItem(
            bookmark,
            new MockBookmarkManager(bookmark),
            new BookmarkResolver(new PlaceholderParser()),
            new IconLocator(),
            new PlaceholderParser(),
            asBand: true);
        using var dockItem = new BookmarkDockItem(bookmarkItem, "test-id");
        var finalIcon = new IconInfo("\uE774");

        // Act
        await bookmarkItem.IsInitialized;
        bookmarkItem.Title = "Final title";
        bookmarkItem.Icon = Icons.Reloading;

        // Assert
        Assert.AreEqual("Final title", dockItem.Title);
        Assert.AreSame(Icons.BookmarksExtensionIcon, dockItem.Icon);

        // Act
        bookmarkItem.Icon = finalIcon;

        // Assert
        Assert.AreSame(finalIcon, dockItem.Icon);
    }

    private sealed class BlockingBookmarkResolver : IBookmarkResolver
    {
        private readonly TaskCompletionSource<(bool Success, Classification Result)> _completion = new();

        public Task<(bool Success, Classification Result)> TryClassifyAsync(string input, CancellationToken cancellationToken = default) =>
            _completion.Task;

        public Classification ClassifyOrUnknown(string input) => Classification.Unknown(input);
    }
}
