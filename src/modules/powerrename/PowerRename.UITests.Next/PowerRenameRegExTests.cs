// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.PowerRename.UITests;

/// <summary>
/// Regular-expression search and replace, date/time replacement terms, and the Boost engine.
/// </summary>
/// <remarks>
/// Covers checklist items 12-15 of microsoft/PowerToys#40663 and replaces the legacy
/// <c>BasicRenameTests.BasicRegularMatch</c>.
/// </remarks>
public sealed partial class PowerRenameTests
{
    [TestMethod("PowerRename.RegEx.ToggleChangesMatching")]
    [TestCategory("PowerRename")]
    public void RegularExpressionToggleChangesWhatMatches()
    {
        // Legacy BasicRegularMatch: the pattern only matches once regex mode is on.
        var folder = CreateTestFolder();
        var first = CreateFile(folder, "testCase1.txt");
        var second = CreateFile(folder, "testCase2.txt");
        var other = CreateFile(folder, "other.txt");
        var window = LaunchPowerRename(first, second, other);

        SetSearchText(window, "^test.*\\.txt$");
        SetReplaceText(window, "matched.txt");
        WaitForRenamedCount(window, 0);

        SetOptionCheckBox(window, RegularExpressionsAutomationId, true);
        WaitForRenamedCount(window, 2);
        WaitForPreviewName(window, "matched.txt");
    }

    [TestMethod("PowerRename.RegEx.CaptureGroup")]
    [TestCategory("PowerRename")]
    public void RegularExpressionSearchAndReplaceUsesCaptureGroups()
    {
        // Checklist items 12 and 13 — search "(.*).png" and replace "foo_$1.png".
        var folder = CreateTestFolder();
        var image = CreateFile(folder, "photo.png");
        var text = CreateFile(folder, "notes.txt");
        var window = LaunchPowerRename(image, text);

        SetOptionCheckBox(window, RegularExpressionsAutomationId, true);
        SetSearchText(window, "(.*).png");
        SetReplaceText(window, "foo_$1.png");

        WaitForPreviewName(window, "foo_photo.png");
        WaitForRenamedCount(window, 1);

        ApplyRenameAndAssertEntries(window, folder, "foo_photo.png", "notes.txt");
    }

    [TestMethod("PowerRename.RegEx.FileCreationDateTime")]
    [TestCategory("PowerRename")]
    public void ReplaceTermCanUseFileCreationDateAndTime()
    {
        // Checklist item 14 — exercise both example forms, including milliseconds and month name.
        var folder = CreateTestFolder();
        var source = CreateFile(folder, "stamp.txt");
        var fileTime = new DateTime(2020, 2, 3, 4, 5, 6, 789, DateTimeKind.Local);
        File.SetCreationTime(source, fileTime);
        File.SetLastWriteTime(source, fileTime);
        File.SetLastAccessTime(source, fileTime);
        fileTime = File.GetCreationTime(source);

        var window = LaunchPowerRename(source);
        SetSearchText(window, "stamp");
        SetReplaceText(window, "$hh-$mm-$ss-$fff_$DD_$MMMM_$YYYY");

        var month = fileTime.ToString("MMMM", CultureInfo.CurrentCulture);
        month = char.ToUpper(month[0], CultureInfo.CurrentCulture) + month[1..];
        var expectedName = $"{fileTime:HH-mm-ss-fff_dd}_{month}_{fileTime:yyyy}.txt";
        WaitForPreviewName(window, expectedName);
        ApplyRenameAndAssertEntries(window, folder, expectedName);
    }

    [TestMethod("PowerRename.RegEx.BoostPerlSyntax")]
    [TestCategory("PowerRename")]
    public void BoostLibraryEnablesPerlRegularExpressionSyntax()
    {
        // Checklist item 15 — a lookbehind only resolves with the Boost engine. UseBoostLib is read
        // when the rename engine is constructed, so each half needs its own PowerRename process.
        var folder = CreateTestFolder();
        var matching = CreateFile(folder, "test.txt");
        var notMatching = CreateFile(folder, "est.txt");

        ConfigureModuleSettings(useBoostLib: false, persistState: false, mruEnabled: false);
        var window = LaunchPowerRename(matching, notMatching);
        SetOptionCheckBox(window, RegularExpressionsAutomationId, true);
        SetSearchText(window, "(?<=t)est");
        SetReplaceText(window, "XYZ");
        WaitForRenamedCount(window, 0);
        ClosePowerRenameWindows();

        ConfigureModuleSettings(useBoostLib: true, persistState: false, mruEnabled: false);
        window = LaunchPowerRename(matching, notMatching);
        SetOptionCheckBox(window, RegularExpressionsAutomationId, true);
        SetSearchText(window, "(?<=t)est");
        SetReplaceText(window, "XYZ");
        WaitForPreviewName(window, "tXYZ.txt");
        WaitForRenamedCount(window, 1);

        ApplyRenameAndAssertEntries(window, folder, "tXYZ.txt", "est.txt");
    }
}
