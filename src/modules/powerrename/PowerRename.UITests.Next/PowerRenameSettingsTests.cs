// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.PowerRename.UITests;

/// <summary>
/// PowerRename settings that change what the window offers on launch: the autocomplete (MRU) list
/// and restoring the values from the last use.
/// </summary>
/// <remarks>Covers checklist items 4 and 5 of microsoft/PowerToys#40663.</remarks>
[TestClass]
[DoNotParallelize]
public sealed class PowerRenameSettingsTests : PowerRenameTestBase
{
    private const string CaseSensitive = "Case sensitive";
    private const string SeedSearchTerm = "seedterm";
    private const string SeedReplaceTerm = "replaceterm";

    [TestInitialize]
    public void PrepareTest() => ClearPersistedRenameState();

    [TestMethod("PowerRename.Settings.AutoComplete")]
    [TestCategory("PowerRename")]
    public void AutoCompleteOffersPreviousTermsOnlyWhileEnabled()
    {
        // Checklist item 4 — the search box suggests earlier terms exactly while autocomplete is on.
        ConfigureModuleSettings(mruEnabled: true, persistState: true);
        SeedMostRecentlyUsedTerms();

        var folder = CreateTestFolder();
        var plain = CreateFile(folder, "plain.txt");

        var window = LaunchPowerRename(plain);
        Assert.IsTrue(
            SuggestionAppears(window, "seed", SeedSearchTerm),
            $"Autocomplete was enabled but the search box never suggested '{SeedSearchTerm}'.");
        ClosePowerRenameWindows();

        ConfigureModuleSettings(mruEnabled: false, persistState: true);
        window = LaunchPowerRename(plain);
        Assert.IsFalse(
            SuggestionAppears(window, "seed", SeedSearchTerm),
            $"Autocomplete was disabled but the search box still suggested '{SeedSearchTerm}'.");
    }

    [TestMethod("PowerRename.Settings.RestoreLastUsedValues")]
    [TestCategory("PowerRename")]
    public void LastUsedValuesAreRestoredOnlyWhileEnabled()
    {
        // Checklist item 5 — "Show values from last use" restores the search/replace text and flags.
        ConfigureModuleSettings(persistState: true, mruEnabled: true);

        var folder = CreateTestFolder();
        var source = CreateFile(folder, "keep.txt");
        var window = LaunchPowerRename(source);
        SetSearchText(window, "keep");
        SetReplaceText(window, "kept");
        SetOptionCheckBox(window, CaseSensitive, true);
        WaitForPreviewName(window, "kept.txt");
        ApplyRenameAndAssertEntries(window, folder, "kept.txt");
        ClosePowerRenameWindows();

        var second = CreateFile(folder, "second.txt");
        window = LaunchPowerRename(second);
        Assert.AreEqual("keep", GetSearchText(window), "The search text from the last use was not restored.");
        Assert.AreEqual("kept", GetReplaceText(window), "The replace text from the last use was not restored.");
        Assert.IsTrue(
            FindExact<CheckBox>(window, CaseSensitive, PreviewTimeoutMS)!.IsChecked,
            "The 'Case sensitive' flag from the last use was not restored.");
        ClosePowerRenameWindows();

        ConfigureModuleSettings(persistState: false, mruEnabled: true);
        window = LaunchPowerRename(second);

        // An empty AutoSuggestBox reports its placeholder through UIA, so assert the previous values
        // are gone rather than that the boxes read as empty.
        Assert.AreNotEqual("keep", GetSearchText(window), "The search text was restored even though the setting is off.");
        Assert.AreNotEqual("kept", GetReplaceText(window), "The replace text was restored even though the setting is off.");
        Assert.IsFalse(
            FindExact<CheckBox>(window, CaseSensitive, PreviewTimeoutMS)!.IsChecked,
            "A flag was restored even though the setting is off.");
    }

    /// <summary>Perform one real rename so PowerRename records the terms in its MRU lists.</summary>
    private void SeedMostRecentlyUsedTerms()
    {
        var folder = CreateTestFolder();
        var source = CreateFile(folder, SeedSearchTerm + ".txt");
        var window = LaunchPowerRename(source);
        SetSearchText(window, SeedSearchTerm);
        SetReplaceText(window, SeedReplaceTerm);
        ApplyRenameAndAssertEntries(window, folder, SeedReplaceTerm + ".txt");
        ClosePowerRenameWindows();
    }

    /// <summary>
    /// Type into the search box with real keystrokes — the suggestion list only opens for user input,
    /// not for a programmatic value change — and report whether the expected suggestion is offered.
    /// </summary>
    private bool SuggestionAppears(Session window, string typedText, string expectedSuggestion)
    {
        Step($"Typing '{typedText}' into the search box to open the suggestion list");
        var box = window.Find<TextBox>(By.Name(SearchBoxName), timeoutMS: PreviewTimeoutMS);
        box.SetText(string.Empty);
        box.Focus();
        Thread.Sleep(300);
        KeyboardHelper.SendKeySequence(typedText.Select(ToKey).ToArray());
        Thread.Sleep(500);

        if (HasSuggestion(window, expectedSuggestion))
        {
            return true;
        }

        // A list that did not open on typing still opens on Down; only then is "no suggestion" real.
        KeyboardHelper.SendKey(Key.Down);
        Thread.Sleep(500);
        return HasSuggestion(window, expectedSuggestion);
    }

    private static bool HasSuggestion(Session window, string suggestion) =>
        FindExact<Element>(window, suggestion, timeoutMS: 2_000) is not null;

    private static Key ToKey(char character)
    {
        var upper = char.ToUpperInvariant(character);
        Assert.IsTrue(upper is >= 'A' and <= 'Z', $"Only letters can be typed by this helper, got '{character}'.");
        return (Key)upper;
    }
}
