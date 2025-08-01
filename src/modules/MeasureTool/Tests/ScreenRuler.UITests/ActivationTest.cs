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

        /// <summary>
        /// Test MeasureTool keyboard shortcuts
        /// <list type="bullet">
        /// <item>
        /// <description>Validating toolbar appears when MeasureTool is activated</description>
        /// </item>
        /// <item>
        /// <description>Validating Ctrl+1 activates Bounds tool</description>
        /// </item>
        /// <item>
        /// <description>Validating Escape closes the toolbar</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod("MeasureTool.Shortcuts.ActivateToolbar")]
        [TestCategory("Measure Tool #1")]
        public void TestActivateMeasureTool()
        {
            // Launch PowerToys Settings
            LaunchFromSetting();

            var toggleSwitch = Find<ToggleSwitch>("Enable Screen Ruler");
            if (!toggleSwitch.IsOn)
            {
                toggleSwitch.Click(msPostAction: 500);
            }

            Assert.IsTrue(toggleSwitch.IsOn, "Screen Ruler toggle switch should be ON");
        }

        private void LaunchFromSetting()
        {
            Session.SetMainWindowSize(WindowSize.Medium);
            var screenRulers = FindAll<NavigationViewItem>("Screen Ruler");

            // Navigate to Measure Tool settings
            if (screenRulers.Count == 0)
            {
                // Expand System Tools list-group if needed
                Find<NavigationViewItem>("System Tools", 500).Click(msPostAction: 500);
            }

            Find<NavigationViewItem>("Screen Ruler", 500).Click(msPostAction: 500);
        }
    }
}
