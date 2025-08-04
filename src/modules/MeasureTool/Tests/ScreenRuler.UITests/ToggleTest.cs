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
    public class ToggleTest : UITestBase
    {
        public ToggleTest()
            : base(PowerToysModule.PowerToysSettings, WindowSize.Large)
        {
        }

        [TestMethod("ScreenRuler.ModuleToggle")]
        [TestCategory("Activation")]
        public void TestToggleScreenRuler()
        {
            ScreenRulerTestHelper.LaunchFromSetting(this);

            // First ensure it's disabled
            ScreenRulerTestHelper.SetScreenRulerToggle(this, enable: false);
            Assert.IsFalse(Find<ToggleSwitch>(By.AccessibilityId("Toggle_ScreenRuler")).IsOn, "Screen Ruler toggle switch should be OFF initially");

            // Then enable it
            ScreenRulerTestHelper.SetScreenRulerToggle(this, enable: true);
            Assert.IsTrue(Find<ToggleSwitch>(By.AccessibilityId("Toggle_ScreenRuler")).IsOn, "Screen Ruler toggle switch should be ON after enabling");

            // Then disable it again
            ScreenRulerTestHelper.SetScreenRulerToggle(this, enable: false);
            Assert.IsFalse(Find<ToggleSwitch>(By.AccessibilityId("Toggle_ScreenRuler")).IsOn, "Screen Ruler toggle switch should be OFF after disabling");
        }
    }
}
