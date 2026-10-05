// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.KeyboardManager.UITests;

/// <summary>
/// Covers the editor's remapping-list search, filters and bulk delete.
/// </summary>
/// <remarks>
/// The seeded mappings deliberately use function keys (F13-F17) as their trigger keys so the
/// Keyboard Manager engine never rewrites the letters this test types into the search box.
/// </remarks>
[TestClass]
[DoNotParallelize]
public sealed class KeyboardManagerSearchFilterTests : KeyboardManagerTestBase
{
    private const int F13 = 0x7C;
    private const int F14 = 0x7D;
    private const int F15 = 0x7E;
    private const int F16 = 0x7F;
    private const int F17 = 0x80;
    private const int LeftControl = 0xA2;
    private const int LeftAlt = 0xA4;
    private const int LeftShift = 0xA0;
    private const int Q = 0x51;
    private const int V = 0x56;
    private const int W = 0x57;

    private const string TargetApp = "notepad";
    private const string AllAppsOption = "All apps";
    private const string GlobalOnlyOption = "Global only";
    private const string ClearFiltersLabel = "Clear filters";
    private const string NoResultsTitle = "No remappings match your filters";
    private const string SelectLabel = "Select";
    private const string CancelLabel = "Cancel";

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

    [TestMethod("KeyboardManager.Editor.SearchAndFilter")]
    [TestCategory("Keyboard Manager")]
    public void SearchesFiltersAndRecoversFromNoResults()
    {
        var editor = OpenSeededEditor();
        var editorProcess = Session.FromProcess(KeyboardManagerTestConstants.EditorProcessName);
        AssertRowCount(editor, editorProcess, 4, "The seeded profile did not load all four mappings.");

        Step("Focusing the title-bar search box with Ctrl+F and searching for the app-specific mapping");
        KeyboardHelper.SendKeys(Key.Ctrl, Key.F);
        KeyboardHelper.SendKeySequence(Key.N, Key.O, Key.T, Key.E, Key.P, Key.A, Key.D);
        AssertRowCount(editor, editorProcess, 1, $"Searching for '{TargetApp}' did not narrow the list to the app-specific mapping. Ctrl+F may not have moved focus into the search box.");
        Assert.IsNotNull(FindExact<Element>(editorProcess, "F17", timeoutMS: 5_000), $"The '{TargetApp}' search result did not keep the Shift+F17 mapping.");
        Assert.IsNull(FindExact<Element>(editorProcess, "F13", timeoutMS: 1_000), $"The '{TargetApp}' search result still showed the unrelated F13 mapping.");

        Step("Clearing the search from the filter flyout");
        OpenFilterFlyout(editorProcess);
        editorProcess.Find<Button>(By.AccessibilityId("ClearFiltersBtn"), timeoutMS: 5_000).Click(msPostAction: 300);
        CloseFilterFlyout();
        AssertRowCount(editor, editorProcess, 4, "Clear filters did not restore the full list after a search.");

        Step("Filtering to Ctrl remappings");
        ToggleModifierFilter(editorProcess, "CtrlFilterToggle");
        AssertRowCount(editor, editorProcess, 1, "The Ctrl modifier filter did not narrow the list to the single Ctrl mapping.");
        Assert.IsNotNull(FindExact<Element>(editorProcess, "F15", timeoutMS: 5_000), "The Ctrl filter did not keep the Ctrl+F15 mapping.");

        Step("Swapping the Ctrl filter for the Alt filter");
        ToggleModifierFilter(editorProcess, "CtrlFilterToggle");
        ToggleModifierFilter(editorProcess, "AltFilterToggle");
        AssertRowCount(editor, editorProcess, 1, "The Alt modifier filter did not narrow the list to the single Alt mapping.");
        Assert.IsNotNull(FindExact<Element>(editorProcess, "F16", timeoutMS: 5_000), "The Alt filter did not keep the Alt+F16 mapping.");
        ToggleModifierFilter(editorProcess, "AltFilterToggle");
        AssertRowCount(editor, editorProcess, 4, "Clearing the Alt filter did not restore the full list.");

        Step("Filtering to global-only mappings");
        SelectAppFilter(editorProcess, GlobalOnlyOption);
        AssertRowCount(editor, editorProcess, 3, "The global-only application filter did not hide the app-specific mapping.");
        Assert.IsNull(FindExact<Element>(editorProcess, "F17", timeoutMS: 1_000), "The global-only filter still showed the app-specific Shift+F17 mapping.");
        SelectAppFilter(editorProcess, AllAppsOption);
        AssertRowCount(editor, editorProcess, 4, "Returning the application filter to all apps did not restore the full list.");

        Step("Driving the list into its no-results state");
        KeyboardHelper.SendKeys(Key.Ctrl, Key.F);
        KeyboardHelper.SendKeySequence(Key.Z, Key.Z, Key.Z, Key.Z);
        AssertRowCount(editor, editorProcess, 0, "A search with no matches still showed remapping rows.");
        Assert.IsNotNull(
            FindExact<Element>(editorProcess, NoResultsTitle, timeoutMS: 5_000),
            "The list did not show its dedicated no-results state for a search with no matches.");

        Step("Recovering from the no-results state through its Clear filters button");
        var clearFromNoResults = FindExact<Button>(editorProcess, ClearFiltersLabel, timeoutMS: 5_000);
        Assert.IsNotNull(clearFromNoResults, "The no-results state did not expose a Clear filters button.");
        clearFromNoResults!.Click(msPostAction: 300);
        AssertRowCount(editor, editorProcess, 4, "Clearing the filters from the no-results state did not restore the full list.");
        Assert.IsNull(
            FindExact<Element>(editorProcess, NoResultsTitle, timeoutMS: 1_000),
            "The no-results state stayed visible after the filters were cleared.");

        Step("Verifying that searching and filtering never touched the stored profile");
        Assert.IsTrue(
            ProfileContainsSeededMappings(),
            $"Searching and filtering changed the persisted profile. Profile: {KeyboardManagerSettings.ReadProfile().ToJsonString()}");
    }

    [TestMethod("KeyboardManager.Editor.BulkDelete")]
    [TestCategory("Keyboard Manager")]
    public void BulkDeletesTheSelectedRemappings()
    {
        var editor = OpenSeededEditor();
        var editorProcess = Session.FromProcess(KeyboardManagerTestConstants.EditorProcessName);
        AssertRowCount(editor, editorProcess, 4, "The seeded profile did not load all four mappings.");

        Step("Entering selection mode");
        var selectionModeButton = editorProcess.Find<Button>(By.AccessibilityId("SelectionModeButton"), timeoutMS: 5_000);
        Assert.AreEqual(SelectLabel, selectionModeButton.Name, "The list toolbar did not start outside selection mode.");
        selectionModeButton.Click(msPostAction: 300);
        Assert.IsTrue(
            editor.WaitFor(
                () => FindExact<Button>(editorProcess, CancelLabel, timeoutMS: 500) is not null,
                timeoutMS: 5_000,
                pollIntervalMS: 200),
            "The selection-mode button did not turn into the Cancel affordance that leaves selection mode.");

        Step("Selecting the F13 and Ctrl+F15 rows");
        SelectRow(editorProcess, "F13");
        SelectRow(editorProcess, "F15");

        var deleteSelected = editorProcess.Find<Button>(By.AccessibilityId("DeleteSelectedBtn"), timeoutMS: 5_000);
        Assert.IsTrue(
            editor.WaitFor(
                () => editorProcess.Find<Button>(By.AccessibilityId("DeleteSelectedBtn"), timeoutMS: 1_000).Name == "Delete selected (2)",
                timeoutMS: 5_000,
                pollIntervalMS: 200),
            $"The bulk-delete button did not report two selected rows. Current label: {deleteSelected.Name}.");
        Assert.IsTrue(deleteSelected.IsEnabled, "The bulk-delete button stayed disabled with two rows selected.");

        Step("Confirming the bulk delete");
        deleteSelected.Click(msPostAction: 300);
        Assert.IsNotNull(
            FindExact<Element>(editorProcess, "Delete selected remappings?", timeoutMS: 5_000),
            "The bulk-delete confirmation dialog did not open.");
        var confirmDelete = FindExact<Button>(editorProcess, "Delete", timeoutMS: 5_000);
        Assert.IsNotNull(confirmDelete, "The bulk-delete confirmation dialog did not expose its Delete button.");
        confirmDelete!.Click(msPostAction: 500);

        Step("Verifying that only the selected mappings were removed");
        Assert.IsTrue(
            editor.WaitFor(
                () => SingleKeyMappings().Count == 0 &&
                    GlobalShortcutMappings().Count == 1 &&
                    ProfileContainsShortcut(GlobalShortcutMappings(), $"{LeftAlt};{F16}") &&
                    AppSpecificShortcutMappings().Count == 1 &&
                    ProfileContainsShortcut(AppSpecificShortcutMappings(), $"{LeftShift};{F17}"),
                timeoutMS: 10_000,
                pollIntervalMS: 250),
            $"The bulk delete did not remove exactly the two selected mappings. Profile: {KeyboardManagerSettings.ReadProfile().ToJsonString()}");
        AssertRowCount(editor, editorProcess, 2, "The list did not settle on the two surviving mappings after the bulk delete.");

        Step("Verifying that the editor left selection mode after deleting");
        Assert.IsTrue(
            editor.WaitFor(
                () => editorProcess.Find<Button>(By.AccessibilityId("SelectionModeButton"), timeoutMS: 1_000).Name == SelectLabel,
                timeoutMS: 5_000,
                pollIntervalMS: 200),
            "The editor stayed in selection mode after the bulk delete completed.");

        Step("Reopening the editor to verify the bulk delete persisted");
        CloseEditor();
        editor = OpenEditor();
        editorProcess = Session.FromProcess(KeyboardManagerTestConstants.EditorProcessName);
        AssertRowCount(editor, editorProcess, 2, "The reopened editor did not persist the bulk delete.");
        Assert.IsNull(FindExact<Element>(editorProcess, "F13", timeoutMS: 1_000), "The reopened editor restored a bulk-deleted mapping.");
    }

    private static bool ProfileContainsShortcut(JsonArray mappings, string originalKeys) =>
        mappings.Any(mapping => mapping?["originalKeys"]?.GetValue<string>() == originalKeys);

    private static bool ProfileContainsSeededMappings() =>
        SingleKeyMappings().Count == 1 &&
        SingleKeyMappings().Any(mapping =>
            mapping?["originalKeys"]?.GetValue<string>() == F13.ToString(System.Globalization.CultureInfo.InvariantCulture) &&
            mapping?["newRemapKeys"]?.GetValue<string>() == F14.ToString(System.Globalization.CultureInfo.InvariantCulture)) &&
        GlobalShortcutMappings().Count == 2 &&
        AppSpecificShortcutMappings().Count == 1;

    private static JsonArray SingleKeyMappings() =>
        KeyboardManagerSettings.ReadProfile()["remapKeys"]?["inProcess"] as JsonArray ?? new JsonArray();

    private static JsonArray GlobalShortcutMappings() =>
        KeyboardManagerSettings.ReadProfile()["remapShortcuts"]?["global"] as JsonArray ?? new JsonArray();

    private static JsonArray AppSpecificShortcutMappings() =>
        KeyboardManagerSettings.ReadProfile()["remapShortcuts"]?["appSpecific"] as JsonArray ?? new JsonArray();

    private static int VisibleRowCount(Session editorProcess) =>
        editorProcess.FindAll<Button>(By.AccessibilityId("MappingMenuButton"), timeoutMS: 2_000)
            .Count(button => button.Displayed);

    /// <summary>
    /// Seeds a deterministic four-mapping profile and opens the editor on it. One single-key remap,
    /// two global shortcuts on different modifiers, and one app-specific shortcut give every filter
    /// dimension (text, modifier, application) something distinct to match.
    /// </summary>
    private Session OpenSeededEditor()
    {
        KeyboardManagerSettings.ApplyProfile(
            KeyboardManagerSettings.BuildProfile(
                singleKeyRemaps: new[] { KeyboardManagerSettings.SingleKeyRemap(F13, F14) },
                shortcutRemaps: new[]
                {
                    new ShortcutRemap(new[] { LeftControl, F15 }, new[] { LeftControl, V }),
                    new ShortcutRemap(new[] { LeftAlt, F16 }, new[] { W }),
                    new ShortcutRemap(new[] { LeftShift, F17 }, new[] { Q }, TargetApp),
                },
                includeLoadProbe: false));

        return OpenEditor();
    }

    private void AssertRowCount(Session editor, Session editorProcess, int expected, string message)
    {
        Assert.IsTrue(
            editor.WaitFor(
                () => VisibleRowCount(editorProcess) == expected,
                timeoutMS: 10_000,
                pollIntervalMS: 250),
            $"{message} Expected {expected} rows but saw {VisibleRowCount(editorProcess)}.");
    }

    private void OpenFilterFlyout(Session editorProcess)
    {
        editorProcess.Find<Button>(By.AccessibilityId("FilterButton"), timeoutMS: 5_000).Click(msPostAction: 300);
        Assert.IsTrue(
            editorProcess.WaitForElement(By.AccessibilityId("ClearFiltersBtn"), timeoutMS: 5_000),
            "The filter flyout did not open.");
    }

    private void CloseFilterFlyout()
    {
        KeyboardHelper.SendKey(Key.Esc);
    }

    private void ToggleModifierFilter(Session editorProcess, string automationId)
    {
        Step($"Toggling {automationId}");
        OpenFilterFlyout(editorProcess);
        editorProcess.Find<Element>(By.AccessibilityId(automationId), timeoutMS: 5_000).Invoke(msPostAction: 300);
        CloseFilterFlyout();
    }

    private void SelectAppFilter(Session editorProcess, string optionName)
    {
        Step($"Selecting the '{optionName}' application filter");
        OpenFilterFlyout(editorProcess);
        editorProcess.Find<ComboBox>(By.AccessibilityId("AppFilterCombo"), timeoutMS: 5_000).Select(optionName);
        CloseFilterFlyout();
    }

    private void SelectRow(Session editorProcess, string keyName)
    {
        var rowKey = FindExact<Element>(editorProcess, keyName, timeoutMS: 5_000);
        Assert.IsNotNull(rowKey, $"The {keyName} mapping row could not be addressed for selection.");
        rowKey!.MouseClick(msPostAction: 300);
    }
}
