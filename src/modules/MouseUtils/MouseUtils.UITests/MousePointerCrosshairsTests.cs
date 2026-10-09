// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Devices.Printers;

namespace MouseUtils.UITests
{
    [TestClass]
    public class MousePointerCrosshairsTests : UITestBase
    {
        [TestMethod("MouseUtils.MousePointerCrosshairs.EnableMousePointerCrosshairs")]
        [TestCategory("Mouse Utils #29")]
        [TestCategory("Mouse Utils #30")]
        [TestCategory("Mouse Utils #31")]
        public void TestEnableMousePointerCrosshairs()
        {
            LaunchFromSetting();

            var settings = new MousePointerCrosshairsSettings();
            settings.CrosshairsColor = "FF0000";
            settings.CrosshairsBorderColor = "FF0000";
            settings.Opacity = "100";
            settings.CenterRadius = "0";
            settings.Thickness = "20";
            settings.BorderSize = "0";
            settings.IsFixLength = false;
            settings.FixedLength = "1";

            var foundCustom = FindMouseUtilElement(MouseUtilsSettings.MouseUtils.FindMyMouse);
            MouseUtilsSettings.SetMouseUtilEnabled(foundCustom, MouseUtilsSettings.MouseUtils.FindMyMouse, false);

            foundCustom = FindMouseUtilElement(MouseUtilsSettings.MouseUtils.MouseHighlighter);
            MouseUtilsSettings.SetMouseUtilEnabled(foundCustom, MouseUtilsSettings.MouseUtils.MouseHighlighter, true);
            MouseUtilsSettings.SetMouseUtilEnabled(foundCustom, MouseUtilsSettings.MouseUtils.MouseHighlighter, false);

            for (int i = 0; i < 10; i++)
            {
                Session.PerformMouseAction(MouseActionType.ScrollDown);
            }

            foundCustom = FindMouseUtilElement(MouseUtilsSettings.MouseUtils.MousePointerCrosshairs);

            MouseUtilsSettings.SetMouseUtilEnabled(foundCustom, MouseUtilsSettings.MouseUtils.MousePointerCrosshairs, false);
            Task.Delay(500).Wait();
            MouseUtilsSettings.SetMouseUtilEnabled(foundCustom, MouseUtilsSettings.MouseUtils.MousePointerCrosshairs, true);

            Assert.IsNotNull(foundCustom);

            // [Test Case] Change activation shortcut and test it.
            var activationShortcutButton = foundCustom.Find<Button>("Activation shortcut");
            Assert.IsNotNull(activationShortcutButton);

            activationShortcutButton.Click(false, 500, 1000);
            var activationShortcutWindow = Session.Find<Window>("Activation shortcut");
            Assert.IsNotNull(activationShortcutWindow);

            // Invalid shortcut key
            Session.SendKeySequence(Key.H);

            var invalidShortcutText = activationShortcutWindow.Find<TextBlock>("Invalid shortcut");
            Assert.IsNotNull(invalidShortcutText);

            Session.SendKeys(Key.Win, Key.Alt, Key.A);

            var saveButton = activationShortcutWindow.Find<Button>("Save");
            Assert.IsNotNull(saveButton);
            saveButton.Click(false, 500, 1000);

            SetMousePointerCrosshairsAppearanceBehavior(ref foundCustom, ref settings);
            Task.Delay(500).Wait();

            // [Test Case]  Press the activation shortcut and verify the crosshairs appear, and that they follow the mouse around.
            var xy0 = Session.GetMousePosition();
            Session.MoveMouseTo(xy0.Item1 - 100, xy0.Item2);

            IOUtil.MouseClick();
            Task.Delay(500).Wait();
            Session.SendKeys(Key.Win, Key.Alt, Key.A);
            Task.Delay(1000).Wait();

            xy0 = Session.GetMousePosition();

            VerifyMousePointerCrosshairsAppears(ref settings);
            Task.Delay(500).Wait();

            for (int i = 0; i < 100; i++)
            {
                IOUtil.MoveMouseBy(-1, 0);
                Task.Delay(10).Wait();
            }

            VerifyMousePointerCrosshairsAppears(ref settings);

            // [Test Case] Press the activation shortcut again and verify the crosshairs disappear.
            Session.SendKeys(Key.Win, Key.Alt, Key.A);
            Task.Delay(1000).Wait();

            VerifyMousePointerCrosshairsNotAppears(ref settings);
            Task.Delay(500).Wait();

            // [Test Case] Disable Mouse Pointer Crosshairs and verify that the module is not activated when you press the activation shortcut.
            MouseUtilsSettings.SetMouseUtilEnabled(foundCustom, MouseUtilsSettings.MouseUtils.MousePointerCrosshairs, false);
            xy0 = Session.GetMousePosition();
            Session.MoveMouseTo(xy0.Item1 - 100, xy0.Item2);
            Session.PerformMouseAction(MouseActionType.LeftClick);
            Session.SendKeys(Key.Win, Key.Alt, Key.A);
            Task.Delay(1000).Wait();

            VerifyMousePointerCrosshairsNotAppears(ref settings);
        }

        [TestMethod("MouseUtils.MousePointerCrosshairs.MousePointerCrosshairsDifferentSettings")]
        [TestCategory("Mouse Utils #32")]
        [TestCategory("Mouse Utils #33")]
        public void TestMousePointerCrosshairsDifferentSettings()
        {
            LaunchFromSetting();

            var settings = new MousePointerCrosshairsSettings();
            settings.CrosshairsColor = "00FF00";
            settings.CrosshairsBorderColor = "00FF00";
            settings.Opacity = "100";
            settings.CenterRadius = "0";
            settings.Thickness = "20";
            settings.BorderSize = "0";
            settings.IsFixLength = false;
            settings.FixedLength = "1";

            var foundCustom = FindMouseUtilElement(MouseUtilsSettings.MouseUtils.FindMyMouse);
            MouseUtilsSettings.SetMouseUtilEnabled(foundCustom, MouseUtilsSettings.MouseUtils.FindMyMouse, false);

            foundCustom = FindMouseUtilElement(MouseUtilsSettings.MouseUtils.MouseHighlighter);
            MouseUtilsSettings.SetMouseUtilEnabled(foundCustom, MouseUtilsSettings.MouseUtils.MouseHighlighter, true);
            MouseUtilsSettings.SetMouseUtilEnabled(foundCustom, MouseUtilsSettings.MouseUtils.MouseHighlighter, false);

            for (int i = 0; i < 10; i++)
            {
                Session.PerformMouseAction(MouseActionType.ScrollDown);
            }

            foundCustom = FindMouseUtilElement(MouseUtilsSettings.MouseUtils.MousePointerCrosshairs);

            // this.FindGroup("Enable Mouse Pointer Crosshairs");
            MouseUtilsSettings.SetMouseUtilEnabled(foundCustom, MouseUtilsSettings.MouseUtils.MousePointerCrosshairs, false);
            Task.Delay(500).Wait();
            MouseUtilsSettings.SetMouseUtilEnabled(foundCustom, MouseUtilsSettings.MouseUtils.MousePointerCrosshairs, true);

            Assert.IsNotNull(foundCustom);

            // [Test Case] Change activation shortcut and test it.
            var activationShortcutButton = foundCustom.Find<Button>("Activation shortcut");
            Assert.IsNotNull(activationShortcutButton);

            activationShortcutButton.Click(false, 500, 1000);
            var activationShortcutWindow = Session.Find<Window>("Activation shortcut");
            Assert.IsNotNull(activationShortcutWindow);

            // Invalid shortcut key
            Session.SendKeySequence(Key.H);

            var invalidShortcutText = activationShortcutWindow.Find<TextBlock>("Invalid shortcut");
            Assert.IsNotNull(invalidShortcutText);

            Session.SendKeys(Key.Win, Key.Alt, Key.P);

            var saveButton = activationShortcutWindow.Find<Button>("Save");
            Assert.IsNotNull(saveButton);
            saveButton.Click(false, 500, 1000);

            SetMousePointerCrosshairsAppearanceBehavior(ref foundCustom, ref settings);
            Task.Delay(500).Wait();

            // [Test Case]  Test the different settings and verify they apply - Change activation shortcut and test it.
            // [Test Case]  Test the different settings and verify they apply - Crosshairs color.
            var xy0 = Session.GetMousePosition();
            Session.MoveMouseTo(xy0.Item1 - 100, xy0.Item2);

            IOUtil.MouseClick();
            Task.Delay(500).Wait();
            Session.SendKeys(Key.Win, Key.Alt, Key.P);
            Task.Delay(1000).Wait();

            xy0 = Session.GetMousePosition();

            VerifyMousePointerCrosshairsAppears(ref settings);
            Task.Delay(500).Wait();

            for (int i = 0; i < 100; i++)
            {
                IOUtil.MoveMouseBy(-1, 0);
                Task.Delay(10).Wait();
            }

            VerifyMousePointerCrosshairsAppears(ref settings);

            // Press the activation shortcut again and verify the crosshairs disappear.
            Session.SendKeys(Key.Win, Key.Alt, Key.P);
            Task.Delay(1000).Wait();

            VerifyMousePointerCrosshairsNotAppears(ref settings);
        }

        private void VerifyMousePointerCrosshairsNotAppears(ref MousePointerCrosshairsSettings settings)
        {
            Task.Delay(500).Wait();
            string expectedColor = string.Empty;
            expectedColor = "#" + settings.CrosshairsColor;
            var location = Session.GetMousePosition();

            int radius = int.Parse(settings.CenterRadius, CultureInfo.InvariantCulture);

            var color = this.GetPixelColorString(location.Item1, location.Item2);
            Assert.AreNotEqual(expectedColor, color);
        }

        private void VerifyMousePointerCrosshairsAppears(ref MousePointerCrosshairsSettings settings)
        {
            Task.Delay(1000).Wait();
            string expectedColor = string.Empty;
            expectedColor = "#" + settings.CrosshairsColor;
            var location = Session.GetMousePosition();

            int radius = int.Parse(settings.CenterRadius, CultureInfo.InvariantCulture);

            var color = this.GetPixelColorString(location.Item1, location.Item2);
            Assert.AreEqual(expectedColor, color, "Center color check failed");

            var colorX = this.GetPixelColorString(location.Item1 + 50, location.Item2);
            Assert.AreEqual(expectedColor, colorX, "Center x + 50 color check failed");

            colorX = this.GetPixelColorString(location.Item1 - 50, location.Item2);
            Assert.AreEqual(expectedColor, colorX, "Center x - 50 color check failed");

            var colorY = this.GetPixelColorString(location.Item1, location.Item2 + 50);
            Assert.AreEqual(expectedColor, colorY, "Center y + 50 color check failed");

            colorY = this.GetPixelColorString(location.Item1, location.Item2 - 50);
            Assert.AreEqual(expectedColor, colorY, "Center y + 50 color check failed");
        }

        private void SetColor(ref Custom foundCustom, string colorName, string colorValue = "000000")
        {
            Assert.IsNotNull(foundCustom);
            var groupAppearanceBehavior = foundCustom.Find<Group>(By.AccessibilityId(MouseUtilsSettings.AccessibilityIds.MousePointerCrosshairsAppearanceBehavior));
            if (groupAppearanceBehavior != null)
            {
                // Set primary button highlight color
                var primaryButtonHighlightColor = foundCustom.Find<Group>(colorName);
                Assert.IsNotNull(primaryButtonHighlightColor);

                var button = primaryButtonHighlightColor.Find<Button>(By.XPath(".//Button"));
                Assert.IsNotNull(button);
                button.Click(false);
                var popupWindow = Session.Find<Window>("Popup");
                Assert.IsNotNull(popupWindow);
                var colorModelComboBox = this.Find<ComboBox>("Color model");
                Assert.IsNotNull(colorModelComboBox);
                colorModelComboBox.Click();
                var selectedItem = colorModelComboBox.Find<NavigationViewItem>("RGB");
                selectedItem.Click();
                var rgbHexEdit = this.Find<TextBox>("RGB hex");
                Assert.IsNotNull(rgbHexEdit);
                rgbHexEdit.SetText(colorValue);

                button.Click();
            }
        }

        private void SetMousePointerCrosshairsAppearanceBehavior(ref Custom foundCustom, ref MousePointerCrosshairsSettings settings)
        {
            Assert.IsNotNull(foundCustom);
            var groupAppearanceBehavior = foundCustom.Find<Group>(By.AccessibilityId(MouseUtilsSettings.AccessibilityIds.MousePointerCrosshairsAppearanceBehavior));
            if (groupAppearanceBehavior != null)
            {
                // groupAppearanceBehavior.Click();
                if (foundCustom.FindAll<TextBox>(settings.GetElementName(MousePointerCrosshairsSettings.SettingsUIElements.ThicknessEdit)).Count == 0)
                {
                    groupAppearanceBehavior.Click();
                    Session.PerformMouseAction(MouseActionType.ScrollDown);
                    Session.PerformMouseAction(MouseActionType.ScrollDown);
                    Session.PerformMouseAction(MouseActionType.ScrollDown);
                }

                // Set the crosshairs color
                SetColor(ref foundCustom, settings.GetElementName(MousePointerCrosshairsSettings.SettingsUIElements.CrosshairsColorGroup), settings.CrosshairsColor);

                // Set the crosshairs border color
                SetColor(ref foundCustom, settings.GetElementName(MousePointerCrosshairsSettings.SettingsUIElements.CrosshairsBorderColorGroup), settings.CrosshairsBorderColor);

                // Set the duration to duration ms
                var opacitySlider = foundCustom.Find<Slider>(settings.GetElementName(MousePointerCrosshairsSettings.SettingsUIElements.OpacitySlider));
                Assert.IsNotNull(opacitySlider);
                Assert.IsNotNull(settings.Opacity);
                int opacityValue = int.Parse(settings.Opacity, CultureInfo.InvariantCulture);
                opacitySlider.QuickSetValue(opacityValue);
                Assert.AreEqual(settings.Opacity, opacitySlider.Text);
                Task.Delay(500).Wait();

                // Set the center radius (px)
                var centerRadiusEdit = foundCustom.Find<TextBox>(settings.GetElementName(MousePointerCrosshairsSettings.SettingsUIElements.CenterRadiusEdit));
                Assert.IsNotNull(centerRadiusEdit);
                centerRadiusEdit.SetText(settings.CenterRadius);
                Assert.AreEqual(settings.CenterRadius, centerRadiusEdit.Text);

                // Set the thickness (px)
                var thicknessEdit = foundCustom.Find<TextBox>(settings.GetElementName(MousePointerCrosshairsSettings.SettingsUIElements.ThicknessEdit));
                Assert.IsNotNull(thicknessEdit);
                thicknessEdit.SetText(settings.Thickness);
                Assert.AreEqual(settings.Thickness, thicknessEdit.Text);

                // Set the border size (px)
                var borderSizeEdit = foundCustom.Find<TextBox>(settings.GetElementName(MousePointerCrosshairsSettings.SettingsUIElements.BorderSizeEdit));
                Assert.IsNotNull(borderSizeEdit);
                borderSizeEdit.SetText(settings.BorderSize);
                Assert.AreEqual(settings.BorderSize, borderSizeEdit.Text);

                // Set the fixed length (px)
                var isFixedLength = foundCustom.Find<ToggleSwitch>(settings.GetElementName(MousePointerCrosshairsSettings.SettingsUIElements.IsFixLengthToggle));
                Assert.IsNotNull(isFixedLength);
                isFixedLength.Toggle(settings.IsFixLength);
                Assert.AreEqual(settings.IsFixLength, isFixedLength.IsOn);
                if (settings.IsFixLength)
                {
                    var fixedLengthEdit = foundCustom.Find<TextBox>(settings.GetElementName(MousePointerCrosshairsSettings.SettingsUIElements.FixedLengthEdit));
                    Assert.IsNotNull(fixedLengthEdit);
                    fixedLengthEdit.SetText(settings.FixedLength);
                    Assert.AreEqual(settings.FixedLength, fixedLengthEdit.Text);
                }
            }
            else
            {
                Assert.Fail("MousePointerCrosshairs Appearance & behavior group not found.");
            }
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

        public Custom? FindMouseUtilElement(MouseUtilsSettings.MouseUtils element)
        {
            string accessibilityId = element switch
            {
                MouseUtilsSettings.MouseUtils.FindMyMouse => MouseUtilsSettings.AccessibilityIds.FindMyMouse,
                MouseUtilsSettings.MouseUtils.MouseHighlighter => MouseUtilsSettings.AccessibilityIds.MouseHighlighter,
                MouseUtilsSettings.MouseUtils.MousePointerCrosshairs => MouseUtilsSettings.AccessibilityIds.MousePointerCrosshairs,
                MouseUtilsSettings.MouseUtils.MouseJump => MouseUtilsSettings.AccessibilityIds.MouseJump,
                _ => throw new ArgumentException($"Unknown MouseUtils element: {element}"),
            };

            var foundCustom = this.Find<Custom>(By.AccessibilityId(accessibilityId));
            for (int i = 0; i < 20; i++)
            {
                if (foundCustom != null)
                {
                    break;
                }

                Session.PerformMouseAction(MouseActionType.ScrollDown);
                foundCustom = this.Find<Custom>(By.AccessibilityId(accessibilityId));
            }

            return foundCustom;
        }

        private void LaunchFromSetting(bool showWarning = false, bool launchAsAdmin = false)
        {
            Session.SetMainWindowSize(WindowSize.Large);

            // Goto Mouse utilities setting page
            if (this.FindAll(By.AccessibilityId(MouseUtilsSettings.AccessibilityIds.MouseUtilitiesNavItem)).Count == 0)
            {
                // Expand Input / Output list-group if needed
                this.Find(By.AccessibilityId(MouseUtilsSettings.AccessibilityIds.InputOutputNavItem)).Click();
            }

            this.Find(By.AccessibilityId(MouseUtilsSettings.AccessibilityIds.MouseUtilitiesNavItem)).Click();
        }
    }
}
