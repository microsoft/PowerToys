// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.TextExpander.UITests;

/// <summary>
/// The Text Expander page in PowerToys Settings: the enable switch, the options it saves for the engine, and Manage
/// snippets, which opens the editor.
/// </summary>
[TestClass]
public sealed class TextExpanderSettingsTests : TextExpanderTestBase
{
    private const string EnableSwitch = "Toggle_TextExpander";
    private const string WordBoundarySwitch = "Toggle_TextExpander_RequireWordBoundary";
    private const string CaseSensitiveSwitch = "Toggle_TextExpander_CaseSensitive";
    private const string RemapsSwitch = "Toggle_TextExpander_TreatRemapsAsTyping";
    private const string InsertionMethod = "Insertion method";
    private const string MinimumLengthToPaste = "Minimum length to paste";

    /// <summary>
    /// The enable switch is the runner's: turning it off stops the engine, so a trigger stays as typed and the options
    /// are unavailable; turning it on starts the engine again, and triggers expand.
    /// </summary>
    [TestMethod("TextExpander.Settings.EnableSwitchStartsAndStopsTheEngine")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Settings")]
    public void EnableSwitchStartsAndStopsTheEngine()
    {
        var settings = OpenTextExpanderPage();
        Assert.IsTrue(IsSwitchOn(settings, EnableSwitch), "Text Expander did not start enabled.");
        Assert.IsTrue(WaitForEngine(running: true), "PowerToys.TextExpander isn't running while Text Expander is enabled.");
        AssertExpands(Greeting.Trigger + " ", Greeting.Replacement + " ", waitForEngine: true);

        Step("Turning Text Expander off");
        SetSwitch(settings, EnableSwitch, on: false);
        Assert.IsTrue(WaitForEngine(running: false, timeoutMS: 20_000), "PowerToys.TextExpander kept running after Text Expander was turned off.");
        Assert.IsFalse(
            settings.Find<ToggleSwitch>(By.AccessibilityId(WordBoundarySwitch), 5_000).IsEnabled,
            "The Advanced options stayed available with Text Expander off.");
        AssertNotExpanded(Greeting.Trigger + " ");

        Step("Turning Text Expander back on");
        SetSwitch(settings, EnableSwitch, on: true);
        Assert.IsTrue(WaitForEngine(running: true, timeoutMS: 30_000), "PowerToys.TextExpander did not start after Text Expander was turned on.");
        Assert.IsTrue(
            settings.Find<ToggleSwitch>(By.AccessibilityId(WordBoundarySwitch), 5_000).IsEnabled,
            "The Advanced options stayed unavailable after Text Expander was turned on.");
        AssertExpands(Greeting.Trigger + " ", Greeting.Replacement + " ", waitForEngine: true);
    }

    /// <summary>Each Advanced option is saved to the module's settings.json, where the engine reads it.</summary>
    [TestMethod("TextExpander.Settings.OptionsAreSavedForTheEngine")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Settings")]
    public void OptionsAreSavedForTheEngine()
    {
        var settings = OpenTextExpanderPage();
        Assert.IsTrue(IsSwitchOn(settings, WordBoundarySwitch), "Wait for space or punctuation did not show the saved value, on.");
        Assert.IsTrue(IsSwitchOn(settings, CaseSensitiveSwitch), "Case-sensitive snippets did not show the saved value, on.");
        Assert.IsTrue(IsSwitchOn(settings, RemapsSwitch), "Use PowerToys Keyboard Manager remaps did not show the saved value, on.");
        Assert.AreEqual("Automatic", InsertionMethodBox(settings).SelectedText, "Insertion method did not show the saved value, Automatic.");
        AssertMinimumLengthEnabled(settings, enabled: true);

        Step("Turning off Wait for space or punctuation");
        SetSwitch(settings, WordBoundarySwitch, on: false);
        AssertOptionSaved("require_word_boundary", false);

        Step("Turning off Case-sensitive snippets");
        SetSwitch(settings, CaseSensitiveSwitch, on: false);
        AssertOptionSaved("case_sensitive", false);

        Step("Turning off Use PowerToys Keyboard Manager remaps");
        SetSwitch(settings, RemapsSwitch, on: false);
        AssertOptionSaved("treat_remaps_as_typing", false);

        // Only Automatic chooses by length, so the other methods turn off Minimum length to paste.
        SelectInsertionMethod(settings, "Always type");
        AssertOptionSaved("injection_backend", "type");
        AssertMinimumLengthEnabled(settings, enabled: false);

        SelectInsertionMethod(settings, "Always paste");
        AssertOptionSaved("injection_backend", "clipboard");
        AssertMinimumLengthEnabled(settings, enabled: false);

        SelectInsertionMethod(settings, "Automatic");
        AssertOptionSaved("injection_backend", "auto");
        AssertMinimumLengthEnabled(settings, enabled: true);
    }

    /// <summary>
    /// Manage snippets opens the editor even while Text Expander is off, and selecting it again brings back the same
    /// editor, restoring it when it's minimized, rather than opening a second one.
    /// </summary>
    [TestMethod("TextExpander.Settings.ManageSnippetsOpensOneEditor")]
    [TestCategory("TextExpander")]
    [TestCategory("TextExpander.Settings")]
    public void ManageSnippetsOpensOneEditor()
    {
        var settings = OpenTextExpanderPage();

        Step("Turning Text Expander off: managing snippets doesn't need the engine");
        SetSwitch(settings, EnableSwitch, on: false);
        Assert.IsTrue(WaitForEngine(running: false, timeoutMS: 20_000), "PowerToys.TextExpander kept running after Text Expander was turned off.");

        var editor = OpenEditor(settings);
        editor.WaitForCount(DefaultSnippets.Count);
        foreach (var snippet in DefaultSnippets)
        {
            Assert.IsTrue(editor.WaitForRow(snippet.Trigger, present: true), $"The editor did not list '{snippet.Trigger}'. Rows: {editor.DescribeRows()}.");
        }

        Step("Minimizing the editor, then selecting Manage snippets again");
        WindowHelper.MinimizeWindow(editor.Handle);
        Assert.IsTrue(settings.WaitFor(() => IsIconic(editor.Handle), 5_000), "The editor did not minimize.");
        settings.Find<Button>(By.AccessibilityId("Button_TextExpander_ManageSnippets"), 10_000).Invoke(msPostAction: 0);

        Assert.IsTrue(settings.WaitFor(() => !IsIconic(editor.Handle), 20_000, pollIntervalMS: 250), "Manage snippets did not restore the minimized editor.");
        var single = WaitHelper.WaitForStable(
            SnippetEditor.ProcessIds,
            ids => ids is [var id] && id == editor.ProcessId,
            timeoutMS: 20_000,
            requiredConsecutiveMatches: 8,
            pollIntervalMS: 250);
        Assert.IsTrue(
            single.Succeeded,
            $"Selecting Manage snippets again should keep the one editor (process {editor.ProcessId}); editor processes: {string.Join(", ", single.LastObservation ?? [])}.");
    }

    private static ComboBox InsertionMethodBox(Session settings) =>
        settings.WaitForExact<ComboBox>(InsertionMethod, 10_000);

    private void SelectInsertionMethod(Session settings, string method)
    {
        Step($"Selecting the insertion method '{method}'");
        InsertionMethodBox(settings).Click(msPostAction: 500);
        settings.WaitForExact<Element>(method, 10_000, controlType: "ListItem").Click(msPostAction: 500);
        Assert.IsTrue(
            settings.WaitFor(() => InsertionMethodBox(settings).SelectedText == method, 10_000, pollIntervalMS: 250),
            $"Insertion method did not change to '{method}'.");
    }

    private void AssertMinimumLengthEnabled(Session settings, bool enabled)
    {
        // The card's header text names both the card and its number box; text blocks don't report being disabled.
        var controls = settings.FindExact<Element>(MinimumLengthToPaste)
            .Where(element => !element.ControlType.Equals("Text", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.IsTrue(controls.Count > 0, $"'{MinimumLengthToPaste}' was not found.");
        Assert.IsTrue(
            settings.WaitFor(() => settings.FindExact<Element>(MinimumLengthToPaste).Where(element => !element.ControlType.Equals("Text", StringComparison.OrdinalIgnoreCase)).All(element => element.IsEnabled == enabled), 5_000, pollIntervalMS: 250),
            $"'{MinimumLengthToPaste}' should be {(enabled ? "available" : "unavailable")}. Controls: {string.Join(", ", controls.Select(control => $"{control.ControlType} enabled={control.IsEnabled}"))}.");
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);
}
