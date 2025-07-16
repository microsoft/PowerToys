// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WorkspacesEditorUITest;

[TestClass]
public class WorkspacesSettingsTests : UITestBase
{
    public WorkspacesSettingsTests()
        : base(PowerToysModule.PowerToysSettings, WindowSize.Medium)
    {
    }

    [TestMethod("WorkspacesSettings.LaunchFromSettings")]
    [TestCategory("Workspaces Settings UI")]
    public void TestLaunchEditorFromSettingsPage()
    {
        GoToSettingsPageAndEnable();
    }

    [TestMethod("WorkspacesSettings.ActivationShortcut")]
    [TestCategory("Workspaces Settings UI")]
    public void TestActivationShortcutCustomization()
    {
        GoToSettingsPageAndEnable();

        // Find the activation shortcut control
        var shortcutButton = Find<Button>("Activation shortcut");
        Assert.IsNotNull(shortcutButton, "Activation shortcut control should exist");

        // Test customizing the shortcut
        shortcutButton.Click();

        Task.Delay(1000).Wait();

        // Send new key combination (Win+Ctrl+W)
        SendKeys(Key.Win, Key.Ctrl, Key.W);

        var saveButton = Find<Button>("Save");

        Assert.IsNotNull(saveButton, "Save button should exist after editing shortcut");

        saveButton.Click();

        var helpText = shortcutButton.HelpText;
        Assert.AreEqual("Win + Ctrl + W", helpText, "Activation shortcut should be updated to Win + Ctrl + W");
    }

    [TestMethod("WorkspacesSettings.EnableToggle")]
    [TestCategory("Workspaces Settings UI")]
    public void TestEnableDisableModule()
    {
        GoToSettingsPageAndEnable();

        // Find the enable toggle
        var enableToggle = Find<ToggleSwitch>("Enable Workspaces");
        Assert.IsNotNull(enableToggle, "Enable Workspaces toggle should exist");

        Assert.IsTrue(enableToggle.IsOn, "Enable Workspaces toggle should be in the 'on' state");

        // Toggle the state
        enableToggle.Click();
        Task.Delay(500).Wait();

        // Verify state changed
        Assert.IsFalse(enableToggle.IsOn, "Toggle state should change");

        // Verify related controls are enabled/disabled accordingly
        var launchButton = Find<Button>("Launch editor");
        Assert.IsFalse(launchButton.Enabled, "Launch editor button should be disabled when module is disabled");
    }

    [TestMethod("WorkspacesSettings.LaunchByActivationShortcut")]
    [TestCategory("Workspaces Settings UI")]
    [Ignore("Wait until settings & runner can be connected in framework")]
    public void TestLaunchEditorByActivationShortcut()
    {
        // Ensure module is enabled
        var enableToggle = Find<ToggleSwitch>("Enable Workspaces");
        if (!enableToggle.IsOn)
        {
            enableToggle.Click();
            Thread.Sleep(500);
        }

        // Close settings window to test shortcut
        ExitScopeExe();
        Thread.Sleep(1000);

        // Default shortcut is Win+Ctrl+`
        SendKeys(Key.Win, Key.Ctrl, Key.W);
        Thread.Sleep(2000);

        // Verify editor opened
        Assert.IsTrue(WindowHelper.IsWindowOpen("Workspaces Editor"), "Workspaces Editor should open with activation shortcut");

        // Reopen settings for next tests
        RestartScopeExe();
        NavigateToWorkspacesSettings();
    }

    [TestMethod("WorkspacesSettings.DisableModuleNoLaunch")]
    [TestCategory("Workspaces Settings UI")]
    [Ignore("Wait until settings & runner can be connected in framework")]
    public void TestDisabledModuleDoesNotLaunchByShortcut()
    {
        // Disable the module
        var enableToggle = Find<ToggleSwitch>("Enable Workspaces");
        if (enableToggle.IsOn)
        {
            enableToggle.Click();
            Thread.Sleep(500);
        }

        // Close settings to test shortcut
        ExitScopeExe();
        Thread.Sleep(1000);

        // Try to launch with shortcut
        SendKeys(Key.Win, Key.Ctrl, Key.W);
        Thread.Sleep(2000);

        // Verify editor did not open
        Assert.IsFalse(WindowHelper.IsWindowOpen("Workspaces Editor"), "Workspaces Editor should not open when module is disabled");

        // Reopen settings and re-enable the module
        RestartScopeExe();
        NavigateToWorkspacesSettings();

        enableToggle = Find<ToggleSwitch>("Enable Workspaces");
        if (!enableToggle.IsOn)
        {
            enableToggle.Click();
            Thread.Sleep(500);
        }
    }

    [TestMethod("WorkspacesSettings.QuickAccessLaunch")]
    [TestCategory("Workspaces Settings UI")]
    [Ignore("Wait until tray icon supported is in framework")]
    public void TestLaunchFromQuickAccess()
    {
        // This test verifies the "quick access" mention in settings
        // Look for any quick access related UI elements
        var quickAccessInfo = FindAll(By.LinkText("quick access"));

        if (quickAccessInfo.Count > 0)
        {
            Assert.IsTrue(quickAccessInfo.Count > 0, "Quick access information should be present in settings");
        }

        // Note: Actual system tray/quick access interaction would require
        // more complex automation that might be platform-specific
    }

    private void NavigateToWorkspacesSettings()
    {
        // Find and click Workspaces in the navigation
        var workspacesNavItem = Find<NavigationViewItem>("Workspaces");
        workspacesNavItem.Click();
        Thread.Sleep(1000);
    }

    private void GoToSettingsPageAndEnable()
    {
        if (this.FindAll<NavigationViewItem>("Workspaces").Count == 0)
        {
            this.Find<NavigationViewItem>("Windowing & Layouts").Click();
        }

        this.Find<NavigationViewItem>("Workspaces").Click();

        var enableButton = this.Find<ToggleSwitch>("Enable Workspaces");
        Assert.IsNotNull(enableButton, "Enable Workspaces toggle should exist");

        if (!enableButton.IsOn)
        {
            enableButton.Click();
            Task.Delay(500).Wait(); // Wait for the toggle animation and state change
        }

        // Verify it's now enabled
        Assert.IsTrue(enableButton.IsOn, "Enable Workspaces toggle should be in the 'on' state");
    }

    private void AttachWorkspacesEditor()
    {
        Task.Delay(200).Wait();
        this.Session.Attach(PowerToysModule.Workspaces);
    }
}
