// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;

namespace LightSwitch.UITests
{
    internal sealed class TestHelper
    {
        private static readonly string[] ShortcutSeparators = { " + ", "+", " " };

        /// <summary>
        /// Performs common test initialization: navigate to settings, enable toggle, verify shortcut
        /// </summary>
        /// <param name="testBase">The test base instance</param>
        /// <param name="testName">Name of the test for assertions</param>
        /// <returns>The activation keys for the test</returns>
        public static Key[] InitializeTest(UITestBase testBase, string testName)
        {
            LaunchFromSetting(testBase);

            var toggleSwitch = SetLightSwitchToggle(testBase, enable: true);
            Assert.IsTrue(
                toggleSwitch.IsOn,
                $"Light Switch toggle switch should be ON for {testName}");

            var activationKeys = ReadActivationShortcut(testBase);
            Assert.IsNotNull(activationKeys, "Should be able to read activation shortcut");
            Assert.IsTrue(activationKeys.Length > 0, "Activation shortcut should contain at least one key");

            return activationKeys;
        }

        /// <summary>
        /// Navigate to the Light Switch settings page
        /// </summary>
        public static void LaunchFromSetting(UITestBase testBase)
        {
            var lightSwitch = testBase.Session.FindAll<NavigationViewItem>(By.AccessibilityId("LightSwitchNavItem"));

            if (lightSwitch.Count == 0)
            {
                testBase.Session.Find<NavigationViewItem>(By.AccessibilityId("SystemToolsNavItem"), 5000).Click(msPostAction: 500);
            }

            testBase.Session.Find<NavigationViewItem>(By.AccessibilityId("LightSwitchNavItem"), 5000).Click(msPostAction: 500);
        }

        /// <summary>
        /// Set the Light Switch enable toggle switch to the specified state
        /// </summary>
        public static ToggleSwitch SetLightSwitchToggle(UITestBase testBase, bool enable)
        {
            var toggleSwitch = testBase.Session.Find<ToggleSwitch>(By.AccessibilityId("Toggle_LightSwitch"), 5000);

            if (toggleSwitch.IsOn != enable)
            {
                toggleSwitch.Click(msPreAction: 1000, msPostAction: 2000);
            }

            if (toggleSwitch.IsOn != enable)
            {
                testBase.Session.SendKey(Key.Space, msPreAction: 0, msPostAction: 2000);
            }

            return toggleSwitch;
        }

        /// <summary>
        /// Read the current activation shortcut from the ShortcutControl
        /// </summary>
        public static Key[] ReadActivationShortcut(UITestBase testBase)
        {
            var shortcutCard = testBase.Session.Find<Element>(By.AccessibilityId("Shortcut_LightSwitch"), 5000);
            var shortcutButton = shortcutCard.Find<Element>(By.AccessibilityId("EditButton"), 5000);
            return ParseShortcutText(shortcutButton.HelpText);
        }

        /// <summary>
        /// Parse shortcut text like "Win + Ctrl + Shift + M" into Key array
        /// </summary>
        private static Key[] ParseShortcutText(string shortcutText)
        {
            if (string.IsNullOrEmpty(shortcutText))
            {
                return new Key[] { Key.Win, Key.Ctrl, Key.Shift, Key.D };
            }

            var keys = new List<Key>();
            var parts = shortcutText.Split(ShortcutSeparators, StringSplitOptions.RemoveEmptyEntries);

            foreach (var part in parts)
            {
                var cleanPart = part.Trim().ToLowerInvariant();
                var key = cleanPart switch
                {
                    "win" or "windows" => Key.Win,
                    "ctrl" or "control" => Key.Ctrl,
                    "shift" => Key.Shift,
                    "alt" => Key.Alt,
                    _ when cleanPart.Length == 1 && char.IsLetter(cleanPart[0]) &&
                         cleanPart[0] >= 'a' && cleanPart[0] <= 'z' =>
                        (Key)Enum.Parse(typeof(Key), cleanPart.ToUpperInvariant()),
                    _ => (Key?)null,
                };

                if (key.HasValue)
                {
                    keys.Add(key.Value);
                }
            }

            return keys.Count > 0 ? keys.ToArray() : new Key[] { Key.Win, Key.Ctrl, Key.Shift, Key.D };
        }

        /// <summary>
        /// Performs common test cleanup: close LightSwitch task
        /// </summary>
        /// <param name="testBase">The test base instance</param>
        public static void CleanupTest(UITestBase testBase)
        {
            // TODO: Make sure the task kills?
            // CloseLightSwitch(testBase);

            // Ensure we're attached to settings after cleanup
            try
            {
                testBase.Session.Attach(PowerToysModule.PowerToysSettings);
            }
            catch
            {
                // Ignore attachment errors - this is just cleanup
            }
        }

        /// <summary>
        /// Perform a update time test operation
        /// </summary>
        public static void PerformUpdateTimeTest(UITestBase testBase)
        {
            // Make sure in manual mode
            var modeCombobox = testBase.Session.Find<Element>(By.AccessibilityId("ModeSelection_LightSwitch"), 5000);
            Assert.IsNotNull(modeCombobox, "Mode combobox not found.");

            var neededTabs = 6;

            if (modeCombobox.Text != "Manual")
            {
                modeCombobox.Click();
                var manualListItem = testBase.Session.Find<Element>(By.AccessibilityId("ManualCBItem_LightSwitch"), 5000);
                Assert.IsNotNull(manualListItem, "Manual combobox item not found.");
                manualListItem.Click();
                neededTabs = 1;
            }

            Assert.AreEqual("Manual", modeCombobox.Text, "Mode combobox should be set to Manual.");

            var timeline = testBase.Session.Find<Element>(By.AccessibilityId("Timeline_LightSwitch"), 5000);
            Assert.IsNotNull(timeline, "Timeline not found.");

            var helpText = timeline.GetAttribute("HelpText");
            string originalEndValue = GetHelpTextValue(helpText, "End");

            for (int i = 0; i < neededTabs; i++)
            {
                testBase.Session.SendKeys(Key.Tab);
            }

            testBase.Session.SendKeys(Key.Enter);
            testBase.Session.SendKeys(Key.Up);
            testBase.Session.SendKeys(Key.Enter);

            helpText = timeline.GetAttribute("HelpText");
            string updatedEndValue = GetHelpTextValue(helpText, "End");

            Assert.AreNotEqual(originalEndValue, updatedEndValue, "Timeline end time should have been updated.");

            helpText = timeline.GetAttribute("HelpText");
            string originalStartValue = GetHelpTextValue(helpText, "Start");

            testBase.Session.SendKeys(Key.Tab);
            testBase.Session.SendKeys(Key.Enter);
            testBase.Session.SendKeys(Key.Up);
            testBase.Session.SendKeys(Key.Enter);

            helpText = timeline.GetAttribute("HelpText");
            string updatedStartValue = GetHelpTextValue(helpText, "Start");

            Assert.AreNotEqual(originalStartValue, updatedStartValue, "Timeline start time should have been updated.");
        }

        /// <summary>
        /// Perform a update geolocation test operation
        /// </summary>
        public static void PerformUserSelectedLocationTest(UITestBase testBase)
        {
            // Make sure in sun time mode
            var modeCombobox = testBase.Session.Find<Element>(By.AccessibilityId("ModeSelection_LightSwitch"), 5000);
            Assert.IsNotNull(modeCombobox, "Mode combobox not found.");

            if (modeCombobox.Text != "Sunset to sunrise")
            {
                modeCombobox.Click();
                var sunriseListItem = testBase.Session.Find<Element>(By.AccessibilityId("SunCBItem_LightSwitch"), 5000);
                Assert.IsNotNull(sunriseListItem, "Sunrise combobox item not found.");
                sunriseListItem.Click();
            }

            Assert.AreEqual("Sunset to sunrise", modeCombobox.Text, "Mode combobox should be set to Sunset to sunrise.");

            var setLocationButton = testBase.Session.Find<Element>(By.AccessibilityId("SetLocationButton_LightSwitch"), 5000);
            Assert.IsNotNull(setLocationButton, "Set location button not found.");
            setLocationButton.Click();

            var autoSuggestTextbox = testBase.Session.Find<Element>(By.AccessibilityId("CitySearchBox_LightSwitch"), 5000);
            Assert.IsNotNull(autoSuggestTextbox, "City search box not found.");
            autoSuggestTextbox.Click();
            autoSuggestTextbox.SendKeys("Seattle");
            autoSuggestTextbox.SendKeys(OpenQA.Selenium.Keys.Down);
            autoSuggestTextbox.SendKeys(OpenQA.Selenium.Keys.Enter);

            var latLong = testBase.Session.Find<Element>(By.AccessibilityId("LocationResultText_LightSwitch"), 5000);
            Assert.IsFalse(string.IsNullOrWhiteSpace(latLong.Text));

            var sunrise = testBase.Session.Find<Element>(By.AccessibilityId("SunriseText_LightSwitch"), 5000);
            Assert.IsFalse(string.IsNullOrWhiteSpace(sunrise.Text));

            var sunset = testBase.Session.Find<Element>(By.AccessibilityId("SunsetText_LightSwitch"), 5000);
            Assert.IsFalse(string.IsNullOrWhiteSpace(sunset.Text));
        }

        /// <summary>
        /// Perform a update geolocation test operation
        /// </summary>
        public static void PerformGeolocationTest(UITestBase testBase)
        {
            // Make sure in sun time mode
            var modeCombobox = testBase.Session.Find<Element>(By.AccessibilityId("ModeSelection_LightSwitch"), 5000);
            Assert.IsNotNull(modeCombobox, "Mode combobox not found.");

            if (modeCombobox.Text != "Sunset to sunrise")
            {
                modeCombobox.Click();
                var sunriseListItem = testBase.Session.Find<Element>(By.AccessibilityId("SunCBItem_LightSwitch"), 5000);
                Assert.IsNotNull(sunriseListItem, "Sunrise combobox item not found.");
                sunriseListItem.Click();
            }

            Assert.AreEqual("Sunset to sunrise", modeCombobox.Text, "Mode combobox should be set to Sunset to sunrise.");

            // Click the select city button
            var setLocationButton = testBase.Session.Find<Element>(By.AccessibilityId("SetLocationButton_LightSwitch"), 5000);
            Assert.IsNotNull(setLocationButton, "Set location button not found.");
            setLocationButton.Click(msPostAction: 8000);

            var latLong = testBase.Session.Find<Element>(By.AccessibilityId("LocationResultText_LightSwitch"), 5000);
            Assert.IsFalse(string.IsNullOrWhiteSpace(latLong.Text));

            var sunrise = testBase.Session.Find<Element>(By.AccessibilityId("SunriseText_LightSwitch"), 5000);
            Assert.IsFalse(string.IsNullOrWhiteSpace(sunrise.Text));

            var sunset = testBase.Session.Find<Element>(By.AccessibilityId("SunsetText_LightSwitch"), 5000);
            Assert.IsFalse(string.IsNullOrWhiteSpace(sunset.Text));
        }

        /// <summary>
        /// Perform a update time test operation
        /// </summary>
        public static void PerformOffsetTest(UITestBase testBase)
        {
            // Make sure in sun time mode
            var modeCombobox = testBase.Session.Find<Element>(By.AccessibilityId("ModeSelection_LightSwitch"), 5000);
            Assert.IsNotNull(modeCombobox, "Mode combobox not found.");

            if (modeCombobox.Text != "Sunset to sunrise")
            {
                modeCombobox.Click();
                var sunriseListItem = testBase.Session.Find<Element>(By.AccessibilityId("SunCBItem_LightSwitch"), 5000);
                Assert.IsNotNull(sunriseListItem, "Sunrise combobox item not found.");
                sunriseListItem.Click();
            }

            Assert.AreEqual("Sunset to sunrise", modeCombobox.Text, "Mode combobox should be set to Sunset to sunrise.");

            // Testing sunrise offset
            var sunriseOffset = testBase.Session.Find<Element>(By.AccessibilityId("SunriseOffset_LightSwitch"), 5000);
            Assert.IsNotNull(sunriseOffset, "Sunrise offset number box not found.");

            var timeline = testBase.Session.Find<Element>(By.AccessibilityId("Timeline_LightSwitch"), 5000);
            Assert.IsNotNull(timeline, "Timeline not found.");

            var helpText = timeline.GetAttribute("HelpText");
            string originalStartValue = GetHelpTextValue(helpText, "Start");

            sunriseOffset.Click();
            testBase.Session.SendKeys(Key.Up);

            helpText = timeline.GetAttribute("HelpText");
            string updatedStartValue = GetHelpTextValue(helpText, "Start");

            Assert.AreNotEqual(originalStartValue, updatedStartValue, "Timeline start time should have been updated.");

            // Testing sunset offset
            var sunsetOffset = testBase.Session.Find<Element>(By.AccessibilityId("SunsetOffset_LightSwitch"), 5000);
            Assert.IsNotNull(sunsetOffset, "Sunrise offset number box not found.");

            helpText = timeline.GetAttribute("HelpText");
            string originalEndValue = GetHelpTextValue(helpText, "End");

            sunsetOffset.Click();
            testBase.Session.SendKeys(Key.Up);

            helpText = timeline.GetAttribute("HelpText");
            string updatedEndValue = GetHelpTextValue(helpText, "End");

            Assert.AreNotEqual(originalEndValue, updatedEndValue, "Timeline end time should have been updated.");
        }

        /// <summary>
        /// Perform a test for shortcut changing themes
        /// </summary>
        public static void PerformShortcutTest(UITestBase testBase, Key[] activationKeys)
        {
            // Test when both are checked
            var systemCheckbox = testBase.Session.Find<Element>(By.AccessibilityId("ChangeSystemCheckbox_LightSwitch"), 5000);
            Assert.IsNotNull(systemCheckbox, "System checkbox not found.");

            var scrollViewer = testBase.Session.Find<Element>(By.AccessibilityId("PageScrollViewer"));
            systemCheckbox.EnsureVisible(scrollViewer);

            int neededTabs = 10;

            if (!systemCheckbox.Selected)
            {
                for (int i = 0; i < neededTabs; i++)
                {
                    testBase.Session.SendKeys(Key.Tab);
                }

                systemCheckbox.Click();
            }

            Assert.IsTrue(systemCheckbox.Selected, "System checkbox should be checked.");

            var appsCheckbox = testBase.Session.Find<Element>(By.AccessibilityId("ChangeAppsCheckbox_LightSwitch"), 5000);
            Assert.IsNotNull(appsCheckbox, "Apps checkbox not found.");

            if (!appsCheckbox.Selected)
            {
                appsCheckbox.Click();
            }

            Assert.IsTrue(appsCheckbox.Selected, "Apps checkbox should be checked.");

            var systemBeforeValue = GetSystemTheme();
            var appsBeforeValue = GetAppsTheme();

            testBase.Session.SendKeys(activationKeys);
            Task.Delay(5000).Wait();

            var systemAfterValue = GetSystemTheme();
            var appsAfterValue = GetAppsTheme();

            Assert.AreNotEqual(systemBeforeValue, systemAfterValue, "System theme should have changed.");
            Assert.AreNotEqual(appsBeforeValue, appsAfterValue, "Apps theme should have changed.");

            // Test with nothing checked
            if (systemCheckbox.Selected)
            {
                systemCheckbox.Click();
            }

            if (appsCheckbox.Selected)
            {
                appsCheckbox.Click();
            }

            Assert.IsFalse(systemCheckbox.Selected, "System checkbox should be unchecked.");
            Assert.IsFalse(appsCheckbox.Selected, "Apps checkbox should be unchecked.");

            var noneSystemBeforeValue = GetSystemTheme();
            var noneAppsBeforeValue = GetAppsTheme();

            testBase.Session.SendKeys(activationKeys);
            Task.Delay(5000).Wait();

            var noneSystemAfterValue = GetSystemTheme();
            var noneAppsAfterValue = GetAppsTheme();

            Assert.AreEqual(noneSystemBeforeValue, noneSystemAfterValue, "System theme should not have changed.");
            Assert.AreEqual(noneAppsBeforeValue, noneAppsAfterValue, "Apps theme should not have changed.");
        }

        /* Helpers */
        private static int GetSystemTheme()
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key is null)
            {
                return 1;
            }

            return (int)key.GetValue("SystemUsesLightTheme", 1);
        }

        private static int GetAppsTheme()
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            if (key is null)
            {
                return 1;
            }

            return (int)key.GetValue("AppsUseLightTheme", 1);
        }

        private static string GetHelpTextValue(string helpText, string key)
        {
            foreach (var part in helpText.Split(';'))
            {
                var kv = part.Split('=');
                if (kv.Length == 2 && kv[0] == key)
                {
                    return kv[1];
                }
            }

            return string.Empty;
        }
    }
}
