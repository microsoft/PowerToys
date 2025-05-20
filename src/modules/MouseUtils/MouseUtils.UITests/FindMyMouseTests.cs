// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Devices.Printers;

namespace MouseUtils.UITests
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
        [TestMethod("MouseUtils.FindMyMouse.EnableFindMyMouse")]
        [TestCategory("Mouse Utils #1")]
        [TestCategory("Mouse Utils #2")]
        [TestCategory("Mouse Utils #3")]
        [TestCategory("Mouse Utils #4")]
        public void TestEnableFindMyMouse()
        {
            LaunchFromSetting();

            var settings = new FindMyMouseSettings();
            settings.OverlayOpacity = "100";
            settings.Radius = "50";
            settings.InitialZoom = "1";
            settings.AnimationDuration = "0";
            settings.BackgroundColor = "000000";
            settings.SpotlightColor = "FFFFFF";

            var foundCustom = this.Find<Custom>("Find My Mouse");
            Assert.IsNotNull(foundCustom);

            if (CheckAnimationEnable(ref foundCustom))
            {
                foundCustom = this.Find<Custom>("Find My Mouse");
            }

            if (foundCustom != null)
            {
                foundCustom.Find<ToggleSwitch>("Enable Find My Mouse").Toggle(true);

                SetFindMyMouseActivationMethod(ref foundCustom, "Press Left Control twice");
                Assert.IsNotNull(foundCustom, "Find My Mouse group not found.");
                SetFindMyMouseAppearanceBehavior(ref foundCustom, ref settings);

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

            // [Test Case]Enable FindMyMouse. Then, without moving your mouse: Press Left Ctrl twice and verify the overlay appears.
            VerifySpotlightSettings(ref settings);

            // [Test Case]Enable FindMyMouse. Then, without moving your mouse: Press any other key and verify the overlay disappears.
            Session.SendKeys(Key.A);
            VerifySpotlightDisappears(ref settings);

            // [Test Case]Enable FindMyMouse. Then, without moving your mouse: Press Left Ctrl twice and verify the overlay appears.
            VerifySpotlightSettings(ref settings);

            // [Test Case]Enable FindMyMouse. Then, without moving your mouse: Press a mouse button and verify the overlay disappears.
            Task.Delay(1000).Wait();

            Session.PerformMouseAction(MouseActionType.LeftClick, 500, 1000);

            VerifySpotlightDisappears(ref settings);
        }

        [TestMethod("MouseUtils.FindMyMouse.FindMyMouseDifferentSettings")]
        [TestCategory("Mouse Utils #10")]
        [TestCategory("Mouse Utils #11")]
        [TestCategory("Mouse Utils #12")]
        public void TestFindMyMouseDifferentSettings()
        {
            LaunchFromSetting();

            var settings = new FindMyMouseSettings();
            settings.OverlayOpacity = "100";
            settings.Radius = "80";
            settings.InitialZoom = "1";
            settings.AnimationDuration = "0";
            settings.BackgroundColor = "FF0000";
            settings.SpotlightColor = "0000FF";

            var foundCustom = this.Find<Custom>("Find My Mouse");
            Assert.IsNotNull(foundCustom);

            if (CheckAnimationEnable(ref foundCustom))
            {
                foundCustom = this.Find<Custom>("Find My Mouse");
            }

            if (foundCustom != null)
            {
                foundCustom.Find<ToggleSwitch>("Enable Find My Mouse").Toggle(true);

                SetFindMyMouseActivationMethod(ref foundCustom, "Press Left Control twice");
                Assert.IsNotNull(foundCustom, "Find My Mouse group not found.");
                SetFindMyMouseAppearanceBehavior(ref foundCustom, ref settings);

                var excludedApps = foundCustom.Find<TextBlock>("Excluded apps");
                if (excludedApps != null)
                {
                    excludedApps.Click();
                    excludedApps.Click();
                }
                else
                {
                    Assert.Fail("Excluded apps group not found.");
                }
            }
            else
            {
                Assert.Fail("Find My Mouse group not found.");
            }

            // [Test Case]Test the different settings and verify they apply, Background color
            // [Test Case]Test the different settings and verify they apply, Spotlight color
            // [Test Case]Test the different settings and verify they apply, Spotlight radius
            VerifySpotlightSettings(ref settings);

            Session.SendKeys(Key.A);
            VerifySpotlightDisappears(ref settings);
        }

        [TestMethod("MouseUtils.FindMyMouse.DisableFindMyMouse")]
        [TestCategory("Mouse Utils #5")]
        public void TestDisableFindMyMouse()
        {
            LaunchFromSetting();

            var settings = new FindMyMouseSettings();
            settings.OverlayOpacity = "100";
            settings.Radius = "50";
            settings.InitialZoom = "1";
            settings.AnimationDuration = "0";
            settings.BackgroundColor = "000000";
            settings.SpotlightColor = "FFFFFF";
            var foundCustom = this.Find<Custom>("Find My Mouse");

            Assert.IsNotNull(foundCustom);

            if (CheckAnimationEnable(ref foundCustom))
            {
                foundCustom = this.Find<Custom>("Find My Mouse");
            }

            if (foundCustom != null)
            {
                foundCustom.Find<ToggleSwitch>("Enable Find My Mouse").Toggle(true);

                foundCustom.Find<ToggleSwitch>("Enable Find My Mouse").Toggle(false);

                foundCustom.Find<ToggleSwitch>("Enable Find My Mouse").Toggle(true);
                SetFindMyMouseActivationMethod(ref foundCustom, "Press Left Control twice");
                Assert.IsNotNull(foundCustom);
                SetFindMyMouseAppearanceBehavior(ref foundCustom, ref settings);

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

            // [Test Case]Enable FindMyMouse. Then, without moving your mouse: Press Left Ctrl twice and verify the overlay appears.
            // VerifySpotlightSettings(ref settings);
            ActivateSpotlight(ref settings);
            VerifySpotlightAppears(ref settings);

            // [Test Case] Disable FindMyMouse. Verify the overlay no longer appears when you press Left Ctrl twice
            foundCustom.Find<ToggleSwitch>("Enable Find My Mouse").Toggle(false);
            Task.Delay(1000).Wait();
            ActivateSpotlight(ref settings);

            VerifySpotlightDisappears(ref settings);

            // [Test Case] Press Left Ctrl twice and verify the overlay appears
            foundCustom.Find<ToggleSwitch>("Enable Find My Mouse").Toggle(true);
            ActivateSpotlight(ref settings);
            VerifySpotlightAppears(ref settings);

            Session.PerformMouseAction(MouseActionType.LeftClick);
        }

        [TestMethod("MouseUtils.FindMyMouse.DisableFindMyMouse3")]
        [TestCategory("Mouse Utils #6")]
        public void TestDisableFindMyMouse3()
        {
            LaunchFromSetting();

            var settings = new FindMyMouseSettings();
            settings.OverlayOpacity = "100";
            settings.Radius = "50";
            settings.InitialZoom = "1";
            settings.AnimationDuration = "0";
            settings.BackgroundColor = "000000";
            settings.SpotlightColor = "FFFFFF";
            var foundCustom = this.Find<Custom>("Find My Mouse");

            Assert.IsNotNull(foundCustom);

            if (CheckAnimationEnable(ref foundCustom))
            {
                foundCustom = this.Find<Custom>("Find My Mouse");
            }

            if (foundCustom != null)
            {
                foundCustom.Find<ToggleSwitch>("Enable Find My Mouse").Toggle(true);

                foundCustom.Find<ToggleSwitch>("Enable Find My Mouse").Toggle(false);

                foundCustom.Find<ToggleSwitch>("Enable Find My Mouse").Toggle(true);
                SetFindMyMouseActivationMethod(ref foundCustom, "Press Left Control twice");
                Assert.IsNotNull(foundCustom);
                SetFindMyMouseAppearanceBehavior(ref foundCustom, ref settings);

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

            // [Test Case]Enable FindMyMouse. Then, without moving your mouse: Press Left Ctrl twice and verify the overlay appears.
            // VerifySpotlightSettings(ref settings);
            ActivateSpotlight(ref settings);
            VerifySpotlightAppears(ref settings);

            // [Test Case] Disable FindMyMouse. Verify the overlay no longer appears when you press Left Ctrl twice
            foundCustom.Find<ToggleSwitch>("Enable Find My Mouse").Toggle(false);
            Task.Delay(1000).Wait();
            ActivateSpotlight(ref settings);

            VerifySpotlightDisappears(ref settings);

            // [Test Case] Press Left Ctrl twice and verify the overlay appears
            foundCustom.Find<ToggleSwitch>("Enable Find My Mouse").Toggle(true);
            ActivateSpotlight(ref settings);
            VerifySpotlightAppears(ref settings);

            Session.PerformMouseAction(MouseActionType.LeftClick);
        }

        [TestMethod("MouseUtils.FindMyMouse.DisableFindMyMouse2")]
        [TestCategory("Mouse Utils #5")]
        public void TestDisableFindMyMouse2()
        {
            LaunchFromSetting();

            var settings = new FindMyMouseSettings();
            settings.OverlayOpacity = "100";
            settings.Radius = "50";
            settings.InitialZoom = "1";
            settings.AnimationDuration = "0";
            settings.BackgroundColor = "000000";
            settings.SpotlightColor = "FFFFFF";
            var foundCustom = this.Find<Custom>("Find My Mouse");
            if (foundCustom != null)
            {
                foundCustom.Find<ToggleSwitch>("Enable Find My Mouse").Toggle(true);

                // foundCustom.Find<ToggleSwitch>("Enable Find My Mouse").Toggle(false);
                SetFindMyMouseActivationMethod(ref foundCustom, "Press Left Control twice");
                Assert.IsNotNull(foundCustom, "Find My Mouse group not found.");

                // SetFindMyMouseAppearanceBehavior(ref foundCustom, ref settings);
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

            // [Test Case]Enable FindMyMouse. Then, without moving your mouse: Press Left Ctrl twice and verify the overlay appears.
            // VerifySpotlightSettings(ref settings);

            // [Test Case] Disable FindMyMouse. Verify the overlay no longer appears when you press Left Ctrl twice
            foundCustom.Find<ToggleSwitch>("Enable Find My Mouse").Toggle(false);
            Task.Delay(2000).Wait();
            Session.SendKey(Key.LCtrl, 0, 0);
            Task.Delay(100).Wait();
            Session.SendKey(Key.LCtrl, 0, 0);

            VerifySpotlightDisappears(ref settings);
        }

        private void VerifySpotlightDisappears(ref FindMyMouseSettings settings)
        {
            Task.Delay(2000).Wait();

            var location = Session.GetMousePosition();
            int radius = int.Parse(settings.Radius, CultureInfo.InvariantCulture);
            var colorSpotlight = this.GetPixelColorString(location.Item1, location.Item2);
            Assert.AreNotEqual("#" + settings.SpotlightColor, colorSpotlight);

            var colorBackground = this.GetPixelColorString(location.Item1 + radius + 50, location.Item2 + radius + 50);
            Assert.AreNotEqual("#" + settings.BackgroundColor, colorBackground);

            var colorBackground2 = this.GetPixelColorString(location.Item1 + radius + 100, location.Item2 + radius + 100);
            Assert.AreNotEqual("#" + settings.BackgroundColor, colorBackground2);
        }

        private void VerifySpotlightAppears(ref FindMyMouseSettings settings)
        {
            Task.Delay(2000).Wait();

            var location = Session.GetMousePosition();
            int radius = int.Parse(settings.Radius, CultureInfo.InvariantCulture);
            var colorSpotlight = this.GetPixelColorString(location.Item1, location.Item2);
            Assert.AreEqual("#" + settings.SpotlightColor, colorSpotlight);

            var colorSpotlight2 = this.GetPixelColorString(location.Item1 + radius - 1, location.Item2);

            // Session.MoveMouseTo(location.Item1 + radius - 10, location.Item2);
            Assert.AreEqual("#" + settings.SpotlightColor, colorSpotlight2);
            Task.Delay(100).Wait();

            var colorBackground = this.GetPixelColorString(location.Item1 + radius + 50, location.Item2 + radius + 50);
            Assert.AreEqual("#" + settings.BackgroundColor, colorBackground);

            var colorBackground2 = this.GetPixelColorString(location.Item1 + radius + 100, location.Item2 + radius + 100);
            Assert.AreEqual("#" + settings.BackgroundColor, colorBackground2);
        }

        private void ActivateSpotlight(ref FindMyMouseSettings settings)
        {
            var xy = Session.GetMousePosition();
            Session.MoveMouseTo(xy.Item1 - 200, xy.Item2 - 100);
            Task.Delay(1000).Wait();

            Session.PerformMouseAction(MouseActionType.LeftClick);
            Task.Delay(5000).Wait();
            if (settings.SelectedActivationMethod == FindMyMouseSettings.ActivationMethod.PressLeftControlTwice)
            {
                Session.SendKey(Key.LCtrl, 0, 0);
                Task.Delay(100).Wait();
                Session.SendKey(Key.LCtrl, 0, 0);
            }
            else if (settings.SelectedActivationMethod == FindMyMouseSettings.ActivationMethod.PressRightControlTwice)
            {
                Session.SendKey(Key.RCtrl, 0, 0);
                Task.Delay(100).Wait();
                Session.SendKey(Key.RCtrl, 0, 0);
            }
            else if (settings.SelectedActivationMethod == FindMyMouseSettings.ActivationMethod.ShakeMouse)
            {
                // Simulate shake mouse;
            }
            else if (settings.SelectedActivationMethod == FindMyMouseSettings.ActivationMethod.CustomShortcut)
            {
                // Simulate custom shortcut
            }
        }

        private void VerifySpotlightSettings(ref FindMyMouseSettings settings, bool equal = true)
        {
            ActivateSpotlight(ref settings);

            VerifySpotlightAppears(ref settings);
        }

        private void SetFindMyMouseActivationMethod(ref Custom? foundCustom, string method)
        {
            Assert.IsNotNull(foundCustom);
            var groupActivation = foundCustom.Find<TextBlock>("Activation method");
            if (groupActivation != null)
            {
                groupActivation.Click();
                string findMyMouseComboBoxKey = "Activation method";
                var foundElements = foundCustom.FindAll<ComboBox>(findMyMouseComboBoxKey);
                if (foundElements.Count != 0)
                {
                    var myMouseComboBox = foundCustom.Find<ComboBox>(findMyMouseComboBoxKey);
                    Assert.IsNotNull(myMouseComboBox);
                    myMouseComboBox.Click();
                    var selectedItem = myMouseComboBox.Find<NavigationViewItem>(method);
                    Assert.IsNotNull(selectedItem);
                    selectedItem.Click();
                }
                else
                {
                    Assert.IsTrue(false, "ComboBox is not found in the setting page.");
                }
            }
            else
            {
                Assert.Fail("Activation method group not found.");
            }
        }

        private void SetFindMyMouseAppearanceBehavior(ref Custom foundCustom, ref FindMyMouseSettings settings)
        {
            Assert.IsNotNull(foundCustom);
            var groupAppearanceBehavior = foundCustom.Find<TextBlock>("Appearance & behavior");
            if (groupAppearanceBehavior != null)
            {
                // groupAppearanceBehavior.Click();
                if (foundCustom.FindAll<Slider>("Overlay opacity (%)").Count == 0)
                {
                    groupAppearanceBehavior.Click();
                }

                // Set the BackGround color
                var backgroundColor = foundCustom.Find<Group>("Background color");
                Assert.IsNotNull(backgroundColor);

                var button = backgroundColor.Find<Button>(By.XPath(".//Button"));
                Assert.IsNotNull(button);
                button.Click();

                var popupWindow = this.Find<Window>("Popup");
                Assert.IsNotNull(popupWindow);
                Task.Delay(1000).Wait();
                var colorModelComboBox = this.Find<ComboBox>("Color model");
                Assert.IsNotNull(colorModelComboBox);
                colorModelComboBox.Click();
                var selectedItem = colorModelComboBox.Find<NavigationViewItem>("RGB");
                selectedItem.Click();
                Task.Delay(100).Wait();
                var rgbHexEdit = this.Find<TextBox>("RGB hex");
                Assert.IsNotNull(rgbHexEdit);
                Task.Delay(100).Wait();
                rgbHexEdit.SetText(settings.BackgroundColor);
                Task.Delay(100).Wait();
                button.Click();

                // Set the Spotlight color
                var spotlightColor = foundCustom.Find<Group>("Spotlight color");
                Assert.IsNotNull(spotlightColor);

                var spotlightColorButton = spotlightColor.Find<Button>(By.XPath(".//Button"));
                Assert.IsNotNull(spotlightColorButton);
                spotlightColorButton.Click();

                var spotlightColorPopupWindow = Session.Find<Window>("Popup");
                Assert.IsNotNull(spotlightColorPopupWindow);
                var spotlightColorModelComboBox = this.Find<ComboBox>("Color model");
                Assert.IsNotNull(spotlightColorModelComboBox);
                spotlightColorModelComboBox.Click();
                var selectedItem2 = spotlightColorModelComboBox.Find<NavigationViewItem>("RGB");
                Assert.IsNotNull(selectedItem2);
                selectedItem2.Click();
                Task.Delay(100).Wait();
                var rgbHexEdit2 = this.Find<TextBox>("RGB hex");
                Assert.IsNotNull(rgbHexEdit2);
                Task.Delay(100).Wait();
                rgbHexEdit2.SetText(settings.SpotlightColor);
                Task.Delay(100).Wait();
                spotlightColorButton.Click(false, 500, 1500);

                // Set the overlay opacity to overlayOpacity%
                var overlayOpacitySlider = foundCustom.Find<Slider>("Overlay opacity (%)");
                Assert.IsNotNull(overlayOpacitySlider);
                Assert.IsNotNull(settings.OverlayOpacity);
                int overlayOpacityValue = int.Parse(settings.OverlayOpacity, CultureInfo.InvariantCulture);
                overlayOpacitySlider.QuickSetValue(overlayOpacityValue);
                Assert.AreEqual(settings.OverlayOpacity, overlayOpacitySlider.Text);
                Task.Delay(1000).Wait();

                // Set the Fade Initial zoom to 0
                var spotlightInitialZoomSlider = foundCustom.Find<Slider>("Spotlight initial zoom");
                Assert.IsNotNull(spotlightInitialZoomSlider);
                Task.Delay(1000).Wait();
                spotlightInitialZoomSlider.QuickSetValue(int.Parse(settings.InitialZoom, CultureInfo.InvariantCulture));
                Assert.AreEqual(settings.InitialZoom, spotlightInitialZoomSlider.Text);
                Task.Delay(1000).Wait();

                //// Change the edit value
                var spotlightRadiusEdit = foundCustom.Find<TextBox>("Spotlight radius (px) Minimum5");
                Assert.IsNotNull(spotlightRadiusEdit);
                Task.Delay(1000).Wait();
                spotlightRadiusEdit.SetText(settings.Radius);
                Assert.AreEqual(settings.Radius, spotlightRadiusEdit.Text);
                Task.Delay(1000).Wait();

                // Set the duration to 0 ms
                var spotlightAnimationDuration = foundCustom.Find<TextBox>("Animation duration (ms) Minimum0");
                Assert.IsNotNull(spotlightAnimationDuration);
                Task.Delay(1000).Wait();
                spotlightAnimationDuration.SetText(settings.AnimationDuration);
                Assert.AreEqual(settings.AnimationDuration, spotlightAnimationDuration.Text);
                Task.Delay(1000).Wait();

                // groupAppearanceBehavior.Click();
            }
            else
            {
                Assert.Fail("Appearance & behavior group not found.");
            }
        }

        private bool CheckAnimationEnable(ref Custom? foundCustom)
        {
            Assert.IsNotNull(foundCustom, "Find My Mouse group not found.");
            var foundElements = foundCustom.FindAll<TextBlock>("Animations are disabled in your system settings.");

            // Assert.IsNull(animationDisabledWarning);
            if (foundElements.Count != 0)
            {
                var openSettingsLink = foundCustom.Find<Element>("Open settings");
                Assert.IsNotNull(openSettingsLink);
                openSettingsLink.Click(false, 500, 3000);

                string settingsWindow = "Settings";
                this.Session.Attach(settingsWindow);
                var animationEffects = this.Find<ToggleSwitch>("Animation effects");
                Assert.IsNotNull(animationEffects);
                animationEffects.Toggle(true);

                Task.Delay(2000).Wait();
                Session.SendKeys(Key.Alt, Key.F4);
                this.Session.Attach(PowerToysModule.PowerToysSettings);
                this.LaunchFromSetting(reload: true);
            }
            else
            {
                return false;
            }

            return true;
        }

        private void LaunchFromSetting(bool reload = false, bool launchAsAdmin = false)
        {
            // this.Session.Attach(PowerToysModule.PowerToysSettings);
            this.Session.SetMainWindowSize(WindowSize.Large);

            // Goto Hosts File Editor setting page
            if (this.FindAll<NavigationViewItem>("Mouse utilities", 10000).Count == 0)
            {
                // Expand Advanced list-group if needed
                this.Find<NavigationViewItem>("Input / Output").Click();
            }

            if (reload)
            {
                this.Find<NavigationViewItem>("Keyboard Manager").Click();
            }

            Task.Delay(1000).Wait();
            this.Find<NavigationViewItem>("Mouse utilities").Click();
        }
    }
}
