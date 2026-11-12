// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UITests;

[TestClass]
public class SettingsSearchTests : CommandPaletteTestBase
{
    private const string CalculatorCommandId = "com.microsoft.cmdpal.calculator";
    private Session? settingsSession;

    private Session SettingsSession
    {
        get
        {
            if (settingsSession is null)
            {
                var window = WindowsFinder.WaitForWindowByApp(
                    "Microsoft.CmdPal.UI",
                    window => window.Title == "Command Palette Settings",
                    timeoutMS: 15_000);
                Assert.IsNotNull(window, "The Command Palette Settings window did not appear.");
                settingsSession = window;
            }

            return settingsSession;
        }
    }

    private TextBox SettingsSearchBox =>
        SettingsSession.Find<TextBox>("Search settings, commands, and extensions");

    [TestMethod]
    [TestCategory("CmdPal")]
    [DataRow(false)]
    [DataRow(true)]
    public void SettingsSearch_SubmissionOpensResults(bool showAllResults)
    {
        OpenSettingsWindow();
        SettingsSession.Find<NavigationViewItem>("General").Click();
        SearchSettings("preview", showAllResults);
        AssertSearchResults("preview");
        Assert.IsNotNull(SettingsSession.Find("Personalization › Interaction"));

        SearchSettings("preview", showAllResults);
        AssertSearchResults("preview");
        SettingsSession.Find<Button>("Back").Invoke();
        Assert.IsNotNull(SettingsSession.Find(By.AccessibilityId("CmdPal_GeneralPage_ActivationKey")));
        SearchSettings("preview", showAllResults: false);
        AssertSearchResults("preview");

        for (var visit = 0; visit < 2; visit++)
        {
            Step("Opening a setting from the search results and returning to the same results");
            SettingsSession.Find<Button>("Automatically expand app details").MouseClick(msPostAction: 0);
            Assert.IsNotNull(SettingsSession.Find<ToggleSwitch>(
                By.AccessibilityId("CmdPal_AppearancePage_ShowAppDetails")));
            SettingsSession.Find<NavigationViewItem>("Personalization").Click();
            SettingsSession.Find<Button>("Back").Invoke();
            AssertSearchResults("preview");
            Assert.IsNotNull(SettingsSession.Find("Personalization › Interaction"));
        }

        SearchSettings("zqxzqxzqx", showAllResults: false);
        AssertSearchResults("zqxzqxzqx", expectFocusMoved: false);
        Assert.IsNotNull(SettingsSession.Find(
            "No settings, commands, or extensions found. Try a different search term."));
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    [DataRow("General", "CmdPal_GeneralPage_ActivationKey")]
    [DataRow("Calculator", "CmdPal_ExtensionPage_Enable")]
    public void SettingsSearch_PageAndProviderSuggestionsMoveFocus(string query, string destinationId)
    {
        OpenSettingsWindow();
        SettingsSession.Find<NavigationViewItem>("Personalization").Click();
        OpenSettingsSuggestion(query);
        Assert.IsNotNull(SettingsSession.Find(By.AccessibilityId(destinationId)));
        AssertSearchLostFocus();
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void SettingsSearch_CommandNameAndChangedAliasOpenCommandSettings()
    {
        OpenSettingsWindow();
        OpenSettingsSuggestion("calculator commands", "Calculator");
        var aliasBox = SettingsSession.Find<TextBox>(
            By.AccessibilityId("CmdPal_ExtensionPage_AliasText"));
        AssertSearchLostFocus();
        var originalAlias = aliasBox.Value;
        using var paletteProcess = Process.GetProcessById(CommandPaletteSession.ProcessId);
        try
        {
            Step("Changing the Calculator alias and finding its command settings by that alias");
            aliasBox.SetText("cmdpal-settings-search-test");
            Assert.IsTrue(aliasBox.WaitForValue("cmdpal-settings-search-test"));
            OpenSettingsSuggestion("cmdpal-settings-search-test", "Calculator");
            var changedAlias = SettingsSession.Find<TextBox>(By.AccessibilityId("CmdPal_ExtensionPage_AliasText"));
            Assert.IsTrue(
                changedAlias.WaitForValue("cmdpal-settings-search-test", timeoutMS: 10_000),
                "Searching for the changed alias did not open its populated command settings.");
            var activationMode = SettingsSession.Find<ComboBox>(
                By.AccessibilityId("CmdPal_ExtensionPage_AliasActivationType")).SelectedText;
            Assert.IsTrue(activationMode is "Direct" or "Indirect", $"Unknown alias activation mode '{activationMode}'.");

            CloseSettingsWindow();
            EnsureCommandPaletteForeground();
            Step("Activating Calculator with the changed alias in the same running process");
            var mainSearch = CommandPaletteSession.Find<TextBox>(By.AccessibilityId("MainSearchBox"));
            mainSearch.SetText("cmdpal-settings-search-test");
            if (activationMode == "Direct")
            {
                Assert.IsNotNull(CommandPaletteSession.Find<Button>("Back", timeoutMS: 15_000));
            }
            else
            {
                Assert.IsTrue(mainSearch.WaitForValue("cmdpal-settings-search-test"));
                InvokeResultWithEnter("Calculator");
            }

            SetSearchBox("17+23");
            Assert.IsNotNull(FindExactResult("40", timeoutMS: 20_000));
            Assert.IsFalse(paletteProcess.HasExited, "Changing an alias must not restart Command Palette.");
        }
        finally
        {
            EnsureCalculatorCommandSettings();
            aliasBox = SettingsSession.Find<TextBox>(By.AccessibilityId("CmdPal_ExtensionPage_AliasText"));
            aliasBox.SetText(originalAlias);
            Assert.IsTrue(aliasBox.WaitForValue(originalAlias), "The Calculator alias was not restored.");
        }
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void ExtensionEnablementUpdatesRunningPalette()
    {
        OpenSettingsWindow();
        OpenSettingsSuggestion("Calculator");
        var toggle = SettingsSession.Find<ToggleSwitch>(By.AccessibilityId("CmdPal_ExtensionPage_Enable"));
        var initialState = toggle.IsOn;
        using var paletteProcess = Process.GetProcessById(CommandPaletteSession.ProcessId);
        try
        {
            SetExtensionEnabled(toggle, true);
            CloseSettingsWindow();
            SetSearchBox("calculator");
            Assert.IsTrue(
                CommandPaletteSession.WaitFor(IsCalculatorOffered, timeoutMS: 15_000),
                "Enabling Calculator did not add its command to the running palette.");

            OpenSettingsWindow();
            OpenSettingsSuggestion("Calculator");
            toggle = SettingsSession.Find<ToggleSwitch>(By.AccessibilityId("CmdPal_ExtensionPage_Enable"));
            SetExtensionEnabled(toggle, false);
            CloseSettingsWindow();
            SetSearchBox(string.Empty);
            SetSearchBox("calculator");
            var disabled = WaitHelper.WaitForStable(
                observe: IsCalculatorOffered,
                isMatch: present => !present,
                timeoutMS: 15_000,
                requiredConsecutiveMatches: 3);
            if (!disabled.Succeeded)
            {
                TestContext.WriteLine(Session.Inspect(depth: 8).GetRawText());
            }

            Assert.IsTrue(disabled.Succeeded, "Disabled Calculator is still offered by the running palette.");
            Assert.IsFalse(paletteProcess.HasExited, "Changing extension enablement must not restart Command Palette.");
        }
        finally
        {
            if (settingsSession is null)
            {
                EnsureCommandPaletteForeground();
                OpenSettingsWindow();
            }

            OpenSettingsSuggestion("Calculator");
            toggle = SettingsSession.Find<ToggleSwitch>(By.AccessibilityId("CmdPal_ExtensionPage_Enable"));
            SetExtensionEnabled(toggle, initialState);
        }
    }

    [TestMethod]
    [TestCategory("CmdPal")]
    public void ExtensionGlobalShortcutOpensCalculatorWithoutRestart()
    {
        OpenSettingsWindow();
        OpenSettingsSuggestion("calculator commands", "Calculator");
        var shortcut = SettingsSession.Find<Button>(By.AccessibilityId("EditButton"));
        var originalShortcut = shortcut.HelpText;
        var originalKeys = ParseShortcut(originalShortcut);
        var testKey = originalKeys.Contains(Key.F12) ? Key.F11 : Key.F12;
        using var paletteProcess = Process.GetProcessById(CommandPaletteSession.ProcessId);
        try
        {
            Step($"Assigning Win+Ctrl+{testKey} to the Calculator command");
            SetCommandShortcut([Key.LWin, Key.Ctrl, testKey]);
            Assert.IsTrue(
                SettingsSession.WaitFor(
                    () => SettingsSession.Find<Button>(By.AccessibilityId("EditButton"))
                        .HelpText.Contains(testKey.ToString(), StringComparison.Ordinal),
                    timeoutMS: 5_000),
                "The command shortcut did not update.");
            CloseSettingsWindow();
            Step("Opening Calculator with its changed global shortcut");
            KeyboardHelper.SendKeys(Key.LWin, Key.Ctrl, testKey);
            var shown = WaitHelper.WaitForStable(
                observe: () => WindowsFinder.ListByApp("Microsoft.CmdPal.UI").Any(
                    window => window.Title == "Command Palette" &&
                              !WindowHelper.IsWindowCloaked(new IntPtr(window.Hwnd))),
                isMatch: visible => visible,
                timeoutMS: 20_000,
                requiredConsecutiveMatches: 2);
            Assert.IsTrue(shown.Succeeded, "The command's global shortcut did not show Command Palette.");
            Assert.IsNotNull(CommandPaletteSession.Find<Button>("Back", timeoutMS: 15_000));
            SetSearchBox("17+23");
            Assert.IsNotNull(FindExactResult("40", timeoutMS: 20_000));
            Assert.IsFalse(paletteProcess.HasExited, "Changing a command shortcut must not restart Command Palette.");
        }
        finally
        {
            EnsureCalculatorCommandSettings();
            SetCommandShortcut(originalKeys);
            var restoredShortcut = SettingsSession.Find<Button>(By.AccessibilityId("EditButton")).HelpText;
            CollectionAssert.AreEqual(originalKeys, ParseShortcut(restoredShortcut), "The command shortcut was not restored.");
        }
    }

    private void EnsureCalculatorCommandSettings()
    {
        if (settingsSession is null)
        {
            EnsureCommandPaletteForeground();
            OpenSettingsWindow();
        }

        OpenSettingsSuggestion("calculator commands", "Calculator");
    }

    private bool IsCalculatorOffered() =>
        Session.FindAll<Element>(By.AccessibilityId(CalculatorCommandId), timeoutMS: 0)
            .Any(item => string.Equals(item.AutomationId, CalculatorCommandId, StringComparison.Ordinal) && item.Displayed);

    private void CloseSettingsWindow()
    {
        Step("Closing Command Palette Settings");
        Assert.IsTrue(
            WindowControl.TryCloseWindow(SettingsSession.WindowHandle),
            "The Command Palette Settings window did not close.");
        settingsSession = null;
    }

    private void SetExtensionEnabled(ToggleSwitch toggle, bool enabled)
    {
        Step($"Setting Calculator extension enabled={enabled}");
        if (toggle.IsOn != enabled)
        {
            toggle.Invoke(msPostAction: 0);
        }

        Assert.IsTrue(toggle.WaitForProperty("ToggleState", enabled ? "On" : "Off"));
    }

    private void SetCommandShortcut(Key[] keys)
    {
        SettingsSession.Find<Button>(By.AccessibilityId("EditButton")).Invoke(msPostAction: 0);
        var action = SettingsSession.Find<Button>(keys.Length == 0 ? "Reset" : "Save");
        if (keys.Length > 0)
        {
            KeyboardHelper.SendChord(keys);
        }

        action.Invoke(msPostAction: 0);
        Assert.IsTrue(action.WaitForGone(10_000), "The shortcut dialog did not close.");
    }

    private static Key[] ParseShortcut(string shortcut)
    {
        if (shortcut == "Configure shortcut")
        {
            return [];
        }

        return shortcut.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(part =>
            {
                if (part is "Win" or "Windows")
                {
                    return Key.LWin;
                }

                var keyName = part.Length == 1 && char.IsAsciiDigit(part[0]) ? $"Num{part}" : part;
                Assert.IsTrue(
                    Enum.TryParse<Key>(keyName, ignoreCase: true, out var key) && Enum.IsDefined(key),
                    $"Unsupported shortcut key '{part}'.");
                return key;
            }).ToArray();
    }

    private void SearchSettings(string query, bool showAllResults)
    {
        Step($"Searching settings for '{query}' using {(showAllResults ? "Show all" : "Enter")}");
        SetSettingsSearch(query);
        if (showAllResults)
        {
            var showAll = CommandPaletteSession.Find(By.AccessibilityId("SettingsSearchShowAllResults"));
            StringAssert.StartsWith(showAll.Name, "Show ");
            showAll.Invoke(msPostAction: 0);
        }
        else
        {
            KeyboardHelper.SendChord(Key.Enter);
        }
    }

    private void OpenSettingsSuggestion(string query, string? suggestionTitle = null)
    {
        Step($"Opening the first settings suggestion for '{query}'");
        var search = SetSettingsSearch(query);
        Assert.IsNotNull(CommandPaletteSession.Find(By.AccessibilityId("SettingsSearchShowAllResults")));
        KeyboardHelper.SendChord(Key.Down);
        var title = suggestionTitle ?? query;
        var selected = CommandPaletteSession.WaitFor(
            () => CommandPaletteSession.FindAll<NavigationViewItem>(By.Name(title), timeoutMS: 0)
                .Any(item => item.X + (item.Width / 2) >= search.X && item.Y > search.Y &&
                             item.Y < search.Y + 400 && item.Selected),
            timeoutMS: 10_000);
        Assert.IsTrue(selected, $"The first '{title}' settings suggestion was not selected by Down.");
        KeyboardHelper.SendChord(Key.Enter);
    }

    private TextBox SetSettingsSearch(string query)
    {
        var search = SettingsSearchBox;
        search.MouseClick(msPostAction: 0);
        var originalClipboard = ClipboardHelper.GetText();
        try
        {
            KeyboardHelper.SendChord(Key.Ctrl, Key.A);
            KeyboardHelper.SendChord(Key.Backspace);
            Assert.IsTrue(search.WaitForValue(string.Empty));
            ClipboardHelper.SetText(query);
            KeyboardHelper.SendChord(Key.Ctrl, Key.V);
            Assert.IsTrue(search.WaitForValue(query));
        }
        finally
        {
            ClipboardHelper.SetText(originalClipboard);
        }

        Assert.IsTrue(
            CommandPaletteSession.WaitFor(
                () => CommandPaletteSession.Has(By.AccessibilityId("SettingsSearchShowAllResults"), timeoutMS: 0) ||
                      CommandPaletteSession.Has(By.Name("No results found"), timeoutMS: 0),
                timeoutMS: 10_000),
            $"Settings search suggestions were not ready for '{query}'.");
        Assert.IsTrue(
            WindowControl.WaitForForeground(new IntPtr(SettingsSession.WindowHandle), timeoutMS: 5_000),
            "The Settings window did not own foreground before keyboard input.");
        return search;
    }

    private void AssertSearchResults(string query, bool expectFocusMoved = true)
    {
        Assert.IsNotNull(SettingsSession.Find($"Results for '{query}'"));
        if (expectFocusMoved)
        {
            AssertSearchLostFocus();
        }
    }

    private void AssertSearchLostFocus() =>
        Assert.IsTrue(
            SettingsSearchBox.WaitForProperty("HasKeyboardFocus", "false"),
            "Opening settings search results must move focus out of the search box.");
}
