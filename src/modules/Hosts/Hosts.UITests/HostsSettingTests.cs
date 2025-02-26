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
        [TestMethod]
        public void TestShowWarningDialog()
        {
            this.GotoHostsFileEditor();

            this.Find<ToggleSwitch>("Enable Hosts File Editor").Toggle(true);
            this.Find<ToggleSwitch>("Launch as administrator").Toggle(false);
            this.Find<ToggleSwitch>("Show a warning at startup").Toggle(true);

            this.Find<Button>("Launch Hosts File Editor").Click();

            // wait for 500 ms to make sure Hosts File Editor is launched
            Task.Delay(500).Wait();

            this.Session.Attach(PowerToysModule.Hosts);

            // Should show warning dialog
            Assert.IsTrue(this.FindAll("Warning").Count > 0, "Should show warning dialog");

            // Quit Hosts File Editor
            this.Find<Button>("Quit").Click();

            try
            {
                // Wait for 500 ms to make sure Hosts File Editor is closed
                Task.Delay(500).Wait();

                this.Session.FindAll<Window>("Hosts File Editor");
                Assert.IsTrue(false, "Hosts File Editor should be closed");
            }
            catch (Exception ex)
            {
                // Hosts File Editor should be closed
                Assert.IsTrue(ex.Message.Contains("Currently selected window has been closed"), "Hosts File Editor should be closed");
            }
        }

        [TestMethod]
        public void TestNotShowWarningDialog()
        {
            this.GotoHostsFileEditor();

            this.Find<ToggleSwitch>("Enable Hosts File Editor").Toggle(true);
            this.Find<ToggleSwitch>("Launch as administrator").Toggle(false);
            this.Find<ToggleSwitch>("Show a warning at startup").Toggle(false);

            this.Find<Button>("Launch Hosts File Editor").Click();

            Task.Delay(500).Wait();

            this.Session.Attach(PowerToysModule.Hosts);

            // Should NOT show warning dialog
            Assert.IsTrue(this.FindAll("Warning").Count == 0, "Should not show warning dialog");

            this.Session.Find<Window>("Hosts File Editor").Close();
        }

        private void GotoHostsFileEditor()
        {
            // Goto Hosts File Editor setting page
            if (this.FindAll<NavigationViewItem>("Hosts File Editor").Count == 0)
            {
                // Expand Advanced list-group if needed
                this.Find<NavigationViewItem>("Advanced").Click();
            }

            this.Find<NavigationViewItem>("Hosts File Editor").Click();
        }
    }
}
