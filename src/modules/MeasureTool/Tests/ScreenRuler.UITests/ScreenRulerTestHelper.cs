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
    public static class ScreenRulerTestHelper
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
            // Find the ShortcutControl in the MeasureTool_ActivationShortcut settings card
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
                    case "m":
                        keys.Add(Key.M);
                        break;
                    case "h":
                        keys.Add(Key.H);
                        break;
                    case "r":
                        keys.Add(Key.R);
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
        /// Check if MeasureToolUI window is open
        /// </summary>
        /// <param name="testBase">The test base instance</param>
        /// <returns>True if the window is open</returns>
        public static bool IsMeasureToolUIOpen(UITestBase testBase)
        {
            // Try different possible window names for MeasureToolUI
            var possibleNames = new[]
            {
                "PowerToys.ScreenRuler",
            };

            foreach (var name in possibleNames)
            {
                if (testBase.IsWindowOpen(name))
                {
                    return true;
                }
            }

            // Alternative approach: try to find specific UI elements that would be present in MeasureToolUI
            try
            {
                // Look for elements with their actual automation names from Resources.resw
                var measureElements = testBase.Session.FindAll<Element>(By.Name(BoundsButtonName), 500, true);
                if (measureElements.Count > 0)
                {
                    return true;
                }

                measureElements = testBase.Session.FindAll<Element>(By.Name(SpacingButtonName), 500, true);
                if (measureElements.Count > 0)
                {
                    return true;
                }

                measureElements = testBase.Session.FindAll<Element>(By.Name(HorizontalSpacingButtonName), 500, true);
                if (measureElements.Count > 0)
                {
                    return true;
                }

                measureElements = testBase.Session.FindAll<Element>(By.Name(VerticalSpacingButtonName), 500, true);
                if (measureElements.Count > 0)
                {
                    return true;
                }

                // Also try looking for ToggleButton elements by class name since these are ToggleButtons in the UI
                measureElements = testBase.Session.FindAll<Element>(By.ClassName("ToggleButton"), 500, true);
                if (measureElements.Count > 0)
                {
                    // Check if any of the toggle buttons have the expected automation names
                    foreach (var element in measureElements)
                    {
                        try
                        {
                            var name = element.Name;
                            if (name.Contains("Bounds") || name.Contains("Spacing"))
                            {
                                return true;
                            }
                        }
                        catch
                        {
                            // Continue checking other elements
                        }
                    }
                }
            }
            catch
            {
                // Ignore exceptions when searching for elements
            }

            return false;
        }

        /// <summary>
        /// Wait for MeasureToolUI to appear within the specified timeout
        /// </summary>
        /// <param name="testBase">The test base instance</param>
        /// <param name="timeoutMs">Timeout in milliseconds</param>
        /// <returns>True if MeasureToolUI appeared within timeout</returns>
        public static bool WaitForMeasureToolUI(UITestBase testBase, int timeoutMs = 5000)
        {
            var startTime = DateTime.Now;
            var timeout = TimeSpan.FromMilliseconds(timeoutMs);

            while (DateTime.Now - startTime < timeout)
            {
                if (IsMeasureToolUIOpen(testBase))
                {
                    return true;
                }

                Task.Delay(200).Wait();
            }

            return false;
        }

        /// <summary>
        /// Wait for MeasureToolUI to disappear within the specified timeout
        /// </summary>
        /// <param name="testBase">The test base instance</param>
        /// <param name="timeoutMs">Timeout in milliseconds</param>
        /// <returns>True if MeasureToolUI disappeared within timeout</returns>
        public static bool WaitForMeasureToolUIToDisappear(UITestBase testBase, int timeoutMs = 5000)
        {
            var startTime = DateTime.Now;
            var timeout = TimeSpan.FromMilliseconds(timeoutMs);

            while (DateTime.Now - startTime < timeout)
            {
                if (!IsMeasureToolUIOpen(testBase))
                {
                    return true;
                }

                Task.Delay(200).Wait();
            }

            return false;
        }

        /// <summary>
        /// Close MeasureToolUI if it's open
        /// </summary>
        /// <param name="testBase">The test base instance</param>
        public static void CloseMeasureToolUI(UITestBase testBase)
        {
            if (IsMeasureToolUIOpen(testBase))
            {
                // Try to find and click the close button using the tooltip text
                try
                {
                    var closeButton = testBase.Session.Find<Button>(By.AccessibilityId("Button_Close"), 1000, true);
                    if (closeButton != null)
                    {
                        closeButton.Click();
                        return;
                    }
                }
                catch
                {
                    // Continue with other methods
                }

                // Try escape key
                testBase.SendKeys(Key.Esc);
                Task.Delay(500).Wait();

                // If still open, try Alt+F4
                if (IsMeasureToolUIOpen(testBase))
                {
                    testBase.SendKeys(Key.Alt, Key.F4);
                    Task.Delay(500).Wait();
                }
            }
        }

        /// <summary>
        /// Get a specific MeasureToolUI button by its automation name
        /// </summary>
        /// <param name="testBase">The test base instance</param>
        /// <param name="buttonName">The automation name of the button (e.g., "Bounds (Ctrl+1)")</param>
        /// <returns>The button element if found, null otherwise</returns>
        public static Element? GetMeasureToolButton(UITestBase testBase, string buttonName)
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
        /// Click a specific MeasureToolUI button by its automation name
        /// </summary>
        /// <param name="testBase">The test base instance</param>
        /// <param name="buttonName">The automation name of the button (e.g., "Bounds (Ctrl+1)")</param>
        /// <returns>True if the button was found and clicked, false otherwise</returns>
        public static bool ClickMeasureToolButton(UITestBase testBase, string buttonName)
        {
            var button = GetMeasureToolButton(testBase, buttonName);
            if (button != null)
            {
                button.Click();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Check if a specific MeasureToolUI button is checked
        /// </summary>
        /// <param name="testBase">The test base instance</param>
        /// <param name="buttonName">The automation name of the button (e.g., "Bounds (Ctrl+1)")</param>
        /// <returns>True if the button is checked, false if unchecked or not found</returns>
        public static bool IsMeasureToolButtonChecked(UITestBase testBase, string buttonName)
        {
            var button = GetMeasureToolButton(testBase, buttonName);
            if (button != null)
            {
                return button.Selected;
            }

            return false;
        }
    }
}
