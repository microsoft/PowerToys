// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScreenRuler.UITests.Next;

[TestClass]
public class TestShortcutActivation : UITestBase
{
    public TestShortcutActivation()
        : base(PowerToysModule.PowerToysSettings, WindowSize.Large, enableModules: new[] { TestHelper.ModuleSettingsKey })
    {
    }

    [TestMethod]
    [TestCategory("Activation")]
    public void TestScreenRulerShortcutActivation()
    {
        var activationKeys = TestHelper.InitializeTest(this, "activation test");

        try
        {
            // Test 1: pressing the activation shortcut shows the toolbar.
            Assert.IsTrue(
                TestHelper.SendShortcutUntilVisible(this, activationKeys),
                $"ScreenRulerUI should appear after pressing activation shortcut: {string.Join(" + ", activationKeys)}");

            // Test 2: pressing the activation shortcut again hides the toolbar (it's a toggle).
            KeyboardHelper.SendKeys(activationKeys);
            Assert.IsTrue(
                TestHelper.WaitForScreenRulerUIToDisappear(this, 3000),
                $"ScreenRulerUI should disappear after pressing activation shortcut again: {string.Join(" + ", activationKeys)}");

            // Test 3: while disabled, the shortcut must not activate the utility.
            // testBase.Session already targets the Settings window, so no re-attach is needed
            // (winappcli targets by hwnd/process, not foreground).
            TestHelper.SetAndVerifyScreenRulerToggle(this, enable: false, "disabled state test");
            KeyboardHelper.SendKeys(activationKeys);
            Thread.Sleep(1500);
            Assert.IsFalse(
                TestHelper.IsScreenRulerUIOpen(this),
                "ScreenRulerUI should not appear when Screen Ruler is disabled");

            // Test 4: re-enable and confirm the shortcut activates it again.
            TestHelper.SetAndVerifyScreenRulerToggle(this, enable: true, "re-enabled state test");
            Assert.IsTrue(
                TestHelper.SendShortcutUntilVisible(this, activationKeys),
                $"ScreenRulerUI should appear after re-enabling and pressing activation shortcut: {string.Join(" + ", activationKeys)}");

            // Test 5: the utility can be closed via the cleanup helper.
            TestHelper.CloseScreenRulerUI(this);
            Assert.IsTrue(
                TestHelper.WaitForScreenRulerUIToDisappear(this, 3000),
                "ScreenRulerUI should close after calling CloseScreenRulerUI");
        }
        finally
        {
            TestHelper.CleanupTest(this);
        }
    }
}
