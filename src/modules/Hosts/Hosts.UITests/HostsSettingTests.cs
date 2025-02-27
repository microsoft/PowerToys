// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hosts.UITests
{
    [TestClass]
    public class HostsSettingTests : UITestBase
    {
        /// <summary>
        /// Test Warning Dialog at startup
        /// <list type="bullet">
        /// <item>
        /// <description>Validating Warning-Dialog will be shown if 'Show a warning at startup' toggle is On.</description>
        /// </item>
        /// <item>
        /// <description>Validating Warning-Dialog will NOT be shown if 'Show a warning at startup' toggle is Off.</description>
        /// </item>
        /// <item>
        /// <description>Validating click 'Quit' button in Warning-Dialog, the Hosts File Editor window would be closed.</description>
        /// </item>
        /// <item>
        /// <description>Validating click 'Accept' button in Warning-Dialog, the Hosts File Editor window would NOT be closed.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod]
        public void TestWarningDialog()
        {
            this.LaunchFromSetting(showWarning: true);

            // Validating Warning-Dialog will be shown if 'Show a warning at startup' toggle is on
            Assert.IsTrue(this.FindAll("Warning").Count > 0, "Should show warning dialog");

            // Quit Hosts File Editor
            this.Find<Button>("Quit").Click();

            // Wait for 500 ms to make sure Hosts File Editor is closed
            Task.Delay(500).Wait();

            // Validating click 'Quit' button in Warning-Dialog, the Hosts File Editor window would be closed
            Assert.IsTrue(this.IsHostsFileEditorClosed(), "Hosts File Editor should be closed after click Quit button in Warning Dialog");

            // Re-attaching to Setting Windows
            this.Session.Attach(PowerToysModule.PowerToysSettings);

            this.Find<Button>("Launch Hosts File Editor").Click();

            // wait for 500 ms to make sure Hosts File Editor is launched
            Task.Delay(500).Wait();

            this.Session.Attach(PowerToysModule.Hosts);

            // Should show warning dialog
            Assert.IsTrue(this.FindAll("Warning").Count > 0, "Should show warning dialog");

            // Quit Hosts File Editor
            this.Find<Button>("Accept").Click();

            Task.Delay(500).Wait();

            // Validating click 'Accept' button in Warning-Dialog, the Hosts File Editor window would NOT be closed
            Assert.IsFalse(this.IsHostsFileEditorClosed(), "Hosts File Editor should NOT be closed after click Accept button in Warning Dialog");

            // Close Hosts File Editor window
            this.Session.Find<Window>("Hosts File Editor").Close();

            // Restore back to PowerToysSettings Session
            this.Session.Attach(PowerToysModule.PowerToysSettings);

            this.LaunchFromSetting(showWarning: false);

            // Should NOT show warning dialog
            Assert.IsTrue(this.FindAll("Warning").Count == 0, "Should NOT show warning dialog");

            // Host Editor Window should not be closed
            Assert.IsFalse(this.IsHostsFileEditorClosed(), "Hosts File Editor should NOT be closed");

            // Close Hosts File Editor window
            this.Session.Find<Window>("Hosts File Editor").Close();

            // Restore back to PowerToysSettings Session
            this.Session.Attach(PowerToysModule.PowerToysSettings);
        }

        private bool IsHostsFileEditorClosed()
        {
            try
            {
                this.Session.FindAll<Window>("Hosts File Editor");
            }
            catch (Exception ex)
            {
                // Validate if editor window closed by checking exception.Message
                return ex.Message.Contains("Currently selected window has been closed");
            }

            return false;
        }

        private void LaunchFromSetting(bool showWarning = false, bool launchAsAdmin = false)
        {
            // Goto Hosts File Editor setting page
            if (this.FindAll<NavigationViewItem>("Hosts File Editor").Count == 0)
            {
                // Expand Advanced list-group if needed
                this.Find<NavigationViewItem>("Advanced").Click();
            }

            this.Find<NavigationViewItem>("Hosts File Editor").Click();

            this.Find<ToggleSwitch>("Enable Hosts File Editor").Toggle(true);
            this.Find<ToggleSwitch>("Launch as administrator").Toggle(launchAsAdmin);
            this.Find<ToggleSwitch>("Show a warning at startup").Toggle(showWarning);

            // launch Hosts File Editor
            this.Find<Button>("Launch Hosts File Editor").Click();

            Task.Delay(500).Wait();

            this.Session.Attach(PowerToysModule.Hosts);
        }
    }
}
