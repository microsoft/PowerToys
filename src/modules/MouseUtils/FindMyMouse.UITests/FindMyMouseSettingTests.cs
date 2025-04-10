// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using Windows.Devices.Printers;

namespace FindMyMouse.UITests
{
    [TestClass]
    public class FindMyMouseSettingTests : UITestBase
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
        public void TestEnable2()
        {
            this.LaunchFromSetting(showWarning: true);
            this.Find<ToggleSwitch>("Enable Find My Mouse").Toggle(true);

            // this.Find<ToggleSwitch>("Enable Mouse Pointer Crosshairs").Toggle(true);
            // this.Find<NavigationViewItem>("Activation method").Click();
            string findMyMouseCombox = "Activation method";
            var foundElements = this.FindAll<Element>(findMyMouseCombox);
            bool comboBoxFound = false;
            foreach (var element in foundElements)
            {
                string controlType = element.ControlType;
                if (controlType == "ControlType.ComboBox")
                {
                    element.Click();
                    comboBoxFound = true;
                }
            }

            Assert.IsTrue(comboBoxFound, "ComboBox is not found in the setting page.");

            // this.Find<NavigationViewItem>("Custom shortcut").Click();
            this.Find<NavigationViewItem>("Press Left Control twice").Click();

            bool isfind = FindGroup("Appearance & behavior");

            Assert.IsTrue(isfind, "Find My Mouse group is not found in the setting page.");

            Program.SimulateKeyPress(0xA2);
            Task.Delay(100).Wait();
            Program.SimulateKeyPress(0xA2);

            Task.Delay(10000).Wait();
        }

        [TestMethod]
        public void TestEnable3()
        {
            this.LaunchFromSetting(showWarning: true);
            this.Find<ToggleSwitch>("Enable Find My Mouse").Toggle(true);

            // this.Find<ToggleSwitch>("Enable Mouse Pointer Crosshairs").Toggle(true);
            // this.Find<NavigationViewItem>("Activation method").Click();
            string findMyMouseCombox = "Activation method";
            var foundElements = this.FindAll<ComboBox>(findMyMouseCombox);
            if (foundElements.Count != 0)
            {
                this.Find<ComboBox>(findMyMouseCombox).Click();
                var myMouseCombox = this.Find<ComboBox>(findMyMouseCombox);
                myMouseCombox.Find<NavigationViewItem>("Press Left Control twice").Click();
            }
            else
            {
                Assert.IsTrue(false, "ComboBox is not found in the setting page.");
            }

            Task.Delay(10000).Wait();
        }

        [TestMethod]
        public void TestEnableFz()
        {
            this.LaunchFromSetting_FZ(showWarning: true);
            this.Find<ToggleSwitch>("Enable FancyZones").Toggle(true);

            // Session.KeyboardAction(Keys.Shift + Keys.Home + "`");
            Task.Delay(10000).Wait();

            // Session.HotkeysFz();
            Program.SimulateKeyPress(0x5B);
            Task.Delay(10000).Wait();
            Program.SimulateKeyPress(0x5B);
            Task.Delay(10000).Wait();

            Program.SimulateShortcut(0x5B, 0x10, 0x09);
            Task.Delay(10000).Wait();
        }

        private bool FindGroup(string groupName)
        {
            try
            {
                var foundElements = this.FindAll<Element>(groupName);
                foreach (var element in foundElements)
                {
                  string className = element.ClassName;
                  string name = element.Name;
                  string text = element.Text;
                  string helptext = element.HelpText;
                  string controlType = element.ControlType;
                }

                if (foundElements.Count == 0)
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                // Validate if group is not found by checking exception.Message
                return ex.Message.Contains("No element found");
            }

            return true;
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
                this.Find<NavigationViewItem>("Input / Output").Click();
            }

            this.Find<NavigationViewItem>("Mouse utilities").Click();

            // this.Find<ToggleSwitch>("Enable Hosts File Editor").Toggle(true);
            // this.Find<ToggleSwitch>("Launch as administrator").Toggle(launchAsAdmin);
            // this.Find<ToggleSwitch>("Show a warning at startup").Toggle(showWarning);

            // launch Hosts File Editor

            // Task.Delay(500).Wait();

            // this.Session.Attach(PowerToysModule.Hosts);
        }

        private void LaunchFromSetting_FZ(bool showWarning = false, bool launchAsAdmin = false)
        {
            // Goto Hosts File Editor setting page
            if (this.FindAll<NavigationViewItem>("FancyZones").Count == 0)
            {
                // Expand Advanced list-group if needed
                this.Find<NavigationViewItem>("Windowing & Layouts").Click();
            }

            this.Find<NavigationViewItem>("FancyZones").Click();

            // this.Find<ToggleSwitch>("Enable Hosts File Editor").Toggle(true);
            // this.Find<ToggleSwitch>("Launch as administrator").Toggle(launchAsAdmin);
            // this.Find<ToggleSwitch>("Show a warning at startup").Toggle(showWarning);

            // launch Hosts File Editor

            // Task.Delay(500).Wait();

            // this.Session.Attach(PowerToysModule.Hosts);
        }
    }
}
