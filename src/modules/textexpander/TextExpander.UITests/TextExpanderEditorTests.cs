// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UITests;

/// <summary>
/// The snippet editor, opened with Manage snippets: listing, adding, validating, editing, duplicating, deleting and
/// searching snippets, and the snippets database menu. Every change is checked in the database file too.
/// </summary>
[TestClass]
[SkipWhenElevated]
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
    /// Insert variable > Clipboard > Only part of it > Link puts {{clipboard:link}} in Replacement. F2 on it opens the
    /// Clipboard panel, where picking from clipboard history makes it {{clipboard:pick:link}}, which the preview names and
    /// Save keeps.
    /// </summary>
    [TestMethod("TextExpander.Editor.InsertsAClipboardPartAndChangesItInTheClipboardPanel")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Editor")]
    public void InsertsAClipboardPartAndChangesItInTheClipboardPanel()
    {
        var added = new Snippet(";tehist", "{{clipboard:pick:link}}");
        var editor = OpenEditor(OpenTextExpanderPage());
        editor.WaitForCount(DefaultSnippets.Count);
        editor.OpenNewSnippet();
        editor.SetTrigger(added.Trigger);
        editor.FocusReplacement();

        Step("Selecting Insert variable > Clipboard > Only part of it > Link");
        editor.Session.Find<Button>(By.AccessibilityId("Button_TextExpander_InsertVariable"), 10_000).Invoke(msPostAction: 300);
        editor.Session.WaitForExact<Element>("Clipboard", 10_000, "MenuItem").Click(msPostAction: 300);
        editor.Session.WaitForExact<Element>("Only part of what's on your clipboard now", 10_000, "MenuItem").Click(msPostAction: 300);
        editor.Session.WaitForExact<Element>("Link", 10_000, "MenuItem").Click(msPostAction: 300);
        Assert.IsTrue(editor.WaitForReplacementText("{{clipboard:link}}"), $"Replacement holds '{TextExpanderFiles.Visible(editor.ReplacementText)}'.");

        Step("Pressing Left, into the variable, then F2");
        editor.FocusReplacement();
        TextInput.Press(0x23);
        TextInput.Press(0x25);
        TextInput.Press(0x71);
        var copy = editor.Session.Find<ComboBox>(By.AccessibilityId("TextExpander_ClipboardCopy"), 10_000);
        Step("Choosing Pick from clipboard history when it runs, then Update");
        copy.Select("Pick from clipboard history when it runs");
        editor.Session.Find<Button>(By.AccessibilityId("Button_TextExpander_ClipboardUpdate"), 10_000).Invoke(msPostAction: 300);
        Assert.IsTrue(editor.WaitForReplacementText(added.Replacement), $"Replacement holds '{TextExpanderFiles.Visible(editor.ReplacementText)}'.");
        Assert.IsTrue(editor.Session.Has(By.Name("Link from clipboard history"), timeoutMS: 10_000), "The preview doesn't name the variable.");

        editor.Save();
        Assert.IsTrue(editor.WaitForRow(added.Trigger, present: true), $"The new snippet isn't listed. Rows: {editor.DescribeRows()}.");
        AssertDatabase([.. DefaultSnippets, added]);
    }

    /// <summary>
    /// Pointing at a kind under Insert variable > Clipboard > Only part of it says what it needs, and screen readers get
    /// the same as its help text. In the Clipboard panel, what counts and what doesn't follows the kind chosen.
    /// </summary>
    [TestMethod("TextExpander.Editor.ShowsWhatEachClipboardPartNeeds")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Editor")]
    public void ShowsWhatEachClipboardPartNeeds()
    {
        var editor = OpenEditor(OpenTextExpanderPage());
        editor.WaitForCount(DefaultSnippets.Count);
        editor.OpenNewSnippet();
        editor.SetTrigger(";tehint");
        editor.FocusReplacement();

        Step("Opening Insert variable > Clipboard > Only part of it");
        editor.Session.Find<Button>(By.AccessibilityId("Button_TextExpander_InsertVariable"), 10_000).Invoke(msPostAction: 300);
        editor.Session.WaitForExact<Element>("Clipboard", 10_000, "MenuItem").Click(msPostAction: 300);
        editor.Session.WaitForExact<Element>("Only part of what's on your clipboard now", 10_000, "MenuItem").Click(msPostAction: 300);
        var link = editor.Session.WaitForExact<Element>("Link", 10_000, "MenuItem");
        Assert.AreEqual("Starts with https://, http:// or www., such as https://example.com/track. Just example.com isn't found.", link.HelpText);
        var phone = editor.Session.WaitForExact<Element>("Phone number", 10_000, "MenuItem");
        Assert.AreEqual("7 to 15 digits in groups, such as (425) 555-0123, or all together after a +.", phone.HelpText);
        StringAssert.StartsWith(editor.Session.WaitForExact<Element>("Email address", 10_000, "MenuItem").HelpText, "A name, an @ and a domain with a dot");
        StringAssert.Contains(editor.Session.WaitForExact<Element>("Number", 10_000, "MenuItem").HelpText, "your region's separators, such as ");

        Step("Selecting Phone number, then F2 on it");
        phone.Click(msPostAction: 300);
        Assert.IsTrue(editor.WaitForReplacementText("{{clipboard:phone}}"), $"Replacement holds '{TextExpanderFiles.Visible(editor.ReplacementText)}'.");
        editor.FocusReplacement();
        TextInput.Press(0x23);
        TextInput.Press(0x25);
        TextInput.Press(0x71);
        AssertExamples(editor, "Counts as a phone number", "Counts: (425) 555-0123", "Doesn't count: 4255550123, add a + or spaces");

        Step("Choosing Only the link in it");
        editor.Session.Find<ComboBox>(By.AccessibilityId("TextExpander_ClipboardPart"), 10_000).Select("Only the link in it");
        AssertExamples(editor, "Counts as a link", "Counts: https://example.com/track", "Doesn't count: example.com, add https:// or www.");

        Step("Pressing Esc, which leaves the snippet as it was");
        TextInput.Escape();
        Assert.IsTrue(editor.WaitForReplacementText("{{clipboard:phone}}"), $"Replacement holds '{TextExpanderFiles.Visible(editor.ReplacementText)}'.");
    }

    /// <summary>
    /// A snippet has one cursor position: with $|$ in Replacement, Insert variable's Cursor position is unavailable and
    /// says why, whether the snippet came with one or one was just added. Either used to close the editor.
    /// </summary>
    [TestMethod("TextExpander.Editor.OffersOneCursorPositionPerSnippet")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Editor")]
    public void OffersOneCursorPositionPerSnippet()
    {
        const string OnlyOne = "Only one cursor position per snippet.";
        var added = new Snippet(";tecursor", "$|$");
        var editor = OpenEditor(OpenTextExpanderPage());
        editor.WaitForCount(DefaultSnippets.Count);

        editor.OpenRow(Letter.Trigger);
        var cursor = OpenCursorPositionItem(editor);
        Assert.IsFalse(cursor.IsEnabled, $"Cursor position is offered in '{Letter.Trigger}', which has one.");
        Assert.AreEqual(OnlyOne, cursor.HelpText);
        CloseCursorPositionMenu(editor);
        editor.CancelAndWait();

        editor.OpenNewSnippet();
        editor.SetTrigger(added.Trigger);
        editor.FocusReplacement();
        cursor = OpenCursorPositionItem(editor);
        Assert.IsTrue(cursor.IsEnabled, "Cursor position isn't offered in an empty replacement.");
        Step("Selecting Cursor position");
        cursor.Click(msPostAction: 300);
        Assert.IsTrue(editor.WaitForReplacementText(added.Replacement), $"Replacement holds '{TextExpanderFiles.Visible(editor.ReplacementText)}'.");
        cursor = OpenCursorPositionItem(editor);
        Assert.IsFalse(cursor.IsEnabled, "A second cursor position is offered.");
        Assert.AreEqual(OnlyOne, cursor.HelpText);
        CloseCursorPositionMenu(editor);
        editor.Save();

        Assert.IsTrue(editor.WaitForRow(added.Trigger, present: true), $"The new snippet isn't listed. Rows: {editor.DescribeRows()}.");
        AssertDatabase([.. DefaultSnippets, added]);
    }

    /// <summary>
    /// {{Date}} isn't a variable, so it's typed as written: the dialog says so under Replacement, and Use {{date}} fixes
    /// it. Saved as written, its row says it's typed as written and what it likely means.
    /// </summary>
    [TestMethod("TextExpander.Editor.FlagsAVariableWrittenWrongAndUseFixesIt")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Editor")]
    public void FlagsAVariableWrittenWrongAndUseFixesIt()
    {
        var written = new Snippet(";tesent", "Sent {{Date}}");
        var editor = OpenEditor(OpenTextExpanderPage());
        editor.WaitForCount(DefaultSnippets.Count);
        editor.OpenNewSnippet();
        editor.SetTrigger(written.Trigger);
        editor.TypeReplacement(written.Replacement);
        Assert.IsTrue(editor.WaitForReplacementText(written.Replacement), $"Replacement holds '{TextExpanderFiles.Visible(editor.ReplacementText)}'.");
        Assert.IsTrue(
            editor.WaitForText("{{Date}} isn't a variable, so it's typed as written. Did you mean {{date}}?"),
            "The dialog didn't say {{Date}} is typed as written.");

        Step("Selecting Use {{date}}");
        var use = editor.Session.Find<Element>(By.AccessibilityId("Button_TextExpander_UseVariable"), 10_000);
        Assert.AreEqual("Use {{date}}", use.Name);
        use.Invoke(msPostAction: 300);
        Assert.IsTrue(editor.WaitForReplacementText("Sent {{date}}"), $"Replacement holds '{TextExpanderFiles.Visible(editor.ReplacementText)}'.");
        Assert.IsTrue(
            editor.Session.WaitFor(() => !editor.Session.Has(By.AccessibilityId("Button_TextExpander_UseVariable"), timeoutMS: 0), 10_000, pollIntervalMS: 250),
            "The note stayed after the fix.");

        Step("Writing it wrong again and saving it as written");
        editor.TypeReplacement(written.Replacement);
        Assert.IsTrue(editor.WaitForReplacementText(written.Replacement), $"Replacement holds '{TextExpanderFiles.Visible(editor.ReplacementText)}'.");
        editor.Save();
        Assert.IsTrue(editor.WaitForRow(written.Trigger, present: true), $"The new snippet isn't listed. Rows: {editor.DescribeRows()}.");
        Assert.IsTrue(
            editor.Session.WaitFor(() => editor.Row(written.Trigger)?.Name.EndsWith("Typed as written: {{Date}} isn't a variable. Did you mean {{date}}?", StringComparison.Ordinal) == true, 10_000, pollIntervalMS: 250),
            $"The row doesn't say {{{{Date}}}} is typed as written. Rows: {editor.DescribeRows()}.");
        AssertDatabase([.. DefaultSnippets, written]);
    }

    /// <summary>
    /// Insert variable > Clipboard > Pick a copy when it runs puts {{clipboard:pick}} in Replacement, which the preview
    /// names. While Windows clipboard history is off, a note under Replacement says nothing goes in there.
    /// </summary>
    [TestMethod("TextExpander.Editor.InsertsAPickFromClipboardHistoryAndSaysWhenHistoryIsOff")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Editor")]
    public void InsertsAPickFromClipboardHistoryAndSaysWhenHistoryIsOff()
    {
        const string off = "Clipboard history is off, so there's no copy to pick when this runs, and nothing is inserted in its place.";
        const string offByPolicy = "Your organization has turned off clipboard history, so there's no copy to pick when this runs, and nothing is inserted in its place.";
        var added = new Snippet(";tehist", "{{clipboard:pick}}");
        var editor = OpenEditor(OpenTextExpanderPage());
        editor.WaitForCount(DefaultSnippets.Count);
        editor.OpenNewSnippet();
        editor.SetTrigger(added.Trigger);
        editor.FocusReplacement();

        Step("Selecting Insert variable > Clipboard > Pick a copy when it runs");
        editor.Session.Find<Button>(By.AccessibilityId("Button_TextExpander_InsertVariable"), 10_000).Invoke(msPostAction: 300);
        editor.Session.WaitForExact<Element>("Clipboard", 10_000, "MenuItem").Click(msPostAction: 300);
        editor.Session.WaitForExact<Element>("Pick a copy from clipboard history when it runs", 10_000, "MenuItem").Click(msPostAction: 300);
        Assert.IsTrue(editor.WaitForReplacementText(added.Replacement), $"Replacement holds '{TextExpanderFiles.Visible(editor.ReplacementText)}'.");
        Assert.IsTrue(editor.Session.Has(By.Name("Copy from clipboard history"), timeoutMS: 10_000), "The preview doesn't name the variable.");

        bool on = ClipboardHistoryState.IsOn();
        bool byPolicy = ClipboardHistoryState.TurnedOffByPolicy();
        Step($"Clipboard history is {(on ? "on" : byPolicy ? "off by policy" : "off")}");
        if (on)
        {
            Assert.IsFalse(editor.WaitForText(off, timeoutMS: 2_000), "A note says clipboard history is off while it's on.");
        }
        else
        {
            Assert.IsTrue(editor.WaitForText(byPolicy ? offByPolicy : off), "No note says clipboard history is off.");
            Assert.AreEqual(
                !byPolicy,
                editor.Session.Has(By.AccessibilityId("Button_TextExpander_TurnOnClipboardHistory"), timeoutMS: 2_000),
                "Turn on clipboard history is offered unless a policy turned it off.");
        }

        editor.Save();
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

    /// <summary>
    /// The keyboard carries on from the snippet just saved, deleted or duplicated: the focus is on its row, or on the next one
    /// after a delete, so Enter opens it. Delete, Ctrl+D and Shift+F10 act on the row with the focus, Delete after asking.
    /// The focus used to go back to a row the list had reused, with no name and no size, and Tab never reached a row's
    /// Duplicate and Delete.
    /// </summary>
    [TestMethod("TextExpander.Editor.TheKeyboardCarriesOnFromTheSnippetJustChanged")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Editor")]
    public void TheKeyboardCarriesOnFromTheSnippetJustChanged()
    {
        const ushort Delete = 0x2E;
        const ushort D = 0x44;
        const ushort F10 = 0x79;
        const ushort Shift = 0x10;
        const ushort Control = 0x11;

        var editor = OpenEditor(OpenTextExpanderPage());
        editor.WaitForCount(DefaultSnippets.Count);

        editor.OpenRow(Address.Trigger);
        var edited = Address with { Replacement = "Edited by the Text Expander UI test" };
        editor.TypeReplacement(edited.Replacement);
        editor.Save();
        AssertEnterOpens(editor, edited.Trigger, "after Save");

        WaitForRowFocus(editor, edited.Trigger, "after Cancel");
        editor.Press("Delete", Delete);
        Assert.IsTrue(editor.WaitForText("Delete snippet?"), "Delete on the row did not ask first.");
        Step("Confirming Delete");
        editor.Session.WaitForId<Button>("PrimaryButton", "Delete", 10_000).Invoke(msPostAction: 0);
        Assert.IsTrue(editor.WaitForRow(Address.Trigger, present: false), $"The deleted snippet is still listed. Rows: {editor.DescribeRows()}.");
        AssertEnterOpens(editor, Letter.Trigger, "after Delete, on the next row");

        WaitForRowFocus(editor, Letter.Trigger, "after Cancel");
        editor.Press("Ctrl+D", D, Control);
        editor.WaitForSnippetDialog("New snippet");
        var copyTrigger = editor.TriggerText;
        Assert.AreNotEqual(Letter.Trigger, copyTrigger, "Ctrl+D suggested the original's trigger.");
        Assert.IsTrue(editor.WaitForReplacementText(Letter.Replacement), $"Ctrl+D's copy has the replacement '{TextExpanderFiles.Visible(editor.ReplacementText)}'.");
        editor.Save();
        editor.WaitForCount(DefaultSnippets.Count);
        AssertEnterOpens(editor, copyTrigger, "after Duplicate");

        WaitForRowFocus(editor, copyTrigger, "after Cancel");
        editor.Press("Shift+F10", F10, Shift);
        Assert.IsTrue(editor.Session.WaitForElement(By.AccessibilityId("MenuItem_TextExpander_DeleteSnippet"), 10_000), "Shift+F10 on the row did not open its menu.");
        editor.Press("Esc", 0x1B);
        Assert.IsTrue(
            editor.Session.WaitFor(() => !editor.Session.Has<Element>(By.AccessibilityId("MenuItem_TextExpander_DeleteSnippet"), timeoutMS: 0), 10_000, pollIntervalMS: 250),
            "Esc did not close the row's menu.");

        AssertDatabase([Greeting, Letter, Ticket, Note, new Snippet(copyTrigger, Letter.Replacement)]);
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

        editor.Search("Anytown");
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

    /// <summary>
    /// With no app set for .json files, as on a new PC, Open in text editor opens the database in Notepad, rather than
    /// Windows asking which app to use.
    /// </summary>
    [TestMethod("TextExpander.Editor.OpensTheDatabaseInNotepadWhenNoAppIsSetForJson")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Editor")]
    public void OpensTheDatabaseInNotepadWhenNoAppIsSetForJson()
    {
        if (HasAppFor(".json"))
        {
            Assert.Inconclusive("An app is set for .json files here, so it opens the database.");
        }

        var editor = OpenEditor(OpenTextExpanderPage());
        editor.WaitForCount(DefaultSnippets.Count);
        int[] before = ProcessIds("notepad");
        editor.OpenDatabaseMenu();
        Step("Selecting Open in text editor");
        editor.Session.Find<Button>(By.AccessibilityId("Button_TextExpander_OpenSnippetFile"), 10_000).Invoke(msPostAction: 0);

        var openWith = false;
        var opened = WaitHelper.WaitForStable(
            () =>
            {
                openWith |= ProcessIds("OpenWith").Length > 0;
                return WindowControl.EnumerateAllWindows().FirstOrDefault(window =>
                    window.IsVisible
                    && window.Title.Contains(TextExpanderFiles.SnippetsFileName, StringComparison.OrdinalIgnoreCase)
                    && ProcessIds("notepad").Contains(window.ProcessId));
            },
            window => window.Hwnd != IntPtr.Zero,
            timeoutMS: 20_000,
            pollIntervalMS: 250);
        try
        {
            Assert.IsFalse(openWith, "Windows asked which app to open the database with.");
            Assert.IsTrue(opened.Succeeded, $"Notepad didn't open the database. Windows: {string.Join(", ", WindowsFinder.ListAll().Select(window => $"{window.ProcessName} '{window.Title}'"))}.");
            Step($"Notepad shows '{opened.LastObservation.Title}'");
        }
        finally
        {
            WindowControl.TryKillProcessTreeByNameAndWait("OpenWith", 5_000);
            foreach (int id in ProcessIds("notepad").Except(before))
            {
                try
                {
                    using var notepad = Process.GetProcessById(id);
                    notepad.Kill();
                    notepad.WaitForExit(5_000);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                }
            }
        }
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

    /// <summary>Waits for the Clipboard panel to show <paramref name="heading"/> over what counts, and the first of each list.</summary>
    private void AssertExamples(SnippetEditor editor, string heading, string firstCounts, string firstDoesnt)
    {
        foreach ((string id, string name) in new[] { ("TextExpander_ClipboardExamples_Counts", heading), ("TextExpander_ClipboardExample_Counts_0", firstCounts), ("TextExpander_ClipboardExample_DoesntCount_0", firstDoesnt) })
        {
            var shown = WaitHelper.WaitForStable(
                () => editor.Session.Has(By.AccessibilityId(id), timeoutMS: 1_000) ? editor.Session.Find<Element>(By.AccessibilityId(id), 2_000).Name : null,
                actual => actual == name,
                timeoutMS: 15_000,
                requiredConsecutiveMatches: 2,
                pollIntervalMS: 250);
            Assert.IsTrue(shown.Succeeded, $"The Clipboard panel's {id} says '{shown.LastObservation}', not '{name}'.");
        }

        Step($"The Clipboard panel shows '{heading}'");
    }

    /// <summary>
    /// Waits until the focus is on the row of <paramref name="trigger"/>, where the editor reports it. Where winappcli can't
    /// read the focus, the key pressed next shows where it is instead.
    /// </summary>
    private void WaitForRowFocus(SnippetEditor editor, string trigger, string when)
    {
        bool? focused = null;
        editor.Session.WaitFor(() => (focused = editor.RowHasFocus(trigger)) != false, 10_000, pollIntervalMS: 250);
        Assert.AreNotEqual(false, focused, $"The focus isn't on the '{trigger}' row {when}. Rows: {editor.DescribeRows()}.");
        Step($"The '{trigger}' row has the focus {when} (reported: {focused?.ToString() ?? "unknown"})");
        Thread.Sleep(300);
    }

    /// <summary>Checks that the focus is on the row of <paramref name="trigger"/>: Enter opens that snippet, which is then cancelled.</summary>
    private void AssertEnterOpens(SnippetEditor editor, string trigger, string when)
    {
        WaitForRowFocus(editor, trigger, when);
        editor.Press("Enter", 0x0D);
        editor.WaitForSnippetDialog("Edit snippet");
        Assert.AreEqual(trigger, editor.TriggerText, $"Enter {when} opened another snippet.");
        editor.CancelAndWait();
    }

    /// <summary>Opens Insert variable and gets its Cursor position item.</summary>
    private Element OpenCursorPositionItem(SnippetEditor editor)
    {
        Step("Opening Insert variable");
        editor.Session.Find<Button>(By.AccessibilityId("Button_TextExpander_InsertVariable"), 10_000).Invoke(msPostAction: 300);
        return editor.Session.WaitForExact<Element>("Cursor position", 10_000, "MenuItem");
    }

    /// <summary>Closes Insert variable with Esc, which leaves the snippet dialog open.</summary>
    private static void CloseCursorPositionMenu(SnippetEditor editor)
    {
        editor.Press("Esc", 0x1B);
        Assert.IsTrue(
            editor.Session.WaitFor(() => editor.Session.FindExact<Element>("Cursor position", "MenuItem").Count == 0, 10_000, pollIntervalMS: 250),
            "Esc did not close Insert variable.");
        Assert.IsTrue(editor.IsDialogOpen, "Esc closed the snippet dialog along with Insert variable.");
    }

    /// <summary>Whether an app is set to open files with <paramref name="extension"/>: a command or a packaged app's handler.</summary>
    private static bool HasAppFor(string extension)
    {
        // ASSOCF_INIT_IGNOREUNKNOWN, so no app is an error rather than the "Unknown" type, which is Open With.
        foreach (int what in new[] { 1, 18 })
        {
            uint length = 0;
            if (AssocQueryStringW(0x400, what, extension, null, IntPtr.Zero, ref length) is 0 or 1 && length > 1)
            {
                return true;
            }
        }

        return false;
    }

    [System.Runtime.InteropServices.DllImport("shlwapi.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int AssocQueryStringW(uint flags, int what, string association, string? extra, IntPtr output, ref uint length);
}
