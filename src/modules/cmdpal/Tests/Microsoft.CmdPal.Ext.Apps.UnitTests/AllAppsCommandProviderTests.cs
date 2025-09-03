// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AllAppsCommandProviderTests : AppsTestBase
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
    public void LookupAppWithEmptyNameReturnsNotNull()
    {
        // Setup
        var mockApp = TestDataHelper.CreateTestWin32Program("Notepad", "C:\\Windows\\System32\\notepad.exe");
        MockCache.AddWin32Program(mockApp);
        var page = new AllAppsPage(MockCache);

        var provider = new AllAppsCommandProvider(page);

        // Act
        var result = provider.LookupApp(string.Empty);

        // Assert
        Assert.IsNotNull(result);
    }

    [TestMethod]
    public async Task ProviderWithMockData_LookupApp_ReturnsCorrectApp()
    {
        // Arrange
        var testApp = TestDataHelper.CreateTestWin32Program("TestApp", "C:\\TestApp.exe");
        MockCache.AddWin32Program(testApp);

        var provider = new AllAppsCommandProvider(Page);

        // Wait for initialization to complete
        await WaitForPageInitializationAsync();

        // Act
        var result = provider.LookupApp("TestApp");

        // Assert
        Assert.IsNotNull(result);
        Assert.AreEqual("TestApp", result.Title);
    }

    [TestMethod]
    public async Task ProviderWithMockData_LookupApp_ReturnsNullForNonExistentApp()
    {
        // Arrange
        var testApp = TestDataHelper.CreateTestWin32Program("TestApp", "C:\\TestApp.exe");
        MockCache.AddWin32Program(testApp);

        var provider = new AllAppsCommandProvider(Page);

        // Wait for initialization to complete
        await WaitForPageInitializationAsync();

        // Act
        var result = provider.LookupApp("NonExistentApp");

        // Assert
        Assert.IsNull(result);
    }

    [TestMethod]
    public void ProviderWithMockData_TopLevelCommands_IncludesListItem()
    {
        // Arrange
        var provider = new AllAppsCommandProvider(Page);

        // Act
        var commands = provider.TopLevelCommands();

        // Assert
        Assert.IsNotNull(commands);
        Assert.IsTrue(commands.Length >= 1); // At least the list item should be present
    }
}
