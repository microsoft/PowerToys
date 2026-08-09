// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScreenRuler.UITests
{
    [TestClass]
    public class TestGuides : UITestBase
    {
        private const string GuideInputWindowClass = "PowerToys.ScreenRuler.GuideInput";
        private const string GuideRenderWindowClass = "PowerToys.ScreenRuler.GuideRender";
        private const string ToolbarWindowClass = "WinUIDesktopWin32WindowClass";
        private const string ToolbarWindowTitle = "PowerToys.ScreenRuler";

        private delegate bool EnumWindowsCallback(IntPtr window, IntPtr parameter);

        public TestGuides()
            : base(PowerToysModule.PowerToysSettings, WindowSize.Large)
        {
        }

        [TestMethod("ScreenRuler.Guides")]
        [TestCategory("Guides")]
        public void TestScreenRulerGuides()
        {
            var activationKeys = TestHelper.InitializeTest(this, "guide test");
            SendKeys(activationKeys);
            Assert.IsTrue(TestHelper.WaitForScreenRulerUI(this), "Screen Ruler toolbar should open.");

            Session.Attach(PowerToysModule.ScreenRuler);
            var processId = GetProcessIdForWindow(ToolbarWindowClass, ToolbarWindowTitle);
            Assert.IsTrue(processId.HasValue, "Screen Ruler process should own the toolbar.");

            var boundsButton = Session.Find<ToggleSwitch>(By.AccessibilityId(TestHelper.BoundsButtonId), 5000, true);
            var horizontalGuideButton = Session.Find<Element>(By.AccessibilityId(TestHelper.AddHorizontalGuideButtonId), 5000, true);
            var verticalGuideButton = Session.Find<Element>(By.AccessibilityId(TestHelper.AddVerticalGuideButtonId), 5000, true);

            Assert.IsTrue(horizontalGuideButton.Enabled, "Add horizontal guide should be enabled.");
            Assert.IsTrue(verticalGuideButton.Enabled, "Add vertical guide should be enabled.");
            Assert.AreEqual(
                0,
                Session.FindAll<Element>(By.AccessibilityId(TestHelper.ClearGuidesButtonId), 500, true).Count,
                "Clear guides should be hidden until a guide exists.");

            boundsButton.Click();
            Assert.IsTrue(boundsButton.IsOn, "Bounds should be selected before guide placement.");

            PlaceGuide(horizontalGuideButton);
            var clearGuidesButton = Session.Find<Element>(By.AccessibilityId(TestHelper.ClearGuidesButtonId), 5000, true);
            Assert.IsTrue(clearGuidesButton.Enabled, "Clear guides should appear after adding a guide.");
            boundsButton = Session.Find<ToggleSwitch>(By.AccessibilityId(TestHelper.BoundsButtonId), 5000, true);
            Assert.IsTrue(boundsButton.IsOn, "Horizontal guide placement should preserve the active tool.");

            PlaceGuide(verticalGuideButton);
            boundsButton = Session.Find<ToggleSwitch>(By.AccessibilityId(TestHelper.BoundsButtonId), 5000, true);
            Assert.IsTrue(boundsButton.IsOn, "Vertical guide placement should preserve the active tool.");
            Assert.IsTrue(
                WaitForWindowClass(processId.Value, GuideInputWindowClass, shouldBeVisible: true),
                "Committed guides should expose edit hit targets while the toolbar is visible.");

            SendKeys(activationKeys);
            Assert.IsTrue(TestHelper.WaitForScreenRulerUIToDisappear(this), "The toolbar should hide while guides remain.");
            using (var guideHost = Process.GetProcessById(processId.Value))
            {
                Assert.IsFalse(guideHost.HasExited, "The guide host should remain running.");
            }

            Assert.IsTrue(
                HasVisibleWindow(processId.Value, GuideRenderWindowClass),
                "Guide render windows should remain visible while the toolbar is hidden.");
            Assert.IsTrue(
                WaitForWindowClass(processId.Value, GuideInputWindowClass, shouldBeVisible: false),
                "Guide input windows should be hidden so passive guides are click-through.");

            SendKeys(activationKeys);
            Assert.IsTrue(TestHelper.WaitForScreenRulerUI(this), "The toolbar should reopen for guide editing.");
            Assert.AreEqual(
                processId.Value,
                GetProcessIdForWindow(ToolbarWindowClass, ToolbarWindowTitle),
                "Reopening should reuse the resident guide host.");
            Assert.IsTrue(
                WaitForWindowClass(processId.Value, GuideInputWindowClass, shouldBeVisible: true),
                "Reopening should restore guide edit hit targets.");

            Session.Attach(PowerToysModule.ScreenRuler);
            clearGuidesButton = Session.Find<Element>(By.AccessibilityId(TestHelper.ClearGuidesButtonId), 5000, true);
            clearGuidesButton.Click();
            Assert.AreEqual(
                0,
                Session.FindAll<Element>(By.AccessibilityId(TestHelper.ClearGuidesButtonId), 1000, true).Count,
                "Clear guides should hide after the final guide is removed.");
            Assert.IsTrue(
                WaitForWindowClass(processId.Value, GuideInputWindowClass, shouldBeVisible: false),
                "Clear all should remove every guide hit target.");

            SendKeys(activationKeys);
            Assert.IsTrue(TestHelper.WaitForScreenRulerUIToDisappear(this), "The toolbar should close when no guides remain.");
            Assert.IsTrue(
                WaitForProcessExit(processId.Value),
                "The Screen Ruler host should exit after clearing guides and dismissing the toolbar.");

            TestHelper.CleanupTest(this);
        }

        private void PlaceGuide(Element button)
        {
            button.Click();
            var cursor = GetMousePosition();
            MoveMouseTo(cursor.Item1, cursor.Item2 + 200);
            Task.Delay(200).Wait();
            Session.PerformMouseAction(MouseActionType.LeftClick);
            Task.Delay(300).Wait();
        }

        private static bool WaitForWindowClass(int processId, string className, bool shouldBeVisible, int timeoutMs = 5000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            do
            {
                if (HasVisibleWindow(processId, className) == shouldBeVisible)
                {
                    return true;
                }

                Task.Delay(100).Wait();
            }
            while (DateTime.UtcNow < deadline);

            return false;
        }

        private static bool WaitForProcessExit(int processId, int timeoutMs = 5000)
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                return process.WaitForExit(timeoutMs);
            }
            catch (ArgumentException)
            {
                return true;
            }
        }

        private static int? GetProcessIdForWindow(string className, string title)
        {
            int? result = null;
            _ = EnumWindows(
                (window, parameter) =>
                {
                    if (!IsWindowVisible(window) ||
                        !string.Equals(GetWindowClass(window), className, StringComparison.Ordinal) ||
                        !string.Equals(GetWindowTitle(window), title, StringComparison.Ordinal))
                    {
                        return true;
                    }

                    _ = GetWindowThreadProcessId(window, out uint processId);
                    result = (int)processId;
                    return false;
                },
                IntPtr.Zero);
            return result;
        }

        private static bool HasVisibleWindow(int processId, string className)
        {
            var result = false;
            _ = EnumWindows(
                (window, parameter) =>
                {
                    _ = GetWindowThreadProcessId(window, out uint ownerProcessId);
                    if (ownerProcessId == processId &&
                        IsWindowVisible(window) &&
                        string.Equals(GetWindowClass(window), className, StringComparison.Ordinal))
                    {
                        result = true;
                        return false;
                    }

                    return true;
                },
                IntPtr.Zero);
            return result;
        }

        private static string GetWindowClass(IntPtr window)
        {
            var value = new StringBuilder(256);
            _ = GetClassName(window, value, value.Capacity);
            return value.ToString();
        }

        private static string GetWindowTitle(IntPtr window)
        {
            var value = new StringBuilder(256);
            _ = GetWindowText(window, value, value.Capacity);
            return value.ToString();
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsCallback callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder className, int maximumCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text, int maximumCount);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    }
}
