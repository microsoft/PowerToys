// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest;

namespace ScreenRuler.UITests
{
    public static class TestHelper
    {
        private static readonly string[] ShortcutSeparators = { " + ", "+", " " };

        // Button automation names from Resources.resw
        public const string BoundsButtonName = "Bounds (Ctrl+1)";
        public const string SpacingButtonName = "Spacing (Ctrl+2)";
        public const string HorizontalSpacingButtonName = "Horizontal spacing (Ctrl+3)";
        public const string VerticalSpacingButtonName = "Vertical spacing (Ctrl+4)";
        public const string CloseButtonName = "Close (Esc)";

        /// <summary>
        /// Navigate to the Screen Ruler (Measure Tool) settings page
        /// </summary>
        /// <param name="testBase">The test base instance</param>
        public static void LaunchFromSetting(UITestBase testBase)
        {
            // Use reflection to access protected Find method, or we need to make this method part of the UITestBase
            // For now, let's work with the public Session property
            var screenRulers = testBase.Session.FindAll<NavigationViewItem>(By.AccessibilityId("Shell_Nav_ScreenRuler"));

            if (screenRulers.Count == 0)
            {
                testBase.Session.Find<NavigationViewItem>(By.AccessibilityId("Shell_Nav_TopLevelSystemTools"), 500).Click(msPostAction: 500);
            }

            testBase.Session.Find<NavigationViewItem>(By.AccessibilityId("Shell_Nav_ScreenRuler"), 500).Click(msPostAction: 500);
        }

        /// <summary>
        /// Set the Screen Ruler toggle switch to the specified state
        /// </summary>
        /// <param name="testBase">The test base instance</param>
        /// <param name="enable">True to enable, false to disable</param>
        public static void SetScreenRulerToggle(UITestBase testBase, bool enable)
        {
            var toggleSwitch = testBase.Session.Find<ToggleSwitch>(By.AccessibilityId("Toggle_ScreenRuler"));
            if (toggleSwitch.IsOn != enable)
            {
                toggleSwitch.Click(msPostAction: 500);
            }
        }

        /// <summary>
        /// Read the current activation shortcut from the ShortcutControl
        /// </summary>
        /// <param name="testBase">The test base instance</param>
        /// <returns>Array of keys representing the activation shortcut</returns>
        public static Key[] ReadActivationShortcut(UITestBase testBase)
        {
            // Find the ShortcutControl in the ScreenRuler_ActivationShortcut settings card
            var shortcutCard = testBase.Session.Find<Element>(By.AccessibilityId("Shortcut_ScreenRuler"), 500);
            var shortcutButton = shortcutCard.Find<Element>(By.AccessibilityId("EditButton"), 500);

            var shortcutText = shortcutButton.HelpText;
            return ParseShortcutText(shortcutText);
        }

        /// <summary>
        /// Parse shortcut text like "Win + Ctrl + Shift + M" into Key array
        /// </summary>
        /// <param name="shortcutText">The shortcut text to parse</param>
        /// <returns>Array of keys</returns>
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
                switch (cleanPart)
                {
                    case "win":
                    case "windows":
                        keys.Add(Key.Win);
                        break;
                    case "ctrl":
                    case "control":
                        keys.Add(Key.Ctrl);
                        break;
                    case "shift":
                        keys.Add(Key.Shift);
                        break;
                    case "alt":
                        keys.Add(Key.Alt);
                        break;

                    // Add more key mappings as needed
                    default:
                        if (cleanPart.Length == 1 && char.IsLetter(cleanPart[0]))
                        {
                            // Try to parse single letter keys
                            var keyValue = (Key)Enum.Parse(typeof(Key), cleanPart.ToUpperInvariant());
                            keys.Add(keyValue);
                        }

                        break;
                }
            }

            return keys.Count > 0 ? keys.ToArray() : new Key[] { Key.Win, Key.Ctrl, Key.Shift, Key.M };
        }

        /// <summary>
        /// Check if ScreenRulerUI window is open
        /// </summary>
        /// <param name="testBase">The test base instance</param>
        /// <returns>True if the window is open</returns>
        public static bool IsScreenRulerUIOpen(UITestBase testBase)
        {
            if (testBase.IsWindowOpen("PowerToys.ScreenRuler"))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Wait for ScreenRulerUI to reach the specified state within the timeout
        /// </summary>
        /// <param name="testBase">The test base instance</param>
        /// <param name="shouldBeOpen">True to wait for the UI to appear, false to wait for it to disappear</param>
        /// <param name="timeoutMs">Timeout in milliseconds</param>
        /// <param name="pollingIntervalMs">Polling interval in milliseconds</param>
        /// <returns>True if ScreenRulerUI reached the expected state within timeout</returns>
        public static bool WaitForScreenRulerUIState(UITestBase testBase, bool shouldBeOpen, int timeoutMs = 5000, int pollingIntervalMs = 100)
        {
            var startTime = DateTime.Now;
            var timeout = TimeSpan.FromMilliseconds(timeoutMs);

            while (DateTime.Now - startTime < timeout)
            {
                bool isCurrentlyOpen = IsScreenRulerUIOpen(testBase);

                if (isCurrentlyOpen == shouldBeOpen)
                {
                    return true;
                }

                Task.Delay(pollingIntervalMs).Wait();
            }

            return false;
        }

        /// <summary>
        /// Wait for ScreenRulerUI to appear within the specified timeout
        /// </summary>
        /// <param name="testBase">The test base instance</param>
        /// <param name="timeoutMs">Timeout in milliseconds</param>
        /// <returns>True if ScreenRulerUI appeared within timeout</returns>
        public static bool WaitForScreenRulerUI(UITestBase testBase, int timeoutMs = 5000)
        {
            return WaitForScreenRulerUIState(testBase, shouldBeOpen: true, timeoutMs);
        }

        /// <summary>
        /// Wait for ScreenRulerUI to disappear within the specified timeout
        /// </summary>
        /// <param name="testBase">The test base instance</param>
        /// <param name="timeoutMs">Timeout in milliseconds</param>
        /// <returns>True if ScreenRulerUI disappeared within timeout</returns>
        public static bool WaitForScreenRulerUIToDisappear(UITestBase testBase, int timeoutMs = 5000)
        {
            return WaitForScreenRulerUIState(testBase, shouldBeOpen: false, timeoutMs);
        }

        /// <summary>
        /// Close ScreenRulerUI if it's open
        /// </summary>
        /// <param name="testBase">The test base instance</param>
        public static void CloseScreenRulerUI(UITestBase testBase)
        {
            if (IsScreenRulerUIOpen(testBase))
            {
                var closeButton = testBase.Session.Find<Button>(By.AccessibilityId("Button_Close"), 10000, true);
                if (closeButton != null)
                {
                    closeButton.Click();
                    return;
                }
            }
        }

        /// <summary>
        /// Get a specific ScreenRulerUI button by its automation name
        /// </summary>
        /// <param name="testBase">The test base instance</param>
        /// <param name="buttonName">The automation name of the button (e.g., "Bounds (Ctrl+1)")</param>
        /// <returns>The button element if found, null otherwise</returns>
        public static Element? GetScreenRulerButton(UITestBase testBase, string buttonName)
        {
            try
            {
                return testBase.Session.Find<Element>(By.Name(buttonName), 1000, true);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Click a specific ScreenRulerUI button by its automation name
        /// </summary>
        /// <param name="testBase">The test base instance</param>
        /// <param name="buttonName">The automation name of the button (e.g., "Bounds (Ctrl+1)")</param>
        /// <returns>True if the button was found and clicked, false otherwise</returns>
        public static bool ClickScreenRulerButton(UITestBase testBase, string buttonName)
        {
            var button = GetScreenRulerButton(testBase, buttonName);
            if (button != null)
            {
                button.Click();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Check if a specific ScreenRulerUI button is checked
        /// </summary>
        /// <param name="testBase">The test base instance</param>
        /// <param name="buttonName">The automation name of the button (e.g., "Bounds (Ctrl+1)")</param>
        /// <returns>True if the button is checked, false if unchecked or not found</returns>
        public static bool IsScreenRulerButtonChecked(UITestBase testBase, string buttonName)
        {
            var button = GetScreenRulerButton(testBase, buttonName);
            if (button != null)
            {
                return button.Selected;
            }

            return false;
        }
    }
}
