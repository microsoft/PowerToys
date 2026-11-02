// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.KeyboardManager.UITests;

/// <summary>
/// Guards what a screen reader announces in the editor: every control a keyboard user lands on must
/// expose a meaningful UIA Name, not an empty string or an implementation type name.
/// </summary>
/// <remarks>
/// Seeded with function keys (F13-F15) so the running engine never rewrites keys the harness sends.
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class KeyboardManagerAccessibilityTests : KeyboardManagerTestBase
{
    private const int F13 = 0x7C;
    private const int F14 = 0x7D;
    private const int F15 = 0x7E;
    private const int LeftControl = 0xA2;
    private const int V = 0x56;

    private const string ImplementationNamespace = "KeyboardManagerEditorUI.";

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

    [TestMethod("KeyboardManager.Editor.Accessibility.MainPageNames")]
    [TestCategory("Keyboard Manager")]
    [TestCategory("Accessibility")]
    public void MainPageExposesMeaningfulNames()
    {
        OpenSeededEditor();
        var editorProcess = Session.FromProcess(KeyboardManagerTestConstants.EditorProcessName);

        Step("Checking the New button and the mapping list names");
        Assert.AreEqual(
            "New remapping",
            editorProcess.Find<Button>(By.AccessibilityId("NewRemappingBtn"), timeoutMS: 5_000).Name,
            "The icon + text New button must announce what it creates.");
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(editorProcess.Find<Element>(By.AccessibilityId("RemappingsListView"), timeoutMS: 5_000).Name),
            "The key remapping list has no accessible name, so screen readers announce an unnamed list.");

        Step("Checking that mapping rows describe trigger and action instead of a type name");
        var rows = editorProcess.FindAll<Element>(By.Name("maps to"), timeoutMS: 5_000)
            .Where(element => element.ControlType.Contains("ListItem", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.IsTrue(
            rows.Any(row => row.Name.Contains("F13", StringComparison.Ordinal) && row.Name.Contains("F14", StringComparison.Ordinal)),
            $"No row announced the F13 to F14 mapping. Row names: [{string.Join(" | ", rows.Select(row => row.Name))}].");
        Assert.IsTrue(
            rows.Any(row => row.Name.Contains("F15", StringComparison.Ordinal)),
            $"No row announced the Ctrl+F15 shortcut mapping. Row names: [{string.Join(" | ", rows.Select(row => row.Name))}].");
        Assert.AreEqual(
            0,
            editorProcess.FindAll<Element>(By.Name(ImplementationNamespace), timeoutMS: 1_000)
                .Count(element => element.Name.StartsWith(ImplementationNamespace, StringComparison.Ordinal)),
            "An element still announces an implementation type name.");

        Step("Checking that per-row toggles and menus carry the row context");
        AssertContextualNames(editorProcess, "MappingEnabledToggle", "Enable ");
        AssertContextualNames(editorProcess, "MappingMenuButton", "More options for ");
    }

    [TestMethod("KeyboardManager.Editor.Accessibility.DialogNames")]
    [TestCategory("Keyboard Manager")]
    [TestCategory("Accessibility")]
    public void RemappingDialogExposesMeaningfulNames()
    {
        var editor = OpenSeededEditor();
        editor.Find<Button>(By.AccessibilityId("NewRemappingBtn"), timeoutMS: 10_000).Invoke(msPostAction: 300);
        var editorProcess = Session.FromProcess(KeyboardManagerTestConstants.EditorProcessName);
        Assert.IsTrue(
            editorProcess.WaitForElement(By.AccessibilityId("TriggerKeyToggleBtn"), timeoutMS: 15_000),
            "The Add new remapping dialog did not finish loading.");

        Step("Checking trigger/action type pickers and recording buttons");
        Assert.AreEqual("Trigger type", editorProcess.Find<Element>(By.AccessibilityId("TriggerTypeComboBox"), timeoutMS: 5_000).Name);
        Assert.AreEqual("Action type", editorProcess.Find<Element>(By.AccessibilityId("ActionTypeComboBox"), timeoutMS: 5_000).Name);

        var triggerRecord = editorProcess.Find<Element>(By.AccessibilityId("TriggerKeyToggleBtn"), timeoutMS: 5_000);
        Assert.AreEqual("Record trigger keys", triggerRecord.Name);
        StringAssert.Contains(triggerRecord.HelpText, "Tab", "The recording button must tell screen reader users how to stop recording.");
        Assert.AreEqual("Record action keys", editorProcess.Find<Element>(By.AccessibilityId("ActionKeyToggleBtn"), timeoutMS: 5_000).Name);

        Step("Checking the key dropdown buttons and the key list");
        var keyButtons = editorProcess.FindAll<Element>(By.AccessibilityId("KeyButton"), timeoutMS: 5_000)
            .Where(button => button.Displayed && button.Width > 0 && button.Height > 0)
            .ToList();
        Assert.IsTrue(keyButtons.Count > 0, "The dialog did not expose any key dropdown buttons.");
        foreach (var keyButton in keyButtons)
        {
            Assert.AreEqual("Select key", keyButton.Name, "An empty key dropdown must announce its purpose.");
        }

        keyButtons[0].Click(msPostAction: 300);
        var keyList = editorProcess.Find<Element>(By.AccessibilityId("KeyListView"), timeoutMS: 5_000);
        Assert.AreEqual("Keys", keyList.Name, "The key picker list has no accessible name.");
        Assert.AreEqual(
            0,
            editorProcess.FindAll<Element>(By.Name("KeyCode ="), timeoutMS: 1_000).Count,
            "A key picker item announces its record ToString() instead of the key name.");

        Step("Closing the picker and the dialog without saving");
        KeyboardHelper.SendKey(Key.Esc);
        KeyboardHelper.SendKey(Key.Esc);
    }

    private static void AssertContextualNames(Session editorProcess, string automationId, string expectedPrefix)
    {
        var controls = editorProcess.FindAll<Element>(By.AccessibilityId(automationId), timeoutMS: 5_000)
            .Where(control => control.Displayed)
            .ToList();
        Assert.IsTrue(controls.Count >= 2, $"Expected one {automationId} per seeded mapping but found {controls.Count}.");
        foreach (var control in controls)
        {
            Assert.IsTrue(
                control.Name.StartsWith(expectedPrefix, StringComparison.Ordinal) && control.Name.Length > expectedPrefix.Length,
                $"{automationId} announced '{control.Name}', which does not identify the mapping it acts on.");
        }

        Assert.AreEqual(
            controls.Count,
            controls.Select(control => control.Name).Distinct(StringComparer.Ordinal).Count(),
            $"Two {automationId} controls announce the same name, so screen reader users cannot tell the rows apart.");
    }

    private Session OpenSeededEditor()
    {
        KeyboardManagerSettings.ApplyProfile(
            KeyboardManagerSettings.BuildProfile(
                singleKeyRemaps: new[] { KeyboardManagerSettings.SingleKeyRemap(F13, F14) },
                shortcutRemaps: new[] { new ShortcutRemap(new[] { LeftControl, F15 }, new[] { LeftControl, V }) },
                includeLoadProbe: false));

        return OpenEditor();
    }
}
