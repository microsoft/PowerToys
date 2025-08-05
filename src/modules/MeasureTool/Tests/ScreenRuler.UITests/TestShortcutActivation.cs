// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScreenRuler.UITests
{
    [TestClass]
    public class TestShortcutActivation : UITestBase
    {
        public TestShortcutActivation()
            : base(PowerToysModule.PowerToysSettings, WindowSize.Large)
        {
        }

        [TestMethod("ScreenRuler.ShortcutActivation")]
        [TestCategory("Activation")]
        public void TestScreenRulerShortcutActivation()
        {
            TestHelper.LaunchFromSetting(this);

            // Ensure Screen Ruler is enabled for the test
            TestHelper.SetScreenRulerToggle(this, enable: true);
            Assert.IsTrue(
                Find<ToggleSwitch>(By.AccessibilityId("Toggle_ScreenRuler")).IsOn,
                "Screen Ruler toggle switch should be ON for activation test");

            // Read the current activation shortcut
            var activationKeys = TestHelper.ReadActivationShortcut(this);
            Assert.IsNotNull(activationKeys, "Should be able to read activation shortcut");
            Assert.IsTrue(activationKeys.Length > 0, "Activation shortcut should contain at least one key");

            // Test 1: Press the activation shortcut and verify the toolbar appears
            SendKeys(activationKeys);
            bool screenRulerAppeared = TestHelper.WaitForScreenRulerUI(this, 1000);
            Assert.IsTrue(
                screenRulerAppeared,
                $"ScreenRulerUI should appear after pressing activation shortcut: {string.Join(" + ", activationKeys)}");

            // Test 2: Press the activation shortcut again and verify the toolbar disappears
            SendKeys(activationKeys);
            bool screenRulerDisappeared = TestHelper.WaitForScreenRulerUIToDisappear(this, 1000);
            Assert.IsTrue(
                screenRulerDisappeared,
                $"ScreenRulerUI should disappear after pressing activation shortcut again: {string.Join(" + ", activationKeys)}");

            // Test 3: Disable Screen Ruler and verify that the activation shortcut no longer activates the utility
            TestHelper.SetScreenRulerToggle(this, enable: false);
            Assert.IsFalse(
                Find<ToggleSwitch>(By.AccessibilityId("Toggle_ScreenRuler")).IsOn,
                "Screen Ruler toggle switch should be OFF after disabling");

            // Try to activate with shortcut while disabled
            SendKeys(activationKeys);
            Task.Delay(1000).Wait(); // Wait to ensure it doesn't appear
            Assert.IsFalse(
                TestHelper.IsScreenRulerUIOpen(this),
                "ScreenRulerUI should not appear when Screen Ruler is disabled");

            // Test 4: Enable Screen Ruler and press the activation shortcut and verify the toolbar appears
            TestHelper.SetScreenRulerToggle(this, enable: true);
            Assert.IsTrue(
                Find<ToggleSwitch>(By.AccessibilityId("Toggle_ScreenRuler")).IsOn,
                "Screen Ruler toggle switch should be ON after re-enabling");

            SendKeys(activationKeys);
            screenRulerAppeared = TestHelper.WaitForScreenRulerUI(this, 1000);
            Assert.IsTrue(
                screenRulerAppeared,
                $"ScreenRulerUI should appear after re-enabling and pressing activation shortcut: {string.Join(" + ", activationKeys)}");

            // Test 5: Verify the utility can be closed via the cleanup method
            // Note: TestHelper.CloseScreenRulerUI already uses the proper way to close via close button
            TestHelper.CloseScreenRulerUI(this);
            bool screenRulerClosed = TestHelper.WaitForScreenRulerUIToDisappear(this, 1000);
            Assert.IsTrue(
                screenRulerClosed,
                "ScreenRulerUI should close after calling CloseScreenRulerUI");

            // Clean up - ensure ScreenRulerUI is closed
            TestHelper.CloseScreenRulerUI(this);
        }
    }
}
