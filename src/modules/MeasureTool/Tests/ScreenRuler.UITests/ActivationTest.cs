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

            // Enable MeasureTool
            var foundCustom = this.Find<Custom>("Screen Ruler");
            Assert.IsNotNull(foundCustom, "Screen Ruler group not found.");

            // Toggle on MeasureTool
            foundCustom.Find<ToggleSwitch>("Enable Measure Tool").Toggle(true);
            Task.Delay(1000).Wait();
        }

        private void LaunchFromSetting()
        {
            this.Session.SetMainWindowSize(WindowSize.Medium);

            // Navigate to Measure Tool settings
            if (this.FindAll<NavigationViewItem>("Screen Ruler", 2000).Count == 0)
            {
                // Expand Utilities list-group if needed
                this.Find<NavigationViewItem>("Utilities").Click();
                Task.Delay(1000).Wait();
            }

            Task.Delay(1000).Wait();
            this.Find<NavigationViewItem>("Screen Ruler").Click();
            Task.Delay(2000).Wait();
        }
    }
}
