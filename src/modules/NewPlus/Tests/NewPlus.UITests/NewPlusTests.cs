// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.NewPlus.UITests;

/// <summary>Exercises the New+ release checklist through Settings and Explorer.</summary>
/// <remarks>
/// Covers microsoft/PowerToys#40683: enabled state (1-2), template location (3), file/folder
/// creation (4-5), removal (6), default templates (7), and display options (8-9).
/// Windows 11 always exercises the tier-1 menu; New+ intentionally has no classic fallback there.
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed partial class NewPlusTests : UITestBase
{
    [TestMethod]
    [TestCategory("NewPlus")]
    public void ContextMenuTracksModuleEnabledState()
    {
        var explorer = OpenExplorer();
        AssertRootMenu(explorer, expected: true);

        SetModuleEnabled(false);
        AssertRootMenu(explorer, expected: false);

        SetModuleEnabled(true);
        AssertRootMenu(explorer, expected: true);
    }

    [TestMethod]
    [TestCategory("NewPlus")]
    public void ChangingTemplateLocationCreatesAnEmptyFolder()
    {
        var templates = ChooseNewTemplateFolder();
        Assert.IsTrue(Directory.Exists(templates), "The folder picker did not create the new template folder.");
        Assert.AreEqual(0, Directory.GetFileSystemEntries(templates).Length, "Changing location unexpectedly populated the folder.");
        AssertTemplateMenu(OpenExplorer(), []);
    }

    [TestMethod]
    [TestCategory("NewPlus")]
    public void FileTemplateCreatesAFileWithMatchingContents()
    {
        var templates = ChooseNewTemplateFolder();
        var source = Path.Combine(templates, "Report.txt");
        File.WriteAllText(source, "New+ file template\r\nSecond line.\r\n");

        var explorer = OpenExplorer();
        AssertTemplateMenu(explorer, ["Report.txt"]);
        InvokeTemplate(explorer, "Report.txt");
        FileSystemAssert.AreDirectoryTreesEqual(templates, outputFolder);
    }

    [TestMethod]
    [TestCategory("NewPlus")]
    public void FolderTemplateCopiesNestedFilesAndEmptyFolders()
    {
        var templates = ChooseNewTemplateFolder();
        var project = Directory.CreateDirectory(Path.Combine(templates, "Project")).FullName;
        Directory.CreateDirectory(Path.Combine(project, "Nested", "Empty"));
        File.WriteAllText(Path.Combine(project, "Readme.txt"), "Root file");
        File.WriteAllBytes(Path.Combine(project, "Nested", "Data.bin"), [0, 1, 127, 128, 255]);

        var explorer = OpenExplorer();
        AssertTemplateMenu(explorer, ["Project"]);
        InvokeTemplate(explorer, "Project");
        FileSystemAssert.AreDirectoryTreesEqual(templates, outputFolder);
    }

    [TestMethod]
    [TestCategory("NewPlus")]
    public void DeletingTemplatesRemovesThemFromTheMenu()
    {
        var templates = ChooseNewTemplateFolder();
        var file = Path.Combine(templates, "Remove me.txt");
        var folder = Directory.CreateDirectory(Path.Combine(templates, "Remove folder")).FullName;
        File.WriteAllText(file, "Template to remove");
        File.WriteAllText(Path.Combine(folder, "Child.txt"), "Nested template to remove");

        var explorer = OpenExplorer();
        AssertTemplateMenu(explorer, ["Remove folder", "Remove me.txt"]);
        File.Delete(file);
        Directory.Delete(folder, recursive: true);

        Assert.AreEqual(0, Directory.GetFileSystemEntries(templates).Length);
        AssertTemplateMenu(explorer, []);
        Assert.AreEqual(0, Directory.GetFileSystemEntries(outputFolder).Length, "Inspecting the menu created an item.");
    }

    [TestMethod]
    [TestCategory("NewPlus")]
    public void ReenablingWithAnEmptyFolderRestoresDefaultTemplates()
    {
        var templates = ChooseNewTemplateFolder();
        var explorer = OpenExplorer();
        AssertTemplateMenu(explorer, []);
        var examples = Path.Combine(
            Path.GetDirectoryName(GetSettingsExecutable())!,
            "Assets",
            "NewPlus",
            "Templates");
        Assert.IsTrue(Directory.Exists(examples), $"The product payload is missing its default templates: {examples}");
        var expectedNames = Directory.GetFileSystemEntries(examples).Select(Path.GetFileName).Cast<string>().ToArray();
        Assert.IsTrue(expectedNames.Length > 0, "The product payload has no default template examples.");

        SetModuleEnabled(false);
        AssertRootMenu(explorer, expected: false);
        SetModuleEnabled(true);

        FileSystemAssert.AreDirectoryTreesEqual(examples, templates);
        AssertTemplateMenu(explorer, expectedNames);
    }

    [TestMethod]
    [TestCategory("NewPlus")]
    public void HideFileExtensionChangesMenuButPreservesCreatedExtension()
    {
        var templates = ChooseNewTemplateFolder();
        File.WriteAllText(Path.Combine(templates, "Report.txt"), "Keep the extension on disk");
        Directory.CreateDirectory(Path.Combine(templates, "Folder.with.dots"));
        var explorer = OpenExplorer();

        AssertTemplateMenu(explorer, ["Folder.with.dots", "Report.txt"]);
        SetDisplayOption(HideExtensionName, "HideFileExtension", true);
        AssertTemplateMenu(explorer, ["Folder.with.dots", "Report"]);
        InvokeTemplate(explorer, "Report");
        FileSystemAssert.AreFilesEqual(Path.Combine(templates, "Report.txt"), Path.Combine(outputFolder, "Report.txt"));
        Assert.IsFalse(File.Exists(Path.Combine(outputFolder, "Report")), "Hiding an extension removed it from the created file.");

        SetDisplayOption(HideExtensionName, "HideFileExtension", false);
        AssertTemplateMenu(explorer, ["Folder.with.dots", "Report.txt"]);
    }

    [TestMethod]
    [TestCategory("NewPlus")]
    public void HideStartingDigitsChangesMenuAndCreatedNames()
    {
        const string unicodeName = "Cafe\u0301-\u6F22-\U0001F680.txt";
        const string numberedName = "01. " + unicodeName;
        var templates = ChooseNewTemplateFolder();
        File.WriteAllText(Path.Combine(templates, numberedName), "Numbered Unicode template");
        File.WriteAllText(Path.Combine(templates, "001231.txt"), "Digits are the entire filename");
        var sourceFolder = Directory.CreateDirectory(Path.Combine(templates, "02. Project")).FullName;
        File.WriteAllText(Path.Combine(sourceFolder, "Child.txt"), "Folder contents");
        var explorer = OpenExplorer();

        AssertTemplateMenu(explorer, ["02. Project", "001231.txt", numberedName]);
        SetDisplayOption(HideDigitsName, "HideStartingDigits", true);
        AssertTemplateMenu(explorer, ["Project", "001231.txt", unicodeName]);
        InvokeTemplate(explorer, unicodeName);
        FileSystemAssert.AreFilesEqual(Path.Combine(templates, numberedName), Path.Combine(outputFolder, unicodeName));
        InvokeTemplate(explorer, "Project");
        FileSystemAssert.AreDirectoryTreesEqual(sourceFolder, Path.Combine(outputFolder, "Project"));

        SetDisplayOption(HideDigitsName, "HideStartingDigits", false);
        AssertTemplateMenu(explorer, ["02. Project", "001231.txt", numberedName]);
        InvokeTemplate(explorer, numberedName);
        FileSystemAssert.AreFilesEqual(Path.Combine(templates, numberedName), Path.Combine(outputFolder, numberedName));
    }
}
