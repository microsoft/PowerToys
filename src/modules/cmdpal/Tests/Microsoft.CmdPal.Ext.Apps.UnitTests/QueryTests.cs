// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class QueryTests : CommandPaletteUnitTestBase
{
    [TestMethod]
    public void QueryReturnsExpectedResults()
    {
        // Arrange
        var mockCache = new MockAppCache();
        var win32App = TestDataHelper.CreateTestWin32Program("Notepad", "C:\\Windows\\System32\\notepad.exe");
        var uwpApp = TestDataHelper.CreateTestUWPApplication("Calculator");
        mockCache.AddWin32Program(win32App);
        mockCache.AddUWPApplication(uwpApp);

        for (var i = 0; i < 10; i++)
        {
            mockCache.AddWin32Program(TestDataHelper.CreateTestWin32Program($"App{i}"));
            mockCache.AddUWPApplication(TestDataHelper.CreateTestUWPApplication($"UWP App {i}"));
        }

        var page = new AllAppsPage(mockCache);
        var provider = new AllAppsCommandProvider(page);

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
}
