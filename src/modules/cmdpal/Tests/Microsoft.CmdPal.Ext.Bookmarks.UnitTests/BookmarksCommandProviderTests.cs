// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Bookmarks.Commands;
using Microsoft.CmdPal.Ext.Bookmarks.Helpers;
using Microsoft.CmdPal.Ext.Bookmarks.Persistence;
using Microsoft.CmdPal.Ext.Bookmarks.Services;
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
    [Timeout(5000)]
    public async Task ProviderPassesResolverClassificationToLauncher_Unchanged()
    {
        // Arrange
        var bookmarkAddress = @"C:\Données\été\résumé.txt";
        var bookmark = new BookmarkData("Accented bookmark", bookmarkAddress);
        var mockBookmarkManager = new MockBookmarkManager(bookmark);
        var expectedClassification = new Classification(
            CommandKind.FileDocument,
            bookmarkAddress,
            bookmarkAddress,
            string.Empty,
            LaunchMethod.ShellExecute,
            @"C:\Données\été",
            false);
        var resolver = new TestBookmarkResolver(expectedClassification);
        var mockLauncher = new MockProcessLauncher();
        var provider = new BookmarksCommandProvider(mockBookmarkManager, resolver, processLauncher: mockLauncher);

        var commands = provider.TopLevelCommands();
        var bookmarkItem = commands.OfType<Pages.BookmarkListItem>().Single();
        await bookmarkItem.IsInitialized;
        var launchCommand = bookmarkItem.Command as LaunchBookmarkCommand;
        Assert.IsNotNull(launchCommand);

        // Act — invoke the command; the mock launcher captures what's launched
        _ = launchCommand.Invoke(this);

        // Assert
        Assert.AreEqual(bookmarkAddress, resolver.LastClassifyInput);
        Assert.IsNotNull(mockLauncher.LastLaunchedClassification);
        Assert.AreEqual(expectedClassification.Target, mockLauncher.LastLaunchedClassification.Target);
        Assert.AreEqual(expectedClassification.Kind, mockLauncher.LastLaunchedClassification.Kind);
        Assert.AreEqual(expectedClassification.Launch, mockLauncher.LastLaunchedClassification.Launch);
    }

    private sealed class MockProcessLauncher : IProcessLauncher
    {
        public Classification? LastLaunchedClassification { get; private set; }

        public bool Launch(Classification classification, bool runAsAdmin = false)
        {
            LastLaunchedClassification = classification;
            return true;
        }
    }

    private sealed class TestBookmarkResolver : IBookmarkResolver
    {
        private readonly Classification _classification;

        internal string? LastClassifyInput { get; private set; }

        internal TestBookmarkResolver(Classification classification)
        {
            _classification = classification;
        }

        public Task<(bool Success, Classification Result)> TryClassifyAsync(string input, System.Threading.CancellationToken cancellationToken = default)
            => Task.FromResult((true, _classification));

        public Classification ClassifyOrUnknown(string input)
        {
            LastClassifyInput = input;
            return _classification;
        }
    }
}
