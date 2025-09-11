// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
                return new Key[] { Key.Win, Key.Ctrl, Key.Shift, Key.M };
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
            // TODO: Make sure in manual time mode

            // TODO: Update light time, ensure time is updated in settings

            // TODO: Update dark time, ensure time is update in settings
            Assert.Fail("Not implemented");
        }

        /// <summary>
        /// Perform a test for shortcut changing themes
        /// </summary>
        public static void PerformShortcutTest(UITestBase testBase)
        {
            // TODO: Make system mode is checked

            // TODO: Activate shortcut and check theme change before and after

            // TODO: Make sure apps mode is checked

            // TODO: Activate shortcut and check theme change before and after

            // TODO: Make sure nothing is checked

            // TODO: Activate shortcut and make sure nothing changes before and after
            Assert.Fail("Not implemented");
        }
    }
}
