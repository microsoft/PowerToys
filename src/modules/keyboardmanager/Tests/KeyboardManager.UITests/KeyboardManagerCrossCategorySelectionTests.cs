// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.KeyboardManager.UITests;

/// <summary>
/// Exercises multi-selection across the separate URL, program and text lists.
/// The editor stores these in one native shortcut profile but presents different
/// sections, so per-section selection must not lose or duplicate deletions.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class KeyboardManagerCrossCategorySelectionTests : KeyboardManagerTestBase
{
    private const int F13 = 0x7C;
    private const int F14 = 0x7D;
    private const int F15 = 0x7E;
    private const int LeftControl = 0xA2;

    private const string TestUrl = "https://example.com/powertoys/selection";
    private const string TestProgram = "notepad.exe";
    private const string TestText = "PowerToys selection test";

    private static KeyboardInputFixtureLock? fixtureLock;

    protected override bool ReuseScopeAcrossTests => true;

    [ClassInitialize]
    public static void InitializeClass(TestContext context)
    {
        _ = context;
        fixtureLock = KeyboardInputFixtureLock.Acquire();
    }

    [ClassCleanup]
    public static void CleanupClass()
    {
        fixtureLock?.Dispose();
        fixtureLock = null;
    }

    [TestCleanup]
    public Task CleanupTest() => CleanupKeyboardManagerTestAsync();

    [TestMethod("KeyboardManager.Editor.MixedSectionBulkDelete")]
    [TestCategory("Keyboard Manager")]
    public void BulkDeleteRemovesTwoActionCategoriesButPreservesThird()
    {
        var editor = OpenEditor();
        var session = Session.FromProcess(KeyboardManagerTestConstants.EditorProcessName);

        Step("Creating one URL, one program, and one text action");
        CreateAction(editor, session, F13, "Open URL", "UrlPathInput", TestUrl);
        CreateAction(editor, session, F14, "Open app", "ProgramPathInput", TestProgram);
        CreateAction(editor, session, F15, "Insert text", "TextContentBox", TestText);
        AssertProfileCount(editor, 3, "The three action types did not persist.");

        Step("Entering selection mode across three distinct list sections");
        StartSelection(session);
        SelectRow(session, "F13");
        SelectRow(session, "F15");

        var delete = session.Find<Button>(By.AccessibilityId("DeleteSelectedBtn"), timeoutMS: 5_000);
        Assert.AreEqual("Delete selected (2)", delete.Name, "Selections across URL and text sections were not aggregated.");
        Assert.IsTrue(delete.IsEnabled, "Bulk delete was disabled despite two selected action rows.");

        Step("Deleting only the selected URL and text action rows");
        delete.Click(msPostAction: 300);
        Assert.IsNotNull(
            FindExact<Element>(session, "Delete selected remappings?", timeoutMS: 5_000),
            "The mixed-section confirmation dialog did not open.");
        var confirm = FindExact<Button>(session, "Delete", timeoutMS: 5_000);
        Assert.IsNotNull(confirm, "Mixed-section deletion did not expose the primary action.");
        confirm!.Click(msPostAction: 500);

        AssertProfileCount(editor, 1, "Mixed-section bulk deletion did not remove exactly two actions.");
        Assert.IsTrue(ProfileHasShortcut(LeftControl, F14), "Deleting URL and text removed the unrelated program mapping.");
        Assert.IsFalse(ProfileHasShortcut(LeftControl, F13), "The URL mapping survived bulk deletion.");
        Assert.IsFalse(ProfileHasShortcut(LeftControl, F15), "The text mapping survived bulk deletion.");

        Step("Reopening the editor to verify the surviving action is still visible");
        CloseEditor();
        editor = OpenEditor();
        session = Session.FromProcess(KeyboardManagerTestConstants.EditorProcessName);
        Assert.IsNotNull(FindExact<Element>(session, "F14", timeoutMS: 5_000), "The program action did not survive the editor restart.");
        Assert.IsNull(FindExact<Element>(session, "F13", timeoutMS: 1_000), "The deleted URL action reappeared.");
        Assert.IsNull(FindExact<Element>(session, "F15", timeoutMS: 1_000), "The deleted text action reappeared.");
        AssertProfileCount(editor, 1, "The surviving mapping was not persisted after restart.");
    }

    [TestMethod("KeyboardManager.Editor.MixedSectionCancel")]
    [TestCategory("Keyboard Manager")]
    public void CancellingMixedSectionDeletePreservesEveryAction()
    {
        var editor = OpenEditor();
        var session = Session.FromProcess(KeyboardManagerTestConstants.EditorProcessName);

        CreateAction(editor, session, F13, "Open URL", "UrlPathInput", TestUrl);
        CreateAction(editor, session, F14, "Open app", "ProgramPathInput", TestProgram);
        CreateAction(editor, session, F15, "Insert text", "TextContentBox", TestText);
        AssertProfileCount(editor, 3, "The seeded profile did not contain three categories.");
        var snapshot = ProfileSnapshot();

        Step("Selecting actions in the URL and program sections");
        StartSelection(session);
        SelectRow(session, "F13");
        SelectRow(session, "F14");

        var delete = session.Find<Button>(By.AccessibilityId("DeleteSelectedBtn"), timeoutMS: 5_000);
        Assert.AreEqual("Delete selected (2)", delete.Name, "The selection count was incorrect across sections.");
        delete.Click(msPostAction: 300);
        Assert.IsNotNull(
            FindExact<Element>(session, "Delete selected remappings?", timeoutMS: 5_000),
            "Bulk delete did not prompt for confirmation.");

        Step("Dismissing confirmation must not mutate the native profile");
        KeyboardHelper.SendKey(Key.Esc);
        Assert.IsTrue(
            editor.WaitFor(
                () => FindExact<Element>(session, "Delete selected remappings?", timeoutMS: 200) is null,
                timeoutMS: 5_000,
                pollIntervalMS: 200),
            "Escape did not dismiss mixed-section deletion.");
        Assert.AreEqual(snapshot, ProfileSnapshot(), "Cancelling mixed-section deletion modified native mappings.");
        AssertProfileCount(editor, 3, "Cancelling mixed-section deletion removed one or more actions.");

        Step("Leaving selection mode returns to normal rows without changing data");
        var selectionToggle = session.Find<Button>(By.AccessibilityId("SelectionModeButton"), timeoutMS: 5_000);
        selectionToggle.Click(msPostAction: 300);
        Assert.AreEqual("Select", selectionToggle.Name, "Selection mode remained active after Cancel.");
        Assert.AreEqual(snapshot, ProfileSnapshot(), "Leaving selection mode modified action mappings.");

        Step("Restarting editor after cancellation must retain every action");
        CloseEditor();
        editor = OpenEditor();
        session = Session.FromProcess(KeyboardManagerTestConstants.EditorProcessName);
        Assert.IsNotNull(FindExact<Element>(session, "F13", timeoutMS: 5_000), "The URL action was lost after cancellation.");
        Assert.IsNotNull(FindExact<Element>(session, "F14", timeoutMS: 5_000), "The program action was lost after cancellation.");
        Assert.IsNotNull(FindExact<Element>(session, "F15", timeoutMS: 5_000), "The text action was lost after cancellation.");
        AssertProfileCount(editor, 3, "Reopening editor lost a mapping after cancelled bulk deletion.");
    }

    private void CreateAction(
        Session editor,
        Session session,
        int key,
        string actionName,
        string fieldAutomationId,
        string fieldValue)
    {
        Step($"Creating Ctrl+0x{key:X2} to {actionName}");
        editor.Find<Button>(By.AccessibilityId("NewRemappingBtn"), timeoutMS: 10_000)
            .Invoke(msPostAction: 300);
        Assert.IsTrue(
            session.WaitForElement(By.AccessibilityId("TriggerKeyToggleBtn"), timeoutMS: 10_000),
            "The new action dialog did not open.");

        RecordShortcut(session, LeftControl, key);
        session.Find<ComboBox>(By.AccessibilityId("ActionTypeComboBox"), timeoutMS: 5_000)
            .Click(msPostAction: 300);
        var option = FindExact<Element>(session, actionName, timeoutMS: 5_000);
        Assert.IsNotNull(option, $"The {actionName} option was not found.");
        option!.MouseClick(msPostAction: 300);

        var field = session.Find<TextBox>(By.AccessibilityId(fieldAutomationId), timeoutMS: 5_000);
        field.SetText(fieldValue);

        var save = FindExact<Button>(session, "Save", timeoutMS: 5_000);
        Assert.IsNotNull(save, $"{actionName} did not expose Save.");
        Assert.IsTrue(save!.IsEnabled, $"{actionName} did not enable Save for a complete shortcut.");
        save.Invoke(msPostAction: 500);

        Assert.IsTrue(
            editor.WaitFor(
                () => ProfileHasShortcut(LeftControl, key),
                timeoutMS: 10_000,
                pollIntervalMS: 250),
            $"{actionName} did not persist its Ctrl+0x{key:X2} trigger.");
    }

    private void RecordShortcut(Session session, int modifier, int keyCode)
    {
        var toggle = session.Find<Element>(By.AccessibilityId("TriggerKeyToggleBtn"), timeoutMS: 5_000);
        toggle.Invoke(msPostAction: 200);
        Assert.IsTrue(toggle.WaitForProperty("ToggleState", "On", timeoutMS: 3_000), "The trigger recorder did not start.");

        try
        {
            KeyboardHelper.PressKey((Key)checked((byte)modifier));
            KeyboardHelper.PressKey((Key)checked((byte)keyCode));
        }
        finally
        {
            KeyboardHelper.ReleaseKey((Key)checked((byte)keyCode));
            KeyboardHelper.ReleaseKey((Key)checked((byte)modifier));
        }

        toggle = session.Find<Element>(By.AccessibilityId("TriggerKeyToggleBtn"), timeoutMS: 5_000);
        if (string.Equals(toggle.GetProperty("ToggleState"), "On", StringComparison.OrdinalIgnoreCase))
        {
            toggle.Invoke(msPostAction: 200);
        }

        Assert.IsTrue(toggle.WaitForProperty("ToggleState", "Off", timeoutMS: 3_000), "The trigger recorder did not stop.");
    }

    private static void StartSelection(Session session)
    {
        var toggle = session.Find<Button>(By.AccessibilityId("SelectionModeButton"), timeoutMS: 5_000);
        Assert.AreEqual("Select", toggle.Name, "Selection mode was unexpectedly active.");
        toggle.Click(msPostAction: 300);
        Assert.AreEqual("Cancel", toggle.Name, "The editor did not enter selection mode.");
    }

    private static void SelectRow(Session session, string keyName)
    {
        var element = FindExact<Element>(session, keyName, timeoutMS: 5_000);
        Assert.IsNotNull(element, $"The {keyName} row was not present for selection.");
        element!.MouseClick(msPostAction: 300);
    }

    private void AssertProfileCount(Session editor, int expected, string reason)
    {
        Assert.IsTrue(
            editor.WaitFor(
                () => CurrentActionCount() == expected,
                timeoutMS: 10_000,
                pollIntervalMS: 250),
            $"{reason} Expected {expected} shortcut actions, observed {CurrentActionCount()}. Profile: {ProfileSnapshot()}");
    }

    private static int CurrentActionCount() =>
        GlobalShortcuts().Count + GlobalTextMappings().Count;

    private static JsonArray GlobalTextMappings() =>
        KeyboardManagerSettings.ReadProfile()["remapShortcutsToText"]?["global"] as JsonArray ?? new JsonArray();

    private static JsonArray GlobalShortcuts() =>
        KeyboardManagerSettings.ReadProfile()["remapShortcuts"]?["global"] as JsonArray ?? new JsonArray();

    private static bool ProfileHasShortcut(int modifier, int keyCode)
    {
        string source = $"{modifier};{keyCode}";
        return GlobalShortcuts().Concat(GlobalTextMappings()).Any(mapping =>
            mapping?["originalKeys"]?.GetValue<string>() == source);
    }

    private static string ProfileSnapshot() =>
        KeyboardManagerSettings.ReadProfile().ToJsonString();
}
