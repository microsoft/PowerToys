// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Nodes;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.KeyboardManager.UITests;

[TestClass]
[DoNotParallelize]
public sealed class KeyboardManagerTextExpansionEditorTests : KeyboardManagerTestBase
{
    private const string SourceText = "brb";
    private const string ReplacementText = "be right back";
    private const int WinBoth = 0x104;
    private const int Space = 0x20;

    private static KeyboardInputFixtureLock? inputFixtureLock;

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

    [TestMethod("KeyboardManager.Editor.TextExpansionSearchFilterSelection")]
    [TestCategory("Keyboard Manager")]
    public void TextExpansionParticipatesInSearchFilterAndSelection()
    {
        var profile = KeyboardManagerSettings.BuildProfile(includeLoadProbe: false);
        profile["textReplacements"] = new JsonObject
        {
            ["inProcess"] = new JsonArray
            {
                new JsonObject
                {
                    ["id"] = "8c6f9e3a-5a70-4afd-8462-aa5df2a4ceaf",
                    ["sourceText"] = SourceText,
                    ["activationKeys"] = new JsonArray(WinBoth, Space),
                    ["replacementText"] = ReplacementText,
                    ["enabled"] = true,
                },
            },
        };
        KeyboardManagerSettings.ApplyProfile(profile);

        var editor = OpenEditor(requireForeground: false);
        var editorProcess = Session.FromProcess(KeyboardManagerTestConstants.EditorProcessName);

        Assert.IsNull(FindExact<Element>(editorProcess, "Nothing mapped yet", timeoutMS: 500), "The editor showed the empty state despite the text expansion mapping.");
        Assert.IsNotNull(FindExact<Element>(editorProcess, SourceText, timeoutMS: 5_000), "The editor did not load the text expansion source.");
        Assert.IsNotNull(FindExact<Element>(editorProcess, ReplacementText, timeoutMS: 5_000), "The editor did not load the text expansion replacement.");

        Step("Filtering the editor by text expansion replacement text");
        SetSearchText(editorProcess, ReplacementText);
        Assert.IsTrue(
            editor.WaitFor(() => FindExact<Element>(editorProcess, SourceText, timeoutMS: 300) is not null, timeoutMS: 5_000),
            "Searching by replacement text hid the matching text expansion.");

        SetSearchText(editorProcess, "no-such-text-expansion");
        Assert.IsTrue(
            editor.WaitFor(
                () => FindExact<Element>(editorProcess, SourceText, timeoutMS: 300) is null &&
                    FindExact<Element>(editorProcess, "No remappings match your filters", timeoutMS: 300) is not null,
                timeoutMS: 5_000),
            "A non-matching search did not show the no-results state.");

        SetSearchText(editorProcess, string.Empty);
        Assert.IsTrue(
            editor.WaitFor(() => FindExact<Element>(editorProcess, SourceText, timeoutMS: 300) is not null, timeoutMS: 5_000),
            "Clearing the search did not restore the text expansion row.");

        Step("Entering and leaving multi-select mode for the text expansion list");
        var selectionModeButton = editorProcess.Find<Button>(By.AccessibilityId("SelectionModeButton"), timeoutMS: 5_000);
        selectionModeButton.Invoke(msPostAction: 300);
        Assert.IsTrue(
            editor.WaitFor(
                () => string.Equals(
                    "Cancel",
                    editorProcess.Find<Button>(By.AccessibilityId("SelectionModeButton"), timeoutMS: 500).GetProperty("Name"),
                    StringComparison.OrdinalIgnoreCase),
                timeoutMS: 5_000),
            "The editor did not enter multi-select mode.");
        Assert.IsFalse(
            editorProcess.Find<Element>(By.AccessibilityId("TextExpansionsListView"), timeoutMS: 5_000).IsOffscreen,
            "The text expansion list was not visible in multi-select mode.");
        Assert.IsFalse(
            editorProcess.Find<Button>(By.AccessibilityId("DeleteSelectedBtn"), timeoutMS: 5_000).IsEnabled,
            "Delete selected was enabled before any mapping was selected.");

        selectionModeButton = editorProcess.Find<Button>(By.AccessibilityId("SelectionModeButton"), timeoutMS: 5_000);
        selectionModeButton.Invoke(msPostAction: 300);
        Assert.IsTrue(
            editor.WaitFor(
                () => string.Equals(
                    "Select",
                    editorProcess.Find<Button>(By.AccessibilityId("SelectionModeButton"), timeoutMS: 500).GetProperty("Name"),
                    StringComparison.OrdinalIgnoreCase),
                timeoutMS: 5_000),
            "The editor did not leave multi-select mode.");

        Step("Filtering the text expansion by the synthetic Win (Both) virtual-key code");
        editorProcess.Find<Button>(By.AccessibilityId("FilterButton"), timeoutMS: 5_000).Invoke(msPostAction: 300);
        var winFilter = editorProcess.Find<Element>(By.AccessibilityId("WinFilterToggle"), timeoutMS: 5_000);
        winFilter.Invoke(msPostAction: 300);
        Assert.IsTrue(
            string.Equals("On", winFilter.GetProperty("ToggleState"), StringComparison.OrdinalIgnoreCase),
            "The Win filter did not turn on.");
        Assert.IsTrue(
            editor.WaitFor(() => FindExact<Element>(editorProcess, SourceText, timeoutMS: 300) is not null, timeoutMS: 5_000),
            "The Win filter did not recognize the text expansion's Win (Both) activation key.");

        winFilter.Invoke(msPostAction: 200);
        var altFilter = editorProcess.Find<Element>(By.AccessibilityId("AltFilterToggle"), timeoutMS: 5_000);
        altFilter.Invoke(msPostAction: 300);
        Assert.IsTrue(
            editor.WaitFor(
                () => FindExact<Element>(editorProcess, SourceText, timeoutMS: 300) is null &&
                    FindExact<Element>(editorProcess, "No remappings match your filters", timeoutMS: 300) is not null,
                timeoutMS: 5_000),
            "The Alt filter did not exclude the Win-only text expansion.");

        editorProcess.Find<Button>(By.AccessibilityId("ClearFiltersBtn"), timeoutMS: 5_000).Invoke(msPostAction: 300);
        Assert.IsTrue(
            editor.WaitFor(() => FindExact<Element>(editorProcess, SourceText, timeoutMS: 300) is not null, timeoutMS: 5_000),
            "Clearing modifier filters did not restore the text expansion row.");
    }

    private static void SetSearchText(Session editor, string value)
    {
        editor.Find<TextBox>(By.Name("Search remappings"), timeoutMS: 5_000).SetText(value);
    }
}
