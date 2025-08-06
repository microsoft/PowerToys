// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScreenRuler.UITests
{
    public static class TestHelper
    {
        private static readonly string[] ShortcutSeparators = { " + ", "+", " " };

        // Button automation names from Resources.resw
        public const string BoundsButtonId = "Button_Bounds";
        public const string SpacingButtonName = "Button_Spacing";
        public const string HorizontalSpacingButtonName = "Button_SpacingHorizontal";
        public const string VerticalSpacingButtonName = "Button_SpacingVertical";
        public const string CloseButtonId = "Button_Close";

        /// <summary>
        /// Performs common test initialization: navigate to settings, enable toggle, verify shortcut
        /// </summary>
        /// <param name="testBase">The test base instance</param>
        /// <param name="testName">Name of the test for assertions</param>
        /// <returns>The activation keys for the test</returns>
        public static Key[] InitializeTest(UITestBase testBase, string testName)
        {
            LaunchFromSetting(testBase);
            SetScreenRulerToggle(testBase, enable: true);

            var toggleSwitch = testBase.Session.Find<ToggleSwitch>(By.AccessibilityId("Toggle_ScreenRuler"), 5000);
            Assert.IsTrue(
                toggleSwitch.IsOn,
                $"Screen Ruler toggle switch should be ON for {testName}");

            var activationKeys = ReadActivationShortcut(testBase);
            Assert.IsNotNull(activationKeys, "Should be able to read activation shortcut");
            Assert.IsTrue(activationKeys.Length > 0, "Activation shortcut should contain at least one key");

            return activationKeys;
        }

        /// <summary>
        /// Performs common test cleanup: close ScreenRuler UI
        /// </summary>
        /// <param name="testBase">The test base instance</param>
        public static void CleanupTest(UITestBase testBase)
        {
            CloseScreenRulerUI(testBase);
        }

        /// <summary>
        /// Navigate to the Screen Ruler (Measure Tool) settings page
        /// </summary>
        public static void LaunchFromSetting(UITestBase testBase)
        {
            var screenRulers = testBase.Session.FindAll<NavigationViewItem>(By.AccessibilityId("Shell_Nav_ScreenRuler"));

            if (screenRulers.Count == 0)
            {
                testBase.Session.Find<NavigationViewItem>(By.AccessibilityId("Shell_Nav_TopLevelSystemTools"), 5000).Click(msPostAction: 500);
            }

            testBase.Session.Find<NavigationViewItem>(By.AccessibilityId("Shell_Nav_ScreenRuler"), 5000).Click(msPostAction: 500);
        }

        /// <summary>
        /// Set the Screen Ruler toggle switch to the specified state
        /// </summary>
        public static void SetScreenRulerToggle(UITestBase testBase, bool enable)
        {
            var toggleSwitch = testBase.Session.Find<ToggleSwitch>(By.AccessibilityId("Toggle_ScreenRuler"), 5000);
            if (toggleSwitch.IsOn != enable)
            {
                toggleSwitch.Click(msPostAction: 2000);
            }
        }

        /// <summary>
        /// Set the Screen Ruler toggle and verify its state
        /// </summary>
        /// <param name="testBase">The test base instance</param>
        /// <param name="enable">True to enable, false to disable</param>
        /// <param name="testName">Name of the test for assertion messages</param>
        public static void SetAndVerifyScreenRulerToggle(UITestBase testBase, bool enable, string testName)
        {
            SetScreenRulerToggle(testBase, enable);
            var toggleSwitch = testBase.Session.Find<ToggleSwitch>(By.AccessibilityId("Toggle_ScreenRuler"), 5000);
            Assert.AreEqual(
                enable,
                toggleSwitch.IsOn,
                $"Screen Ruler toggle switch should be {(enable ? "ON" : "OFF")} for {testName}");
        }

        /// <summary>
        /// Read the current activation shortcut from the ShortcutControl
        /// </summary>
        public static Key[] ReadActivationShortcut(UITestBase testBase)
        {
            var shortcutCard = testBase.Session.Find<Element>(By.AccessibilityId("Shortcut_ScreenRuler"), 5000);
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
                    _ when cleanPart.Length == 1 && char.IsLetter(cleanPart[0]) =>
                        (Key)Enum.Parse(typeof(Key), cleanPart.ToUpperInvariant()),
                    _ => (Key?)null,
                };

                if (key.HasValue)
                {
                    keys.Add(key.Value);
                }
            }

            return keys.Count > 0 ? keys.ToArray() : new Key[] { Key.Win, Key.Ctrl, Key.Shift, Key.M };
        }

        /// <summary>
        /// Check if ScreenRulerUI window is open
        /// </summary>
        public static bool IsScreenRulerUIOpen(UITestBase testBase) => testBase.IsWindowOpen("PowerToys.ScreenRuler");

        /// <summary>
        /// Wait for ScreenRulerUI to reach the specified state within the timeout
        /// </summary>
        public static bool WaitForScreenRulerUIState(UITestBase testBase, bool shouldBeOpen, int timeoutMs = 5000, int pollingIntervalMs = 100)
        {
            var endTime = DateTime.Now.AddMilliseconds(timeoutMs);

            while (DateTime.Now < endTime)
            {
                if (IsScreenRulerUIOpen(testBase) == shouldBeOpen)
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
        public static bool WaitForScreenRulerUI(UITestBase testBase, int timeoutMs = 5000) =>
            WaitForScreenRulerUIState(testBase, shouldBeOpen: true, timeoutMs);

        /// <summary>
        /// Wait for ScreenRulerUI to disappear within the specified timeout
        /// </summary>
        public static bool WaitForScreenRulerUIToDisappear(UITestBase testBase, int timeoutMs = 5000) =>
            WaitForScreenRulerUIState(testBase, shouldBeOpen: false, timeoutMs);

        /// <summary>
        /// Close ScreenRulerUI if it's open
        /// </summary>
        public static void CloseScreenRulerUI(UITestBase testBase)
        {
            if (IsScreenRulerUIOpen(testBase))
            {
                var closeButton = GetScreenRulerButton(testBase, CloseButtonId, 10000);
                closeButton?.Click();
            }
        }

        /// <summary>
        /// Get a specific ScreenRulerUI button by its automation name
        /// </summary>
        public static Element? GetScreenRulerButton(UITestBase testBase, string buttonName, int timeoutMs = 1000)
        {
            try
            {
                return testBase.Session.Find<Element>(By.AccessibilityId(buttonName), timeoutMs, true);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Clear the clipboard content using STA thread
        /// </summary>
        public static void ClearClipboard()
        {
            ExecuteInSTAThread(() => System.Windows.Forms.Clipboard.Clear());
        }

        /// <summary>
        /// Get text content from clipboard using STA thread
        /// </summary>
        public static string GetClipboardText()
        {
            string result = string.Empty;
            ExecuteInSTAThread(() =>
            {
                if (System.Windows.Forms.Clipboard.ContainsText())
                {
                    result = System.Windows.Forms.Clipboard.GetText();
                }
            });
            return result ?? string.Empty;
        }

        /// <summary>
        /// Execute an action in an STA thread with error handling
        /// </summary>
        private static void ExecuteInSTAThread(Action action)
        {
            try
            {
                var staThread = new Thread(() =>
                {
                    try
                    {
                        action();
                    }
                    catch
                    {
                        // Ignore clipboard errors
                    }
                });

                staThread.SetApartmentState(ApartmentState.STA);
                staThread.Start();
                staThread.Join(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Ignore clipboard errors
            }
        }

        /// <summary>
        /// Validate clipboard content contains valid spacing measurement for the specified type
        /// </summary>
        public static bool ValidateSpacingClipboardContent(string clipboardText, string spacingType)
        {
            if (string.IsNullOrEmpty(clipboardText))
            {
                return false;
            }

            return spacingType switch
            {
                "Spacing" => Regex.IsMatch(clipboardText, @"\d+\s*[×x]\s*\d+"),
                "Horizontal Spacing" or "Vertical Spacing" => Regex.IsMatch(clipboardText, @"^\d+$"),
                _ => false,
            };
        }

        /// <summary>
        /// Perform a complete spacing tool test operation
        /// </summary>
        public static void PerformSpacingToolTest(UITestBase testBase, string buttonId, string testName)
        {
            ClearClipboard();

            // Launch ScreenRuler UI
            var activationKeys = ReadActivationShortcut(testBase);
            testBase.SendKeys(activationKeys);

            Assert.IsTrue(
                WaitForScreenRulerUI(testBase, 2000),
                $"ScreenRulerUI should appear after pressing activation shortcut for {testName}: {string.Join(" + ", activationKeys)}");

            // Click spacing button
            var spacingButton = GetScreenRulerButton(testBase, buttonId, 10000);
            Assert.IsNotNull(spacingButton, $"{testName} button should be found");

            spacingButton!.Click();
            Task.Delay(500).Wait();

            // Perform measurement action
            PerformMeasurementAction(testBase);

            // Validate results
            ValidateClipboardResults(testName);

            // Cleanup
            CloseScreenRulerUI(testBase);
            Assert.IsTrue(
                WaitForScreenRulerUIToDisappear(testBase, 2000),
                $"{testName}: ScreenRulerUI should close after calling CloseScreenRulerUI");
        }

        /// <summary>
        /// Perform a bounds tool test operation
        /// </summary>
        public static void PerformBoundsToolTest(UITestBase testBase)
        {
            ClearClipboard();

            var activationKeys = ReadActivationShortcut(testBase);
            testBase.SendKeys(activationKeys);

            Assert.IsTrue(
                WaitForScreenRulerUI(testBase, 2000),
                $"ScreenRulerUI should appear after pressing activation shortcut: {string.Join(" + ", activationKeys)}");

            // Click bounds button
            var boundsButton = GetScreenRulerButton(testBase, BoundsButtonId, 10000);
            Assert.IsNotNull(boundsButton, "Bounds button should be found");

            boundsButton.Click();
            Task.Delay(500).Wait();

            // Perform drag operation to create 100x100 box
            var currentPos = testBase.GetMousePosition();
            int startX = currentPos.Item1;
            int startY = currentPos.Item2 + 200;

            testBase.MoveMouseTo(startX, startY);
            Task.Delay(200).Wait();

            // Drag operation
            testBase.Session.PerformMouseAction(MouseActionType.LeftDown);
            Task.Delay(100).Wait();

            testBase.MoveMouseTo(startX + 99, startY + 99);
            Task.Delay(200).Wait();

            testBase.Session.PerformMouseAction(MouseActionType.LeftUp);
            Task.Delay(500).Wait();

            // Dismiss selection
            testBase.Session.PerformMouseAction(MouseActionType.RightClick);
            Task.Delay(500).Wait();

            // Validate results
            string clipboardText = GetClipboardText();
            Assert.IsFalse(string.IsNullOrEmpty(clipboardText), "Clipboard should contain measurement data");
            Assert.IsTrue(
                clipboardText.Contains("100 × 100"),
                $"Clipboard should contain '100 × 100', but contained: '{clipboardText}'");

            // Cleanup
            CloseScreenRulerUI(testBase);
            Assert.IsTrue(
                WaitForScreenRulerUIToDisappear(testBase, 2000),
                "ScreenRulerUI should close after calling CloseScreenRulerUI");
        }

        /// <summary>
        /// Perform a measurement action (move mouse and click)
        /// </summary>
        private static void PerformMeasurementAction(UITestBase testBase)
        {
            var currentPos = testBase.GetMousePosition();
            int startX = currentPos.Item1;
            int startY = currentPos.Item2 + 200;

            testBase.MoveMouseTo(startX, startY);
            Task.Delay(200).Wait();

            testBase.Session.PerformMouseAction(MouseActionType.LeftClick);
            Task.Delay(500).Wait();

            testBase.Session.PerformMouseAction(MouseActionType.RightClick);
            Task.Delay(500).Wait();
        }

        /// <summary>
        /// Validate clipboard results for spacing tests
        /// </summary>
        private static void ValidateClipboardResults(string testName)
        {
            string clipboardText = GetClipboardText();
            Assert.IsFalse(string.IsNullOrEmpty(clipboardText), $"{testName}: Clipboard should contain measurement data");

            bool containsValidPattern = ValidateSpacingClipboardContent(clipboardText, testName);
            Assert.IsTrue(
                containsValidPattern,
                $"{testName}: Clipboard should contain valid spacing measurement, but contained: '{clipboardText}'");
        }
    }
}
