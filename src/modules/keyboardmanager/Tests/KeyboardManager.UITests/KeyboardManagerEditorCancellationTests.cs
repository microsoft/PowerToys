// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.KeyboardManager.UITests;

/// <summary>
/// Verifies that cancelling editor, delete and selection operations has no side
/// effects on the user's remappings. Every scenario runs against an isolated
/// profile restored by KeyboardManagerSettingsScope after the class finishes.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class KeyboardManagerEditorCancellationTests : KeyboardManagerTestBase
{
    private const int A = 0x41;
    private const int B = 0x42;
    private const int F13 = 0x7C;
    private const int F14 = 0x7D;
    private const int F15 = 0x7E;
    private const int F16 = 0x7F;

    private static KeyboardInputFixtureLock? inputFixtureLock;

    protected override bool ReuseScopeAcrossTests => true;

    [ClassInitialize]
    public static void InitializeClass(TestContext context)
    {
        _ = context;
        inputFixtureLock = KeyboardInputFixtureLock.Acquire();
    }

    [ClassCleanup]
    public static void CleanupClass()
    {
        inputFixtureLock?.Dispose();
        inputFixtureLock = null;
    }

    [TestCleanup]
    public Task CleanupTest() => CleanupKeyboardManagerTestAsync();

    [TestMethod("KeyboardManager.Editor.CancelSingleDelete")]
    [TestCategory("Keyboard Manager")]
    public void EscapeFromSingleRowDeletionPreservesProfileAndRow()
    {
        var editor = OpenSeededEditor();
        var session = EditorSession();
        var before = ProfileSnapshot();
        AssertVisibleRows(session, 2);

        Step("Opening the first mapping's delete menu");
        ClickDeleteMenuForRow(session, "F13");

        var deleteDialog = FindExact<Element>(session, "Delete", timeoutMS: 5_000);
        Assert.IsNotNull(deleteDialog, "The single-row delete dialog did not expose Delete.");

        Step("Dismissing deletion without modifying the original profile");
        KeyboardHelper.SendKey(Key.Esc);

        Assert.IsTrue(
            editor.WaitFor(
                () => ProfileSnapshot() == before &&
                    VisibleRows(session) == 2 &&
                    FindExact<Element>(session, "F13", timeoutMS: 500) is not null,
                timeoutMS: 5_000,
                pollIntervalMS: 200),
            "Cancelling the deletion changed the remappings or removed a row.");

        Step("Repeating the operation to verify cancellation left the row usable");
        ClickDeleteMenuForRow(session, "F13");
        var confirm = FindExact<Button>(session, "Delete", timeoutMS: 5_000);
        Assert.IsNotNull(confirm, "The reopened single-row confirmation did not expose Delete.");
        confirm!.Click(msPostAction: 300);

        Assert.IsTrue(
            editor.WaitFor(
                () => CountSingleKeyMappings() == 1 &&
                    !HasMapping(F13, F14) &&
                    HasMapping(F15, F16),
                timeoutMS: 10_000,
                pollIntervalMS: 250),
            "Deleting after cancellation removed the wrong row or did not persist.");
        AssertVisibleRows(session, 1);
    }

    [TestMethod("KeyboardManager.Editor.CancelBulkDelete")]
    [TestCategory("Keyboard Manager")]
    public void EscapeFromBulkDeletionPreservesAllSelectedRows()
    {
        var editor = OpenSeededEditor();
        var session = EditorSession();
        var original = ProfileSnapshot();
        AssertVisibleRows(session, 2);

        Step("Entering selection mode and marking both rows");
        EnterSelectionMode(session);
        SelectRow(session, "F13");
        SelectRow(session, "F15");

        var deleteSelected = session.Find<Button>(By.AccessibilityId("DeleteSelectedBtn"), timeoutMS: 5_000);
        Assert.IsTrue(deleteSelected.IsEnabled, "Bulk delete stayed disabled after selecting two remappings.");
        Assert.AreEqual("Delete selected (2)", deleteSelected.Name, "The selection counter was not updated.");

        Step("Opening and cancelling the bulk-delete confirmation");
        deleteSelected.Click(msPostAction: 300);
        Assert.IsNotNull(
            FindExact<Element>(session, "Delete selected remappings?", timeoutMS: 5_000),
            "The bulk-delete confirmation was not shown.");
        KeyboardHelper.SendKey(Key.Esc);

        Assert.IsTrue(
            editor.WaitFor(
                () => FindExact<Element>(session, "Delete selected remappings?", timeoutMS: 200) is null &&
                    VisibleRows(session) == 2 &&
                    ProfileSnapshot() == original,
                timeoutMS: 5_000,
                pollIntervalMS: 200),
            "Cancelling bulk deletion unexpectedly modified the profile.");

        Step("Leaving selection mode after cancellation");
        LeaveSelectionMode(session);
        AssertVisibleRows(session, 2);
        Assert.AreEqual(original, ProfileSnapshot(), "Cancelling selection modified native remappings.");
    }

    [TestMethod("KeyboardManager.Editor.SelectionCancel")]
    [TestCategory("Keyboard Manager")]
    public void LeavingSelectionModeClearsStaleSelections()
    {
        var editor = OpenSeededEditor();
        var session = EditorSession();
        var original = ProfileSnapshot();

        Step("Selecting one row, then leaving selection mode");
        EnterSelectionMode(session);
        SelectRow(session, "F13");
        Assert.AreEqual(
            "Delete selected (1)",
            session.Find<Button>(By.AccessibilityId("DeleteSelectedBtn"), timeoutMS: 5_000).Name,
            "The selected count did not reflect the first selected row.");
        LeaveSelectionMode(session);

        Step("Reentering selection mode must not inherit the previous selection");
        EnterSelectionMode(session);
        var bulkDelete = session.Find<Button>(By.AccessibilityId("DeleteSelectedBtn"), timeoutMS: 5_000);
        Assert.IsFalse(bulkDelete.IsEnabled, "A prior selection remained active after leaving and reentering selection mode.");
        Assert.AreEqual("Delete selected (0)", bulkDelete.Name, "The selected-count label was not reset.");

        Step("Selecting the other row must count only that row");
        SelectRow(session, "F15");
        Assert.IsTrue(
            editor.WaitFor(
                () => session.Find<Button>(By.AccessibilityId("DeleteSelectedBtn"), timeoutMS: 500).Name == "Delete selected (1)",
                timeoutMS: 5_000,
                pollIntervalMS: 200),
            "Selection mode counted stale rows after it was reopened.");

        LeaveSelectionMode(session);
        Assert.AreEqual(original, ProfileSnapshot(), "Changing selection mode touched persisted mappings.");
        AssertVisibleRows(session, 2);
    }

    [TestMethod("KeyboardManager.Editor.CancelEdit")]
    [TestCategory("Keyboard Manager")]
    public void EscapeFromEditDialogRetainsOriginalMapping()
    {
        var editor = OpenSeededEditor();
        var session = EditorSession();
        var original = ProfileSnapshot();

        Step("Opening the existing F13 mapping for editing");
        var existing = FindExact<Element>(session, "F13", timeoutMS: 5_000);
        Assert.IsNotNull(existing, "The seeded F13 mapping was not shown.");
        existing!.MouseClick(msPostAction: 300);
        Assert.IsTrue(
            editor.WaitForElement(By.AccessibilityId("ActionKeyToggleBtn"), timeoutMS: 5_000),
            "The existing mapping did not open the unified edit dialog.");

        Step("Dismissing edit without saving");
        KeyboardHelper.SendKey(Key.Esc);
        Assert.IsTrue(
            editor.WaitFor(
                () => FindExact<Button>(session, "Save", timeoutMS: 300) is null,
                timeoutMS: 5_000,
                pollIntervalMS: 200),
            "Escape did not close the edit dialog.");
        Assert.AreEqual(original, ProfileSnapshot(), "Exiting edit modified the native profile.");
        AssertVisibleRows(session, 2);

        Step("Reopening editor to check persistence after cancellation");
        CloseEditor();
        var reopened = OpenEditor();
        AssertVisibleRows(EditorSession(), 2);
        Assert.IsTrue(
            reopened.WaitFor(
                () => HasMapping(F13, F14) && HasMapping(F15, F16),
                timeoutMS: 10_000,
                pollIntervalMS: 250),
            "An unsaved edit replaced a previously stored remapping.");
    }

    [TestMethod("KeyboardManager.Editor.CancelNew")]
    [TestCategory("Keyboard Manager")]
    public void CancelledNewMappingNeverAppearsInProfileOrEditor()
    {
        var editor = OpenSeededEditor();
        var session = EditorSession();
        var original = ProfileSnapshot();

        Step("Opening a new mapping dialog");
        editor.Find<Button>(By.AccessibilityId("NewRemappingBtn"), timeoutMS: 5_000).Invoke(msPostAction: 300);
        Assert.IsTrue(
            session.WaitForElement(By.AccessibilityId("TriggerKeyToggleBtn"), timeoutMS: 5_000),
            "The new mapping dialog did not appear.");

        Step("Recording a complete, valid A-to-B mapping");
        Record(session, "TriggerKeyToggleBtn", A);
        Record(session, "ActionKeyToggleBtn", B);
        var save = FindExact<Button>(session, "Save", timeoutMS: 5_000);
        Assert.IsNotNull(save, "Save was not available in the new mapping dialog.");
        Assert.IsTrue(save!.IsEnabled, "Recording two complete columns did not enable Save.");

        Step("Cancelling the valid mapping rather than saving it");
        KeyboardHelper.SendKey(Key.Esc);
        Assert.IsTrue(
            editor.WaitFor(
                () => FindExact<Button>(session, "Save", timeoutMS: 200) is null,
                timeoutMS: 5_000,
                pollIntervalMS: 200),
            "Escape did not close the new mapping dialog.");
        Assert.AreEqual(original, ProfileSnapshot(), "A cancelled mapping modified the persisted profile.");
        Assert.IsFalse(HasMapping(A, B), "A cancelled A-to-B mapping was persisted.");
        AssertVisibleRows(session, 2);
    }

    private Session OpenSeededEditor()
    {
        KeyboardManagerSettings.ApplyProfile(
            KeyboardManagerSettings.BuildProfile(
                singleKeyRemaps: new[]
                {
                    KeyboardManagerSettings.SingleKeyRemap(F13, F14),
                    KeyboardManagerSettings.SingleKeyRemap(F15, F16),
                },
                includeLoadProbe: false));

        return OpenEditor();
    }

    private static Session EditorSession() =>
        Session.FromProcess(KeyboardManagerTestConstants.EditorProcessName);

    private static string ProfileSnapshot() =>
        KeyboardManagerSettings.ReadProfile().ToJsonString();

    private static int CountSingleKeyMappings() =>
        (KeyboardManagerSettings.ReadProfile()["remapKeys"]?["inProcess"] as JsonArray)?.Count ?? 0;

    private static bool HasMapping(int from, int to)
    {
        var mappings = KeyboardManagerSettings.ReadProfile()["remapKeys"]?["inProcess"] as JsonArray;
        return mappings?.Any(mapping =>
            mapping?["originalKeys"]?.GetValue<string>() == from.ToString(System.Globalization.CultureInfo.InvariantCulture) &&
            mapping?["newRemapKeys"]?.GetValue<string>() == to.ToString(System.Globalization.CultureInfo.InvariantCulture)) == true;
    }

    private static int VisibleRows(Session session) =>
        session.FindAll<Button>(By.AccessibilityId("MappingMenuButton"), timeoutMS: 5_000)
            .Count(button => button.Displayed);

    private void AssertVisibleRows(Session session, int expected)
    {
        Assert.AreEqual(expected, VisibleRows(session), $"Expected {expected} visible remapping rows.");
    }

    private static void SelectRow(Session session, string name)
    {
        var row = FindExact<Element>(session, name, timeoutMS: 5_000);
        Assert.IsNotNull(row, $"The {name} mapping row was not found.");
        row!.MouseClick(msPostAction: 300);
    }

    private static void EnterSelectionMode(Session session)
    {
        var button = session.Find<Button>(By.AccessibilityId("SelectionModeButton"), timeoutMS: 5_000);
        Assert.AreEqual("Select", button.Name, "The toolbar was already in selection mode.");
        button.Click(msPostAction: 300);
        Assert.AreEqual("Cancel", button.Name, "The toolbar failed to enter selection mode.");
    }

    private static void LeaveSelectionMode(Session session)
    {
        var button = session.Find<Button>(By.AccessibilityId("SelectionModeButton"), timeoutMS: 5_000);
        Assert.AreEqual("Cancel", button.Name, "The toolbar was not in selection mode.");
        button.Click(msPostAction: 300);
        Assert.AreEqual("Select", button.Name, "The toolbar did not leave selection mode.");
    }

    private static void ClickDeleteMenuForRow(Session session, string name)
    {
        var key = FindExact<Element>(session, name, timeoutMS: 5_000);
        Assert.IsNotNull(key, $"Could not locate the {name} row before deleting.");
        var menu = session.FindAll<Button>(By.AccessibilityId("MappingMenuButton"), timeoutMS: 5_000)
            .Where(button => button.Displayed)
            .MinBy(button => Math.Abs(button.Y - key!.Y));
        Assert.IsNotNull(menu, $"Could not find the menu button beside the {name} row.");
        menu!.Click(msPostAction: 300);
        var delete = FindExact<Element>(session, "Delete", timeoutMS: 5_000);
        Assert.IsNotNull(delete, "The row menu did not show Delete.");
        delete!.Click(msPostAction: 300);
    }

    private void Record(Session session, string toggleId, params int[] keys)
    {
        Step($"Recording keys through {toggleId}");
        var toggle = session.Find<Element>(By.AccessibilityId(toggleId), timeoutMS: 5_000);
        toggle.Invoke(msPostAction: 200);
        Assert.IsTrue(toggle.WaitForProperty("ToggleState", "On", timeoutMS: 3_000), $"{toggleId} never entered record mode.");
        try
        {
            foreach (int key in keys)
            {
                KeyboardHelper.PressKey((Key)checked((byte)key));
            }
        }
        finally
        {
            foreach (int key in keys.Reverse())
            {
                KeyboardHelper.ReleaseKey((Key)checked((byte)key));
            }
        }

        toggle = session.Find<Element>(By.AccessibilityId(toggleId), timeoutMS: 5_000);
        if (string.Equals(toggle.GetProperty("ToggleState"), "On", StringComparison.OrdinalIgnoreCase))
        {
            toggle.Invoke(msPostAction: 200);
        }

        Assert.IsTrue(toggle.WaitForProperty("ToggleState", "Off", timeoutMS: 3_000), $"{toggleId} did not stop recording.");
    }
}
