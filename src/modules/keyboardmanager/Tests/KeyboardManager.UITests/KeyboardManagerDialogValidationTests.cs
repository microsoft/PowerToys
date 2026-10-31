// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.KeyboardManager.UITests;

/// <summary>
/// Verifies that the remapping dialog's Save enablement follows its currently
/// visible action and trigger, not stale data from an action that was previously
/// selected. These are UI tests of the WinUI dialog, not native input tests.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class KeyboardManagerDialogValidationTests : KeyboardManagerTestBase
{
    private const int F13 = 0x7C;
    private const int F14 = 0x7D;
    private const int LeftControl = 0xA2;

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

    [TestMethod("KeyboardManager.Editor.ActionSwitchValidation")]
    [TestCategory("Keyboard Manager")]
    public void SwitchingActionTypesInvalidatesOnlyTheVisibleAction()
    {
        var editor = OpenEditor();
        var dialog = OpenNewDialog(editor);
        var baseline = ReadProfile();

        Step("Creating a complete key-to-key remapping in the dialog");
        RecordKeys(dialog, "TriggerKeyToggleBtn", LeftControl, F13);
        RecordKeys(dialog, "ActionKeyToggleBtn", LeftControl, F14);
        AssertSaveEnabled(dialog, true, "A complete shortcut should allow Save.");

        Step("Switching from key remap to URL must require a non-empty URL");
        SelectAction(dialog, "Open URL", "UrlPathInput");
        AssertSaveEnabled(dialog, false, "The old shortcut action incorrectly enabled saving an empty URL.");
        var url = dialog.Find<TextBox>(By.AccessibilityId("UrlPathInput"), timeoutMS: 5_000);
        url.SetText("https://example.com/powertoys");
        AssertSaveEnabled(dialog, true, "Supplying a valid URL did not re-enable Save.");

        Step("Switching to a program must not inherit the URL field's completeness");
        SelectAction(dialog, "Open app", "ProgramPathInput");
        AssertSaveEnabled(dialog, false, "An unrelated URL improperly satisfied the program-path requirement.");
        var program = dialog.Find<TextBox>(By.AccessibilityId("ProgramPathInput"), timeoutMS: 5_000);
        program.SetText("notepad.exe");
        AssertSaveEnabled(dialog, true, "Specifying a program path did not enable Save.");

        Step("Switching to text must require non-empty inserted text");
        SelectAction(dialog, "Insert text", "TextContentBox");
        AssertSaveEnabled(dialog, false, "An unrelated program path improperly satisfied the text requirement.");
        var text = dialog.Find<TextBox>(By.AccessibilityId("TextContentBox"), timeoutMS: 5_000);
        text.SetText("PowerToys Keyboard Manager");
        AssertSaveEnabled(dialog, true, "Specifying text did not enable Save.");

        Step("Clearing the current action must disable Save, even with other completed actions");
        text.SetText(string.Empty);
        AssertSaveEnabled(dialog, false, "Clearing text did not disable Save.");

        Step("Cancelling ensures that the exploratory action switches never persist");
        KeyboardHelper.SendKey(Key.Esc);
        AssertDialogClosed(editor, dialog);
        Assert.AreEqual(baseline, ReadProfile(), "Switching action types changed the native profile before Save.");
    }

    [TestMethod("KeyboardManager.Editor.MissingUrlTrigger")]
    [TestCategory("Keyboard Manager")]
    public void UrlActionRequiresModifierShortcutAndRecoversAfterIncompleteTrigger()
    {
        var editor = OpenEditor();
        var dialog = OpenNewDialog(editor);
        var baseline = ReadProfile();

        Step("Selecting URL and entering the value before any trigger");
        SelectAction(dialog, "Open URL", "UrlPathInput");
        dialog.Find<TextBox>(By.AccessibilityId("UrlPathInput"), timeoutMS: 5_000)
            .SetText("https://example.com/keyboard-manager");
        AssertSaveEnabled(dialog, false, "URL Save was enabled with no trigger.");

        Step("Recording one key is not a legal URL shortcut");
        RecordKeys(dialog, "TriggerKeyToggleBtn", F13);
        AssertSaveEnabled(dialog, false, "URL Save was enabled for a single-key trigger.");

        Step("Replacing the trigger with Ctrl+F13 permits the URL action");
        RecordKeys(dialog, "TriggerKeyToggleBtn", LeftControl, F13);
        AssertSaveEnabled(dialog, true, "A complete Ctrl+F13 URL action stayed disabled.");

        Step("Deleting the URL value invalidates the otherwise complete shortcut");
        dialog.Find<TextBox>(By.AccessibilityId("UrlPathInput"), timeoutMS: 5_000)
            .SetText(string.Empty);
        AssertSaveEnabled(dialog, false, "The URL became empty but Save remained enabled.");

        KeyboardHelper.SendKey(Key.Esc);
        AssertDialogClosed(editor, dialog);
        Assert.AreEqual(baseline, ReadProfile(), "An incomplete URL action was written to the profile.");
    }

    [TestMethod("KeyboardManager.Editor.MissingProgramTrigger")]
    [TestCategory("Keyboard Manager")]
    public void ProgramActionRequiresModifierShortcutAndNonWhitespacePath()
    {
        var editor = OpenEditor();
        var dialog = OpenNewDialog(editor);
        var baseline = ReadProfile();

        Step("Selecting Open app and supplying a path before a trigger");
        SelectAction(dialog, "Open app", "ProgramPathInput");
        var path = dialog.Find<TextBox>(By.AccessibilityId("ProgramPathInput"), timeoutMS: 5_000);
        path.SetText("notepad.exe");
        AssertSaveEnabled(dialog, false, "Program Save was enabled without a trigger.");

        Step("A single function key is not a valid program action trigger");
        RecordKeys(dialog, "TriggerKeyToggleBtn", F13);
        AssertSaveEnabled(dialog, false, "Program Save was enabled for an unmodified single key.");

        Step("Recording Ctrl+F13 completes the shortcut");
        RecordKeys(dialog, "TriggerKeyToggleBtn", LeftControl, F13);
        AssertSaveEnabled(dialog, true, "A completed program action did not enable Save.");

        Step("Whitespace-only path must not be treated as a program");
        path.SetText("   ");
        AssertSaveEnabled(dialog, false, "A whitespace-only program path enabled Save.");

        Step("Restoring the executable name should recover without reopening dialog");
        path.SetText("notepad.exe");
        AssertSaveEnabled(dialog, true, "Correcting the program path did not restore Save.");

        KeyboardHelper.SendKey(Key.Esc);
        AssertDialogClosed(editor, dialog);
        Assert.AreEqual(baseline, ReadProfile(), "A cancelled program action changed native mappings.");
    }

    [TestMethod("KeyboardManager.Editor.TextOutputValidation")]
    [TestCategory("Keyboard Manager")]
    public void TextActionDistinguishesMissingTextFromLiteralWhitespace()
    {
        var editor = OpenEditor();
        var dialog = OpenNewDialog(editor);
        var baseline = ReadProfile();

        Step("Selecting Insert text with no trigger and no payload");
        SelectAction(dialog, "Insert text", "TextContentBox");
        AssertSaveEnabled(dialog, false, "Empty text action enabled Save.");

        Step("Supplying text alone must not allow saving without trigger");
        var text = dialog.Find<TextBox>(By.AccessibilityId("TextContentBox"), timeoutMS: 5_000);
        text.SetText("Hello PowerToys");
        AssertSaveEnabled(dialog, false, "Text without a trigger enabled Save.");

        Step("Recording Ctrl+F13 completes the text action");
        RecordKeys(dialog, "TriggerKeyToggleBtn", LeftControl, F13);
        AssertSaveEnabled(dialog, true, "Valid trigger and text did not enable Save.");

        Step("Clearing text should immediately disable Save");
        text.SetText(string.Empty);
        AssertSaveEnabled(dialog, false, "An empty text payload was accepted.");

        Step("Nonempty literal whitespace is still text, not an empty value");
        text.SetText(" ");
        AssertSaveEnabled(dialog, true, "Literal whitespace was unexpectedly rejected as empty text.");

        KeyboardHelper.SendKey(Key.Esc);
        AssertDialogClosed(editor, dialog);
        Assert.AreEqual(baseline, ReadProfile(), "Changing text validation modified a profile without Save.");
    }

    [TestMethod("KeyboardManager.Editor.SelfMappingValidation")]
    [TestCategory("Keyboard Manager")]
    public void ShortcutToItselfIsBlockedThenCorrectedInSameDialog()
    {
        var editor = OpenEditor();
        var dialog = OpenNewDialog(editor);
        var baseline = ReadProfile();

        Step("Recording Ctrl+F13 for both trigger and target");
        RecordKeys(dialog, "TriggerKeyToggleBtn", LeftControl, F13);
        RecordKeys(dialog, "ActionKeyToggleBtn", LeftControl, F13);
        AssertSaveEnabled(dialog, false, "A shortcut mapping to itself should be rejected.");

        Step("Replacing only the target with Ctrl+F14 should resolve the conflict");
        RecordKeys(dialog, "ActionKeyToggleBtn", LeftControl, F14);
        AssertSaveEnabled(dialog, true, "Changing the target to a distinct shortcut did not clear self-mapping validation.");

        Step("Changing target back to source should restore the error");
        RecordKeys(dialog, "ActionKeyToggleBtn", LeftControl, F13);
        AssertSaveEnabled(dialog, false, "Reintroducing a self-mapping was not rejected.");

        KeyboardHelper.SendKey(Key.Esc);
        AssertDialogClosed(editor, dialog);
        Assert.AreEqual(baseline, ReadProfile(), "A rejected self-mapping was persisted.");
    }

    private Session OpenNewDialog(Session editor)
    {
        Step("Opening the new remapping dialog");
        editor.Find<Button>(By.AccessibilityId("NewRemappingBtn"), timeoutMS: 10_000)
            .Invoke(msPostAction: 300);
        var dialog = Session.FromProcess(KeyboardManagerTestConstants.EditorProcessName);
        Assert.IsTrue(
            dialog.WaitForElement(By.AccessibilityId("TriggerKeyToggleBtn"), timeoutMS: 10_000),
            "The new remapping dialog did not expose the trigger recorder.");
        AssertSaveEnabled(dialog, false, "The empty new mapping dialog should not allow Save.");
        return dialog;
    }

    private static void SelectAction(Session dialog, string actionName, string requiredField)
    {
        dialog.Find<ComboBox>(By.AccessibilityId("ActionTypeComboBox"), timeoutMS: 5_000)
            .Click(msPostAction: 300);
        var choice = FindExact<Element>(dialog, actionName, timeoutMS: 5_000);
        Assert.IsNotNull(choice, $"Could not select the {actionName} action.");
        choice!.MouseClick(msPostAction: 300);
        Assert.IsTrue(
            dialog.WaitForElement(By.AccessibilityId(requiredField), timeoutMS: 5_000),
            $"Selecting {actionName} did not show the {requiredField} field.");
    }

    private void RecordKeys(Session dialog, string toggleId, params int[] keys)
    {
        Step($"Recording [{string.Join(", ", keys.Select(k => $"0x{k:X2}"))}] in {toggleId}");
        var toggle = dialog.Find<Element>(By.AccessibilityId(toggleId), timeoutMS: 5_000);
        toggle.Invoke(msPostAction: 200);
        Assert.IsTrue(toggle.WaitForProperty("ToggleState", "On", timeoutMS: 3_000), $"{toggleId} did not enter recording mode.");

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

        toggle = dialog.Find<Element>(By.AccessibilityId(toggleId), timeoutMS: 5_000);
        if (string.Equals(toggle.GetProperty("ToggleState"), "On", StringComparison.OrdinalIgnoreCase))
        {
            toggle.Invoke(msPostAction: 200);
        }

        Assert.IsTrue(toggle.WaitForProperty("ToggleState", "Off", timeoutMS: 3_000), $"{toggleId} did not leave recording mode.");
    }

    private static void AssertSaveEnabled(Session dialog, bool expected, string message)
    {
        var save = FindExact<Button>(dialog, "Save", timeoutMS: 5_000);
        Assert.IsNotNull(save, "The remapping dialog does not expose Save.");
        Assert.AreEqual(expected, save!.IsEnabled, message);
    }

    private static void AssertDialogClosed(Session editor, Session dialog)
    {
        Assert.IsTrue(
            editor.WaitFor(
                () => FindExact<Button>(dialog, "Save", timeoutMS: 200) is null,
                timeoutMS: 5_000,
                pollIntervalMS: 200),
            "The remapping dialog did not close.");
    }

    private static string ReadProfile() =>
        KeyboardManagerSettings.ReadProfile().ToJsonString();
}
