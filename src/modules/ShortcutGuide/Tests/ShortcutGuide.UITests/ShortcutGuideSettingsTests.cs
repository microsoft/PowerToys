// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ShortcutGuide.UITests;

/// <summary>
/// Tests that verify the Shortcut Guide settings page: navigation, enable/disable toggle,
/// and reading the activation shortcut.
/// </summary>
[TestClass]
public class ShortcutGuideSettingsTests : UITestBase
{
    public ShortcutGuideSettingsTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: new[] { TestHelper.ModuleSettingsKey })
    {
    }

    /// <summary>
    /// Verify that the Shortcut Guide settings page can be navigated to from the dashboard,
    /// the enable toggle is present, and the activation ShortcutControl exposes a parseable chord.
    /// </summary>
    [TestMethod]
    [TestCategory("ShortcutGuide")]
    [TestCategory("Settings")]
    public void NavigateToPageAndReadShortcut()
    {
        try
        {
            RunNavigateToPageAndReadShortcut();
        }
        finally
        {
            WindowControl.TryCloseByApp("PowerToys.Settings");
        }
    }

    /// <summary>
    /// Toggle the module OFF and verify the process exits; toggle back ON and verify it restarts.
    /// </summary>
    [TestMethod]
    [TestCategory("ShortcutGuide")]
    [TestCategory("Settings")]
    public void ToggleEnablesAndDisablesProcess()
    {
        try
        {
            RunToggleEnablesAndDisablesProcess();
        }
        finally
        {
            TestHelper.CloseShortcutGuideOverlay();
            WindowControl.TryCloseByApp("PowerToys.Settings");
        }
    }

    private void RunNavigateToPageAndReadShortcut()
    {
        // Navigate to the Shortcut Guide settings page.
        TestHelper.NavigateToSettingsPage(this);

        // The enable toggle must be present on the page.
        var toggle = Session.Find<ToggleSwitch>(By.AccessibilityId("Toggle_ShortcutGuide"), 5000);
        Assert.IsNotNull(toggle, "Shortcut Guide enable toggle was not found on the settings page.");
        TestContext.WriteLine($"Enable toggle found; IsOn={toggle.IsOn}");

        // Enable the module so the ShortcutControl is interactive.
        if (!toggle.IsOn)
        {
            toggle.Toggle(true);
            Assert.IsTrue(
                toggle.WaitForProperty("ToggleState", "On", 5000),
                "Toggle did not switch to On.");
        }

        // The ShortcutControl's EditButton must expose a parseable chord via HelpText.
        var editButton = Session.Find<Button>(By.AccessibilityId("EditButton"), 5000);
        Assert.IsNotNull(editButton, "ShortcutControl EditButton was not found.");

        var keys = TestHelper.ReadActivationShortcut(this);
        Assert.IsTrue(keys.Length > 0, $"Activation shortcut is empty or could not be parsed.");
        TestContext.WriteLine($"Activation shortcut: [{string.Join(", ", keys)}]");
    }

    private void RunToggleEnablesAndDisablesProcess()
    {
        TestHelper.NavigateToSettingsPage(this);

        var toggle = Session.Find<ToggleSwitch>(By.AccessibilityId("Toggle_ShortcutGuide"), 5000);
        bool initialIsOn = toggle.IsOn;
        TestContext.WriteLine($"Initial toggle state: IsOn={initialIsOn}");

        try
        {
            // Ensure the module is ON first so we can test the OFF transition.
            if (!toggle.IsOn)
            {
                toggle.Toggle(true);
                Assert.IsTrue(
                    toggle.WaitForProperty("ToggleState", "On", 5000),
                    "Priming: toggle did not switch to On.");
                Assert.IsTrue(
                    WaitForProcess(TestHelper.ShortcutGuideProcess, expected: true, timeoutMS: 10_000),
                    "Priming: PowerToys.ShortcutGuide did not start after enabling.");
            }

            // Toggle OFF and verify the process exits.
            toggle.Toggle(false);
            Assert.IsTrue(
                toggle.WaitForProperty("ToggleState", "Off", 5000),
                "Toggle did not switch to Off.");
            Assert.IsTrue(
                WaitForProcess(TestHelper.ShortcutGuideProcess, expected: false, timeoutMS: 10_000),
                "PowerToys.ShortcutGuide did not exit after toggling module OFF.");
            TestContext.WriteLine("Toggled OFF; ShortcutGuide process exited.");

            // Toggle ON and verify the process restarts.
            toggle.Toggle(true);
            Assert.IsTrue(
                toggle.WaitForProperty("ToggleState", "On", 5000),
                "Toggle did not switch back to On.");
            Assert.IsTrue(
                WaitForProcess(TestHelper.ShortcutGuideProcess, expected: true, timeoutMS: 10_000),
                "PowerToys.ShortcutGuide did not start after re-enabling.");
            TestContext.WriteLine("Toggled ON; ShortcutGuide process running.");
        }
        finally
        {
            // Restore the toggle to its initial state.
            try
            {
                if (toggle.IsOn != initialIsOn)
                {
                    toggle.Toggle(initialIsOn);
                }
            }
            catch
            {
                // Best-effort restore — never throw from finally.
            }
        }
    }

    /// <summary>Poll for a process becoming present or absent.</summary>
    private static bool WaitForProcess(string name, bool expected, int timeoutMS)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
        while (DateTime.UtcNow < deadline)
        {
            var procs = Process.GetProcessesByName(name);
            bool found = procs.Length > 0;
            foreach (var p in procs)
            {
                p.Dispose();
            }

            if (found == expected)
            {
                return true;
            }

            Thread.Sleep(250);
        }

        return false;
    }
}
