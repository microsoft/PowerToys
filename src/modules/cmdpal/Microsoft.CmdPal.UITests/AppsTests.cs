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
public class AppsTests : CommandPaletteTestBase
{
    public void EnterAppsExtension()
    {
        SetSearchBox("All Apps");

        var searchFileItem = this.Find<NavigationViewItem>("All Apps");
        Assert.AreEqual(searchFileItem.Name, "All Apps");
        searchFileItem.DoubleClick();
    }

    [TestMethod]
    public void OpenWin32App()
    {
        EnterAppsExtension();
        SetAppsExtensionSearchBox("Command Prompt");
        var fileExplorer = this.Find<NavigationViewItem>("Command Prompt");
        Assert.IsNotNull(fileExplorer, "Notepad app not found.");
        fileExplorer.DoubleClick();

        var fileExplorerWindow = this.Find<Window>("Command Prompt", global: true);
        Assert.IsNotNull(fileExplorerWindow, "Command Prompt window not found.");
    }

    [TestMethod]
    public void OpenUWPApp()
    {
        EnterAppsExtension();
        SetAppsExtensionSearchBox("Calculator");
        var calculatorItem = this.Find<NavigationViewItem>("Calculator");
        Assert.IsNotNull(calculatorItem, "Calculator app not found.");
        calculatorItem.DoubleClick();

        var calculatorWindow = this.Find<Window>("Calculator", global: true);
        Assert.IsNotNull(calculatorWindow, "Calculator window not found.");
    }

    [TestMethod]
    public void ClickPrimaryButton()
    {
        EnterAppsExtension();
        SetAppsExtensionSearchBox("Calculator");
        var calculatorItem = this.Find<NavigationViewItem>("Calculator");
        Assert.IsNotNull(calculatorItem, "Calculator app not found.");
        calculatorItem.Click();

        var primaryButton = this.Find<Button>("Run");
        Assert.IsNotNull(primaryButton, "Primary button not found.");
        primaryButton.DoubleClick();
        var calculatorWindow = this.Find<Window>("Calculator", global: true);
        Assert.IsNotNull(calculatorWindow, "Calculator window not found.");
    }

    [TestMethod]
    [STATestMethod]
    public void ClickSecondaryButtonUWPApp()
    {
        EnterAppsExtension();
        SetAppsExtensionSearchBox("Calculator");
        var calculatorItem = this.Find<NavigationViewItem>("Calculator");
        Assert.IsNotNull(calculatorItem, "Calculator app not found.");
        calculatorItem.Click();

        var secondaryButton = this.Find<Button>("Copy path");
        Assert.IsNotNull(secondaryButton, "Secondary button not found.");
        secondaryButton.DoubleClick();

        var clipboardContent = System.Windows.Forms.Clipboard.GetText();
        Assert.IsTrue(clipboardContent.Contains("Calculator"), $"Clipboard content does not contain the expected file name. clipboard: {clipboardContent}");
    }

    [TestMethod]
    public void ClickSecondaryButtonWin32App()
    {
        EnterAppsExtension();
        SetAppsExtensionSearchBox("Registry Editor");
        var calculatorItem = this.Find<NavigationViewItem>("Registry Editor");
        Assert.IsNotNull(calculatorItem, "Registry Editor app not found.");
        calculatorItem.Click();

        var secondaryButton = this.Find<Button>("Run as administrator");
        Assert.IsNotNull(secondaryButton, "Secondary button not found.");
        secondaryButton.DoubleClick();

        UACConfirm();

        var fileExplorerWindow = this.Find<Window>("Registry Editor", global: true);
        Assert.IsNotNull(fileExplorerWindow, "Registry Editor window not found.");
    }
}
