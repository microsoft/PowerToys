// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AllAppsPageTests : AppsTestBase
{
    [TestMethod]
    public void AllAppsPage_Constructor_ThrowsOnNullAppCache()
    {
        // Act & Assert
        Assert.ThrowsException<ArgumentNullException>(() => new AllAppsPage(null!));
    }

    [TestMethod]
    public void AllAppsPage_WithMockCache_InitializesSuccessfully()
    {
        // Arrange
        var mockCache = new MockAppCache();

        // Act
        var page = new AllAppsPage(mockCache);

        // Assert
        Assert.IsNotNull(page);
        Assert.IsNotNull(page.Name);
        Assert.IsNotNull(page.Icon);
    }

    [TestMethod]
    public async Task AllAppsPage_GetItems_ReturnsEmptyWithEmptyCache()
    {
        // Act - Wait for initialization to complete
        await WaitForPageInitializationAsync();
        var items = Page.GetItems();

        // Assert
        Assert.IsNotNull(items);
        Assert.AreEqual(0, items.Length);
    }

    [TestMethod]
    public async Task AllAppsPage_GetItems_ReturnsAppsFromCacheAsync()
    {
        // Arrange
        var mockCache = new MockAppCache();
        var win32App = TestDataHelper.CreateTestWin32Program("Notepad", "C:\\Windows\\System32\\notepad.exe");
        var uwpApp = TestDataHelper.CreateTestUWPApplication("Calculator");

        mockCache.AddWin32Program(win32App);
        mockCache.AddUWPApplication(uwpApp);

        var page = new AllAppsPage(mockCache);

        // Wait a bit for initialization to complete
        await Task.Delay(100);

        // Act
        var items = page.GetItems();

        // Assert
        Assert.IsNotNull(items);
        Assert.AreEqual(2, items.Length);

        // we need to loop the items to ensure we got the correct ones
        Assert.IsTrue(items.Any(i => i.Title == "Notepad"));
        Assert.IsTrue(items.Any(i => i.Title == "Calculator"));
    }

    [TestMethod]
    public async Task AllAppsPage_GetPinnedApps_ReturnsEmptyWhenNoAppsArePinned()
    {
        // Arrange
        var mockCache = new MockAppCache();
        var app = TestDataHelper.CreateTestWin32Program("TestApp", "C:\\TestApp.exe");
        mockCache.AddWin32Program(app);

        var page = new AllAppsPage(mockCache);

        // Wait a bit for initialization to complete
        await Task.Delay(100);

        // Act
        var pinnedApps = page.GetPinnedApps();

        // Assert
        Assert.IsNotNull(pinnedApps);
        Assert.AreEqual(0, pinnedApps.Length);
    }
}
