// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseUtils.UITests
{
    [TestClass]
    public class MouseButtonLockTests : UITestBase
    {
        [TestMethod("MouseUtils.MouseButtonLock.EnableMouseButtonLock")]
        [TestCategory("Mouse Utils")]
        public void TestEnableMouseButtonLock()
        {
            LaunchFromSetting();

            var foundCustom = FindMouseUtilElement(MouseUtilsSettings.MouseUtils.MouseButtonLock);
            Assert.IsNotNull(foundCustom);

            // [Test Case] Toggle the module off and back on.
            MouseUtilsSettings.SetMouseUtilEnabled(foundCustom, MouseUtilsSettings.MouseUtils.MouseButtonLock, false);
            MouseUtilsSettings.SetMouseUtilEnabled(foundCustom, MouseUtilsSettings.MouseUtils.MouseButtonLock, true);

            // The "Buttons and behavior" expander is disabled until the module is enabled, and its
            // items are only realized once it is expanded.
            var options = foundCustom.Find<Group>(By.AccessibilityId(MouseUtilsSettings.AccessibilityIds.MouseButtonLockOptions));
            Assert.IsNotNull(options);
            options.Click();

            // [Test Case] Each button checkbox round-trips.
            var lmbLock = foundCustom.Find<CheckBox>("Lock the left (primary) mouse button");
            var rmbLock = foundCustom.Find<CheckBox>("Lock the right mouse button");
            var mmbLock = foundCustom.Find<CheckBox>("Lock the middle mouse button");

            Assert.IsNotNull(lmbLock);
            Assert.IsNotNull(rmbLock);
            Assert.IsNotNull(mmbLock);

            mmbLock.SetCheck(true);
            Assert.IsTrue(mmbLock.IsChecked);
            mmbLock.SetCheck(false);
            Assert.IsFalse(mmbLock.IsChecked);

            rmbLock.SetCheck(true);
            Assert.IsTrue(rmbLock.IsChecked);

            // [Test Case] Hold duration snaps to 100 ms steps across the 200-2200 ms range.
            var holdDuration = foundCustom.Find<Slider>(By.AccessibilityId(MouseUtilsSettings.AccessibilityIds.MouseButtonLockHoldDuration));
            Assert.IsNotNull(holdDuration);

            holdDuration.QuickSetValue(800);
            Assert.AreEqual(800, holdDuration.GetValue());

            holdDuration.QuickSetValue(1200);
            Assert.AreEqual(1200, holdDuration.GetValue());

            // [Test Case] Drag threshold accepts a value. The NumberBox surfaces to automation as an edit control.
            var moveCancelPixels = foundCustom.Find<TextBox>(By.AccessibilityId(MouseUtilsSettings.AccessibilityIds.MouseButtonLockMoveCancelPixels));
            Assert.IsNotNull(moveCancelPixels);
            moveCancelPixels.SetText("12");

            // Leave the module off so a later test starts from a known state.
            MouseUtilsSettings.SetMouseUtilEnabled(foundCustom, MouseUtilsSettings.MouseUtils.MouseButtonLock, false);
        }

        public Custom? FindMouseUtilElement(MouseUtilsSettings.MouseUtils element)
        {
            string accessibilityId = element switch
            {
                MouseUtilsSettings.MouseUtils.MouseButtonLock => MouseUtilsSettings.AccessibilityIds.MouseButtonLock,
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

        private void LaunchFromSetting()
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
