// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class QueryTests : CommandPaletteUnitTestBase
{
    private string _settingsPath = string.Empty;

    [TestMethod]
    public async Task QueryReturnsExpectedResults()
    {
        // Arrange
        _settingsPath = Path.Combine(Path.GetTempPath(), $"apps-settings-{Guid.NewGuid():N}.json");
        var settings = new AllAppsSettings(_settingsPath);
        using var mockCatalog = new MockAppCatalog();
        var win32App = TestDataHelper.CreateTestWin32Metadata("Notepad", "C:\\Windows\\System32\\notepad.exe");
        var uwpApp = TestDataHelper.CreateTestUWPApplication("Calculator");
        mockCatalog.AddWin32Program(win32App);
        mockCatalog.AddUWPApplication(uwpApp);

        for (var i = 0; i < 10; i++)
        {
            mockCatalog.AddWin32Program(TestDataHelper.CreateTestWin32Metadata($"App{i}"));
            mockCatalog.AddUWPApplication(TestDataHelper.CreateTestUWPApplication($"UWP App {i}"));
        }

        using var itemSource = new AppListItemSource(mockCatalog, settings);
        using var page = new AllAppsPage(itemSource, TestDataHelper.CreateFuzzyMatcherProvider());
        await AppsTestBase.WaitForPageInitializationAsync(page);

        // Act
        var allItems = page.GetItems();

        // Assert
        var notepadResult = Query("notepad", allItems).FirstOrDefault();
        Assert.IsNotNull(notepadResult);
        Assert.AreEqual("Notepad", notepadResult.Title);

        var calculatorResult = Query("cal", allItems).FirstOrDefault();
        Assert.IsNotNull(calculatorResult);
        Assert.AreEqual("Calculator", calculatorResult.Title);
    }

    [TestCleanup]
    public void CleanupSettingsFiles()
    {
        if (!string.IsNullOrEmpty(_settingsPath))
        {
            TestDataHelper.DeleteSettingsFiles(_settingsPath);
        }
    }
}
