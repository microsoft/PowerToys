// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseUtils.UITests
{
    [TestClass]
    public class MouseJumpTests : UITestBase
    {
        [TestMethod("MouseUtils.MouseJump.EnableMouseJump")]
        [TestCategory("Mouse Utils #39")]
        [TestCategory("Mouse Utils #40")]
        [TestCategory("Mouse Utils #41")]
        [TestCategory("Mouse Utils #45")]
        public void TestEnableMouseJump()
        {
            LaunchFromSetting(true);
        }

        [TestMethod("MouseUtils.MouseJump.EnableMouseJump2")]
        [TestCategory("Mouse Utils #39")]
        [TestCategory("Mouse Utils #41")]
        [TestCategory("Mouse Utils #45")]
        public void TestEnableMouseJump2()
        {
            LaunchFromSetting();
            var foundCustom0 = this.Find<Custom>(By.AccessibilityId(MouseUtilsSettings.AccessibilityIds.FindMyMouse));
            if (foundCustom0 != null)
            {
                foundCustom0.Find<ToggleSwitch>(By.AccessibilityId(MouseUtilsSettings.AccessibilityIds.FindMyMouseToggle)).Toggle(true);
                foundCustom0.Find<ToggleSwitch>(By.AccessibilityId(MouseUtilsSettings.AccessibilityIds.FindMyMouseToggle)).Toggle(false);
            }
            else
            {
                Assert.Fail("Find My Mouse custom not found.");
            }

            for (int i = 0; i < 10; i++)
            {
                Session.PerformMouseAction(MouseActionType.ScrollDown);
            }

            var foundCustom = this.Find<Custom>(By.AccessibilityId(MouseUtilsSettings.AccessibilityIds.MouseJump));
            if (foundCustom != null)
            {
                foundCustom.Find<ToggleSwitch>(By.AccessibilityId(MouseUtilsSettings.AccessibilityIds.MouseJumpToggle)).Toggle(true);

                var xy = Session.GetMousePosition();
                Session.MoveMouseTo(xy.Item1, xy.Item2 - 100);

                // Change the shortcut key for MouseHighlighter
                // [TestCase]Change activation shortcut and test it
                var activationShortcutButton = foundCustom.Find<Button>("Activation shortcut");
                Assert.IsNotNull(activationShortcutButton);

                activationShortcutButton.Click(false, 500, 1000);
                var activationShortcutWindow = Session.Find<Window>("Activation shortcut");
                Assert.IsNotNull(activationShortcutWindow);

                // Invalid shortcut key
                Session.SendKeySequence(Key.H);

                // IOUtil.SimulateKeyPress(0x41);
                var invalidShortcutText = activationShortcutWindow.Find<TextBlock>("Invalid shortcut");
                Assert.IsNotNull(invalidShortcutText);

                // IOUtil.SimulateShortcut(0x5B, 0x10, 0x45);
                Session.SendKeys(Key.Win, Key.Shift, Key.Z);

                // Assert.IsNull(activationShortcutWindow.Find<TextBlock>("Invalid shortcut"));
                var saveButton = activationShortcutWindow.Find<Button>("Save");
                Assert.IsNotNull(saveButton);
                saveButton.Click(false, 500, 1500);

                var screenCenter = this.GetScreenCenter();
                Session.MoveMouseTo(screenCenter.CenterX, screenCenter.CenterY, 500, 1000);
                Session.MoveMouseTo(screenCenter.CenterX, screenCenter.CenterY - 300, 500, 1000);

                // [TestCase] Enable Mouse Jump. Then - Press the activation shortcut and verify the screens preview appears.
                // [TestCase] Enable Mouse Jump. Then - Click around the screen preview and ensure that mouse cursor jumped to clicked location.
                Session.SendKeys(Key.Win, Key.Shift, Key.Z);
                VerifyWindowAppears();

                Task.Delay(1000).Wait();

                // [TestCase] Enable Mouse Jump. Then - Disable Mouse Jump and verify that the module is not activated when you press the activation shortcut.
                foundCustom.Find<ToggleSwitch>(By.AccessibilityId(MouseUtilsSettings.AccessibilityIds.MouseJumpToggle)).Toggle(false);
                Session.MoveMouseTo(screenCenter.CenterX, screenCenter.CenterY - 300, 500, 1000);
                Session.SendKeys(Key.Win, Key.Shift, Key.Z);
                Task.Delay(500).Wait();
                VerifyWindowNotAppears();
            }
            else
            {
                Assert.Fail("Mouse Highlighter Custom not found.");
            }

            Task.Delay(500).Wait();
        }

        [TestMethod("MouseUtils.MouseJump.EnableMouseJump3")]
        [TestCategory("Mouse Utils #40")]
        public void TestEnableMouseJump3()
        {
            LaunchFromSetting();
            var foundCustom0 = this.Find<Custom>(By.AccessibilityId(MouseUtilsSettings.AccessibilityIds.FindMyMouse));
            if (foundCustom0 != null)
            {
                foundCustom0.Find<ToggleSwitch>(By.AccessibilityId(MouseUtilsSettings.AccessibilityIds.FindMyMouseToggle)).Toggle(true);
                foundCustom0.Find<ToggleSwitch>(By.AccessibilityId(MouseUtilsSettings.AccessibilityIds.FindMyMouseToggle)).Toggle(false);
            }
            else
            {
                Assert.Fail("Find My Mouse custom not found.");
            }

            for (int i = 0; i < 10; i++)
            {
                Session.PerformMouseAction(MouseActionType.ScrollDown);
            }

            var foundCustom = this.Find<Custom>(By.AccessibilityId(MouseUtilsSettings.AccessibilityIds.MouseJump));
            if (foundCustom != null)
            {
                foundCustom.Find<ToggleSwitch>(By.AccessibilityId(MouseUtilsSettings.AccessibilityIds.MouseJumpToggle)).Toggle(true);

                var xy = Session.GetMousePosition();
                Session.MoveMouseTo(xy.Item1, xy.Item2 - 100);

                // Change the shortcut key for MouseHighlighter
                // [TestCase]Change activation shortcut and test it
                var activationShortcutButton = foundCustom.Find<Button>("Activation shortcut");
                Assert.IsNotNull(activationShortcutButton);

                activationShortcutButton.Click(false, 500, 1000);
                var activationShortcutWindow = Session.Find<Window>("Activation shortcut");
                Assert.IsNotNull(activationShortcutWindow);

                // Invalid shortcut key
                Session.SendKeySequence(Key.H);

                // IOUtil.SimulateKeyPress(0x41);
                var invalidShortcutText = activationShortcutWindow.Find<TextBlock>("Invalid shortcut");
                Assert.IsNotNull(invalidShortcutText);

                // IOUtil.SimulateShortcut(0x5B, 0x10, 0x45);
                Session.SendKeys(Key.Win, Key.Shift, Key.J);

                // Assert.IsNull(activationShortcutWindow.Find<TextBlock>("Invalid shortcut"));
                var saveButton = activationShortcutWindow.Find<Button>("Save");
                Assert.IsNotNull(saveButton);
                saveButton.Click(false, 500, 1500);

                var screenCenter = this.GetScreenCenter();
                Session.MoveMouseTo(screenCenter.CenterX, screenCenter.CenterY, 500, 1000);
                Session.MoveMouseTo(screenCenter.CenterX, screenCenter.CenterY - 300, 500, 1000);

                // [TestCase] Enable Mouse Jump. Then - Change activation shortcut and verify that new shortcut triggers Mouse Jump.
                Session.SendKeys(Key.Win, Key.Shift, Key.J);
                VerifyWindowAppears();
            }
            else
            {
                Assert.Fail("Mouse Highlighter Custom not found.");
            }

            Task.Delay(500).Wait();
        }

        private void VerifyWindowAppears()
        {
            string windowName = "MouseJump";
            Session.Attach(windowName);
            var center = this.Session.GetMainWindowCenter();
            Session.MoveMouseTo(center.CenterX, center.CenterY);
            Session.PerformMouseAction(MouseActionType.LeftClick, 1000, 1000);
            var screenCenter = this.GetScreenCenter();

            // Get Mouse position
            var xy = Session.GetMousePosition();

            double distance = CalculateDistance(xy.Item1, xy.Item2, screenCenter.CenterX, screenCenter.CenterY);
            Assert.IsTrue(distance <= 10, "Mouse Jump window should be opened and mouse should be moved to the center of the screen.");
        }

        private void VerifyWindowNotAppears()
        {
            string windowName = "MouseJump";
            bool open = this.IsWindowOpen(windowName);
            Assert.IsFalse(open, "Mouse Jump window should not be opened.");
        }

        /// <summary>
        /// Calculate the Euclidean distance between two 2D points
        /// </summary>
        /// <param name="x1">X coordinate of first point</param>
        /// <param name="y1">Y coordinate of first point</param>
        /// <param name="x2">X coordinate of second point</param>
        /// <param name="y2">Y coordinate of second point</param>
        /// <returns>Distance (double)</returns>
        public double CalculateDistance(int x1, int y1, int x2, int y2)
        {
            int dx = x2 - x1;
            int dy = y2 - y1;
            return Math.Sqrt((dx * dx) + (dy * dy));
        }

        private void LaunchFromSetting(bool firstTime = false, bool launchAsAdmin = false)
        {
            Session.SetMainWindowSize(WindowSize.Large);
            Task.Delay(1000).Wait();

            // Goto Mouse utilities setting page
            if (this.FindAll(By.AccessibilityId(MouseUtilsSettings.AccessibilityIds.MouseUtilitiesNavItem)).Count == 0)
            {
                // Expand Input / Output list-group if needed
                this.Find(By.AccessibilityId(MouseUtilsSettings.AccessibilityIds.InputOutputNavItem)).Click();
                Task.Delay(2000).Wait();
            }

            // Goto Mouse utilities setting page
            if (this.FindAll(By.AccessibilityId(MouseUtilsSettings.AccessibilityIds.MouseUtilitiesNavItem)).Count == 0)
            {
                RestartScopeExe();
                Session.SetMainWindowSize(WindowSize.Large);
                Task.Delay(1000).Wait();

                // Expand Input / Output list-group if needed
                this.Find(By.AccessibilityId(MouseUtilsSettings.AccessibilityIds.InputOutputNavItem)).Click();
                Task.Delay(2000).Wait();
            }

            // Click on the Mouse utilities
            // Task.Delay(2000).Wait();
            if (firstTime)
            {
                return;
            }
            else
            {
                this.Find(By.AccessibilityId(MouseUtilsSettings.AccessibilityIds.MouseUtilitiesNavItem)).Click();
            }
        }
    }
}
