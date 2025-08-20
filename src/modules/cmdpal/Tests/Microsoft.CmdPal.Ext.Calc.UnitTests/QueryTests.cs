// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.CmdPal.Ext.Calc.Helper;
using Microsoft.CmdPal.Ext.Calc.Pages;
using Microsoft.CmdPal.Ext.UnitTestBase;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Calc.UnitTests;

[TestClass]
public class QueryTests : CommandPaletteUnitTestBase
{
    [DataTestMethod]
    [DataRow("2+2", "4")]
    [DataRow("5*3", "15")]
    [DataRow("10/2", "5")]
    [DataRow("sqrt(16)", "4")]
    [DataRow("2^3", "8")]
    public void TopLevelPageQueryTest(string input, string expectedResult)
    {
        var settings = new Settings();
        var page = new CalculatorListPage(settings);

        // Simulate query execution
        page.UpdateSearchText(string.Empty, input);
        var result = page.GetItems();

        Assert.IsTrue(result.Length == 1, "Valid input should always return result");

        var firstResult = result.FirstOrDefault();

        Assert.IsNotNull(result);
        Assert.IsTrue(
            firstResult.Title.Contains(expectedResult),
            $"Expected result to contain '{expectedResult}' but got '{firstResult.Title}'");
    }

    [TestMethod]
    public void EmptyQueryTest()
    {
        var settings = new Settings();
        var page = new CalculatorListPage(settings);
        page.UpdateSearchText("abc", string.Empty);
        var results = page.GetItems();
        Assert.IsNotNull(results);

        var firstItem = results.FirstOrDefault();
        Assert.AreEqual("Type an equation...", firstItem.Title);
    }

    [TestMethod]
    public void InvalidExpressionTest()
    {
        var settings = new Settings();

        var page = new CalculatorListPage(settings);

        // Simulate query execution
        page.UpdateSearchText(string.Empty, "invalid expression");
        var result = page.GetItems().FirstOrDefault();

        Assert.AreEqual("Type an equation...", result.Title);
    }

    [DataTestMethod]
    [DataRow("sin(60)", "-0.30481", CalculateEngine.TrigMode.Radians)]
    [DataRow("sin(60)", "0.866025", CalculateEngine.TrigMode.Degrees)]
    [DataRow("sin(60)", "0.809016", CalculateEngine.TrigMode.Gradians)]
    public void TrigModeSettingsTest(string input, string expected, CalculateEngine.TrigMode trigMode)
    {
        var settings = new Settings(trigUnit: trigMode);

        var page = new CalculatorListPage(settings);

        page.UpdateSearchText(string.Empty, input);
        var result = page.GetItems().FirstOrDefault();

        Assert.IsNotNull(result);

        Assert.IsTrue(result.Title.Contains(expected, System.StringComparison.Ordinal), $"Calc trigMode convert result isn't correct. Current result: {result.Title}");
    }
}
