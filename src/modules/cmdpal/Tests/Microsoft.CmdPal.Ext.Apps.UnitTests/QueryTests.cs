// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.CommandPalette.Extensions.Toolkit;
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

    [TestMethod]
    [DataRow("camera", "Câmera", true, DisplayName = "Portuguese: camera matches Câmera")]
    [DataRow("camera", "câmera", true, DisplayName = "Portuguese: camera matches câmera (lowercase)")]
    [DataRow("cafe", "Café", true, DisplayName = "French: cafe matches Café")]
    [DataRow("resume", "Résumé", true, DisplayName = "French: resume matches Résumé")]
    [DataRow("nino", "Niño", true, DisplayName = "Spanish: nino matches Niño")]
    [DataRow("espanol", "Español", true, DisplayName = "Spanish: espanol matches Español")]
    [DataRow("uber", "Über", true, DisplayName = "German: uber matches Über")]
    [DataRow("naive", "Naïve", true, DisplayName = "English: naive matches Naïve")]
    [DataRow("fiancee", "Fiancée", true, DisplayName = "French: fiancee matches Fiancée")]
    [DataRow("cliche", "Cliché", true, DisplayName = "French: cliche matches Cliché")]
    [DataRow("CAMERA", "câmera", true, DisplayName = "Case insensitive: CAMERA matches câmera")]
    [DataRow("test", "Câmera", false, DisplayName = "Non-matching: test does not match Câmera")]
    public void FuzzyMatcherHandlesDiacritics(string query, string target, bool shouldMatch)
    {
        // Act
        var score = FuzzyStringMatcher.ScoreFuzzy(query, target);

        // Assert
        if (shouldMatch)
        {
            Assert.IsTrue(score > 0, $"Expected '{query}' to match '{target}' but score was {score}");
        }
        else
        {
            Assert.AreEqual(0, score, $"Expected '{query}' not to match '{target}' but score was {score}");
        }
    }

    [TestMethod]
    public void QueryMatchesAppsWithDiacritics()
    {
        // Arrange - simulating Brazilian Portuguese "Câmera" app
        var mockCache = new MockAppCache();
        var cameraApp = TestDataHelper.CreateTestUWPApplication("Câmera");
        mockCache.AddUWPApplication(cameraApp);

        var page = new AllAppsPage(mockCache);

        // Act
        var allItems = page.GetItems();

        // Assert - searching without accent should find the app
        var result = Query("camera", allItems).FirstOrDefault();
        Assert.IsNotNull(result, "Searching 'camera' should find 'Câmera' app");
        Assert.AreEqual("Câmera", result.Title);

        // Also verify exact match works
        var exactResult = Query("Câmera", allItems).FirstOrDefault();
        Assert.IsNotNull(exactResult, "Searching 'Câmera' should find 'Câmera' app");
        Assert.AreEqual("Câmera", exactResult.Title);
    }
}
