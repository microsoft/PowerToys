// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Bookmarks.UnitTests;

[TestClass]
public class BookmarksCommandProviderTests
{
    [TestMethod]
    public void ProviderHasCorrectId()
    {
        // Setup
        var provider = new BookmarksCommandProvider();

        // Assert
        Assert.AreEqual("Bookmarks", provider.Id);
    }

    [TestMethod]
    public void ProviderHasDisplayName()
    {
        // Setup
        var provider = new BookmarksCommandProvider();

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
}
