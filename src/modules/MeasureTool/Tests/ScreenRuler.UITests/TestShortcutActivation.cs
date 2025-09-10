// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
            var activationKeys = TestHelper.InitializeTest(this, "activation test");

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
            // Ensure we're attached to settings UI before toggling
            Session.Attach(PowerToysModule.PowerToysSettings);
            TestHelper.SetAndVerifyScreenRulerToggle(this, enable: false, "disabled state test");

            // Try to activate with shortcut while disabled
            SendKeys(activationKeys);
            Task.Delay(1000).Wait();
            Assert.IsFalse(
                TestHelper.IsScreenRulerUIOpen(this),
                "ScreenRulerUI should not appear when Screen Ruler is disabled");

            // Test 4: Enable Screen Ruler and press the activation shortcut and verify the toolbar appears
            // Ensure we're attached to settings UI before toggling
            Session.Attach(PowerToysModule.PowerToysSettings);
            TestHelper.SetAndVerifyScreenRulerToggle(this, enable: true, "re-enabled state test");

            SendKeys(activationKeys);
            screenRulerAppeared = TestHelper.WaitForScreenRulerUI(this, 1000);
            Assert.IsTrue(
                screenRulerAppeared,
                $"ScreenRulerUI should appear after re-enabling and pressing activation shortcut: {string.Join(" + ", activationKeys)}");

            // Test 5: Verify the utility can be closed via the cleanup method
            TestHelper.CloseScreenRulerUI(this);
            bool screenRulerClosed = TestHelper.WaitForScreenRulerUIToDisappear(this, 1000);
            Assert.IsTrue(
                screenRulerClosed,
                "ScreenRulerUI should close after calling CloseScreenRulerUI");

            TestHelper.CleanupTest(this);
        }
    }
}
