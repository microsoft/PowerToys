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
    public class ActivationTest : UITestBase
    {
        public ActivationTest()
            : base(PowerToysModule.PowerToysSettings, WindowSize.Large)
        {
        }

        [TestMethod("ScreenRuler.ActivationShortcut")]
        [TestCategory("Activation")]
        public void TestActivationShortcut()
        {
            ScreenRulerTestHelper.LaunchFromSetting(this);

            // Ensure Screen Ruler is enabled
            ScreenRulerTestHelper.SetScreenRulerToggle(this, enable: true);
            Assert.IsTrue(
                Find<ToggleSwitch>(By.AccessibilityId("Toggle_ScreenRuler")).IsOn,
                "Screen Ruler toggle switch should be ON for activation test");

            // Read the current activation shortcut
            var activationKeys = ScreenRulerTestHelper.ReadActivationShortcut(this);
            Assert.IsNotNull(activationKeys, "Should be able to read activation shortcut");
            Assert.IsTrue(activationKeys.Length > 0, "Activation shortcut should contain at least one key");

            // Ensure MeasureToolUI is not already open
            // ScreenRulerTestHelper.CloseMeasureToolUI(this);

            // Wait a moment to ensure UI has closed
            // Task.Delay(1000).Wait();
            //  Assert.IsFalse(
            //    ScreenRulerTestHelper.IsMeasureToolUIOpen(this),
            //    "MeasureToolUI should be closed before activation test");

            // Execute the activation shortcut
            SendKeys(activationKeys);

            // Wait for MeasureToolUI to appear
            bool measureToolAppeared = ScreenRulerTestHelper.WaitForMeasureToolUI(this, 5000);
            Assert.IsTrue(
                measureToolAppeared,
                $"MeasureToolUI should appear after pressing activation shortcut: {string.Join(" + ", activationKeys)}");

            // Verify MeasureToolUI is actually open
            Assert.IsTrue(
                ScreenRulerTestHelper.IsMeasureToolUIOpen(this),
                "MeasureToolUI should be detected as open after activation");

            // Clean up - close the MeasureToolUI
            ScreenRulerTestHelper.CloseMeasureToolUI(this);

            // Verify it closed
            bool measureToolClosed = ScreenRulerTestHelper.WaitForMeasureToolUIToDisappear(this, 3000);
            Assert.IsTrue(measureToolClosed, "MeasureToolUI should close after cleanup");
        }

        [TestMethod("ScreenRuler.ActivationWhenDisabled")]
        [TestCategory("Activation")]
        public void TestActivationWhenDisabled()
        {
            ScreenRulerTestHelper.LaunchFromSetting(this);

            // Read the activation shortcut before disabling
            var activationKeys = ScreenRulerTestHelper.ReadActivationShortcut(this);
            Assert.IsNotNull(activationKeys, "Should be able to read activation shortcut");

            // Disable Screen Ruler
            ScreenRulerTestHelper.SetScreenRulerToggle(this, enable: false);
            Assert.IsFalse(
                Find<ToggleSwitch>(By.AccessibilityId("Toggle_ScreenRuler")).IsOn,
                "Screen Ruler toggle switch should be OFF for disabled test");

            // Ensure MeasureToolUI is not already open
            ScreenRulerTestHelper.CloseMeasureToolUI(this);
            Task.Delay(1000).Wait();

            // Execute the activation shortcut
            SendKeys(activationKeys);

            // Wait and verify MeasureToolUI does NOT appear
            Task.Delay(3000).Wait(); // Give it time to potentially appear

            Assert.IsFalse(
                ScreenRulerTestHelper.IsMeasureToolUIOpen(this),
                $"MeasureToolUI should NOT appear when disabled, even after pressing activation shortcut: {string.Join(" + ", activationKeys)}");
        }

        [TestMethod("ScreenRuler.MultipleActivations")]
        [TestCategory("Activation")]
        public void TestMultipleActivations()
        {
            ScreenRulerTestHelper.LaunchFromSetting(this);

            // Ensure Screen Ruler is enabled
            ScreenRulerTestHelper.SetScreenRulerToggle(this, enable: true);

            var activationKeys = ScreenRulerTestHelper.ReadActivationShortcut(this);

            // Ensure starting clean
            ScreenRulerTestHelper.CloseMeasureToolUI(this);
            Task.Delay(1000).Wait();

            // First activation
            SendKeys(activationKeys);
            bool firstActivation = ScreenRulerTestHelper.WaitForMeasureToolUI(this, 5000);
            Assert.IsTrue(firstActivation, "First activation should show MeasureToolUI");

            // Close it
            ScreenRulerTestHelper.CloseMeasureToolUI(this);
            bool firstClosed = ScreenRulerTestHelper.WaitForMeasureToolUIToDisappear(this, 3000);
            Assert.IsTrue(firstClosed, "MeasureToolUI should close after first test");

            // Second activation
            Task.Delay(500).Wait(); // Brief pause
            SendKeys(activationKeys);
            bool secondActivation = ScreenRulerTestHelper.WaitForMeasureToolUI(this, 5000);
            Assert.IsTrue(secondActivation, "Second activation should also show MeasureToolUI");

            // Clean up
            ScreenRulerTestHelper.CloseMeasureToolUI(this);
            ScreenRulerTestHelper.WaitForMeasureToolUIToDisappear(this, 3000);
        }

        [TestMethod("ScreenRuler.ActivationFromDifferentContext")]
        [TestCategory("Activation")]
        public void TestActivationFromDifferentContext()
        {
            ScreenRulerTestHelper.LaunchFromSetting(this);

            // Ensure Screen Ruler is enabled
            ScreenRulerTestHelper.SetScreenRulerToggle(this, enable: true);

            var activationKeys = ScreenRulerTestHelper.ReadActivationShortcut(this);

            // Navigate away from Screen Ruler settings (go to General page)
            Find<NavigationViewItem>(By.AccessibilityId("Shell_Nav_General"), 1000).Click(msPostAction: 500);

            // Verify we're on a different page
            Assert.IsTrue(Has<Element>("General"), "Should be on General page");

            // Ensure MeasureToolUI is not open
            ScreenRulerTestHelper.CloseMeasureToolUI(this);
            Task.Delay(1000).Wait();

            // Execute activation shortcut from General page
            SendKeys(activationKeys);

            // Verify MeasureToolUI appears even when not on Screen Ruler settings page
            bool measureToolAppeared = ScreenRulerTestHelper.WaitForMeasureToolUI(this, 5000);
            Assert.IsTrue(
                measureToolAppeared,
                "MeasureToolUI should appear even when activated from different settings page");

            // Clean up
            ScreenRulerTestHelper.CloseMeasureToolUI(this);
        }

        [TestMethod("ScreenRuler.DefaultActivationShortcut")]
        [TestCategory("Activation")]
        public void TestDefaultActivationShortcut()
        {
            ScreenRulerTestHelper.LaunchFromSetting(this);

            // Enable Screen Ruler
            ScreenRulerTestHelper.SetScreenRulerToggle(this, enable: true);

            // Read current activation shortcut
            var activationKeys = ScreenRulerTestHelper.ReadActivationShortcut(this);

            // Verify default shortcut (Win+Ctrl+Shift+M)
            var expectedKeys = new Key[] { Key.Win, Key.Ctrl, Key.Shift, Key.M };

            // Compare the shortcuts (order might vary, so check if all expected keys are present)
            bool hasAllExpectedKeys = true;
            foreach (var expectedKey in expectedKeys)
            {
                if (!Array.Exists(activationKeys, k => k == expectedKey))
                {
                    hasAllExpectedKeys = false;
                    break;
                }
            }

            // If not default, that's still valid (user might have customized it)
            // But let's test that whatever shortcut is configured actually works
            ScreenRulerTestHelper.CloseMeasureToolUI(this);
            Task.Delay(1000).Wait();

            SendKeys(activationKeys);
            bool activationWorked = ScreenRulerTestHelper.WaitForMeasureToolUI(this, 5000);
            Assert.IsTrue(
                activationWorked,
                $"Current activation shortcut should work: {string.Join(" + ", activationKeys)}");

            // Clean up
            ScreenRulerTestHelper.CloseMeasureToolUI(this);

            // If we have the default shortcut, specifically test it
            if (hasAllExpectedKeys && activationKeys.Length == expectedKeys.Length)
            {
                Task.Delay(1000).Wait();
                SendKeys(Key.Win, Key.Ctrl, Key.Shift, Key.M);
                bool defaultShortcutWorked = ScreenRulerTestHelper.WaitForMeasureToolUI(this, 5000);
                Assert.IsTrue(defaultShortcutWorked, "Default shortcut Win+Ctrl+Shift+M should work");
                ScreenRulerTestHelper.CloseMeasureToolUI(this);
            }
        }
    }
}
