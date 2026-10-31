// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UITests;

/// <summary>
/// The snippet editor, opened with Manage snippets: listing, adding, validating, editing, duplicating, deleting and
/// searching snippets, and the snippets database menu. Every change is checked in the database file too.
/// </summary>
[TestClass]
public sealed class TextExpanderEditorTests : TextExpanderTestBase
{
    private static readonly string[] StarterTriggers = ["!ddate", "!ttime", "!ddt", "!isodate", "!mysig", "!myemail"];

    internal override IReadOnlyList<Snippet>? InitialSnippets =>
        TestContext.TestName == nameof(CreatesStarterSnippetsWhenThereIsNoDatabase) ? null : DefaultSnippets;

    /// <summary>The editor lists the database's snippets and says where the database is: Saved on this PC.</summary>
    [TestMethod("TextExpander.Editor.ListsSnippetsAndWhereTheyAreSaved")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Editor")]
    public void ListsSnippetsAndWhereTheyAreSaved()
    {
        var editor = OpenEditor(OpenTextExpanderPage());
        editor.WaitForCount(DefaultSnippets.Count);
        foreach (var snippet in DefaultSnippets)
        {
            Assert.IsTrue(editor.WaitForRow(snippet.Trigger, present: true), $"The editor did not list '{snippet.Trigger}'. Rows: {editor.DescribeRows()}.");
        }

        var databaseButton = editor.Session.Find<Button>(By.AccessibilityId("Button_TextExpander_SnippetFolder"), 10_000);
        Assert.AreEqual("Saved on this PC", databaseButton.Name, "The snippets database button should say the database is in the default location.");

        editor.OpenDatabaseMenu();
        Assert.IsTrue(
            editor.WaitForText($"Saved on this PC · {DefaultSnippets.Count} snippets"),
            "The snippets database menu did not give the place and the snippet count.");
        Assert.IsTrue(editor.WaitForText(TextExpanderFiles.SnippetsPath), $"The snippets database menu did not give the database's path, {TextExpanderFiles.SnippetsPath}.");

        Step("Selecting Refresh");
        editor.Session.Find<Button>(By.AccessibilityId("Button_TextExpander_ReloadSnippets"), 10_000).Invoke(msPostAction: 0);
        Assert.IsTrue(editor.WaitForText("Snippets refreshed."), "Refresh did not report that it read the database again.");
        editor.WaitForCount(DefaultSnippets.Count);
    }

    /// <summary>Without a database, the default location gets the six starter snippets, which the editor lists.</summary>
    [TestMethod("TextExpander.Editor.CreatesStarterSnippetsWhenThereIsNoDatabase")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Editor")]
    public void CreatesStarterSnippetsWhenThereIsNoDatabase()
    {
        var editor = OpenEditor(OpenTextExpanderPage());
        editor.WaitForCount(StarterTriggers.Length);
        foreach (var trigger in StarterTriggers)
        {
            Assert.IsTrue(editor.WaitForRow(trigger, present: true), $"The editor did not list the starter '{trigger}'. Rows: {editor.DescribeRows()}.");
        }

        var saved = TextExpanderFiles.ReadSnippets();
        Assert.IsNotNull(saved, "No snippets database was created in the default location.");
        CollectionAssert.AreEqual(StarterTriggers, saved.Select(snippet => snippet.Trigger).ToArray(), $"The database doesn't hold the starter snippets: {TextExpanderFiles.Describe(saved)}.");
        Assert.AreEqual("you@example.com", saved.Single(snippet => snippet.Trigger == "!myemail").Replacement);
    }

    /// <summary>New snippet saves a trigger and its replacement to the database and lists it.</summary>
    [TestMethod("TextExpander.Editor.AddsASnippet")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Editor")]
    public void AddsASnippet()
    {
        var added = new Snippet(";tenew", "Added in the editor");
        var editor = OpenEditor(OpenTextExpanderPage());
        editor.WaitForCount(DefaultSnippets.Count);

        editor.OpenNewSnippet();
        Assert.IsFalse(editor.SaveButton.IsEnabled, "Save should be off while the new snippet has no trigger.");
        editor.SetTrigger(added.Trigger);
        editor.TypeReplacement(added.Replacement);
        Assert.IsTrue(editor.WaitForReplacementText(added.Replacement), $"The replacement box shows '{TextExpanderFiles.Visible(editor.ReplacementText)}'.");
        editor.Save();

        Assert.IsTrue(editor.WaitForText("Snippet saved."), "The editor did not say the snippet was saved.");
        editor.WaitForCount(DefaultSnippets.Count + 1);
        Assert.IsTrue(editor.WaitForRow(added.Trigger, present: true), $"The new snippet isn't listed. Rows: {editor.DescribeRows()}.");
        AssertDatabase([.. DefaultSnippets, added]);
    }

    /// <summary>
    /// The trigger is checked as it's typed, and Save stays off while it's wrong. Cancel on an edited snippet asks
    /// before discarding it, and Discard saves nothing.
    /// </summary>
    [TestMethod("TextExpander.Editor.ValidatesTheTriggerAndAsksBeforeDiscarding")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Editor")]
    public void ValidatesTheTriggerAndAsksBeforeDiscarding()
    {
        var editor = OpenEditor(OpenTextExpanderPage());
        editor.WaitForCount(DefaultSnippets.Count);
        editor.OpenNewSnippet();

        editor.SetTrigger(Greeting.Trigger);
        Assert.IsTrue(
            editor.WaitForText($"Another snippet already uses the trigger \"{Greeting.Trigger}\"."),
            "A trigger another snippet uses was not reported.");
        Assert.IsFalse(editor.SaveButton.IsEnabled, "Save should be off while the trigger is taken.");

        editor.SetTrigger(" ;tespace");
        Assert.IsTrue(editor.WaitForText("A trigger can't start or end with a space."), "A trigger starting with a space was not reported.");
        Assert.IsFalse(editor.SaveButton.IsEnabled, "Save should be off while the trigger starts with a space.");

        editor.SetTrigger(";tevalid");
        Assert.IsTrue(
            editor.Session.WaitFor(() => editor.SaveButton.IsEnabled, 10_000, pollIntervalMS: 250),
            $"Save should be on once the trigger is valid. {editor.DescribeDialog()}");
        editor.TypeReplacement("Never saved");

        editor.Cancel();
        Assert.IsTrue(editor.WaitForText("Discard your changes?"), "Cancel on an edited snippet did not ask before discarding it.");
        Assert.IsTrue(editor.IsDialogOpen, "The snippet dialog closed before the discard question was answered.");

        Step("Selecting Keep editing");
        editor.Session.WaitForExact<Button>("Keep editing", 10_000).Invoke(msPostAction: 0);
        Assert.IsTrue(editor.Session.WaitForExactNameGone("Discard your changes?", 10_000), "Keep editing did not dismiss the question.");
        Assert.AreEqual(";tevalid", editor.TriggerText, "Keep editing lost the trigger.");

        editor.Cancel();
        Assert.IsTrue(editor.WaitForText("Discard your changes?"), "Cancel did not ask again.");
        Step("Selecting Discard");
        editor.Session.WaitForExact<Button>("Discard", 10_000).Invoke(msPostAction: 0);
        Assert.IsTrue(editor.Session.WaitFor(() => !editor.IsDialogOpen, 10_000, pollIntervalMS: 250), "Discard did not close the snippet dialog.");

        editor.WaitForCount(DefaultSnippets.Count);
        Assert.IsFalse(editor.WaitForRow(";tevalid", present: true, timeoutMS: 2_000), "The discarded snippet was listed.");
        AssertDatabase(DefaultSnippets);
    }

    /// <summary>A row opens for editing with Enter; More options duplicates it into a new snippet, or deletes it after asking.</summary>
    [TestMethod("TextExpander.Editor.EditsDuplicatesAndDeletesSnippets")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Editor")]
    public void EditsDuplicatesAndDeletesSnippets()
    {
        var editor = OpenEditor(OpenTextExpanderPage());
        editor.WaitForCount(DefaultSnippets.Count);

        editor.OpenRow(Greeting.Trigger);
        Assert.AreEqual(Greeting.Trigger, editor.TriggerText, "The edited snippet's trigger isn't shown.");
        Assert.IsTrue(editor.WaitForReplacementText(Greeting.Replacement), $"The edited snippet's replacement isn't shown: '{TextExpanderFiles.Visible(editor.ReplacementText)}'.");
        var edited = Greeting with { Replacement = "Hello again from the editor" };
        editor.TypeReplacement(edited.Replacement);
        Assert.IsTrue(editor.WaitForReplacementText(edited.Replacement), $"The replacement box shows '{TextExpanderFiles.Visible(editor.ReplacementText)}'.");
        editor.Save();
        Assert.IsTrue(editor.WaitForText("Snippet saved."), "The editor did not say the snippet was saved.");
        AssertDatabase([edited, .. DefaultSnippets.Skip(1)]);

        editor.RunRowCommand(Address.Trigger, "MenuItem_TextExpander_DuplicateSnippet");
        Assert.IsTrue(editor.Session.WaitFor(() => editor.IsDialogOpen, 15_000, pollIntervalMS: 250), "Duplicate did not open the copy.");
        var copyTrigger = editor.TriggerText;
        Assert.IsFalse(string.IsNullOrWhiteSpace(copyTrigger), "Duplicate did not suggest a trigger for the copy.");
        Assert.AreNotEqual(Address.Trigger, copyTrigger, "Duplicate suggested the original's trigger.");
        Assert.IsTrue(editor.WaitForReplacementText(Address.Replacement), $"The copy's replacement is '{TextExpanderFiles.Visible(editor.ReplacementText)}'.");
        editor.Save();
        editor.WaitForCount(DefaultSnippets.Count + 1);
        AssertDatabase([edited, .. DefaultSnippets.Skip(1), new Snippet(copyTrigger, Address.Replacement)]);

        editor.RunRowCommand(Note.Trigger, "MenuItem_TextExpander_DeleteSnippet");
        Assert.IsTrue(editor.WaitForText("Delete snippet?"), "Delete did not ask first.");
        Step("Confirming Delete");
        editor.Session.WaitForId<Button>("PrimaryButton", "Delete", 10_000).Invoke(msPostAction: 0);
        Assert.IsTrue(editor.WaitForRow(Note.Trigger, present: false), $"The deleted snippet is still listed. Rows: {editor.DescribeRows()}.");
        Assert.IsTrue(editor.WaitForText("Snippet deleted."), "The editor did not say the snippet was deleted.");
        editor.WaitForCount(DefaultSnippets.Count);
        AssertDatabase([edited, Address, Letter, Ticket, new Snippet(copyTrigger, Address.Replacement)]);
    }

    /// <summary>Search filters by trigger and by replacement text, and Clear search shows every snippet again.</summary>
    [TestMethod("TextExpander.Editor.SearchFiltersSnippets")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Editor")]
    public void SearchFiltersSnippets()
    {
        var editor = OpenEditor(OpenTextExpanderPage());
        editor.WaitForCount(DefaultSnippets.Count);

        editor.Search("teaddr");
        editor.WaitForFilteredCount(1, DefaultSnippets.Count);
        Assert.IsTrue(editor.WaitForRow(Address.Trigger, present: true), $"The matching snippet isn't listed. Rows: {editor.DescribeRows()}.");
        Assert.IsTrue(editor.WaitForRow(Greeting.Trigger, present: false), $"A snippet that doesn't match is still listed. Rows: {editor.DescribeRows()}.");

        editor.Search("Redmond");
        editor.WaitForFilteredCount(1, DefaultSnippets.Count);
        Assert.IsTrue(editor.WaitForRow(Address.Trigger, present: true), $"Search didn't match the replacement text. Rows: {editor.DescribeRows()}.");

        editor.Search("no snippet has this");
        Assert.IsTrue(
            editor.WaitForText("No snippets match your search. Try different words or clear the search."),
            "A search without matches did not say so.");

        Step("Selecting Clear search");
        editor.Session.Find<Button>(By.AccessibilityId("Button_TextExpander_ClearSearch"), 10_000).Invoke(msPostAction: 0);
        editor.WaitForCount(DefaultSnippets.Count);
        foreach (var snippet in DefaultSnippets)
        {
            Assert.IsTrue(editor.WaitForRow(snippet.Trigger, present: true), $"Clear search did not list '{snippet.Trigger}'. Rows: {editor.DescribeRows()}.");
        }
    }

    /// <summary>A change another app or PC makes to the database file shows in the open editor by itself.</summary>
    [TestMethod("TextExpander.Editor.ShowsChangesMadeToTheDatabaseFile")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Editor")]
    public void ShowsChangesMadeToTheDatabaseFile()
    {
        var editor = OpenEditor(OpenTextExpanderPage());
        editor.WaitForCount(DefaultSnippets.Count);

        var synced = new Snippet(";tesync", "Written by another app");
        Step("Writing a snippet to the database file, as a sync service would");
        TextExpanderFiles.WriteSnippets([.. DefaultSnippets, synced]);

        editor.WaitForCount(DefaultSnippets.Count + 1, timeoutMS: 30_000);
        Assert.IsTrue(editor.WaitForRow(synced.Trigger, present: true), $"The snippet written to the file isn't listed. Rows: {editor.DescribeRows()}.");
    }

    private static void AssertDatabase(IReadOnlyList<Snippet> expected)
    {
        IReadOnlyList<Snippet>? actual = null;
        var saved = WaitHelper.WaitForStable(
            () => actual = TextExpanderFiles.ReadSnippets(),
            snippets => snippets is not null && snippets.SequenceEqual(expected),
            timeoutMS: 10_000,
            requiredConsecutiveMatches: 2,
            pollIntervalMS: 250,
            shouldRetryException: ex => ex is System.Text.Json.JsonException or IOException);
        Assert.IsTrue(
            saved.Succeeded,
            $"The database should hold {TextExpanderFiles.Describe(expected)}, but holds {TextExpanderFiles.Describe(actual)}.");
    }
}
