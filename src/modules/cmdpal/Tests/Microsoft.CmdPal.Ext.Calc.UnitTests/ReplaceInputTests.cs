// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Ext.Calc.Pages;
using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Calc.UnitTests;

[TestClass]
public class ReplaceInputTests : CommandPaletteUnitTestBase
{
    [TestMethod]
    public void ReplaceInputOnEquals_Enabled_ReplacesText()
    {
        // Arrange
        var settings = new Settings(replaceInputOnEquals: true);
        var page = new CalculatorListPage(settings);

        // Act
        page.UpdateSearchText(string.Empty, "5*5=");

        // Assert
        // The SearchText property should be updated to the result "25"
        Assert.AreEqual("25", page.SearchText);
    }

    [TestMethod]
    public void ReplaceInputOnEquals_Disabled_DoesNotReplaceText()
    {
        // Arrange
        var settings = new Settings(replaceInputOnEquals: false);
        var page = new CalculatorListPage(settings);

        // Act
        page.UpdateSearchText(string.Empty, "5*5=");

        // Assert
        // The SearchText property should NOT be updated to the result "25"
        // It should remain empty or whatever the base class does, but definitely not the result.
        // Wait, the base class implementation of SearchText might not be updated by UpdateSearchText unless we explicitly set it.
        // In CalculatorListPage.cs:
        // if (replaceInput && ...) { SearchText = result.Title; return; }
        // If it returns early, SearchText is set.
        // If it doesn't return early, it calls UpdateResult, which doesn't set SearchText.
        // However, CommandPalettePage (base) might not store the "newSearch" into "SearchText" automatically before calling UpdateSearchText.
        // Usually the UI binding updates SearchText, then calls UpdateSearchText.
        // But here we are calling UpdateSearchText manually.
        // So SearchText will be whatever we initialized it to, or what we set it to.
        // In the test, SearchText starts as null or empty.
        Assert.AreNotEqual("25", page.SearchText);
    }

    [TestMethod]
    public void ReplaceInputOnEquals_Enabled_DoesNotReplaceIfDoesNotEndWithEquals()
    {
        // Arrange
        var settings = new Settings(replaceInputOnEquals: true);
        var page = new CalculatorListPage(settings);

        // Act
        page.UpdateSearchText(string.Empty, "5*5");

        // Assert
        Assert.AreNotEqual("25", page.SearchText);
    }
}
