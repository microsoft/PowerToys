// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.Ext.Bookmarks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

[TestClass]
public class BookmarksCommandProviderTests
{
    [TestMethod]
    public void ProviderHasCorrectId()
    {
        // Setup
        var mockDataSource = new MockBookmarkDataSource();
        var provider = new BookmarksCommandProvider(mockDataSource);

        // Assert
        Assert.AreEqual("Bookmarks", provider.Id);
    }

    [TestMethod]
    public void ProviderHasDisplayName()
    {
        // Setup
        var mockDataSource = new MockBookmarkDataSource();
        var provider = new BookmarksCommandProvider(mockDataSource);

        // Assert
        Assert.IsNotNull(provider.DisplayName);
        Assert.IsTrue(provider.DisplayName.Length > 0);
    }

    [TestMethod]
    public void ProviderHasIcon()
    {
        // Setup
        var provider = new BookmarksCommandProvider();

        // Assert
        Assert.IsNotNull(provider.Icon);
    }

    [TestMethod]
    public void TopLevelCommandsNotEmpty()
    {
        // Setup
        var provider = new BookmarksCommandProvider();

        // Act
        var commands = provider.TopLevelCommands();

        // Assert
        Assert.IsNotNull(commands);
        Assert.IsTrue(commands.Length > 0);
    }

    [TestMethod]
    public void ProviderWithMockData_LoadsBookmarksCorrectly()
    {
        // Arrange
        var jsonData = @"{
            ""Data"": [
                {
                    ""Name"": ""Test Bookmark"",
                    ""Bookmark"": ""https://test.com""
                },
                {
                    ""Name"": ""Another Bookmark"",
                    ""Bookmark"": ""https://another.com""
                }
            ]
        }";

        var dataSource = new MockBookmarkDataSource(jsonData);
        var provider = new BookmarksCommandProvider(dataSource);

        // Act
        var commands = provider.TopLevelCommands();

        // Assert
        Assert.IsNotNull(commands);

        var addCommand = commands.Where(c => c.Title.Contains("Add bookmark")).FirstOrDefault();
        var testBookmark = commands.Where(c => c.Title.Contains("Test Bookmark")).FirstOrDefault();

        // Should have three commands：Add + two custom bookmarks
        Assert.AreEqual(3, commands.Length);

        Assert.IsNotNull(addCommand);
        Assert.IsNotNull(testBookmark);
    }

    [TestMethod]
    public void ProviderWithEmptyData_HasOnlyAddCommand()
    {
        // Arrange
        var dataSource = new MockBookmarkDataSource(@"{ ""Data"": [] }");
        var provider = new BookmarksCommandProvider(dataSource);

        // Act
        var commands = provider.TopLevelCommands();

        // Assert
        Assert.IsNotNull(commands);

        // Only have Add command
        Assert.AreEqual(1, commands.Length);

        var addCommand = commands.Where(c => c.Title.Contains("Add bookmark")).FirstOrDefault();
        Assert.IsNotNull(addCommand);
    }

    [TestMethod]
    public void ProviderWithInvalidData_HandlesGracefully()
    {
        // Arrange
        var dataSource = new MockBookmarkDataSource("invalid json");
        var provider = new BookmarksCommandProvider(dataSource);

        // Act
        var commands = provider.TopLevelCommands();

        // Assert
        Assert.IsNotNull(commands);

        // Only have one command. Will ignore json parse error.
        Assert.AreEqual(1, commands.Length);

        var addCommand = commands.Where(c => c.Title.Contains("Add bookmark")).FirstOrDefault();
        Assert.IsNotNull(addCommand);
    }
}
