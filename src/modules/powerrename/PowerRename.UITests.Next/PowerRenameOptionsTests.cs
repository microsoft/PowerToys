// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.PowerRename.UITests;

/// <summary>
/// Rename options exposed by the PowerRename window: text formatting, item-type exclusions, the
/// "Apply to" scope, enumeration, case sensitivity, and match-all-occurrences.
/// </summary>
/// <remarks>
/// Covers checklist items 6-11 of microsoft/PowerToys#40663 and replaces the legacy
/// <c>BasicRenameTests.BasicInput</c>, <c>BasicMatchFileName</c>, <c>BasicMatchAllOccurrences</c>,
/// and <c>BasicCaseSensitive</c>.
/// </remarks>
public sealed partial class PowerRenameTests
{
    private const string Uppercase = "toggleButton_upperCase";
    private const string Lowercase = "toggleButton_lowerCase";
    private const string TitleCase = "toggleButton_titleCase";
    private const string CapitalizeEachWord = "toggleButton_capitalize";
    private const string IncludeFiles = "toggleButton_includeFiles";
    private const string IncludeFolders = "toggleButton_includeFolders";
    private const string IncludeSubfolders = "toggleButton_includeSubfolders";

    private static readonly string[] TextFormattingModes = { Uppercase, Lowercase, TitleCase, CapitalizeEachWord };

    [TestMethod("PowerRename.Preview.SearchAndReplace")]
    [TestCategory("PowerRename")]
    public void SearchAndReplaceUpdatesThePreview()
    {
        // Legacy BasicInput + BasicMatchFileName.
        var folder = CreateTestFolder();
        var source = CreateFile(folder, "testCase1.txt");
        var window = LaunchPowerRename(source);

        SetSearchText(window, "testCase1");
        SetReplaceText(window, "replaced");

        Assert.AreEqual("testCase1", GetSearchText(window), "The search box did not keep the typed text.");
        Assert.AreEqual("replaced", GetReplaceText(window), "The replace box did not keep the typed text.");
        WaitForPreviewName(window, "replaced.txt");

        ApplyRenameAndAssertEntries(window, folder, "replaced.txt");
    }

    [TestMethod("PowerRename.TextFormatting.MutuallyExclusive")]
    [TestCategory("PowerRename")]
    public void TextFormattingModesAreMutuallyExclusive()
    {
        // Checklist item 6 — only one casing mode can be active at a time.
        var folder = CreateTestFolder();
        var source = CreateFile(folder, "hello world.TXT");
        var window = LaunchPowerRename(source);

        foreach (var mode in TextFormattingModes)
        {
            SetToggleButton(window, mode, true);
            foreach (var other in TextFormattingModes.Where(candidate => candidate != mode))
            {
                Assert.IsFalse(
                    IsToggleButtonOn(window, other),
                    $"Selecting '{mode}' left '{other}' active; the casing modes must be mutually exclusive.");
            }
        }
    }

    [TestMethod("PowerRename.TextFormatting.Rename")]
    [TestCategory("PowerRename")]
    [DataRow(Uppercase, "HELLO WORLD.TXT")]
    [DataRow(Lowercase, "hello world.txt")]
    [DataRow(TitleCase, "Hello World.TXT")]
    public void TextFormattingRenamesUsingTheSelectedMode(string modeAutomationId, string expectedName)
    {
        // Checklist item 6 — each casing mode applied on its own with no search term.
        var folder = CreateTestFolder();
        var source = CreateFile(folder, "hello world.TXT");
        var window = LaunchPowerRename(source);

        SetToggleButton(window, modeAutomationId, true);
        WaitForPreviewName(window, expectedName);

        ApplyRenameAndAssertEntries(window, folder, expectedName);
    }

    [TestMethod("PowerRename.Exclude.FilesFoldersSubfolders")]
    [TestCategory("PowerRename")]
    public void ExcludingFilesFoldersAndSubfolderItemsFiltersTheRename()
    {
        // Checklist item 7 — the three exclusions are independent and combinable.
        var folder = CreateTestFolder();
        var file = CreateFile(folder, "filex.txt");
        var subFolder = CreateSubFolder(folder, "folderx");
        CreateFile(subFolder, "innerx.txt");

        var window = LaunchPowerRename(file, subFolder);
        SetSearchText(window, "x");
        SetReplaceText(window, "y");

        WaitForPreviewName(window, "filey.txt");
        WaitForPreviewName(window, "foldery");
        WaitForPreviewName(window, "innery.txt");

        SetToggleButton(window, IncludeFiles, false);
        WaitForPreviewToDropName(window, "filey.txt");
        WaitForPreviewToDropName(window, "innery.txt");
        WaitForPreviewName(window, "foldery");

        SetToggleButton(window, IncludeFiles, true);
        SetToggleButton(window, IncludeFolders, false);
        WaitForPreviewToDropName(window, "foldery");
        WaitForPreviewName(window, "filey.txt");
        WaitForPreviewName(window, "innery.txt");

        SetToggleButton(window, IncludeFolders, true);
        SetToggleButton(window, IncludeSubfolders, false);
        WaitForPreviewToDropName(window, "innery.txt");
        WaitForPreviewName(window, "filey.txt");
        WaitForPreviewName(window, "foldery");

        // Several exclusions at once: with files and folders both excluded nothing is left to rename.
        SetToggleButton(window, IncludeFiles, false);
        SetToggleButton(window, IncludeFolders, false);
        WaitForRenamedCount(window, 0);
    }

    [TestMethod("PowerRename.ApplyTo.Scope")]
    [TestCategory("PowerRename")]
    [DataRow("RenamePartsFilenameAndExtension", "info.info")]
    [DataRow("RenamePartsFilenameOnly", "info.data")]
    [DataRow("RenamePartsExtensionOnly", "data.info")]
    public void ApplyToScopeLimitsTheReplacement(string itemAutomationId, string expectedName)
    {
        // Checklist item 8 — the "Apply to" combo box selects exactly one scope.
        var folder = CreateTestFolder();
        var source = CreateFile(folder, "data.data");
        var window = LaunchPowerRename(source);

        SetOptionCheckBox(window, MatchAllOccurrencesAutomationId, true);
        SetSearchText(window, "data");
        SetReplaceText(window, "info");
        SelectApplyTo(window, itemAutomationId);

        WaitForPreviewName(window, expectedName);
        ApplyRenameAndAssertEntries(window, folder, expectedName);
    }

    [TestMethod("PowerRename.Enumerate.AdvancedCounter")]
    [TestCategory("PowerRename")]
    public void EnumerateItemsHonoursAdvancedCounterSyntax()
    {
        // Checklist item 9 — ${start=10,increment=2,padding=4} over three items.
        var folder = CreateTestFolder();
        var first = CreateFile(folder, "a.txt");
        var second = CreateFile(folder, "b.txt");
        var third = CreateFile(folder, "c.txt");

        var window = LaunchPowerRename(first, second, third);
        SetToggleButton(window, "toggleButton_enumItems", true);
        SetOptionCheckBox(window, RegularExpressionsAutomationId, true);
        SetSearchText(window, ".*");
        SetReplaceText(window, "item_${start=10,increment=2,padding=4}.txt");

        WaitForPreviewName(window, "item_0010.txt");
        WaitForPreviewName(window, "item_0012.txt");
        WaitForPreviewName(window, "item_0014.txt");

        ApplyRenameAndAssertEntries(window, folder, "item_0010.txt", "item_0012.txt", "item_0014.txt");
    }

    [TestMethod("PowerRename.Search.CaseSensitive")]
    [TestCategory("PowerRename")]
    public void CaseSensitiveSearchOnlyMatchesTheExactCasing()
    {
        // Checklist item 10, replacing the legacy BasicCaseSensitive.
        var folder = CreateTestFolder();
        var source = CreateFile(folder, "testCase1.txt");
        var window = LaunchPowerRename(source);

        SetSearchText(window, "testcase1");
        SetReplaceText(window, "match1");
        WaitForPreviewName(window, "match1.txt");
        WaitForRenamedCount(window, 1);

        SetOptionCheckBox(window, CaseSensitiveAutomationId, true);
        WaitForPreviewToDropName(window, "match1.txt");
        WaitForRenamedCount(window, 0);

        SetOptionCheckBox(window, CaseSensitiveAutomationId, false);
        WaitForPreviewName(window, "match1.txt");
        ApplyRenameAndAssertEntries(window, folder, "match1.txt");
    }

    [TestMethod("PowerRename.Search.MatchAllOccurrences")]
    [TestCategory("PowerRename")]
    public void MatchAllOccurrencesReplacesEveryMatch()
    {
        // Checklist item 11, replacing the legacy BasicMatchAllOccurrences.
        var folder = CreateTestFolder();
        var source = CreateFile(folder, "test-test.txt");
        var window = LaunchPowerRename(source);

        SetSearchText(window, "test");
        SetReplaceText(window, "best");
        WaitForPreviewName(window, "best-test.txt");

        SetOptionCheckBox(window, MatchAllOccurrencesAutomationId, true);
        WaitForPreviewName(window, "best-best.txt");

        ApplyRenameAndAssertEntries(window, folder, "best-best.txt");
    }
}
