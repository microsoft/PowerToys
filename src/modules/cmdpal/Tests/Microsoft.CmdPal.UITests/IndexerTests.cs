// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UITests;

[TestClass]
public class IndexerTests : CommandPaletteTestBase
{
    private const string TestFileContent = "This is Indexer UI test sample";
    private const string TestFileName = "indexer_test_item.txt";
    private const string TestFileBaseName = "indexer_test_item";
    private const string TestFolderName = "Downloads";

    public IndexerTests()
        : base()
    {
        // create a empty file in Downloads folder
        // to ensure that the indexer has something to search for
        var downloadsPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads";
        var emptyFilePath = System.IO.Path.Combine(downloadsPath, TestFileName);
        if (!System.IO.File.Exists(emptyFilePath))
        {
            using (var fileStream = System.IO.File.Create(emptyFilePath))
            {
                var content = TestFileContent;
                var contentBytes = Encoding.UTF8.GetBytes(content);
                fileStream.Write(contentBytes, 0, contentBytes.Length);
            }
        }
    }

    public void EnterIndexerExtension()
    {
        SetSearchBox("files");

        var searchFileItem = this.Find<NavigationViewItem>("Search files");
        Assert.AreEqual(searchFileItem.Name, "Search files");
        searchFileItem.DoubleClick();
    }

    [TestMethod]
    public void BasicIndexerSearchTest()
    {
        EnterIndexerExtension();
        SetFilesExtensionSearchBox("Downloads");
        Assert.IsNotNull(this.Find<NavigationViewItem>("Downloads"));
    }

    [TestMethod]
    public void IndexerOpenFileTest()
    {
        EnterIndexerExtension();
        SetFilesExtensionSearchBox(TestFileName);

        var searchItem = this.Find<NavigationViewItem>(TestFileName);

        Assert.IsNotNull(searchItem);

        searchItem.Click();

        var openButton = this.Find<Button>(By.AccessibilityId("PrimaryCommandButton"));
        Assert.IsNotNull(openButton);

        openButton.Click();

        FindDefaultAppDialogAndClickButton();

        var notepadWindow = FindNotepadWindow(TestFileBaseName, global: true);

        Assert.IsNotNull(notepadWindow);
    }

    [TestMethod]
    public void IndexerDoubleClickOpenFileTest()
    {
        EnterIndexerExtension();
        SetFilesExtensionSearchBox(TestFileName);

        var searchItem = this.Find<NavigationViewItem>(TestFileName);

        Assert.IsNotNull(searchItem);

        searchItem.DoubleClick();

        FindDefaultAppDialogAndClickButton();

        var notepadWindow = FindNotepadWindow(TestFileBaseName, global: true);

        Assert.IsNotNull(notepadWindow);
    }

    [TestMethod]
    public void IndexerOpenFolderTest()
    {
        EnterIndexerExtension();
        SetFilesExtensionSearchBox(TestFolderName);

        var searchItem = this.Find<NavigationViewItem>(TestFolderName);
        Assert.IsNotNull(searchItem);
        searchItem.Click();

        var openButton = this.Find<Button>("Open");
        Assert.IsNotNull(openButton);

        openButton.Click();
        var fileExplorer = FindExplorerWindow(TestFolderName, global: true);

        Assert.IsNotNull(fileExplorer);
    }

    [TestMethod]
    public void IndexerDoubleClickOpenFolderTest()
    {
        EnterIndexerExtension();
        SetFilesExtensionSearchBox(TestFolderName);

        var searchItem = this.Find<NavigationViewItem>(TestFolderName);
        Assert.IsNotNull(searchItem);
        searchItem.DoubleClick();

        var fileExplorer = FindExplorerWindow(TestFolderName, global: true);

        Assert.IsNotNull(fileExplorer);
    }

    [TestMethod]
    public void IndexerBrowseFolderTest()
    {
        EnterIndexerExtension();
        SetFilesExtensionSearchBox(TestFolderName);

        var searchItem = this.Find<NavigationViewItem>(TestFolderName);
        Assert.IsNotNull(searchItem);
        searchItem.Click();

        var openButton = this.Find<Button>(By.AccessibilityId("SecondaryCommandButton"));
        Assert.IsNotNull(openButton);

        openButton.Click();

        var testItem = this.Find<NavigationViewItem>(TestFileName);
        Assert.IsNotNull(testItem);
    }

    [STATestMethod]
    [TestMethod]
    public void IndexerCopyPathTest()
    {
        EnterIndexerExtension();
        SetFilesExtensionSearchBox(TestFileName);

        var searchItem = this.Find<NavigationViewItem>(TestFileName);
        Assert.IsNotNull(searchItem);
        searchItem.Click();

        OpenContextMenu();
        var copyPathButton = this.Find<NavigationViewItem>("Copy path");
        Assert.IsNotNull(copyPathButton);
        copyPathButton.Click();

        var clipboardContent = System.Windows.Forms.Clipboard.GetText();
        Assert.IsTrue(clipboardContent.Contains(TestFileName), $"Clipboard content does not contain the expected file name. clipboard: {clipboardContent}");
    }

    [TestMethod]
    public void IndexerShowInFolderTest()
    {
        EnterIndexerExtension();
        SetFilesExtensionSearchBox(TestFileName);

        var searchItem = this.Find<NavigationViewItem>(TestFileName);
        Assert.IsNotNull(searchItem);
        searchItem.Click();

        OpenContextMenu();
        var showInFolderButton = this.Find<NavigationViewItem>("Show in folder");
        Assert.IsNotNull(showInFolderButton);
        showInFolderButton.Click();

        var fileExplorer = FindExplorerWindow(TestFolderName, global: true, timeoutMS: 20000);

        Assert.IsNotNull(fileExplorer);
    }

    [TestMethod]
    public void IndexerOpenPathInConsoleTest()
    {
        EnterIndexerExtension();
        SetFilesExtensionSearchBox(TestFileName);

        var searchItem = this.Find<NavigationViewItem>(TestFileName);
        Assert.IsNotNull(searchItem);
        searchItem.Click();

        OpenContextMenu();
        var copyPathButton = this.Find<NavigationViewItem>("Open path in console");
        Assert.IsNotNull(copyPathButton);
        copyPathButton.Click();

        var textItem = FindByPartialName("C:\\Windows\\system32\\cmd.exe", global: true);
        Assert.IsNotNull(textItem, "The console did not open with the expected path.");
    }

    [TestMethod]
    public void IndexerOpenPropertiesTest()
    {
        EnterIndexerExtension();
        SetFilesExtensionSearchBox(TestFileName);

        var searchItem = this.Find<NavigationViewItem>(TestFileName);
        Assert.IsNotNull(searchItem);
        searchItem.Click();

        OpenContextMenu();
        var copyPathButton = this.Find<NavigationViewItem>("Properties");
        Assert.IsNotNull(copyPathButton);
        copyPathButton.Click();

        var propertiesWindow = FindByClassNameAndNamePattern<Window>("#32770", "Properties", global: true);
        Assert.IsNotNull(propertiesWindow, "The properties window did not open for the selected file.");
    }
}
