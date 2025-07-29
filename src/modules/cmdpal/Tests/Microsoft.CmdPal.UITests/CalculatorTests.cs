// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UITests;

[TestClass]
public class CalculatorTests : CommandPaletteTestBase
{
    public CalculatorTests()
        : base()
    {
    }

    public void EnterCalculatorExtension()
    {
        SetSearchBox("Calculator");
        var calculatorItem = this.Find<NavigationViewItem>("Calculator");
        Assert.AreEqual(calculatorItem.Name, "Calculator");
        calculatorItem.DoubleClick();
    }

    private string ConvertResult(string originalResult, int originalBase, int convertToBase)
    {
        Assert.IsNotEmpty(originalResult, "Original result cannot be empty.");
        Assert.IsTrue(originalBase is 2 or 8 or 10 or 16, "Original base must be one of the following: 2, 8, 10, or 16.");
        Assert.IsTrue(convertToBase is 2 or 8 or 10 or 16, "Convert to base must be one of the following: 2, 8, 10, or 16.");

        var originalDecimal = Convert.ToInt32(originalResult, originalBase);

        // support base two, decimal, hexadecimal, and octal
        return convertToBase switch
        {
            2 => Convert.ToString(originalDecimal, 2),
            8 => Convert.ToString(originalDecimal, 8),
            10 => Convert.ToString(originalDecimal, 10),
            16 => Convert.ToString(originalDecimal, 16).ToUpper(System.Globalization.CultureInfo.CurrentCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(convertToBase), "Unsupported base conversion"),
        };
    }

    [TestMethod]
    [DataRow("2+2", "4")]
    [DataRow("2*3", "6")]
    [DataRow("2^3", "8")]
    public void CalculatorBasicTests(string expression, string expectation)
    {
        EnterCalculatorExtension();
        SetCalculatorExtensionSearchBox(expression);
        var resultItem = this.Find<NavigationViewItem>(expectation);

        Assert.IsNotNull(resultItem);
        Assert.AreEqual(resultItem.Name, expectation, $"Expected result '{expectation}' not found for expression '{expression}'.");
    }

    [TestMethod]
    [DataRow("2+2", "4")]
    public void CalculatorResultDoubleClickTests(string expression, string expectation)
    {
        EnterCalculatorExtension();
        SetCalculatorExtensionSearchBox(expression);
        var resultItem = this.Find<NavigationViewItem>(expectation);

        Assert.IsNotNull(resultItem);
        Assert.AreEqual(resultItem.Name, expectation, $"Expected result '{expectation}' not found for expression '{expression}'.");

        resultItem.DoubleClick();
        var resultItems = this.FindAll<NavigationViewItem>(expectation);
        Assert.AreEqual(2, resultItems.Count, $"Expected exactly two result item for '{expectation}' after double-clicking, but found {resultItems.Count}.");
    }

    [TestMethod]
    [DataRow("2+2", "4")]
    public void CalculatorPrimaryButtonTests(string expression, string expectation)
    {
        EnterCalculatorExtension();
        SetCalculatorExtensionSearchBox(expression);
        var resultItem = this.Find<NavigationViewItem>(expectation);

        Assert.IsNotNull(resultItem);
        Assert.AreEqual(resultItem.Name, expectation, $"Expected result '{expectation}' not found for expression '{expression}'.");

        var primaryButton = this.Find<Button>("Save");
        primaryButton.Click();
        var resultItems = this.FindAll<NavigationViewItem>(expectation);
        Assert.AreEqual(2, resultItems.Count, $"Expected exactly two result item for '{expectation}' after click primary button, but found {resultItems.Count}.");
    }

    [STATestMethod]
    [TestMethod]
    [DataRow("2+2", "4")]
    public void CalculatorSecondaryButtonTests(string expression, string expectation)
    {
        EnterCalculatorExtension();
        SetCalculatorExtensionSearchBox(expression);
        var resultItem = this.Find<NavigationViewItem>(expectation);

        Assert.IsNotNull(resultItem);
        Assert.AreEqual(resultItem.Name, expectation, $"Expected result '{expectation}' not found for expression '{expression}'.");

        var secondaryButton = this.Find<Button>("Copy");
        secondaryButton.Click();

        var clipboardContent = System.Windows.Forms.Clipboard.GetText();
        Assert.IsTrue(clipboardContent.Equals(expectation, StringComparison.Ordinal), $"Clipboard content does not equal the expected result. clipboard: {clipboardContent}");
    }

    [STATestMethod]
    [TestMethod]
    [DataRow("2+2", "4")]
    public void CalculatorContextMenuSaveTests(string expression, string expectation)
    {
        EnterCalculatorExtension();
        SetCalculatorExtensionSearchBox(expression);
        var resultItem = this.Find<NavigationViewItem>(expectation);

        Assert.IsNotNull(resultItem);
        Assert.AreEqual(resultItem.Name, expectation, $"Expected result '{expectation}' not found for expression '{expression}'.");

        OpenContextMenu();

        var saveItem = this.Find<NavigationViewItem>("Save");
        saveItem.Click();

        var clipboardContent = System.Windows.Forms.Clipboard.GetText();
        Assert.IsTrue(clipboardContent.Equals(expectation, StringComparison.Ordinal), $"Clipboard content does not equal the expected result. clipboard: {clipboardContent}");
    }

    [STATestMethod]
    [TestMethod]
    [DataRow("2+2", "4", 10)]
    [DataRow("0b10 * 0b10", "4", 2)]
    [DataRow("0xa * 0xa", "100", 16)]
    public void CalculatorContextMenuBaseConvertTests(string expression, string expectation, int originalBase)
    {
        EnterCalculatorExtension();
        SetCalculatorExtensionSearchBox(expression);
        var resultItem = this.Find<NavigationViewItem>(expectation);

        Assert.IsNotNull(resultItem);
        Assert.AreEqual(resultItem.Name, expectation, $"Expected result '{expectation}' not found for expression '{expression}'.");

        OpenContextMenu();

        var testBaseList = new List<int> { 2, 10, 16 };
        foreach (var convertToBase in testBaseList)
        {
            var convertedResult = ConvertResult(expectation, 10, convertToBase);
            var convertedItem = this.Find<NavigationViewItem>(convertedResult);
            Assert.IsNotNull(convertedItem, $"Convert to base {convertToBase} item not found.");
        }

        var binaryResult = ConvertResult(expectation, 10, 2);
        var binaryItem = this.Find<NavigationViewItem>(binaryResult);
        binaryItem.Click();

        var clipboardContent = System.Windows.Forms.Clipboard.GetText();
        Assert.IsTrue(clipboardContent.Equals(expectation, StringComparison.Ordinal), $"Clipboard content does not equal the expected result. clipboard: {clipboardContent}");
    }
}
