// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
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

    public NavigationViewItem SearchAppByName(string name)
    {
        EnterAppsExtension();
        SetAppsExtensionSearchBox(name);
        var item = this.Find<NavigationViewItem>(name);
        Assert.IsNotNull(item, $"{name} app not found.");

        return item;
    }

    [TestMethod]
    public void OpenWin32AppTest()
    {
        const string appName = "Notepad";
        var notepadItem = SearchAppByName(appName);
        notepadItem.DoubleClick();

        var commandPromptWindow = FindWindowsTerminalWindow();
        Assert.IsNotNull(commandPromptWindow, "Command Prompt window not found.");
    }

    [TestMethod]
    public void OpenUWPAppTest()
    {
        const string appName = "Calculator";
        var calculatorItem = SearchAppByName(appName);
        calculatorItem.DoubleClick();

        var calculatorWindow = this.Find<Window>("Calculator", global: true);
        Assert.IsNotNull(calculatorWindow, "Calculator window not found.");
    }

    [TestMethod]
    public void ClickPrimaryButtonTest()
    {
        const string appName = "Notepad";
        var notepadItem = SearchAppByName(appName);
        notepadItem.Click();

        var primaryButton = this.Find<Button>("Run");
        Assert.IsNotNull(primaryButton, "Primary button not found.");
        primaryButton.Click();
        var calculatorWindow = this.Find<Window>(By.ClassName("Notepad"), global: true);
        Assert.IsNotNull(calculatorWindow, "Notepad window not found.");
    }

    [STATestMethod]
    [TestMethod]
    public void ClickSecondaryButtonUWPAppTest()
    {
        const string appName = "Calculator";
        var calculatorItem = SearchAppByName(appName);

        calculatorItem.Click();

        var secondaryButton = this.Find<Button>("Copy path");
        Assert.IsNotNull(secondaryButton, "Secondary button not found.");
        secondaryButton.Click();

        var clipboardContent = System.Windows.Forms.Clipboard.GetText();
        Assert.IsTrue(clipboardContent.Contains("Calculator"), $"Clipboard content does not contain the expected file name. clipboard: {clipboardContent}");
    }

    /*
    [TestMethod]
    public void ClickSecondaryButtonWin32AppTest()
    {
        const string appName = "Registry Editor";
        var calculatorItem = SearchAppByName(appName);

        calculatorItem.Click();

        var secondaryButton = this.Find<Button>("Run as administrator");
        Assert.IsNotNull(secondaryButton, "Secondary button not found.");
        secondaryButton.Click();

        UACConfirm();

        var fileExplorerWindow = this.Find<Window>(By.ClassName("RegEdit_RegEdit"), global: true);
        Assert.IsNotNull(fileExplorerWindow, "Registry Editor window not found.");
    }*/

    [TestMethod]
    public void OpenContextMenuTest()
    {
        const string appName = "Notepad";
        var notepadItem = SearchAppByName(appName);
        notepadItem.Click();
        OpenContextMenu();

        var pinButton = this.Find<NavigationViewItem>("Pin");
        Assert.IsNotNull(pinButton);
    }

    [TestMethod]
    public void ContextMenuRunButtonTest()
    {
        const string appName = "Notepad";
        var notepadItem = SearchAppByName(appName);
        notepadItem.Click();
        OpenContextMenu();

        var runButton = this.Find<NavigationViewItem>("Run");
        Assert.IsNotNull(runButton);
        runButton.Click();

        var notepadWindow = this.Find<Window>(By.ClassName("Notepad"), global: true);
        Assert.IsNotNull(notepadWindow, "Notepad window not found.");
    }

    /*
    [TestMethod]
    public void ContextMenuRunAsAdminButtonTest()
    {
        const string appName = "Notepad";
        var notepadItem = SearchAppByName(appName);
        notepadItem.Click();
        OpenContextMenu();

        var runButton = this.Find<NavigationViewItem>("Run as administrator");
        Assert.IsNotNull(runButton);
        runButton.Click();

        UACConfirm();

        var notepadWindow = this.Find<Window>(By.ClassName("Notepad"), global: true);
        Assert.IsNotNull(notepadWindow, "Notepad window not found.");
    }*/

    [STATestMethod]
    [TestMethod]
    public void ContextMenuCopyPathButtonTest()
    {
        const string appName = "Notepad";
        var notepadItem = SearchAppByName(appName);
        notepadItem.Click();
        OpenContextMenu();

        var copyPathButton = this.Find<NavigationViewItem>("Copy path");
        Assert.IsNotNull(copyPathButton);
        copyPathButton.Click();

        var clipboardContent = System.Windows.Forms.Clipboard.GetText();
        Assert.IsTrue(clipboardContent.Contains("Notepad"), $"Clipboard content does not contain the expected file name. clipboard: {clipboardContent}");
    }

    [TestMethod]
    public void ContextMenuPinTest()
    {
        const string appName = "Notepad";
        var notepadItem = SearchAppByName(appName);
        notepadItem.Click();
        OpenContextMenu();

        var pinButton = this.Find<NavigationViewItem>("Pin");
        Assert.IsNotNull(pinButton);
        pinButton.Click();

        SetAppsExtensionSearchBox(string.Empty);
        var item = this.Find<NavigationViewItem>(appName);
        Assert.IsNotNull(item, $"{appName} app not found.");
        OpenContextMenu();
        var unPinButton = this.Find<NavigationViewItem>("Unpin");
        Assert.IsNotNull(unPinButton);
        unPinButton.Click();
        SetAppsExtensionSearchBox(string.Empty);
    }

    [TestMethod]
    public void ContextMenuOpenContainingFolderTest()
    {
        const string appName = "Calculator";
        var notepadItem = SearchAppByName(appName);
        notepadItem.Click();
        OpenContextMenu();

        var openContainingFolderButton = this.Find<NavigationViewItem>("Open containing folder");
        Assert.IsNotNull(openContainingFolderButton);
        openContainingFolderButton.Click();

        var fileExplorerWindow = FindFileExploerWindow();

        Assert.IsNotNull(fileExplorerWindow);
    }

    [TestMethod]
    public void ContextMenuOpenPathInConsole()
    {
        const string appName = "Notepad";
        var notepadItem = SearchAppByName(appName);
        notepadItem.Click();
        OpenContextMenu();

        var openInConsoleButton = this.Find<NavigationViewItem>("Open path in console");
        Assert.IsNotNull(openInConsoleButton);
        openInConsoleButton.Click();

        var commandPromptWindow = FindWindowsTerminalWindow();
        Assert.IsNotNull(commandPromptWindow, "Command Prompt window not found.");
    }

    [TestMethod]
    public void ContextMenuOpenLocationTest()
    {
        const string appName = "Command Prompt";
        var notepadItem = SearchAppByName(appName);
        notepadItem.Click();
        OpenContextMenu();
        var openLocationButton = this.Find<NavigationViewItem>("Open location");
        Assert.IsNotNull(openLocationButton, "Open location button not found.");
        openLocationButton.Click();
        var fileExplorerWindow = FindFileExploerWindow();
        Assert.IsNotNull(fileExplorerWindow, "File Explorer window not found.");
    }

    [TestMethod]
    public void ContextMenuSearchTest()
    {
        const string appName = "Command Prompt";
        var notepadItem = SearchAppByName(appName);
        notepadItem.Click();
        OpenContextMenu();

        var contextMenuSearchBox = this.Find<TextBox>("Search commands...");
        Assert.IsNotNull(contextMenuSearchBox, "Context menu search box not found.");

        Assert.AreEqual(contextMenuSearchBox.SetText("Open location", true).Text, "Open location");
        var openLocationButton = this.Find<NavigationViewItem>("Open location");
        Assert.IsNotNull(openLocationButton, "Open location button not found.");
        openLocationButton.Click();

        var fileExplore = FindFileExploerWindow();
        Assert.IsNotNull(fileExplore, "File Explorer window not found.");
    }
}
