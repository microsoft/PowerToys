// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class IntegrationTests
{
    [TestMethod]
    public void FullIntegration_AppCacheToCommandProvider_WorksCorrectly()
    {
        // Arrange - Create a mock cache with test data
        var mockCache = new MockAppCache();
        
        // Add various types of applications
        var notepadApp = TestDataHelper.CreateTestWin32Program(
            "Notepad", 
            "C:\\Windows\\System32\\notepad.exe", 
            enabled: true, 
            valid: true);
            
        var calculatorApp = TestDataHelper.CreateTestUWPApplication(
            "Calculator", 
            "Microsoft.WindowsCalculator_8wekyb3d8bbwe", 
            enabled: true);
            
        var disabledApp = TestDataHelper.CreateTestWin32Program(
            "DisabledApp", 
            "C:\\DisabledApp.exe", 
            enabled: false, 
            valid: true);

        mockCache.Win32s.Add(notepadApp);
        mockCache.Win32s.Add(disabledApp);
        mockCache.UWPs.Add(calculatorApp);

        // Create the page and provider with dependency injection
        var page = new AllAppsPage(mockCache);
        var provider = new AllAppsCommandProvider(page);
        
        // Wait for initialization
        Thread.Sleep(100);

        // Act & Assert - Test the complete flow
        
        // 1. Check that the provider has top level commands
        var topLevelCommands = provider.TopLevelCommands();
        Assert.IsNotNull(topLevelCommands);
        Assert.IsTrue(topLevelCommands.Length > 0);

        // 2. Check that page returns correct items (filtering works)
        var items = page.GetItems();
        Assert.IsNotNull(items);
        Assert.AreEqual(2, items.Length); // Only enabled apps should be returned
        
        var itemTitles = items.Select(i => i.Title).ToArray();
        Assert.IsTrue(itemTitles.Contains("Notepad"));
        Assert.IsTrue(itemTitles.Contains("Calculator"));
        Assert.IsFalse(itemTitles.Contains("DisabledApp"));

        // 3. Test app lookup functionality
        var foundNotepad = provider.LookupApp("Notepad");
        Assert.IsNotNull(foundNotepad);
        Assert.AreEqual("Notepad", foundNotepad.Title);

        var foundCalculator = provider.LookupApp("Calculator");
        Assert.IsNotNull(foundCalculator);
        Assert.AreEqual("Calculator", foundCalculator.Title);

        var notFound = provider.LookupApp("NonExistentApp");
        Assert.IsNull(notFound);

        // 4. Test cache reload functionality
        mockCache.SetShouldReload(true);
        Assert.IsTrue(mockCache.ShouldReload());
        
        mockCache.ResetReloadFlag();
        Assert.IsFalse(mockCache.ShouldReload());
    }

    [TestMethod]
    public void CacheReload_TriggersPageRefresh()
    {
        // Arrange
        var mockCache = new MockAppCache();
        var initialApp = TestDataHelper.CreateTestWin32Program("InitialApp", "C:\\InitialApp.exe");
        mockCache.Win32s.Add(initialApp);

        var page = new AllAppsPage(mockCache);
        Thread.Sleep(100); // Wait for initial load

        // Verify initial state
        var initialItems = page.GetItems();
        Assert.AreEqual(1, initialItems.Length);
        Assert.AreEqual("InitialApp", initialItems[0].Title);

        // Act - Simulate a cache reload scenario
        mockCache.SetShouldReload(true);
        
        // Add a new app to the cache
        var newApp = TestDataHelper.CreateTestWin32Program("NewApp", "C:\\NewApp.exe");
        mockCache.Win32s.Add(newApp);

        // Force a rebuild by accessing items (this would normally be triggered by UI)
        var updatedItems = page.GetItems();

        // Assert
        Assert.AreEqual(2, updatedItems.Length);
        var titles = updatedItems.Select(i => i.Title).ToArray();
        Assert.IsTrue(titles.Contains("InitialApp"));
        Assert.IsTrue(titles.Contains("NewApp"));
        
        // Verify reload flag was reset
        Assert.IsFalse(mockCache.ShouldReload());
    }

    [TestMethod]
    public void DefaultConstructors_MaintainBackwardCompatibility()
    {
        // This test ensures that existing code using default constructors still works
        
        // Act - Use default constructors as existing code would
        var defaultPage = new AllAppsPage();
        var defaultProvider = new AllAppsCommandProvider();

        // Assert - Basic functionality should work
        Assert.IsNotNull(defaultPage);
        Assert.IsNotNull(defaultPage.Name);
        Assert.IsNotNull(defaultPage.Icon);

        Assert.IsNotNull(defaultProvider);
        Assert.IsNotNull(defaultProvider.DisplayName);
        Assert.IsNotNull(defaultProvider.Icon);

        var commands = defaultProvider.TopLevelCommands();
        Assert.IsNotNull(commands);
        Assert.IsTrue(commands.Length > 0);
    }
}
