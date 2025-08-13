// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AllAppsPageTests
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
    public void AllAppsPage_GetItems_ReturnsEmptyWithEmptyCache()
    {
        // Arrange
        var mockCache = new MockAppCache();
        var page = new AllAppsPage(mockCache);
        
        // Wait a bit for initialization to complete
        Thread.Sleep(100);

        // Act
        var items = page.GetItems();

        // Assert
        Assert.IsNotNull(items);
        Assert.AreEqual(0, items.Length);
    }

    [TestMethod]
    public void AllAppsPage_GetItems_ReturnsAppsFromCache()
    {
        // Arrange
        var mockCache = new MockAppCache();
        var win32App = TestDataHelper.CreateTestWin32Program("Notepad", "C:\\Windows\\System32\\notepad.exe");
        var uwpApp = TestDataHelper.CreateTestUWPApplication("Calculator");
        
        mockCache.Win32s.Add(win32App);
        mockCache.UWPs.Add(uwpApp);

        var page = new AllAppsPage(mockCache);
        
        // Wait a bit for initialization to complete
        Thread.Sleep(100);

        // Act
        var items = page.GetItems();

        // Assert
        Assert.IsNotNull(items);
        Assert.AreEqual(2, items.Length);
        
        var notepadItem = items.FirstOrDefault(i => i.Title == "Notepad");
        var calculatorItem = items.FirstOrDefault(i => i.Title == "Calculator");
        
        Assert.IsNotNull(notepadItem);
        Assert.IsNotNull(calculatorItem);
    }

    [TestMethod]
    public void AllAppsPage_GetItems_FiltersDisabledApps()
    {
        // Arrange
        var mockCache = new MockAppCache();
        var enabledApp = TestDataHelper.CreateTestWin32Program("EnabledApp", "C:\\EnabledApp.exe", enabled: true);
        var disabledApp = TestDataHelper.CreateTestWin32Program("DisabledApp", "C:\\DisabledApp.exe", enabled: false);
        
        mockCache.Win32s.Add(enabledApp);
        mockCache.Win32s.Add(disabledApp);

        var page = new AllAppsPage(mockCache);
        
        // Wait a bit for initialization to complete
        Thread.Sleep(100);

        // Act
        var items = page.GetItems();

        // Assert
        Assert.IsNotNull(items);
        Assert.AreEqual(1, items.Length);
        Assert.AreEqual("EnabledApp", items.First().Title);
    }

    [TestMethod]
    public void AllAppsPage_GetItems_FiltersInvalidWin32Apps()
    {
        // Arrange
        var mockCache = new MockAppCache();
        var validApp = TestDataHelper.CreateTestWin32Program("ValidApp", "C:\\ValidApp.exe", enabled: true, valid: true);
        var invalidApp = TestDataHelper.CreateTestWin32Program("InvalidApp", "C:\\InvalidApp.exe", enabled: true, valid: false);
        
        mockCache.Win32s.Add(validApp);
        mockCache.Win32s.Add(invalidApp);

        var page = new AllAppsPage(mockCache);
        
        // Wait a bit for initialization to complete
        Thread.Sleep(100);

        // Act
        var items = page.GetItems();

        // Assert
        Assert.IsNotNull(items);
        Assert.AreEqual(1, items.Length);
        Assert.AreEqual("ValidApp", items.First().Title);
    }

    [TestMethod]
    public void AllAppsPage_GetPinnedApps_ReturnsEmptyWhenNoAppsArePinned()
    {
        // Arrange
        var mockCache = new MockAppCache();
        var app = TestDataHelper.CreateTestWin32Program("TestApp", "C:\\TestApp.exe");
        mockCache.Win32s.Add(app);

        var page = new AllAppsPage(mockCache);
        
        // Wait a bit for initialization to complete
        Thread.Sleep(100);

        // Act
        var pinnedApps = page.GetPinnedApps();

        // Assert
        Assert.IsNotNull(pinnedApps);
        Assert.AreEqual(0, pinnedApps.Length);
    }
}
