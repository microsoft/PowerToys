// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel.DataTransfer;

namespace ScreenRuler.UITests
{
    [TestClass]
    public class TestBounds : UITestBase
    {
        public TestBounds()
            : base(PowerToysModule.PowerToysSettings, WindowSize.Large)
        {
        }

        [TestMethod("ScreenRuler.BoundsTool")]
        [TestCategory("Bounds")]
        public void TestScreenRulerBoundsTool()
        {
            TestHelper.LaunchFromSetting(this);

            // Ensure Screen Ruler is enabled for the test
            TestHelper.SetScreenRulerToggle(this, enable: true);
            Assert.IsTrue(
                Find<ToggleSwitch>(By.AccessibilityId("Toggle_ScreenRuler")).IsOn,
                "Screen Ruler toggle switch should be ON for bounds test");

            // Read the current activation shortcut
            var activationKeys = TestHelper.ReadActivationShortcut(this);
            Assert.IsNotNull(activationKeys, "Should be able to read activation shortcut");
            Assert.IsTrue(activationKeys.Length > 0, "Activation shortcut should contain at least one key");

            // Clear clipboard before test
            ClearClipboard();

            // Test 1: Press the activation shortcut and verify the toolbar appears
            SendKeys(activationKeys);
            bool screenRulerAppeared = TestHelper.WaitForScreenRulerUI(this, 2000);
            Assert.IsTrue(
                screenRulerAppeared,
                $"ScreenRulerUI should appear after pressing activation shortcut: {string.Join(" + ", activationKeys)}");

            // Test 2: Find and click the bounds button
            var boundsButton = TestHelper.GetScreenRulerButton(this, TestHelper.BoundsButtonId, 10000);
            Assert.IsNotNull(boundsButton, "Bounds button should be found");

            boundsButton.Click();
            Task.Delay(500).Wait(); // Wait for bounds mode to activate

            // Test 3: Get current mouse position and move 200px lower
            var currentPos = GetMousePosition();
            int startX = currentPos.Item1;
            int startY = currentPos.Item2 + 200;

            // Move to starting position
            MoveMouseTo(startX, startY);
            Task.Delay(200).Wait();

            // Test 4: Perform drag operation to create 100x100 box
            // Start drag (left mouse down)
            Session.PerformMouseAction(MouseActionType.LeftDown);
            Task.Delay(100).Wait();

            // Drag diagonally to create 100x100 box
            int endX = startX + 99;
            int endY = startY + 99;
            MoveMouseTo(endX, endY);
            Task.Delay(200).Wait();

            // End drag (left mouse up)
            Session.PerformMouseAction(MouseActionType.LeftUp);
            Task.Delay(500).Wait(); // Wait for measurement to be calculated and copied to clipboard

            // Test 5: Right click to dismiss the current bounds selection
            Session.PerformMouseAction(MouseActionType.RightClick);
            Task.Delay(500).Wait();

            // Test 6: Check clipboard content
            string clipboardText = GetClipboardText();
            Assert.IsFalse(string.IsNullOrEmpty(clipboardText), "Clipboard should contain measurement data");

            // The clipboard should contain "100 × 100"
            // Check for the dimensions in various possible formats
            bool containsExpectedDimensions = clipboardText.Contains("100 × 100");

            Assert.IsTrue(
                containsExpectedDimensions,
                $"Clipboard should contain '100 × 100', but contained: '{clipboardText}'");

            // Test 7: Close Screen Ruler UI
            TestHelper.CloseScreenRulerUI(this);
            bool screenRulerClosed = TestHelper.WaitForScreenRulerUIToDisappear(this, 2000);
            Assert.IsTrue(
                screenRulerClosed,
                "ScreenRulerUI should close after calling CloseScreenRulerUI");

            // Clean up - ensure ScreenRulerUI is closed
            TestHelper.CloseScreenRulerUI(this);
        }

        /// <summary>
        /// Clear the clipboard content using modern Windows API
        /// </summary>
        private void ClearClipboard()
        {
            try
            {
                // Use STA thread for clipboard access
                var staThread = new Thread(() =>
                {
                    try
                    {
                        System.Windows.Forms.Clipboard.Clear();
                    }
                    catch (Exception)
                    {
                        // Ignore clipboard errors during clearing
                    }
                });
                staThread.SetApartmentState(ApartmentState.STA);
                staThread.Start();
                staThread.Join();
            }
            catch (Exception)
            {
                // Ignore clipboard errors during clearing
            }
        }

        /// <summary>
        /// Get text content from clipboard using multiple approaches
        /// </summary>
        /// <returns>Clipboard text content or empty string if not available</returns>
        private string GetClipboardText()
        {
            string result = string.Empty;

            try
            {
                // Use STA thread for clipboard access
                var staThread = new Thread(() =>
                {
                    try
                    {
                        if (System.Windows.Forms.Clipboard.ContainsText())
                        {
                            result = System.Windows.Forms.Clipboard.GetText();
                        }
                    }
                    catch (Exception)
                    {
                        // result remains empty
                    }
                });

                staThread.SetApartmentState(ApartmentState.STA);
                staThread.Start();
                staThread.Join(TimeSpan.FromSeconds(5)); // 5 second timeout
            }
            catch (Exception)
            {
                // result remains empty
            }

            return result ?? string.Empty;
        }
    }
}
