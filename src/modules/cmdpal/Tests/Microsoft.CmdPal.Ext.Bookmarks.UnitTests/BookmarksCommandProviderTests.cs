// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Pages;
using Microsoft.CmdPal.Ext.Bookmarks.Persistence;
using Microsoft.CmdPal.Ext.Bookmarks.Services;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.ApplicationModel.DataTransfer;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

[TestClass]
public class BookmarksCommandProviderTests
{
    [TestMethod]
    public void ProviderHasCorrectId()
    {
        // Setup
        var mockBookmarkManager = new MockBookmarkManager();
        var provider = new BookmarksCommandProvider(mockBookmarkManager, Mock.Of<IBookmarkIconLocator>());

        // Assert
        Assert.AreEqual("Bookmarks", provider.Id);
    }

    [TestMethod]
    public void ProviderHasDisplayName()
    {
        // Setup
        var mockBookmarkManager = new MockBookmarkManager();
        var provider = new BookmarksCommandProvider(mockBookmarkManager, Mock.Of<IBookmarkIconLocator>());

        // Assert
        Assert.IsNotNull(provider.DisplayName);
        Assert.IsTrue(provider.DisplayName.Length > 0);
    }

    [TestMethod]
    public void ProviderHasIcon()
    {
        // Setup
        var mockBookmarkManager = new MockBookmarkManager();
        var provider = new BookmarksCommandProvider(mockBookmarkManager, Mock.Of<IBookmarkIconLocator>());

        // Assert
        Assert.IsNotNull(provider.Icon);
    }

    [TestMethod]
    public void TopLevelCommandsNotEmpty()
    {
        // Setup
        var mockBookmarkManager = new MockBookmarkManager();
        var provider = new BookmarksCommandProvider(mockBookmarkManager, Mock.Of<IBookmarkIconLocator>());

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
        var provider = new BookmarksCommandProvider(mockBookmarkManager, Mock.Of<IBookmarkIconLocator>());

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
    [Timeout(5000)]
    public async Task BookmarkListItem_LocalFileDataPackageTracksTargetUpdates()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            var bookmark = new BookmarkData("Local file", filePath);
            var bookmarkManager = new MockBookmarkManager(bookmark);
            using var item = new BookmarkListItem(
                bookmark,
                bookmarkManager,
                new BlockAfterFirstBookmarkResolver(),
                new IconLocator(Mock.Of<IFaviconLoader>()),
                new PlaceholderParser());

            await item.IsInitialized;

            Assert.IsNotNull(item.DataPackage);
            var dataPackageView = item.DataPackage.GetView();
            Assert.AreEqual(DataPackageOperation.Copy, dataPackageView.RequestedOperation);
            Assert.IsTrue(dataPackageView.Contains(StandardDataFormats.StorageItems));
            var storageItems = await dataPackageView.GetStorageItemsAsync().AsTask();
            Assert.AreEqual(1, storageItems.Count);
            Assert.IsTrue(string.Equals(filePath, storageItems[0].Path, StringComparison.OrdinalIgnoreCase));

            var renamed = bookmark with { Name = "Renamed local file" };
            bookmarkManager.RaiseBookmarkUpdated(bookmark, renamed);
            Assert.IsNotNull(item.DataPackage);

            bookmarkManager.RaiseBookmarkUpdated(renamed, renamed with { Bookmark = "https://example.com" });
            Assert.IsNull(item.DataPackage);
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [TestMethod]
    public void ProviderWithEmptyData_HasOnlyAddCommand()
    {
        // Arrange
        var mockBookmarkManager = new MockBookmarkManager();
        var provider = new BookmarksCommandProvider(mockBookmarkManager, Mock.Of<IBookmarkIconLocator>());

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
        var provider = new BookmarksCommandProvider(new MockBookmarkManager(), Mock.Of<IBookmarkIconLocator>());

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
        var provider = new BookmarksCommandProvider(new MockBookmarkManager(bookmark), Mock.Of<IBookmarkIconLocator>());

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
            new IconLocator(Mock.Of<IFaviconLoader>()),
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
            new IconLocator(Mock.Of<IFaviconLoader>()),
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

    [TestMethod]
    [DataRow("add")]
    [DataRow("remove")]
    [DataRow("update")]
    [DataRow("clear")]
    public async Task PrunesFaviconCacheAtStartupAndAfterBookmarkChanges(string change)
    {
        var original = new BookmarkData("Original", "https://example.test/{query}");
        BookmarkData[] bookmarks = [new("Without scheme", "www.example.test"), original, new("Settings", "ms-settings:display")];
        var manager = new Mock<IBookmarksManager>();
        manager.SetupGet(value => value.Bookmarks).Returns(() => bookmarks);
        var pruned = new TaskCompletionSource<Uri[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var faviconLoader = new Mock<IFaviconLoader>();
        faviconLoader.Setup(value => value.Prune(It.IsAny<IEnumerable<Uri>>()))
            .Callback<IEnumerable<Uri>>(uris => pruned.TrySetResult(uris.ToArray()));
        var provider = new BookmarksCommandProvider(manager.Object, new IconLocator(faviconLoader.Object));

        var retained = await pruned.Task.WaitAsync(TimeSpan.FromSeconds(10));
        CollectionAssert.AreEquivalent(new[] { new Uri("https://www.example.test"), new Uri("https://example.test/") }, retained);

        pruned = new TaskCompletionSource<Uri[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var updated = original with { Bookmark = "https://changed.example.test/{query}" };
        Uri[] expected;
        switch (change)
        {
            case "add":
                bookmarks = [.. bookmarks, updated with { Id = Guid.NewGuid() }];
                manager.Raise(value => value.BookmarkAdded += null, bookmarks[^1]);
                expected = [new("https://www.example.test"), new("https://example.test/"), new("https://changed.example.test/")];
                break;
            case "remove":
                bookmarks = [bookmarks[0], bookmarks[2]];
                manager.Raise(value => value.BookmarkRemoved += null, original);
                expected = [new("https://www.example.test")];
                break;
            case "update":
                bookmarks = [bookmarks[0], updated, bookmarks[2]];
                manager.Raise(value => value.BookmarkUpdated += null, original, updated);
                expected = [new("https://www.example.test"), new("https://changed.example.test/")];
                break;
            default:
                bookmarks = [];
                manager.Raise(value => value.BookmarkRemoved += null, original);
                expected = [];
                break;
        }

        retained = await pruned.Task.WaitAsync(TimeSpan.FromSeconds(10));
        CollectionAssert.AreEquivalent(expected, retained);
        GC.KeepAlive(provider);
    }

    private sealed class BlockingBookmarkResolver : IBookmarkResolver
    {
        private readonly TaskCompletionSource<(bool Success, Classification Result)> _completion = new();

        public Task<(bool Success, Classification Result)> TryClassifyAsync(string input, CancellationToken cancellationToken = default) =>
            _completion.Task;

        public Classification ClassifyOrUnknown(string input) => Classification.Unknown(input);
    }

    private sealed class BlockAfterFirstBookmarkResolver : IBookmarkResolver
    {
        private readonly BookmarkResolver _resolver = new(new PlaceholderParser());
        private int _callCount;

        public async Task<(bool Success, Classification Result)> TryClassifyAsync(string input, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _callCount) == 1)
            {
                return await _resolver.TryClassifyAsync(input, cancellationToken);
            }

            await Task.Delay(System.Threading.Timeout.Infinite, cancellationToken);
            return (false, Classification.Unknown(input));
        }

        public Classification ClassifyOrUnknown(string input) => _resolver.ClassifyOrUnknown(input);
    }
}
