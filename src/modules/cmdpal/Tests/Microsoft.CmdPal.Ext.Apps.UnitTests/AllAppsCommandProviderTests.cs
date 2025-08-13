// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AllAppsCommandProviderTests
{
    [TestMethod]
    public void ProviderHasDisplayName()
    {
        // Setup
        var provider = new AllAppsCommandProvider();

        // Assert
        Assert.IsNotNull(provider.DisplayName);
        Assert.IsTrue(provider.DisplayName.Length > 0);
    }

    [TestMethod]
    public void ProviderHasIcon()
    {
        // Setup
        var provider = new AllAppsCommandProvider();

        // Assert
        Assert.IsNotNull(provider.Icon);
    }

    [TestMethod]
    public void TopLevelCommandsNotEmpty()
    {
        // Setup
        var provider = new AllAppsCommandProvider();

        // Act
        var commands = provider.TopLevelCommands();

        // Assert
        Assert.IsNotNull(commands);
        Assert.IsTrue(commands.Length > 0);
    }

    [TestMethod]
    public void LookupAppReturnsValidResult()
    {
        // Setup
        var provider = new AllAppsCommandProvider();

        // Act - try to lookup a common app
        var result = provider.LookupApp("notepad");

        Assert.IsNotNull(result);
    }

    [TestMethod]
    public void LookupAppWithEmptyNameReturnsNull()
    {
        // Setup
        var provider = new AllAppsCommandProvider();

        // Act
        var result = provider.LookupApp(string.Empty);

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void Constructor_ThrowsOnNullPage()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => new AllAppsCommandProvider(null!));
    }

    [TestMethod]
    public void ProviderWithMockData_LookupApp_ReturnsCorrectApp()
    {
        // Arrange
        var mockCache = new MockAppCache();
        var testApp = TestDataHelper.CreateTestWin32Program("TestApp", "C:\\TestApp.exe");
        mockCache.Win32s.Add(testApp);

        var page = new AllAppsPage(mockCache);
        var provider = new AllAppsCommandProvider(page);
        
        // Wait a bit for initialization to complete
        Thread.Sleep(100);

        // Act
        var result = provider.LookupApp("TestApp");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("TestApp", result.Title);
    }

    [TestMethod]
    public void ProviderWithMockData_LookupApp_ReturnsNullForNonExistentApp()
    {
        // Arrange
        var mockCache = new MockAppCache();
        var testApp = TestDataHelper.CreateTestWin32Program("TestApp", "C:\\TestApp.exe");
        mockCache.Win32s.Add(testApp);

        var page = new AllAppsPage(mockCache);
        var provider = new AllAppsCommandProvider(page);
        
        // Wait a bit for initialization to complete
        Thread.Sleep(100);

        // Act
        var result = provider.LookupApp("NonExistentApp");

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void ProviderWithMockData_TopLevelCommands_IncludesListItem()
    {
        // Arrange
        var mockCache = new MockAppCache();
        var page = new AllAppsPage(mockCache);
        var provider = new AllAppsCommandProvider(page);

        // Act
        var commands = provider.TopLevelCommands();

        // Assert
        Assert.IsNotNull(commands);
        Assert.IsTrue(commands.Length >= 1); // At least the list item should be present
    }
}
