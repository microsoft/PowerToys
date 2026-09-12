// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.ShortcutGuide.UITests;

/// <summary>
/// End-to-end activation test for the Shortcut Guide overlay:
///   1. Navigate to the Shortcut Guide settings page and enable the module.
///   2. Read the activation shortcut from the ShortcutControl.
///   3. Fire the hotkey and verify the overlay process appears.
///   4. Dismiss the overlay (Escape) and verify it disappears.
///   5. Disable the module and confirm the hotkey no longer activates the overlay.
/// </summary>
[TestClass]
public class ShortcutGuideActivationTests : UITestBase
{
    public ShortcutGuideActivationTests()
        : base(PowerToysModule.PowerToysSettings, WindowSize.Large, enableModules: new[] { TestHelper.ModuleSettingsKey })
    {
    }

    [TestMethod]
    [TestCategory("ShortcutGuide")]
    [TestCategory("Activation")]
    public void ActivationShortcutShowsAndDismissesOverlay()
    {
        var activationKeys = TestHelper.InitializeTest(this, "activation test");

        try
        {
            // Test 1: pressing the activation shortcut shows the overlay.
            Assert.IsTrue(
                TestHelper.SendShortcutUntilVisible(activationKeys),
                $"Shortcut Guide overlay should appear after pressing the activation shortcut: [{string.Join(", ", activationKeys)}]");
            TestContext.WriteLine("Overlay appeared after pressing the activation shortcut.");

            // Test 2: pressing Escape dismisses the overlay.
            KeyboardHelper.SendKeys([Key.Escape]);
            Assert.IsTrue(
                TestHelper.WaitForShortcutGuideOverlayToDisappear(5000),
                "Shortcut Guide overlay should disappear after pressing Escape.");
            TestContext.WriteLine("Overlay dismissed via Escape.");

            // Test 3: disabling the module means the shortcut no longer activates the overlay.
            TestHelper.SetAndVerifyShortcutGuideToggle(this, enable: false, "disabled-state test");

            // Give the runner time to de-register the hotkey before sending it.
            Thread.Sleep(1000);
            KeyboardHelper.SendKeys(activationKeys);
            Thread.Sleep(2000);
            Assert.IsFalse(
                TestHelper.IsShortcutGuideOverlayOpen(),
                "Shortcut Guide overlay must not appear when the module is disabled.");
            TestContext.WriteLine("Confirmed: overlay did not appear while module is disabled.");

            // Test 4: re-enable and confirm the shortcut works again.
            TestHelper.SetAndVerifyShortcutGuideToggle(this, enable: true, "re-enabled test");
            Assert.IsTrue(
                TestHelper.SendShortcutUntilVisible(activationKeys),
                $"Shortcut Guide overlay should appear after re-enabling and pressing the activation shortcut: [{string.Join(", ", activationKeys)}]");
            TestContext.WriteLine("Overlay appeared after re-enabling the module.");

            // Clean up: dismiss the overlay that is still open.
            KeyboardHelper.SendKeys([Key.Escape]);
            TestHelper.WaitForShortcutGuideOverlayToDisappear(3000);
        }
        finally
        {
            TestHelper.CleanupTest(this);
            WindowControl.TryCloseByApp("PowerToys.Settings");
        }
    }
}
