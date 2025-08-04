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

        [TestMethod("ScreenRuler.ModuleToggle")]
        [TestCategory("Activation")]
        public void TestToggleScreenRuler()
        {
            LaunchFromSetting();

            // First ensure it's disabled
            SetScreenRulerToggle(enable: false);
            Assert.IsFalse(Find<ToggleSwitch>(By.AccessibilityId("Toggle_ScreenRuler")).IsOn, "Screen Ruler toggle switch should be OFF initially");

            // Then enable it
            SetScreenRulerToggle(enable: true);
            Assert.IsTrue(Find<ToggleSwitch>(By.AccessibilityId("Toggle_ScreenRuler")).IsOn, "Screen Ruler toggle switch should be ON after enabling");

            // Then disable it again
            SetScreenRulerToggle(enable: false);
            Assert.IsFalse(Find<ToggleSwitch>(By.AccessibilityId("Toggle_ScreenRuler")).IsOn, "Screen Ruler toggle switch should be OFF after disabling");
        }

        private void LaunchFromSetting()
        {
            var screenRulers = FindAll<NavigationViewItem>(By.AccessibilityId("Shell_Nav_ScreenRuler"));

            if (screenRulers.Count == 0)
            {
                Find<NavigationViewItem>(By.AccessibilityId("Shell_Nav_TopLevelSystemTools"), 500).Click(msPostAction: 500);
            }

            Find<NavigationViewItem>(By.AccessibilityId("Shell_Nav_ScreenRuler"), 500).Click(msPostAction: 500);
        }

        private void SetScreenRulerToggle(bool enable)
        {
            var toggleSwitch = Find<ToggleSwitch>(By.AccessibilityId("Toggle_ScreenRuler"));
            if (toggleSwitch.IsOn != enable)
            {
                toggleSwitch.Click(msPostAction: 500);
            }
        }
    }
}
