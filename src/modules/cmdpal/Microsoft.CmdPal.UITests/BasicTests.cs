// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UITests;

[TestClass]
public class BasicTests : UITestBase
{
    public BasicTests()
        : base(PowerToysModule.CommandPalette)
    {
    }

    private void SetSearchBox(string text)
    {
        Assert.AreEqual(this.Find<TextBox>("Type here to search...").SetText(text, true).Text, text);
    }

    private void SetFilesExtensionSearchBox(string text)
    {
        Assert.AreEqual(this.Find<TextBox>("Search for files and folders...").SetText(text, true).Text, text);
    }

    private void SetCalculatorExtensionSearchBox(string text)
    {
        Assert.AreEqual(this.Find<TextBox>("Type an equation...").SetText(text, true).Text, text);
    }

    private void SetTimeAndDaterExtensionSearchBox(string text)
    {
        Assert.AreEqual(this.Find<TextBox>("Search values or type a custom time stamp...").SetText(text, true).Text, text);
    }

    [TestMethod]
    public void BasicFileSearchTest()
    {
        SetSearchBox("files");

        var searchFileItem = this.Find<NavigationViewItem>("Search files");
        Assert.AreEqual(searchFileItem.Name, "Search files");
        searchFileItem.DoubleClick();

        SetFilesExtensionSearchBox("AppData");

        Assert.IsNotNull(this.Find<NavigationViewItem>("AppData"));
    }

    [TestMethod]
    public void BasicCalculatorTest()
    {
        SetSearchBox("calculator");

        var searchFileItem = this.Find<NavigationViewItem>("Calculator");
        Assert.AreEqual(searchFileItem.Name, "Calculator");
        searchFileItem.DoubleClick();

        SetCalculatorExtensionSearchBox("1+2");

        Assert.IsNotNull(this.Find<NavigationViewItem>("3"));
    }

    [TestMethod]
    public void BasicTimeAndDateTest()
    {
        SetSearchBox("time and date");

        var searchFileItem = this.Find<NavigationViewItem>("Time and Date");
        Assert.AreEqual(searchFileItem.Name, "Time and Date");
        searchFileItem.DoubleClick();

        SetTimeAndDaterExtensionSearchBox("year");

        Assert.IsNotNull(this.Find<NavigationViewItem>("2025"));
    }
}
