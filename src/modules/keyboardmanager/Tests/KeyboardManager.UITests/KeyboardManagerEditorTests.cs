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
    private const int E = 0x45;
    private const int LeftControl = 0xA2;

    private static KeyboardInputFixtureLock? inputFixtureLock;

    protected override bool ReuseScopeAcrossTests => true;

    [ClassInitialize]
    public static void InitializeClass(TestContext testContext)
    {
        _ = testContext;
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

    [TestMethod("KeyboardManager.Editor.CreateEditPersistDelete")]
    [TestCategory("Keyboard Manager")]
    public void CreatesEditsPersistsAndDeletesRemapping()
    {
        var editor = OpenEditor();
        Assert.IsNotNull(FindExact<Element>(editor, "Nothing mapped yet"), "The editor did not start from an empty profile.");

        KeyboardManagerSettings.ApplyProfile(
            KeyboardManagerSettings.BuildProfile(
                singleKeyRemaps: new[] { KeyboardManagerSettings.SingleKeyRemap(D, E) },
                includeLoadProbe: false));
        CloseEditor();
        editor = OpenEditor();
        Assert.IsNotNull(FindExact<Element>(editor, "D", timeoutMS: 5_000), "The editor did not load the unrelated D to E mapping baseline.");

        Step("Opening Add new remapping");
        editor.Find<Button>(By.AccessibilityId("NewRemappingBtn"), timeoutMS: 10_000).Invoke(msPostAction: 300);
        var editorProcess = Session.FromProcess(KeyboardManagerTestConstants.EditorProcessName);
        Assert.IsTrue(
            editorProcess.WaitForElement(By.AccessibilityId("TriggerKeyToggleBtn"), timeoutMS: 15_000),
            "The Add new remapping dialog did not finish loading.");
        var save = FindExact<Button>(editorProcess, "Save", timeoutMS: 5_000);
        Assert.IsNotNull(save, "The Add new remapping dialog did not expose Save.");
        Assert.IsFalse(save!.IsEnabled, "Save was enabled before the mapping had a trigger and action.");

        RecordKeys(editorProcess, "TriggerKeyToggleBtn", A);
        RecordKeys(editorProcess, "ActionKeyToggleBtn", B);
        save = FindExact<Button>(editorProcess, "Save", timeoutMS: 5_000);
        Assert.IsNotNull(save, "Save disappeared after recording a complete mapping.");
        Assert.IsTrue(save!.IsEnabled, "Save did not enable after recording A to B.");

        Step("Saving A to B");
        save.Invoke(msPostAction: 500);
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
        Assert.IsTrue(
            editor.WaitFor(
                () =>
                {
                    if (FindExact<Element>(editorProcess, "Edit remapping", timeoutMS: 200) is not null)
                    {
                        return true;
                    }

                    sourceKey = FindExact<Element>(editorProcess, "A", timeoutMS: 500);
                    if (sourceKey is null)
                    {
                        return false;
                    }

                    sourceKey.MouseClick(msPostAction: 200);
                    return FindExact<Element>(editorProcess, "Edit remapping", timeoutMS: 500) is not null;
                },
                timeoutMS: 10_000,
                pollIntervalMS: 250),
            "The edit dialog did not open after retrying the mapping-row interaction.");

        RecordKeys(editorProcess, "ActionKeyToggleBtn", C);
        save = FindExact<Button>(editorProcess, "Save", timeoutMS: 5_000);
        Assert.IsNotNull(save, "The edit dialog did not expose Save.");
        save!.Invoke(msPostAction: 500);
        Assert.IsTrue(
            editor.WaitFor(
                () => ProfileContainsEditedAndUnrelatedMappings() && EditorSettingsContainsMappings((A, C), (D, E)),
                timeoutMS: 10_000,
                pollIntervalMS: 250),
            $"The editor did not finish persisting only the exact A to C mapping before closing. Profile: {KeyboardManagerSettings.ReadProfile().ToJsonString()}; editor settings: {KeyboardManagerSettings.ReadEditorSettings().ToJsonString()}");

        Step("Closing and reopening the editor to verify the edited mapping persisted");
        CloseEditor();
        editor = OpenEditor();
        editorProcess = Session.FromProcess(KeyboardManagerTestConstants.EditorProcessName);
        Assert.IsNotNull(FindExact<Element>(editorProcess, "A", timeoutMS: 5_000), "The reopened editor did not show source key A after editing.");
        Assert.IsTrue(
            editorProcess.FindAll<Element>(By.Name("C"), timeoutMS: 5_000)
                .Any(element => element.Name.Equals("C", StringComparison.OrdinalIgnoreCase)),
            "The reopened editor did not show target key C after editing.");
        Assert.AreEqual(
            2,
            editorProcess.FindAll<Button>(By.AccessibilityId("MappingMenuButton"), timeoutMS: 5_000).Count(button => button.Displayed),
            "The reopened editor did not show exactly the edited and unrelated mapping rows.");
        Assert.IsTrue(
            editor.WaitFor(
                () => ProfileContainsEditedAndUnrelatedMappings() && EditorSettingsContainsMappings((A, C), (D, E)),
                timeoutMS: 10_000,
                pollIntervalMS: 250),
            $"The reopened editor did not persist only the exact A to C mapping. Profile: {KeyboardManagerSettings.ReadProfile().ToJsonString()}; editor settings: {KeyboardManagerSettings.ReadEditorSettings().ToJsonString()}");

        string? editedMappingId = EditorSettingsMappingId(A, C);
        Assert.IsFalse(string.IsNullOrEmpty(editedMappingId), "The edited A to C mapping did not retain a metadata ID.");

        Step("Disabling the edited mapping through its row toggle");
        sourceKey = FindExact<Element>(editorProcess, "A", timeoutMS: 5_000);
        Assert.IsNotNull(sourceKey, "The edited A mapping could not be addressed for disabling.");
        var mappingToggle = editorProcess.FindAll<Element>(By.AccessibilityId("MappingEnabledToggle"), timeoutMS: 5_000)
            .Where(toggle => toggle.Displayed)
            .MinBy(toggle => Math.Abs(toggle.Y - sourceKey!.Y));
        Assert.IsNotNull(mappingToggle, "The edited A mapping row did not expose its active-state toggle.");
        Assert.AreEqual("On", mappingToggle!.GetProperty("ToggleState"), "The edited A mapping was not active before disabling.");
        mappingToggle.Invoke(msPostAction: 300);

        Assert.IsTrue(
            editor.WaitFor(
                () => SingleKeyMappings().Count == 1 &&
                    ProfileContainsSingleKeyMapping(D, E) &&
                    EditorSettingsContainsMapping(A, C, isActive: false) &&
                    EditorSettingsContainsMapping(D, E, isActive: true) &&
                    EditorSettingsMappingId(A, C) == editedMappingId,
                timeoutMS: 10_000,
                pollIntervalMS: 250),
            $"Disabling A to C did not atomically preserve only D to E and the original metadata ID. Profile: {KeyboardManagerSettings.ReadProfile().ToJsonString()}; editor settings: {KeyboardManagerSettings.ReadEditorSettings().ToJsonString()}");

        Step("Re-enabling the edited mapping through its row toggle");
        sourceKey = FindExact<Element>(editorProcess, "A", timeoutMS: 5_000);
        Assert.IsNotNull(sourceKey, "The inactive A mapping disappeared before re-enabling.");
        mappingToggle = editorProcess.FindAll<Element>(By.AccessibilityId("MappingEnabledToggle"), timeoutMS: 5_000)
            .Where(toggle => toggle.Displayed)
            .MinBy(toggle => Math.Abs(toggle.Y - sourceKey!.Y));
        Assert.IsNotNull(mappingToggle, "The inactive A mapping row did not expose its active-state toggle.");
        Assert.AreEqual("Off", mappingToggle!.GetProperty("ToggleState"), "The edited A mapping did not show the inactive state.");
        mappingToggle.Invoke(msPostAction: 300);

        Assert.IsTrue(
            editor.WaitFor(
                () => ProfileContainsEditedAndUnrelatedMappings() &&
                    EditorSettingsContainsMappings((A, C), (D, E)) &&
                    EditorSettingsMappingId(A, C) == editedMappingId,
                timeoutMS: 10_000,
                pollIntervalMS: 250),
            $"Re-enabling A to C did not atomically restore both mappings and the original metadata ID. Profile: {KeyboardManagerSettings.ReadProfile().ToJsonString()}; editor settings: {KeyboardManagerSettings.ReadEditorSettings().ToJsonString()}");

        Step("Deleting the edited mapping through its row menu");
        sourceKey = FindExact<Element>(editorProcess, "A", timeoutMS: 5_000);
        Assert.IsNotNull(sourceKey, "The edited A mapping could not be addressed for deletion.");
        var mappingMenu = editorProcess.FindAll<Button>(By.AccessibilityId("MappingMenuButton"), timeoutMS: 5_000)
            .Where(button => button.Displayed)
            .MinBy(button => Math.Abs(button.Y - sourceKey!.Y));
        Assert.IsNotNull(mappingMenu, "The edited A mapping row did not expose its menu.");
        mappingMenu!.Click(msPostAction: 300);
        var deleteMenuItem = FindExact<Element>(editorProcess, "Delete", timeoutMS: 5_000);
        Assert.IsNotNull(deleteMenuItem, "The mapping row menu did not expose Delete.");
        deleteMenuItem!.Click(msPostAction: 300);
        var confirmDelete = FindExact<Button>(editorProcess, "Delete", timeoutMS: 5_000);
        Assert.IsNotNull(confirmDelete, "The delete confirmation dialog did not expose its Delete button.");
        confirmDelete!.Click(msPostAction: 500);

        Assert.IsTrue(
            editor.WaitFor(
                () => FindExact<Element>(editorProcess, "D", timeoutMS: 500) is not null &&
                    FindExact<Element>(editorProcess, "A", timeoutMS: 500) is null &&
                    SingleKeyMappings().Count == 1 &&
                    ProfileContainsSingleKeyMapping(D, E) &&
                    EditorSettingsContainsMappings((D, E)),
                timeoutMS: 10_000,
                pollIntervalMS: 250),
            $"The editor did not delete only the edited A mapping. Profile: {KeyboardManagerSettings.ReadProfile().ToJsonString()}; editor settings: {KeyboardManagerSettings.ReadEditorSettings().ToJsonString()}");

        Step("Closing and reopening the editor to verify deletion persisted");
        CloseEditor();
        editor = OpenEditor();
        Assert.IsNotNull(FindExact<Element>(editor, "D", timeoutMS: 5_000), "The reopened editor lost the unrelated D to E mapping after deletion.");
        Assert.IsNull(FindExact<Element>(editor, "A", timeoutMS: 1_000), "The reopened editor restored the deleted A mapping.");

        Step("Deleting the native profile while retaining editor metadata");
        CloseEditor();
        File.Delete(KeyboardManagerSettings.ProfilePath);
        editor = OpenEditor();
        Assert.IsTrue(
            editor.WaitFor(
                () => EditorSettingsContainsMapping(D, E, isActive: false),
                timeoutMS: 10_000,
                pollIntervalMS: 250),
            $"The editor did not reconcile retained D to E metadata as inactive after the native profile was removed. Editor settings: {KeyboardManagerSettings.ReadEditorSettings().ToJsonString()}");

        Step("Creating a fresh mapping after missing-profile recovery");
        editor.Find<Button>(By.AccessibilityId("NewRemappingBtn"), timeoutMS: 10_000).Invoke(msPostAction: 300);
        editorProcess = Session.FromProcess(KeyboardManagerTestConstants.EditorProcessName);
        Assert.IsTrue(
            editorProcess.WaitForElement(By.AccessibilityId("TriggerKeyToggleBtn"), timeoutMS: 15_000),
            "The remapping dialog did not load after missing-profile recovery.");
        RecordKeys(editorProcess, "TriggerKeyToggleBtn", A);
        RecordKeys(editorProcess, "ActionKeyToggleBtn", B);
        save = FindExact<Button>(editorProcess, "Save", timeoutMS: 5_000);
        Assert.IsNotNull(save, "The recovered editor did not expose Save for A to B.");
        save!.Invoke(msPostAction: 500);
        Assert.IsTrue(
            editor.WaitFor(
                () => ProfileContainsOnlySingleKeyMapping(A, B, C) &&
                    EditorSettingsContainsMapping(A, B, isActive: true) &&
                    EditorSettingsContainsMapping(D, E, isActive: false),
                timeoutMS: 10_000,
                pollIntervalMS: 250),
            $"The editor did not create a fresh A to B profile while retaining inactive D to E metadata. Profile: {KeyboardManagerSettings.ReadProfile().ToJsonString()}; editor settings: {KeyboardManagerSettings.ReadEditorSettings().ToJsonString()}");
    }

    [TestMethod("KeyboardManager.Editor.InputAndValidation")]
    [TestCategory("Keyboard Manager")]
    public void DialogSupportsRecordingDropdownsKeyboardAndValidation()
    {
        var editor = OpenEditor();
        editor.Find<Button>(By.AccessibilityId("NewRemappingBtn"), timeoutMS: 10_000).Invoke(msPostAction: 300);
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
        editor.Find<Button>(By.AccessibilityId("NewRemappingBtn"), timeoutMS: 10_000).Invoke(msPostAction: 300);
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
        save!.Invoke(msPostAction: 500);
        Assert.IsTrue(
            editor.WaitFor(
                () => ProfileContainsSingleKeyMapping((int)Key.Num1, (int)Key.Num2),
                timeoutMS: 10_000,
                pollIntervalMS: 250),
            "Mouse and keyboard dropdown selection did not persist the expected 1 to 2 mapping.");

        Step("Opening a fresh dialog for Ctrl+B to D app-specific validation");
        editor.Find<Button>(By.AccessibilityId("NewRemappingBtn"), timeoutMS: 10_000).Invoke(msPostAction: 300);
        RecordKeys(editorProcess, "TriggerKeyToggleBtn", LeftControl, B);
        RecordKeys(editorProcess, "ActionKeyToggleBtn", D);
        appSpecific = editorProcess.Find<CheckBox>(By.AccessibilityId("AppSpecificCheckBox"), timeoutMS: 5_000);
        Assert.IsTrue(appSpecific.IsEnabled, "App-specific scope did not enable after the trigger became Ctrl+B.");
        appSpecific.Click(msPostAction: 300);
        Assert.IsTrue(appSpecific.IsChecked, "The app-specific scope checkbox did not become checked.");

        save = FindExact<Button>(editorProcess, "Save", timeoutMS: 5_000);
        Assert.IsNotNull(save, "The app-specific mapping did not expose Save.");
        Step("Attempting to save without an application name");
        save!.Invoke(msPostAction: 300);
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

    [TestMethod("KeyboardManager.Editor.ActionPersistence")]
    [TestCategory("Keyboard Manager")]
    public void PersistsAndReloadsSpecialActions()
    {
        const string url = "https://example.com/keyboard-manager";
        const string programPath = "notepad.exe";
        const string programArgs = "action-persistence.txt";
        const string startInDirectory = @"C:\Windows";
        const string text = "Keyboard Manager text action";

        var editor = OpenEditor();
        Step("Creating Ctrl+A to Open URL");
        editor.Find<Button>(By.AccessibilityId("NewRemappingBtn"), timeoutMS: 10_000).Invoke(msPostAction: 300);
        var editorProcess = Session.FromProcess(KeyboardManagerTestConstants.EditorProcessName);
        Assert.IsTrue(
            editorProcess.WaitForElement(By.AccessibilityId("TriggerKeyToggleBtn"), timeoutMS: 15_000),
            "The Open URL remapping dialog did not finish loading.");
        RecordKeys(editorProcess, "TriggerKeyToggleBtn", LeftControl, A);
        SelectActionType(editorProcess, "Open URL", "UrlPathInput");
        editorProcess.Find<TextBox>(By.AccessibilityId("UrlPathInput"), timeoutMS: 5_000).SetText(url);
        var save = FindExact<Button>(editorProcess, "Save", timeoutMS: 5_000);
        Assert.IsNotNull(save, "The Open URL mapping did not expose Save.");
        save!.Invoke(msPostAction: 500);
        Assert.IsTrue(
            editor.WaitFor(
                () => ProfileContainsOpenUrlMapping(LeftControl, A, url) &&
                    EditorSettingsContainsOpenUrlMapping(LeftControl, A, url),
                timeoutMS: 10_000,
                pollIntervalMS: 250),
            $"The Open URL mapping did not persist canonically. Profile: {KeyboardManagerSettings.ReadProfile().ToJsonString()}; editor settings: {KeyboardManagerSettings.ReadEditorSettings().ToJsonString()}");

        Step("Creating Ctrl+B to Open app");
        editor.Find<Button>(By.AccessibilityId("NewRemappingBtn"), timeoutMS: 10_000).Invoke(msPostAction: 300);
        editorProcess = Session.FromProcess(KeyboardManagerTestConstants.EditorProcessName);
        Assert.IsTrue(
            editorProcess.WaitForElement(By.AccessibilityId("TriggerKeyToggleBtn"), timeoutMS: 15_000),
            "The Open app remapping dialog did not finish loading.");
        RecordKeys(editorProcess, "TriggerKeyToggleBtn", LeftControl, B);
        SelectActionType(editorProcess, "Open app", "ProgramPathInput");
        editorProcess.Find<TextBox>(By.AccessibilityId("ProgramPathInput"), timeoutMS: 5_000).SetText(programPath);
        editorProcess.Find<TextBox>(By.AccessibilityId("ProgramArgsInput"), timeoutMS: 5_000).SetText(programArgs);
        editorProcess.Find<TextBox>(By.AccessibilityId("StartInPathInput"), timeoutMS: 5_000).SetText(startInDirectory);
        save = FindExact<Button>(editorProcess, "Save", timeoutMS: 5_000);
        Assert.IsNotNull(save, "The Open app mapping did not expose Save.");
        save!.Invoke(msPostAction: 500);
        Assert.IsTrue(
            editor.WaitFor(
                () => ProfileContainsOpenAppMapping(LeftControl, B, programPath, programArgs, startInDirectory) &&
                    EditorSettingsContainsOpenAppMapping(LeftControl, B, programPath, programArgs, startInDirectory),
                timeoutMS: 10_000,
                pollIntervalMS: 250),
            $"The Open app mapping did not persist canonically. Profile: {KeyboardManagerSettings.ReadProfile().ToJsonString()}; editor settings: {KeyboardManagerSettings.ReadEditorSettings().ToJsonString()}");

        Step("Creating Ctrl+C to Insert text");
        editor.Find<Button>(By.AccessibilityId("NewRemappingBtn"), timeoutMS: 10_000).Invoke(msPostAction: 300);
        editorProcess = Session.FromProcess(KeyboardManagerTestConstants.EditorProcessName);
        Assert.IsTrue(
            editorProcess.WaitForElement(By.AccessibilityId("TriggerKeyToggleBtn"), timeoutMS: 15_000),
            "The Insert text remapping dialog did not finish loading.");
        RecordKeys(editorProcess, "TriggerKeyToggleBtn", LeftControl, C);
        SelectActionType(editorProcess, "Insert text", "TextContentBox");
        editorProcess.Find<TextBox>(By.AccessibilityId("TextContentBox"), timeoutMS: 5_000).SetText(text);
        save = FindExact<Button>(editorProcess, "Save", timeoutMS: 5_000);
        Assert.IsNotNull(save, "The Insert text mapping did not expose Save.");
        save!.Invoke(msPostAction: 500);
        Assert.IsTrue(
            editor.WaitFor(
                () => ProfileContainsTextMapping(LeftControl, C, text) &&
                    EditorSettingsContainsTextMapping(LeftControl, C, text),
                timeoutMS: 10_000,
                pollIntervalMS: 250),
            $"The Insert text mapping did not persist canonically. Profile: {KeyboardManagerSettings.ReadProfile().ToJsonString()}; editor settings: {KeyboardManagerSettings.ReadEditorSettings().ToJsonString()}");

        Step("Reopening the editor to verify native action readback");
        CloseEditor();
        editor = OpenEditor();
        editorProcess = Session.FromProcess(KeyboardManagerTestConstants.EditorProcessName);
        Assert.AreEqual(
            3,
            editorProcess.FindAll<Button>(By.AccessibilityId("MappingMenuButton"), timeoutMS: 5_000).Count(button => button.Displayed),
            "The reopened editor did not show exactly the Open URL, Open app, and Insert text mappings.");
        Assert.IsTrue(
            editor.WaitFor(
                () => ProfileContainsOpenUrlMapping(LeftControl, A, url) &&
                    ProfileContainsOpenAppMapping(LeftControl, B, programPath, programArgs, startInDirectory) &&
                    ProfileContainsTextMapping(LeftControl, C, text) &&
                    EditorSettingsContainsOpenUrlMapping(LeftControl, A, url) &&
                    EditorSettingsContainsOpenAppMapping(LeftControl, B, programPath, programArgs, startInDirectory) &&
                    EditorSettingsContainsTextMapping(LeftControl, C, text),
                timeoutMS: 10_000,
                pollIntervalMS: 250),
            $"The reopened editor did not preserve canonical action mappings. Profile: {KeyboardManagerSettings.ReadProfile().ToJsonString()}; editor settings: {KeyboardManagerSettings.ReadEditorSettings().ToJsonString()}");
    }

    private static void SelectActionType(Session editor, string actionName, string expectedInputId)
    {
        editor.Find<ComboBox>(By.AccessibilityId("ActionTypeComboBox"), timeoutMS: 5_000).Click(msPostAction: 300);
        var action = FindExact<Element>(editor, actionName, timeoutMS: 5_000);
        Assert.IsNotNull(action, $"The action type list did not expose {actionName}.");
        action!.MouseClick(msPostAction: 300);
        Assert.IsTrue(editor.WaitForElement(By.AccessibilityId(expectedInputId), timeoutMS: 5_000), $"Selecting {actionName} did not expose {expectedInputId}.");
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
        ProfileContainsSingleKeyMapping(SingleKeyMappings(), source, target);

    private static bool ProfileContainsOnlySingleKeyMapping(int source, int target, int excludedTarget)
    {
        var mappings = SingleKeyMappings();
        return mappings.Count == 1 &&
            ProfileContainsSingleKeyMapping(mappings, source, target) &&
            !ProfileContainsSingleKeyMapping(mappings, source, excludedTarget);
    }

    private static bool ProfileContainsSingleKeyMapping(JsonArray mappings, int source, int target) =>
        mappings.Any(mapping =>
            mapping?["originalKeys"]?.GetValue<string>() == source.ToString(System.Globalization.CultureInfo.InvariantCulture) &&
            mapping?["newRemapKeys"]?.GetValue<string>() == target.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static bool ProfileContainsEditedAndUnrelatedMappings()
    {
        var mappings = SingleKeyMappings();
        return mappings.Count == 2 &&
               ProfileContainsSingleKeyMapping(mappings, A, C) &&
               !ProfileContainsSingleKeyMapping(mappings, A, B) &&
               ProfileContainsSingleKeyMapping(mappings, D, E);
    }

    private static bool EditorSettingsContainsMappings(params (int Source, int Target)[] expectedMappings)
    {
        var settings = KeyboardManagerSettings.ReadEditorSettings()["ShortcutSettingsDictionary"] as JsonObject;
        if (settings?.Count != expectedMappings.Length)
        {
            return false;
        }

        return expectedMappings.All(expected => settings.Any(entry =>
            entry.Value is JsonObject mappingSettings &&
            mappingSettings["IsActive"]?.GetValue<bool>() == true &&
            mappingSettings["Shortcut"] is JsonObject shortcut &&
            shortcut["OriginalKeys"]?.GetValue<string>() == expected.Source.ToString(System.Globalization.CultureInfo.InvariantCulture) &&
            shortcut["TargetKeys"]?.GetValue<string>() == expected.Target.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static bool EditorSettingsContainsMapping(int source, int target, bool isActive)
    {
        var settings = KeyboardManagerSettings.ReadEditorSettings()["ShortcutSettingsDictionary"] as JsonObject;
        return settings?.Any(entry =>
            entry.Value is JsonObject mappingSettings &&
            mappingSettings["IsActive"]?.GetValue<bool>() == isActive &&
            mappingSettings["Shortcut"] is JsonObject shortcut &&
            shortcut["OriginalKeys"]?.GetValue<string>() == source.ToString(System.Globalization.CultureInfo.InvariantCulture) &&
            shortcut["TargetKeys"]?.GetValue<string>() == target.ToString(System.Globalization.CultureInfo.InvariantCulture)) == true;
    }

    private static string? EditorSettingsMappingId(int source, int target)
    {
        var settings = KeyboardManagerSettings.ReadEditorSettings()["ShortcutSettingsDictionary"] as JsonObject;
        return settings?.FirstOrDefault(entry =>
            entry.Value is JsonObject mappingSettings &&
            mappingSettings["Shortcut"] is JsonObject shortcut &&
            shortcut["OriginalKeys"]?.GetValue<string>() == source.ToString(System.Globalization.CultureInfo.InvariantCulture) &&
            shortcut["TargetKeys"]?.GetValue<string>() == target.ToString(System.Globalization.CultureInfo.InvariantCulture)).Key;
    }

    private static bool ProfileContainsOpenUrlMapping(int modifier, int actionKey, string url) =>
        GlobalShortcutMappings().Any(mapping =>
            mapping?["originalKeys"]?.GetValue<string>() == $"{modifier};{actionKey}" &&
            mapping?["operationType"]?.GetValue<int>() == 2 &&
            mapping?["openUri"]?.GetValue<string>() == url &&
            mapping?["newRemapKeys"] is null);

    private static bool ProfileContainsOpenAppMapping(
        int modifier,
        int actionKey,
        string programPath,
        string programArgs,
        string startInDirectory) =>
        GlobalShortcutMappings().Any(mapping =>
            mapping?["originalKeys"]?.GetValue<string>() == $"{modifier};{actionKey}" &&
            mapping?["operationType"]?.GetValue<int>() == 1 &&
            mapping?["runProgramFilePath"]?.GetValue<string>() == programPath &&
            mapping?["runProgramArgs"]?.GetValue<string>() == programArgs &&
            mapping?["runProgramStartInDir"]?.GetValue<string>() == startInDirectory &&
            mapping?["newRemapKeys"] is null);

    private static bool EditorSettingsContainsOpenUrlMapping(int modifier, int actionKey, string url) =>
        EditorShortcutMappings().Any(mapping =>
            mapping?["IsActive"]?.GetValue<bool>() == true &&
            mapping?["Shortcut"]?["OriginalKeys"]?.GetValue<string>() == $"{modifier};{actionKey}" &&
            mapping?["Shortcut"]?["OperationType"]?.GetValue<int>() == 2 &&
            mapping?["Shortcut"]?["TargetKeys"]?.GetValue<string>() == string.Empty &&
            mapping?["Shortcut"]?["UriToOpen"]?.GetValue<string>() == url);

    private static bool EditorSettingsContainsOpenAppMapping(
        int modifier,
        int actionKey,
        string programPath,
        string programArgs,
        string startInDirectory) =>
        EditorShortcutMappings().Any(mapping =>
            mapping?["IsActive"]?.GetValue<bool>() == true &&
            mapping?["Shortcut"]?["OriginalKeys"]?.GetValue<string>() == $"{modifier};{actionKey}" &&
            mapping?["Shortcut"]?["OperationType"]?.GetValue<int>() == 1 &&
            mapping?["Shortcut"]?["TargetKeys"]?.GetValue<string>() == string.Empty &&
            mapping?["Shortcut"]?["ProgramPath"]?.GetValue<string>() == programPath &&
            mapping?["Shortcut"]?["ProgramArgs"]?.GetValue<string>() == programArgs &&
            mapping?["Shortcut"]?["StartInDirectory"]?.GetValue<string>() == startInDirectory);

    private static bool ProfileContainsTextMapping(int modifier, int actionKey, string text) =>
        GlobalTextMappings().Any(mapping =>
            mapping?["originalKeys"]?.GetValue<string>() == $"{modifier};{actionKey}" &&
            mapping?["unicodeText"]?.GetValue<string>() == text &&
            mapping?["newRemapKeys"] is null);

    private static bool EditorSettingsContainsTextMapping(int modifier, int actionKey, string text) =>
        EditorShortcutMappings().Any(mapping =>
            mapping?["IsActive"]?.GetValue<bool>() == true &&
            mapping?["Shortcut"]?["OriginalKeys"]?.GetValue<string>() == $"{modifier};{actionKey}" &&
            mapping?["Shortcut"]?["OperationType"]?.GetValue<int>() == 3 &&
            mapping?["Shortcut"]?["TargetKeys"]?.GetValue<string>() == string.Empty &&
            mapping?["Shortcut"]?["TargetText"]?.GetValue<string>() == text);

    private static IEnumerable<JsonNode?> EditorShortcutMappings() =>
        (KeyboardManagerSettings.ReadEditorSettings()["ShortcutSettingsDictionary"] as JsonObject)?.Select(entry => entry.Value) ??
        Enumerable.Empty<JsonNode?>();

    private static JsonArray SingleKeyMappings() =>
        KeyboardManagerSettings.ReadProfile()["remapKeys"]?["inProcess"] as JsonArray ?? new JsonArray();

    private static JsonArray AppSpecificShortcutMappings() =>
        KeyboardManagerSettings.ReadProfile()["remapShortcuts"]?["appSpecific"] as JsonArray ?? new JsonArray();

    private static JsonArray GlobalShortcutMappings() =>
        KeyboardManagerSettings.ReadProfile()["remapShortcuts"]?["global"] as JsonArray ?? new JsonArray();

    private static JsonArray GlobalTextMappings() =>
        KeyboardManagerSettings.ReadProfile()["remapShortcutsToText"]?["global"] as JsonArray ?? new JsonArray();
}
