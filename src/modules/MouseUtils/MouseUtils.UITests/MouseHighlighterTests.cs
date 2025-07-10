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
    public class MouseHighlighterTests : UITestBase
    {
        [TestMethod("MouseUtils.MouseHighlighter.EnableMouseHighlighter")]
        [TestCategory("Mouse Utils #17")]
        [TestCategory("Mouse Utils #18")]
        [TestCategory("Mouse Utils #19")]
        [TestCategory("Mouse Utils #20")]
        [TestCategory("Mouse Utils #21")]
        public void TestEnableMouseHighlighter()
        {
            LaunchFromSetting();
            var foundCustom0 = this.Find<Custom>("Find My Mouse");
            if (foundCustom0 != null)
            {
                foundCustom0.Find<ToggleSwitch>("Enable Find My Mouse").Toggle(false);
            }
            else
            {
                Assert.Fail("Find My Mouse custom not found.");
            }

            var settings = new MouseHighlighterSettings();
            settings.PrimaryButtonHighlightColor = "FFFF0000";
            settings.SecondaryButtonHighlightColor = "FF00FF00";
            settings.AlwaysHighlightColor = "004cFF71";
            settings.Radius = "50";
            settings.FadeDelay = "0";
            settings.FadeDuration = "90";

            var foundCustom = this.Find<Custom>("Mouse Highlighter");
            if (foundCustom != null)
            {
                foundCustom.Find<ToggleSwitch>("Enable Mouse Highlighter").Toggle(true);
                foundCustom.Find<ToggleSwitch>("Enable Mouse Highlighter").Toggle(false);

                var xy = Session.GetMousePosition();
                Session.MoveMouseTo(xy.Item1, xy.Item2 - 100);

                Session.PerformMouseAction(MouseActionType.ScrollDown);
                Session.PerformMouseAction(MouseActionType.ScrollDown);
                Session.PerformMouseAction(MouseActionType.ScrollDown);
                foundCustom.Find<ToggleSwitch>("Enable Mouse Highlighter").Toggle(true);

                // Change the shortcut key for MouseHighlighter
                // [TestCase]Change activation shortcut and test it
                var activationShortcutButton = foundCustom.Find<Button>("Activation shortcut");
                Assert.IsNotNull(activationShortcutButton);

                activationShortcutButton.Click();
                Task.Delay(500).Wait();
                var activationShortcutWindow = Session.Find<Window>("Activation shortcut");
                Assert.IsNotNull(activationShortcutWindow);

                // Invalid shortcut key
                Session.SendKeySequence(Key.H);

                // IOUtil.SimulateKeyPress(0x41);
                var invalidShortcutText = activationShortcutWindow.Find<TextBlock>("Invalid shortcut");
                Assert.IsNotNull(invalidShortcutText);

                // IOUtil.SimulateShortcut(0x5B, 0x10, 0x45);
                Session.SendKeys(Key.Win, Key.Shift, Key.H);

                // Assert.IsNull(activationShortcutWindow.Find<TextBlock>("Invalid shortcut"));
                var saveButton = activationShortcutWindow.Find<Button>("Save");
                Assert.IsNotNull(saveButton);
                saveButton.Click(false, 500, 1000);

                SetMouseHighlighterAppearanceBehavior(ref foundCustom, ref settings);

                var xy0 = Session.GetMousePosition();
                Session.MoveMouseTo(xy0.Item1 - 100, xy0.Item2);
                Session.PerformMouseAction(MouseActionType.LeftClick);

                // Check the mouse highlighter is enabled
                Session.SendKeys(Key.Win, Key.Shift, Key.H);

                // IOUtil.SimulateShortcut(0x5B, 0x10, 0x45);
                Task.Delay(1000).Wait();

                // MouseSimulator.LeftClick();
                // [Test Case] Press the activation shortcut and press left and right click somewhere, verifying the highlights are applied.
                // [Test Case] Press the activation shortcut again and verify no highlights appear when the mouse buttons are clicked.
                VerifyMouseHighlighterAppears(ref settings, "leftClick");
                VerifyMouseHighlighterAppears(ref settings, "rightClick");

                // Disable mouse highlighter
                Session.SendKeys(Key.Win, Key.Shift, Key.H);
                Task.Delay(1000).Wait();

                VerifyMouseHighlighterNotAppears(ref settings, "leftClick");
                VerifyMouseHighlighterNotAppears(ref settings, "rightClick");

                // [Test Case] Disable Mouse Highlighter and verify that the module is not activated when you press the activation shortcut.
                foundCustom.Find<ToggleSwitch>("Enable Mouse Highlighter").Toggle(false);
                xy = Session.GetMousePosition();
                Session.MoveMouseTo(xy.Item1 - 100, xy.Item2);

                Session.SendKeys(Key.Win, Key.Shift, Key.H);
                Task.Delay(1000).Wait();

                VerifyMouseHighlighterNotAppears(ref settings, "leftClick");
                VerifyMouseHighlighterNotAppears(ref settings, "rightClick");

                // [Test Case] With left mouse button pressed, drag the mouse and verify the highlight is dragged with the pointer.
                // [Test Case] With right mouse button pressed, drag the mouse and verify the highlight is dragged with the pointer.
                foundCustom.Find<ToggleSwitch>("Enable Mouse Highlighter").Toggle(true);
                xy = Session.GetMousePosition();
                Session.MoveMouseTo(xy.Item1 - 100, xy.Item2);

                Session.SendKeys(Key.Win, Key.Shift, Key.H);
                Task.Delay(1000).Wait();
                VerifyMouseHighlighterDrag(ref settings, "leftClick");
                VerifyMouseHighlighterDrag(ref settings, "rightClick");
            }
            else
            {
                Assert.Fail("Mouse Highlighter Custom not found.");
            }

            Task.Delay(500).Wait();
        }

        [TestMethod("MouseUtils.MouseHighlighter.MouseHighlighterDifferentSettings")]
        [TestCategory("Mouse Utils #22")]
        [TestCategory("Mouse Utils #23")]
        [TestCategory("Mouse Utils #24")]
        public void TestMouseHighlighterDifferentSettings()
        {
            LaunchFromSetting();
            var foundCustom0 = this.Find<Custom>("Find My Mouse");
            if (foundCustom0 != null)
            {
                foundCustom0.Find<ToggleSwitch>("Enable Find My Mouse").Toggle(false);
            }
            else
            {
                Assert.Fail("Find My Mouse custom not found.");
            }

            var settings = new MouseHighlighterSettings();
            settings.PrimaryButtonHighlightColor = "FF000000";
            settings.SecondaryButtonHighlightColor = "FFFFFFFF";
            settings.AlwaysHighlightColor = "004cFF71";
            settings.Radius = "70";
            settings.FadeDelay = "0";
            settings.FadeDuration = "90";

            var foundCustom = this.Find<Custom>("Mouse Highlighter");
            if (foundCustom != null)
            {
                foundCustom.Find<ToggleSwitch>("Enable Mouse Highlighter").Toggle(true);
                foundCustom.Find<ToggleSwitch>("Enable Mouse Highlighter").Toggle(false);

                var xy = Session.GetMousePosition();
                Session.MoveMouseTo(xy.Item1, xy.Item2 - 100);

                Session.PerformMouseAction(MouseActionType.ScrollDown);
                Session.PerformMouseAction(MouseActionType.ScrollDown);
                Session.PerformMouseAction(MouseActionType.ScrollDown);
                foundCustom.Find<ToggleSwitch>("Enable Mouse Highlighter").Toggle(true);

                // Change the shortcut key for MouseHighlighter
                // [TestCase] Test the different settings and verify they apply - Change activation shortcut and test it
                // [Test Case] Test the different settings and verify they apply - Left button highlight color
                // [Test Case] Test the different settings and verify they apply - Right button highlight color
                // [Test Case] Test the different settings and verify they apply - Radius
                var activationShortcutButton = foundCustom.Find<Button>("Activation shortcut");
                Assert.IsNotNull(activationShortcutButton);

                activationShortcutButton.Click();
                Task.Delay(500).Wait();
                var activationShortcutWindow = Session.Find<Window>("Activation shortcut");
                Assert.IsNotNull(activationShortcutWindow);

                // Invalid shortcut key
                Session.SendKeySequence(Key.H);

                // IOUtil.SimulateKeyPress(0x41);
                var invalidShortcutText = activationShortcutWindow.Find<TextBlock>("Invalid shortcut");
                Assert.IsNotNull(invalidShortcutText);

                // IOUtil.SimulateShortcut(0x5B, 0x10, 0x45);
                Session.SendKeys(Key.Win, Key.Shift, Key.O);

                // Assert.IsNull(activationShortcutWindow.Find<TextBlock>("Invalid shortcut"));
                var saveButton = activationShortcutWindow.Find<Button>("Save");
                Assert.IsNotNull(saveButton);
                saveButton.Click(false, 500, 1000);

                SetMouseHighlighterAppearanceBehavior(ref foundCustom, ref settings);

                var xy0 = Session.GetMousePosition();
                Session.MoveMouseTo(xy0.Item1 - 100, xy0.Item2);
                Session.PerformMouseAction(MouseActionType.LeftClick);

                // Check the mouse highlighter is enabled
                Session.SendKeys(Key.Win, Key.Shift, Key.O);

                Task.Delay(1000).Wait();

                VerifyMouseHighlighterAppears(ref settings, "leftClick");
                VerifyMouseHighlighterAppears(ref settings, "rightClick");
            }
            else
            {
                Assert.Fail("Mouse Highlighter Custom not found.");
            }

            Task.Delay(500).Wait();
        }

        private void VerifyMouseHighlighterDrag(ref MouseHighlighterSettings settings, string action = "leftClick")
        {
            Task.Delay(500).Wait();
            string expectedColor = string.Empty;
            if (action == "leftClick")
            {
                IOUtil.SimulateMouseDown(true);
                expectedColor = settings.PrimaryButtonHighlightColor.Substring(2);
            }
            else if (action == "rightClick")
            {
                IOUtil.SimulateMouseDown(false);
                expectedColor = settings.SecondaryButtonHighlightColor.Substring(2);
            }
            else
            {
                Assert.Fail("Invalid action specified.");
            }

            expectedColor = "#" + expectedColor;
            Task.Delay(100).Wait();
            var location = Session.GetMousePosition();
            int radius = int.Parse(settings.Radius, CultureInfo.InvariantCulture);
            var colorLeftClick = this.GetPixelColorString(location.Item1, location.Item2);
            Assert.AreEqual(expectedColor, colorLeftClick);

            var colorLeftClick2 = this.GetPixelColorString(location.Item1 + radius - 1, location.Item2);

            Assert.AreEqual(expectedColor, colorLeftClick2);

            var colorBackground = this.GetPixelColorString(location.Item1 + radius + 50, location.Item2 + radius + 50);
            Assert.AreNotEqual(expectedColor, colorBackground);

            // Drag the mouse
            // Session.MoveMouseTo(location.Item1 - 400, location.Item2);
            for (int i = 0; i < 500; i++)
            {
                IOUtil.MoveMouseBy(-1, 0);
                Task.Delay(10).Wait();
            }

            Task.Delay(2000).Wait();

            location = Session.GetMousePosition();
            colorLeftClick = this.GetPixelColorString(location.Item1, location.Item2);
            Assert.AreEqual(expectedColor, colorLeftClick);

            colorLeftClick2 = this.GetPixelColorString(location.Item1 + radius - 1, location.Item2);

            Assert.AreEqual(expectedColor, colorLeftClick2);

            colorBackground = this.GetPixelColorString(location.Item1 + radius + 50, location.Item2 + radius + 50);
            Assert.AreNotEqual(expectedColor, colorBackground);

            if (action == "leftClick")
            {
                IOUtil.SimulateMouseUp(true);
            }
            else if (action == "rightClick")
            {
                IOUtil.SimulateMouseUp(false);
            }

            int duration = int.Parse(settings.FadeDuration, CultureInfo.InvariantCulture);
            Task.Delay(duration + 100).Wait();
            colorLeftClick = this.GetPixelColorString(location.Item1, location.Item2);
            Assert.AreNotEqual("#" + settings.PrimaryButtonHighlightColor, colorLeftClick);
        }

        private void VerifyMouseHighlighterNotAppears(ref MouseHighlighterSettings settings, string action = "leftClick")
        {
            Task.Delay(500).Wait();
            string expectedColor = string.Empty;
            if (action == "leftClick")
            {
                // MouseSimulator.LeftDown();
                Session.PerformMouseAction(MouseActionType.LeftDown);
                expectedColor = settings.PrimaryButtonHighlightColor.Substring(2);
            }
            else if (action == "rightClick")
            {
                // MouseSimulator.RightDown();
                Session.PerformMouseAction(MouseActionType.RightDown);
                expectedColor = settings.SecondaryButtonHighlightColor.Substring(2);
            }
            else
            {
                Assert.Fail("Invalid action specified.");
            }

            expectedColor = "#" + expectedColor;
            var location = Session.GetMousePosition();
            int radius = int.Parse(settings.Radius, CultureInfo.InvariantCulture);
            var colorLeftClick = this.GetPixelColorString(location.Item1, location.Item2);
            Assert.AreNotEqual(expectedColor, colorLeftClick);
            var colorLeftClick2 = this.GetPixelColorString(location.Item1 + radius - 1, location.Item2);
            Assert.AreNotEqual(expectedColor, colorLeftClick2);
            if (action == "leftClick")
            {
                Session.PerformMouseAction(MouseActionType.LeftUp);
            }
            else if (action == "rightClick")
            {
                Session.PerformMouseAction(MouseActionType.RightUp);
            }
        }

        private void VerifyMouseHighlighterAppears(ref MouseHighlighterSettings settings, string action = "leftClick")
        {
            Task.Delay(500).Wait();
            string expectedColor = string.Empty;
            if (action == "leftClick")
            {
                // MouseSimulator.LeftDown();
                Session.PerformMouseAction(MouseActionType.LeftDown);
                expectedColor = settings.PrimaryButtonHighlightColor.Substring(2);
            }
            else if (action == "rightClick")
            {
                // MouseSimulator.RightDown();
                Session.PerformMouseAction(MouseActionType.RightDown);
                expectedColor = settings.SecondaryButtonHighlightColor.Substring(2);
            }
            else
            {
                Assert.Fail("Invalid action specified.");
            }

            expectedColor = "#" + expectedColor;

            var location = Session.GetMousePosition();
            int radius = int.Parse(settings.Radius, CultureInfo.InvariantCulture);
            var colorLeftClick = this.GetPixelColorString(location.Item1, location.Item2);
            Assert.AreEqual(expectedColor, colorLeftClick);

            var colorLeftClick2 = this.GetPixelColorString(location.Item1 + radius - 1, location.Item2);

            Assert.AreEqual(expectedColor, colorLeftClick2);
            Task.Delay(500).Wait();

            var colorBackground = this.GetPixelColorString(location.Item1 + radius + 50, location.Item2 + radius + 50);
            Assert.AreNotEqual(expectedColor, colorBackground);
            if (action == "leftClick")
            {
                // MouseSimulator.LeftUp();
                Session.PerformMouseAction(MouseActionType.LeftUp);
            }
            else if (action == "rightClick")
            {
                // MouseSimulator.RightUp();
                Session.PerformMouseAction(MouseActionType.RightUp);
            }

            int duration = int.Parse(settings.FadeDuration, CultureInfo.InvariantCulture);
            Task.Delay(duration + 100).Wait();
            colorLeftClick = this.GetPixelColorString(location.Item1, location.Item2);
            Assert.AreNotEqual("#" + settings.PrimaryButtonHighlightColor, colorLeftClick);
        }

        private void SetColor(ref Custom foundCustom, string colorName = "Primary button highlight color", string colorValue = "000000", string opacity = "0")
        {
            Assert.IsNotNull(foundCustom);
            var groupAppearanceBehavior = foundCustom.Find<TextBlock>("Appearance & behavior");
            if (groupAppearanceBehavior != null)
            {
                if (foundCustom.FindAll<TextBox>("Fade duration (ms) Minimum0").Count == 0)
                {
                    groupAppearanceBehavior.Click();
                }

                // Set primary button highlight color
                var primaryButtonHighlightColor = foundCustom.Find<Group>(colorName);
                Assert.IsNotNull(primaryButtonHighlightColor);

                var button = primaryButtonHighlightColor.Find<Button>(By.XPath(".//Button"));
                Assert.IsNotNull(button);
                button.Click(false, 500, 700);

                var popupWindow = Session.Find<Window>("Popup");
                Assert.IsNotNull(popupWindow);
                var colorModelComboBox = this.Find<ComboBox>("Color model");
                Assert.IsNotNull(colorModelComboBox);
                colorModelComboBox.Click(false, 500, 700);
                var selectedItem = colorModelComboBox.Find<NavigationViewItem>("RGB");
                selectedItem.Click();
                var rgbHexEdit = this.Find<TextBox>("RGB hex");
                Assert.IsNotNull(rgbHexEdit);
                Task.Delay(500).Wait();
                rgbHexEdit.SetText(colorValue);
                int retry = 5;
                while (retry > 0)
                {
                    Task.Delay(500).Wait();
                    rgbHexEdit.SetText(colorValue);
                    Task.Delay(500).Wait();
                    string rgbHex = rgbHexEdit.Text;
                    bool isValid = rgbHex.StartsWith('#') && rgbHex.Length == 9 && rgbHex.Substring(1) == colorValue;
                    Task.Delay(500).Wait();
                    if (isValid)
                    {
                        break;
                    }

                    retry--;
                }

                Task.Delay(500).Wait();
                button.Click();
            }
        }

        private void SetMouseHighlighterAppearanceBehavior(ref Custom foundCustom, ref MouseHighlighterSettings settings)
        {
            Assert.IsNotNull(foundCustom);
            var groupAppearanceBehavior = foundCustom.Find<TextBlock>("Appearance & behavior");
            if (groupAppearanceBehavior != null)
            {
                // groupAppearanceBehavior.Click();
                if (foundCustom.FindAll<TextBox>(settings.GetElementName(MouseHighlighterSettings.SettingsUIElements.FadeDurationEdit)).Count == 0)
                {
                    groupAppearanceBehavior.Click();
                }

                // Set primary button highlight color
                SetColor(ref foundCustom, settings.GetElementName(MouseHighlighterSettings.SettingsUIElements.PrimaryButtonHighlightColorGroup), settings.PrimaryButtonHighlightColor);

                // Set secondary button highlight color
                SetColor(ref foundCustom, settings.GetElementName(MouseHighlighterSettings.SettingsUIElements.SecondaryButtonHighlightColorGroup), settings.SecondaryButtonHighlightColor);

                // Set the duration to duration ms
                var fadeDurationEdit = foundCustom.Find<TextBox>(settings.GetElementName(MouseHighlighterSettings.SettingsUIElements.FadeDurationEdit));
                Assert.IsNotNull(fadeDurationEdit);
                fadeDurationEdit.SetText(settings.FadeDuration);
                Assert.AreEqual(settings.FadeDuration, fadeDurationEdit.Text);

                // Set Fade delay(ms)
                var fadeDelayEdit = foundCustom.Find<TextBox>(settings.GetElementName(MouseHighlighterSettings.SettingsUIElements.FadeDelayEdit));
                Assert.IsNotNull(fadeDelayEdit);
                fadeDelayEdit.SetText(settings.FadeDelay);
                Assert.AreEqual(settings.FadeDelay, fadeDelayEdit.Text);

                // Set the fade radius (px)
                var fadeRadiusEdit = foundCustom.Find<TextBox>(settings.GetElementName(MouseHighlighterSettings.SettingsUIElements.RadiusEdit));
                Assert.IsNotNull(fadeRadiusEdit);
                fadeRadiusEdit.SetText(settings.Radius);
                Assert.AreEqual(settings.Radius, fadeRadiusEdit.Text);

                // Set always highlight color
                SetColor(ref foundCustom, settings.GetElementName(MouseHighlighterSettings.SettingsUIElements.AlwaysHighlightColorGroup), settings.AlwaysHighlightColor);
            }
            else
            {
                Assert.Fail("Appearance & behavior group not found.");
            }
        }

        private void LaunchFromSetting(bool showWarning = false, bool launchAsAdmin = false)
        {
            this.Session.SetMainWindowSize(WindowSize.Large);

            // Goto Hosts File Editor setting page
            if (this.FindAll<NavigationViewItem>("Mouse utilities").Count == 0)
            {
                // Expand Advanced list-group if needed
                this.Find<NavigationViewItem>("Input / Output").Click();
            }

            this.Find<NavigationViewItem>("Mouse utilities").Click();
        }
    }
}
