// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Devices.Printers;

namespace FindMyMouse.UITests
{
    [TestClass]
    public class FindMyMouseTests : UITestBase
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
            LaunchFromSetting();
            var foundCustom = this.Find<Custom>("Find My Mouse");
            if (foundCustom != null)
            {
                foundCustom.Find<ToggleSwitch>("Enable Find My Mouse").Toggle(true);

                // foundCustom.Find<ToggleSwitch>("Enable Find My Mouse").Toggle(false);
                var groupActivation = foundCustom.Find<TextBlock>("Activation method");
                if (groupActivation != null)
                {
                    groupActivation.Click();
                    groupActivation.Click();
                }
                else
                {
                    Assert.Fail("Activation method group not found.");
                }

                var groupAppearanceBehavior = foundCustom.Find<TextBlock>("Appearance & behavior");
                if (groupAppearanceBehavior != null)
                {
                    // groupAppearanceBehavior.Click();
                    if (foundCustom.FindAll<Slider>("Overlay opacity (%)").Count == 0)
                    {
                        groupAppearanceBehavior.Click();
                    }

                    // Set the overlay opacity to 100%
                    var overlayOpacitySlider = foundCustom.Find<Slider>("Overlay opacity (%)");
                    Assert.IsNotNull(overlayOpacitySlider);
                    overlayOpacitySlider.QuickSetValue(10);
                    Assert.AreEqual("10", overlayOpacitySlider.Text);

                    //// Changge the edit value
                    // var spotlightRadiusEdit = foundCustom.Find<TextBox>("Spotlight radius (px) Minimum5");
                    // Assert.IsNotNull(spotlightRadiusEdit);
                    // Task.Delay(10000).Wait();
                    // spotlightRadiusEdit.SetText("55");
                    // Assert.AreEqual("55", spotlightRadiusEdit.Text);

                    // Set the BackGroud color
                    var backgroundColor = foundCustom.Find<Group>("Background color");
                    Assert.IsNotNull(backgroundColor);

                    // backgroundColor.Click();

                    // var button = backgroundColor.Find<Button>(By.XPath(".//Button[@ClassName='Microsoft.UI.XAML.Controls.DropDownButton']"));

                    // var button = backgroundColor.Find<Button>(By.ClassName("Microsoft.UI.XAML.Controls.DropDownButton"));
                    var button = backgroundColor.Find<Button>(By.XPath(".//Button"));
                    Assert.IsNotNull(button);
                    button.Click();
                }
                else
                {
                    Assert.Fail("Activation method group not found.");
                }

                var excludedApps = foundCustom.Find<TextBlock>("Excluded apps");
                if (excludedApps != null)
                {
                    excludedApps.Click();
                    excludedApps.Click();
                }
                else
                {
                    Assert.Fail("Activation method group not found.");
                }
            }
            else
            {
                Assert.Fail("Find My Mouse group not found.");
            }

            IOUtil.SimulateKeyPress(0xA2);
            Task.Delay(100).Wait();
            IOUtil.SimulateKeyPress(0xA2);

            Task.Delay(5000).Wait();
            MouseSimulator.LeftClick();
            Task.Delay(5000).Wait();
        }

        [TestMethod]
        public void TestEnableMouseHighlighter()
        {
            LaunchFromSetting();
            var foundCustom = this.Find<Custom>("Mouse Highlighter");
            if (foundCustom != null)
            {
                foundCustom.Find<ToggleSwitch>("Enable Mouse Highlighter").Toggle(true);

                var activationShortcutButton = foundCustom.Find<Button>("Activation shortcut");
                Assert.IsNotNull(activationShortcutButton);

                activationShortcutButton.Click();
                var activationShortcutWindow = Session.Find<Window>("Activation shortcut");
                Assert.IsNotNull(activationShortcutWindow);

                // IOUtil.SimulateShortcut(0x5B, 0x10, 0x45);
                Session.SendKeys(Key.Win, Key.Shift, Key.H);
            }
            else
            {
                Assert.Fail("Mouse Highlighter Custom not found.");
            }

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
            if (this.FindAll<NavigationViewItem>("Mouse utilities").Count == 0)
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
    }
}
