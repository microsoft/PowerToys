// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.PowerRename.UITests;

/// <summary>
/// The preview list itself: per-item inclusion, the renamed-only filter, and the header
/// select/deselect-all control.
/// </summary>
/// <remarks>
/// Covers checklist items 16-18 of microsoft/PowerToys#40663. The modern PowerRename window replaced
/// the clickable "Original"/"Renamed" column headers of the original checklist with a Filter flyout
/// and a header checkbox, so those two items are driven through their current surfaces.
/// </remarks>
public sealed partial class PowerRenameTests
{
    private const string FilterButtonAutomationId = "FilterButton";
    private const string ShowAllFilesAutomationId = "button_showAll";
    private const string ShowOnlyRenamedAutomationId = "button_showRenamed";
    private const string SelectAllCheckBoxAutomationId = "checkBox_selectAll";

    [TestMethod("PowerRename.FileList.UncheckExcludesItem")]
    [TestCategory("PowerRename")]
    public void UncheckedItemsAreExcludedFromTheRename()
    {
        // Checklist item 16.
        var folder = CreateTestFolder();
        var first = CreateFile(folder, "one.txt");
        var second = CreateFile(folder, "two.txt");
        var window = LaunchPowerRename(first, second);

        WaitForOriginalCount(window, 2);
        SetSearchText(window, "o");
        SetReplaceText(window, "0");
        WaitForPreviewName(window, "0ne.txt");
        WaitForPreviewName(window, "tw0.txt");
        WaitForRenamedCount(window, 2);

        SetRowChecked(window, "two.txt", false);
        WaitForRenamedCount(window, 1);

        ApplyRenameAndAssertEntries(window, folder, "0ne.txt", "two.txt");
    }

    [TestMethod("PowerRename.FileList.FilterRenamedOnly")]
    [TestCategory("PowerRename")]
    public void FilterCanShowOnlyItemsThatWillBeRenamed()
    {
        // Checklist item 17.
        var folder = CreateTestFolder();
        var renamed = CreateFile(folder, "match.txt");
        var untouched = CreateFile(folder, "other.txt");
        var window = LaunchPowerRename(renamed, untouched);

        SetSearchText(window, "match");
        SetReplaceText(window, "hit");
        WaitForPreviewName(window, "hit.txt");

        SelectFilter(window, ShowOnlyRenamedAutomationId);
        Assert.IsTrue(
            window.WaitFor(
                () => FindRowCheckBox(window, "other.txt", timeoutMS: 500) is null,
                timeoutMS: PreviewTimeoutMS,
                pollIntervalMS: 250),
            "The renamed-only filter still listed 'other.txt'.");
        Assert.IsNotNull(
            FindRowCheckBox(window, "match.txt", PreviewTimeoutMS),
            "The renamed-only filter dropped the item that will be renamed.");

        SelectFilter(window, ShowAllFilesAutomationId);
        Assert.IsTrue(
            window.WaitFor(
                () => FindRowCheckBox(window, "other.txt", timeoutMS: 500) is not null,
                timeoutMS: PreviewTimeoutMS,
                pollIntervalMS: 250),
            "Switching back to 'Show all files' did not restore the unmatched item.");
    }

    [TestMethod("PowerRename.FileList.SelectDeselectAll")]
    [TestCategory("PowerRename")]
    public void HeaderCheckBoxSelectsAndDeselectsEveryItem()
    {
        // Checklist item 18.
        var folder = CreateTestFolder();
        var first = CreateFile(folder, "one.txt");
        var second = CreateFile(folder, "two.txt");
        var window = LaunchPowerRename(first, second);

        SetSearchText(window, "o");
        SetReplaceText(window, "0");
        WaitForRenamedCount(window, 2);

        SetOptionCheckBox(window, SelectAllCheckBoxAutomationId, false);
        WaitForRenamedCount(window, 0);
        Assert.IsFalse(FindRowCheckBox(window, "one.txt", PreviewTimeoutMS)!.IsChecked, "'one.txt' stayed selected.");
        Assert.IsFalse(FindRowCheckBox(window, "two.txt", PreviewTimeoutMS)!.IsChecked, "'two.txt' stayed selected.");

        SetOptionCheckBox(window, SelectAllCheckBoxAutomationId, true);
        WaitForRenamedCount(window, 2);
        Assert.IsTrue(FindRowCheckBox(window, "one.txt", PreviewTimeoutMS)!.IsChecked, "'one.txt' was not re-selected.");
        Assert.IsTrue(FindRowCheckBox(window, "two.txt", PreviewTimeoutMS)!.IsChecked, "'two.txt' was not re-selected.");
    }

    /// <summary>Pick an entry of the Filter flyout, which the window hosts in its own popup.</summary>
    private void SelectFilter(Session window, string itemAutomationId)
    {
        Step($"Selecting filter '{itemAutomationId}'");
        window.Find<Button>(By.AccessibilityId(FilterButtonAutomationId), PreviewTimeoutMS)
            .Invoke(msPostAction: 500);
        window.Find<Element>(By.AccessibilityId(itemAutomationId), PreviewTimeoutMS)
            .Invoke(msPostAction: 500);
    }
}
