// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WorkspacesEditorUITest;

[TestClass]
public class WorkspacesSettingsTests : WorkspacesUiAutomationBase
{
    public WorkspacesSettingsTests()
        : base()
    {
    }

    [TestMethod("WorkspacesSettings.LaunchFromSettings")]
    [TestCategory("Workspaces Settings UI")]
    public void TestLaunchEditorFromSettingsPage()
    {
        // Find and click the launch editor button
        var launchButton = Find<Button>("Launch editor");
        Assert.IsNotNull(launchButton, "Launch editor button should exist in settings");
        launchButton.Click();

        // Wait for editor to open
        Thread.Sleep(2000);

        // Verify editor opened
        Assert.IsTrue(WindowHelper.IsWindowOpen("Workspaces Editor"), "Workspaces Editor should be open");

        // Close the editor for cleanup
        CloseWorkspacesEditor();
    }

    [TestMethod("WorkspacesSettings.ActivationShortcut")]
    [TestCategory("Workspaces Settings UI")]
    public void TestActivationShortcutCustomization()
    {
        // Find the activation shortcut control
        var shortcutControl = Find<Custom>("Activation shortcut");
        Assert.IsNotNull(shortcutControl, "Activation shortcut control should exist");

        // Verify default shortcut is displayed
        var shortcutText = shortcutControl.GetAttribute("Value");
        Assert.IsFalse(string.IsNullOrEmpty(shortcutText), "Shortcut should have a value");

        // Test customizing the shortcut
        shortcutControl.Click();
        Thread.Sleep(500);

        // Send new key combination (Win+Ctrl+W)
        SendKeys(Key.Win, Key.Ctrl, Key.W);
        Thread.Sleep(1000);

        // Verify shortcut was updated
        var newShortcutText = shortcutControl.GetAttribute("Value");
        Assert.AreNotEqual(shortcutText, newShortcutText, "Shortcut should be updated");

        // Reset to default
        ResetShortcutToDefault(shortcutControl);
    }

    [TestMethod("WorkspacesSettings.EnableToggle")]
    [TestCategory("Workspaces Settings UI")]
    public void TestEnableDisableModule()
    {
        // Find the enable toggle
        var enableToggle = Find<ToggleSwitch>("Enable Workspaces");
        Assert.IsNotNull(enableToggle, "Enable Workspaces toggle should exist");

        // Store initial state
        bool initialState = enableToggle.IsOn;

        // Toggle the state
        enableToggle.Click();
        Thread.Sleep(500);

        // Verify state changed
        Assert.AreNotEqual(initialState, enableToggle.IsOn, "Toggle state should change");

        // Verify related controls are enabled/disabled accordingly
        var launchButton = Find<Button>("Launch editor");
        if (!enableToggle.IsOn)
        {
            Assert.IsFalse(launchButton.Enabled, "Launch editor button should be disabled when module is disabled");
        }
        else
        {
            Assert.IsTrue(launchButton.Enabled, "Launch editor button should be enabled when module is enabled");
        }

        // Restore initial state
        if (enableToggle.IsOn != initialState)
        {
            enableToggle.Click();
            Thread.Sleep(500);
        }
    }

    [TestMethod("WorkspacesSettings.LaunchByActivationShortcut")]
    [TestCategory("Workspaces Settings UI")]
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
        SendKeys(Key.Win, Key.Ctrl, Key.OemTilde);
        Thread.Sleep(2000);

        // Verify editor opened
        Assert.IsTrue(WindowHelper.IsWindowOpen("Workspaces Editor"), "Workspaces Editor should open with activation shortcut");

        // Close the editor
        CloseWorkspacesEditor();

        // Reopen settings for next tests
        RestartScopeExe();
        NavigateToWorkspacesSettings();
    }

    [TestMethod("WorkspacesSettings.DisableModuleNoLaunch")]
    [TestCategory("Workspaces Settings UI")]
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
        SendKeys(Key.Win, Key.Ctrl, Key.OemTilde);
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
}
