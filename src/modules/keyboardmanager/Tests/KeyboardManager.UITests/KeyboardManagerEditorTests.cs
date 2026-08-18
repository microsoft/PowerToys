// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.KeyboardManager.UITests;

[TestClass]
[DoNotParallelize]
public sealed class KeyboardManagerEditorTests : KeyboardManagerTestBase
{
    private const int A = 0x41;
    private const int B = 0x42;
    private const int C = 0x43;
    private const int D = 0x44;
    private const int LeftControl = 0xA2;

    private static KeyboardManagerSettingsScope? settingsScope;

    protected override bool ReuseScopeAcrossTests => true;

    [ClassInitialize]
    public static void InitializeClass(TestContext testContext)
    {
        _ = testContext;
        settingsScope = new KeyboardManagerSettingsScope();
    }

    [ClassCleanup]
    public static void CleanupClass()
    {
        settingsScope?.Dispose();
        settingsScope = null;
    }

    [TestCleanup]
    public Task CleanupTest() => CleanupKeyboardManagerTestAsync();

    [TestMethod("KeyboardManager.Editor.CreateEditPersistDelete")]
    [TestCategory("Keyboard Manager")]
    public void CreatesEditsPersistsAndDeletesRemapping()
    {
        var editor = OpenEditor();
        Assert.IsNotNull(FindExact<Element>(editor, "Nothing mapped yet"), "The editor did not start from an empty profile.");

        Step("Opening Add new remapping");
        editor.Find<Button>(By.AccessibilityId("NewRemappingBtn"), timeoutMS: 10_000).Click(msPostAction: 300);
        var editorProcess = Session.FromProcess(KeyboardManagerTestConstants.EditorProcessName);
        var save = FindExact<Button>(editorProcess, "Save", timeoutMS: 5_000);
        Assert.IsNotNull(save, "The Add new remapping dialog did not expose Save.");
        Assert.IsFalse(save!.IsEnabled, "Save was enabled before the mapping had a trigger and action.");

        RecordKeys(editorProcess, "TriggerKeyToggleBtn", A);
        RecordKeys(editorProcess, "ActionKeyToggleBtn", B);
        save = FindExact<Button>(editorProcess, "Save", timeoutMS: 5_000);
        Assert.IsNotNull(save, "Save disappeared after recording a complete mapping.");
        Assert.IsTrue(save!.IsEnabled, "Save did not enable after recording A to B.");

        Step("Saving A to B");
        save.Click(msPostAction: 500);
        Assert.IsTrue(
            editor.WaitFor(
                () => ProfileContainsSingleKeyMapping(A, B),
                timeoutMS: 10_000,
                pollIntervalMS: 250),
            "The editor did not persist A to B in default.json.");

        Step("Closing and reopening the editor to verify persistence");
        CloseEditor();
        editor = OpenEditor();
        editorProcess = Session.FromProcess(KeyboardManagerTestConstants.EditorProcessName);
        Assert.IsNotNull(FindExact<Element>(editorProcess, "A", timeoutMS: 5_000), "The reopened editor did not show source key A.");
        Assert.IsNotNull(FindExact<Element>(editorProcess, "B", timeoutMS: 5_000), "The reopened editor did not show target key B.");

        Step("Opening the persisted mapping for editing");
        var sourceKey = FindExact<Element>(editorProcess, "A", timeoutMS: 5_000);
        Assert.IsNotNull(sourceKey, "The persisted A mapping could not be addressed for editing.");
        sourceKey!.MouseClick(msPostAction: 400);
        Assert.IsNotNull(FindExact<Element>(editorProcess, "Edit remapping", timeoutMS: 5_000), "The edit dialog did not open.");

        RecordKeys(editorProcess, "ActionKeyToggleBtn", C);
        save = FindExact<Button>(editorProcess, "Save", timeoutMS: 5_000);
        Assert.IsNotNull(save, "The edit dialog did not expose Save.");
        save!.Click(msPostAction: 500);

        Step("Closing and reopening the editor to verify the edited mapping persisted");
        CloseEditor();
        editor = OpenEditor();
        editorProcess = Session.FromProcess(KeyboardManagerTestConstants.EditorProcessName);
        Assert.IsNotNull(FindExact<Element>(editorProcess, "A", timeoutMS: 5_000), "The reopened editor did not show source key A after editing.");
        Assert.IsTrue(
            editorProcess.FindAll<Element>(By.Name("C"), timeoutMS: 5_000)
                .Any(element => element.Name.Equals("C", StringComparison.OrdinalIgnoreCase)),
            "The reopened editor did not show target key C after editing.");
        Assert.IsTrue(ProfileContainsSingleKeyMapping(A, C), "The reopened editor did not persist the exact A to C mapping.");
        Assert.IsFalse(ProfileContainsSingleKeyMapping(A, B), "The original A to B mapping remained after editing.");

        Step("Deleting the edited mapping through its row menu");
        editorProcess.Find<Button>(By.AccessibilityId("MappingMenuButton"), timeoutMS: 5_000).Click(msPostAction: 300);
        var deleteMenuItem = FindExact<Element>(editorProcess, "Delete", timeoutMS: 5_000);
        Assert.IsNotNull(deleteMenuItem, "The mapping row menu did not expose Delete.");
        deleteMenuItem!.Click(msPostAction: 300);
        var confirmDelete = FindExact<Button>(editorProcess, "Delete", timeoutMS: 5_000);
        Assert.IsNotNull(confirmDelete, "The delete confirmation dialog did not expose its Delete button.");
        confirmDelete!.Click(msPostAction: 500);

        Assert.IsTrue(
            editor.WaitFor(
                () => FindExact<Element>(editorProcess, "Nothing mapped yet", timeoutMS: 500) is not null,
                timeoutMS: 10_000,
                pollIntervalMS: 250),
            "The editor did not return to its empty state after deletion.");
        Assert.AreEqual(0, SingleKeyMappings().Count, "Deleting the mapping left a remapKeys entry in default.json.");
    }

    [TestMethod("KeyboardManager.Editor.InputAndValidation")]
    [TestCategory("Keyboard Manager")]
    public void DialogSupportsRecordingDropdownsKeyboardAndValidation()
    {
        var editor = OpenEditor();
        editor.Find<Button>(By.AccessibilityId("NewRemappingBtn"), timeoutMS: 10_000).Click(msPostAction: 300);
        var editorProcess = Session.FromProcess(KeyboardManagerTestConstants.EditorProcessName);

        Step("Recording Ctrl+A in the trigger column and Ctrl+V in the action column");
        RecordKeys(editorProcess, "TriggerKeyToggleBtn", LeftControl, A);
        var appSpecific = editorProcess.Find<CheckBox>(By.AccessibilityId("AppSpecificCheckBox"), timeoutMS: 5_000);
        Assert.IsTrue(appSpecific.IsEnabled, "App-specific scope did not enable for a shortcut trigger.");
        RecordKeys(editorProcess, "ActionKeyToggleBtn", LeftControl, (int)Key.V);

        var save = FindExact<Button>(editorProcess, "Save", timeoutMS: 5_000);
        Assert.IsNotNull(save, "The completed shortcut mapping did not expose Save.");
        Assert.IsTrue(save!.IsEnabled, "Save did not enable after recording both shortcut columns.");

        Step("Cancelling the complete dialog with Escape");
        KeyboardHelper.SendKey(Key.Esc);
        Assert.IsTrue(
            editor.WaitFor(
                () => FindExact<Button>(editorProcess, "Save", timeoutMS: 300) is null,
                timeoutMS: 5_000,
                pollIntervalMS: 200),
            "Escape did not close the remapping dialog.");
        Assert.AreEqual(0, SingleKeyMappings().Count, "Escape cancellation persisted an unexpected key mapping.");
        Assert.AreEqual(0, AppSpecificShortcutMappings().Count, "Escape cancellation persisted an unexpected shortcut mapping.");

        Step("Reopening the dialog to exercise mouse and keyboard dropdown selection");
        editor.Find<Button>(By.AccessibilityId("NewRemappingBtn"), timeoutMS: 10_000).Click(msPostAction: 300);
        RecordKeys(editorProcess, "TriggerKeyToggleBtn", A);
        RecordKeys(editorProcess, "ActionKeyToggleBtn", C);

        var keyButtons = VisibleKeyButtons(editorProcess);
        Assert.AreEqual(2, keyButtons.Count, "A single-key mapping should expose one trigger and one action key dropdown.");

        Step("Scrolling the trigger dropdown and selecting numeric key 1 with the mouse");
        keyButtons[0].Click(msPostAction: 300);
        var keyList = editorProcess.Find<Element>(By.AccessibilityId("KeyListView"), timeoutMS: 5_000);
        keyList.Scroll(ScrollDirection.Down);
        keyList.ScrollToEdge(toBottom: false);
        var triggerChoice = FindExact<Element>(editorProcess, "1", timeoutMS: 5_000);
        Assert.IsNotNull(triggerChoice, "The trigger key dropdown did not expose numeric key 1 after returning to the top of the list.");
        triggerChoice!.MouseClick(msPostAction: 300);

        Step("Changing the action dropdown from C to numeric key 2 with keyboard navigation");
        keyButtons = VisibleKeyButtons(editorProcess);
        keyButtons[^1].Focus();
        KeyboardHelper.SendKey(Key.Enter);
        Assert.IsTrue(editorProcess.WaitForElement(By.AccessibilityId("KeyListView"), timeoutMS: 5_000), "The action key dropdown did not open from Enter.");
        KeyboardHelper.SendKey(Key.Down);
        KeyboardHelper.SendKey(Key.Down);
        KeyboardHelper.SendKey(Key.Enter);

        Step("Saving the dropdown-created 1 to 2 mapping");
        save = FindExact<Button>(editorProcess, "Save", timeoutMS: 5_000);
        Assert.IsNotNull(save, "The dropdown-created mapping did not expose Save.");
        save!.Click(msPostAction: 500);
        Assert.IsTrue(
            editor.WaitFor(
                () => ProfileContainsSingleKeyMapping((int)Key.Num1, (int)Key.Num2),
                timeoutMS: 10_000,
                pollIntervalMS: 250),
            "Mouse and keyboard dropdown selection did not persist the expected 1 to 2 mapping.");

        Step("Opening a fresh dialog for Ctrl+B to D app-specific validation");
        editor.Find<Button>(By.AccessibilityId("NewRemappingBtn"), timeoutMS: 10_000).Click(msPostAction: 300);
        RecordKeys(editorProcess, "TriggerKeyToggleBtn", LeftControl, B);
        RecordKeys(editorProcess, "ActionKeyToggleBtn", D);
        appSpecific = editorProcess.Find<CheckBox>(By.AccessibilityId("AppSpecificCheckBox"), timeoutMS: 5_000);
        Assert.IsTrue(appSpecific.IsEnabled, "App-specific scope did not enable after the trigger became Ctrl+B.");
        appSpecific.Click(msPostAction: 300);
        Assert.IsTrue(appSpecific.IsChecked, "The app-specific scope checkbox did not become checked.");

        save = FindExact<Button>(editorProcess, "Save", timeoutMS: 5_000);
        Assert.IsNotNull(save, "The app-specific mapping did not expose Save.");
        Step("Attempting to save without an application name");
        save!.Click(msPostAction: 300);
        Assert.IsNotNull(FindExact<Button>(editorProcess, "Save", timeoutMS: 2_000), "An app-specific mapping saved without an application name.");

        var appName = editorProcess.Find<TextBox>(By.AccessibilityId("AppNameTextBox"), timeoutMS: 5_000);
        var currentProcess = Path.GetFileNameWithoutExtension(Environment.ProcessPath)!;
        appName.SetText(currentProcess);
        appName.Focus();

        Step("Saving the valid app-specific mapping with Enter");
        KeyboardHelper.SendKey(Key.Enter);
        Assert.IsTrue(
            editor.WaitFor(
                () => AppSpecificShortcutMappings().Any(mapping =>
                    mapping?["originalKeys"]?.GetValue<string>() == $"{LeftControl};{B}" &&
                    mapping?["newRemapKeys"]?.GetValue<string>() == D.ToString(System.Globalization.CultureInfo.InvariantCulture) &&
                    mapping?["targetApp"]?.GetValue<string>().Equals(currentProcess, StringComparison.OrdinalIgnoreCase) == true),
                timeoutMS: 10_000,
                pollIntervalMS: 250),
            "Enter did not save the valid app-specific Ctrl+B to D mapping.");
    }

    private void RecordKeys(Session editor, string toggleAutomationId, params int[] keys)
    {
        Step($"Recording [{string.Join(", ", keys.Select(key => $"0x{key:X2}"))}] through {toggleAutomationId}");
        var toggle = editor.Find<Element>(By.AccessibilityId(toggleAutomationId), timeoutMS: 5_000);
        toggle.Invoke(msPostAction: 200);
        Assert.IsTrue(toggle.WaitForProperty("ToggleState", "On", timeoutMS: 3_000), $"{toggleAutomationId} did not enter recording mode.");

        try
        {
            foreach (var key in keys)
            {
                KeyboardHelper.PressKey((Key)checked((byte)key));
            }
        }
        finally
        {
            foreach (var key in keys.Reverse())
            {
                KeyboardHelper.ReleaseKey((Key)checked((byte)key));
            }
        }

        toggle = editor.Find<Element>(By.AccessibilityId(toggleAutomationId), timeoutMS: 5_000);
        if (string.Equals(toggle.GetProperty("ToggleState"), "On", StringComparison.OrdinalIgnoreCase))
        {
            toggle.Invoke(msPostAction: 200);
        }

        Assert.IsTrue(toggle.WaitForProperty("ToggleState", "Off", timeoutMS: 3_000), $"{toggleAutomationId} did not leave recording mode.");
    }

    private static IReadOnlyList<Element> VisibleKeyButtons(Session editor) =>
        editor.FindAll<Element>(By.AccessibilityId("KeyButton"), timeoutMS: 5_000)
            .Where(button => button.Displayed && button.Width > 0 && button.Height > 0)
            .OrderBy(button => button.X)
            .ToList();

    private static bool ProfileContainsSingleKeyMapping(int source, int target) =>
        SingleKeyMappings().Any(mapping =>
            mapping?["originalKeys"]?.GetValue<string>() == source.ToString(System.Globalization.CultureInfo.InvariantCulture) &&
            mapping?["newRemapKeys"]?.GetValue<string>() == target.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static JsonArray SingleKeyMappings() =>
        KeyboardManagerSettings.ReadProfile()["remapKeys"]?["inProcess"] as JsonArray ?? new JsonArray();

    private static JsonArray AppSpecificShortcutMappings() =>
        KeyboardManagerSettings.ReadProfile()["remapShortcuts"]?["appSpecific"] as JsonArray ?? new JsonArray();
}
