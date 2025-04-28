// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.DirectoryServices;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;
using Microsoft.Diagnostics.Tracing.AutomatedAnalysis;
using Microsoft.FancyZones.UITests.Utils;
using Microsoft.FancyZonesEditor.UITests.Utils;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium.Windows;
using static Microsoft.FancyZonesEditor.UnitTests.Utils.FancyZonesEditorHelper;

namespace UITests_FancyZones
{
    [TestClass]
    public class DragWindowTests : UITestBase
    {
        private static readonly IOTestHelper AppZoneHistory = new FancyZonesEditorFiles().AppZoneHistoryIOHelper;
        private static string nonPrimaryMouseButton = "Right";

        private static string highlightColor = "#0072C9"; // set highlight color
        private static string primaryColor = "#F2F2F2"; // set primary mouse button
        private static int screenMarginTop; // set check position
        private static int screenMarginLeft; // set check position
        private static int screenMarginRight; // set check position
        private static int screenMarginBottom; // set check position

        private static int quarterX;
        private static int quarterY;

        private static string powertoysWindowName = "PowerToys Settings"; // set powertoys settings window name

        public DragWindowTests()
            : base(PowerToysModule.PowerToysSettings, WindowSize.Medium)
        {
        }

        [TestInitialize]
        public void TestInitialize()
        {
            // Get the current mouse button setting
            nonPrimaryMouseButton = SystemInformation.MouseButtonsSwapped ? "Left" : "Right";

            // get PowerToys window Name
            powertoysWindowName = ZoneSwitchHelper.GetActiveWindowTitle();

            // Set a custom layout with 2 subzones and clear app zone history
            SetupCustomLayouts();
            AppZoneHistory.DeleteFile();

            // Ensure FancyZones settings page is visible and enable FancyZones
            LaunchFancyZones();

            // Get screen margins for positioning checks
            GetScreenMargins();

            // Set the FancyZones layout to a custom layout
            this.Find<Element>(By.Name("Custom Column")).Click();
            this.Find<Microsoft.PowerToys.UITest.Button>("Close").Click();

            // make window small to detect zone easily
            Session.Attach(powertoysWindowName, WindowSize.Small);
        }

        /// <summary>
        /// Test Use Shift key to activate zones while dragging a window in FancyZones Zone Behaviour Settings
        /// <list type="bullet">
        /// <item>
        /// <description>Verifies that holding Shift while dragging shows all zones as expected.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod]
        public void TestShowZonesOnShiftDuringDrag()
        {
            string testCaseName = nameof(TestShowZonesOnDragDuringShift);
            Element dragElement = Find<Element>(By.Name("Non Client Input Sink Window")); // element to drag
            var offSet = ZoneSwitchHelper.GetOffset(dragElement, quarterX, quarterY);

            var (initialColor, withShiftColor) = RunDragInteractions(
              preAction: () =>
              {
                  dragElement.DragAndHold(offSet.Dx, offSet.Dy);
              },
              postAction: () =>
              {
                  Session.PressKey(Key.Shift);
                  Task.Delay(500).Wait();
              },
              releaseAction: () =>
              {
                  Session.ReleaseKey(Key.Shift);
                  Task.Delay(5000).Wait(); // Optional: Wait for a moment to ensure window switch
              },
              testCaseName: testCaseName);

            string zoneColorWithoutShift = GetOutWindowPixelColor(30);

            Assert.AreNotEqual(initialColor, withShiftColor, $"[{testCaseName}] Check color did not change.");
            Assert.AreEqual(primaryColor, withShiftColor, $"[{testCaseName}] Zone did not shown.");

            Assert.AreEqual(zoneColorWithoutShift, initialColor, $"[{testCaseName}] Zone color did not deactivate.");
            dragElement.ReleaseDrag();
        }

        /// <summary>
        /// Test dragging a window during Shift key press in FancyZones Zone Behaviour Settings
        /// <list type="bullet">
        /// <item>
        /// <description>Verifies that dragging activates zones as expected.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod]
        public void TestShowZonesOnDragDuringShift()
        {
            string testCaseName = nameof(TestShowZonesOnDragDuringShift);

            var dragElement = Find<Element>(By.Name("Non Client Input Sink Window"));
            var offSet = ZoneSwitchHelper.GetOffset(dragElement, quarterX, quarterY);

            var (initialColor, withDragColor) = RunDragInteractions(
                preAction: () =>
                {
                    dragElement.Drag(offSet.Dx, offSet.Dy);
                    Session.PressKey(Key.Shift);
                },
                postAction: () =>
                {
                    dragElement.DragAndHold(0, 0);
                    Task.Delay(5000).Wait();
                },
                releaseAction: () =>
                {
                    dragElement.ReleaseDrag();
                    Session.ReleaseKey(Key.Shift);
                },
                testCaseName: nameof(TestShowZonesOnDragDuringShift));

            Assert.AreNotEqual(initialColor, withDragColor, $"[{testCaseName}] Zone color did not change and zone did not activate.");
            Assert.AreEqual(highlightColor, withDragColor, $"[{testCaseName}] Zone color did not match the highlightcolor and did not activate.");

            // double check by app-zone-history.json
            string appZoneHistoryJson = AppZoneHistory.GetData();
            string? zoneNumber = ZoneSwitchHelper.GetZoneIndexSetByAppName(powertoysWindowName, appZoneHistoryJson);
            Assert.IsNull(zoneNumber, $"[{testCaseName}] AppZoneHistory layout is not set.");
        }

        /// <summary>
        /// Test toggling zones using a non-primary mouse click during window dragging.
        /// <list type="bullet">
        /// <item>
        /// <description>Verifies that clicking a non-primary mouse button deactivates zones while dragging a window.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod]
        public void TestToggleZonesWithNonPrimaryMouseClick()
        {
            string testCaseName = nameof(TestToggleZonesWithNonPrimaryMouseClick);
            var dragElement = Find<Element>(By.Name("Non Client Input Sink Window"));
            var offSet = ZoneSwitchHelper.GetOffset(dragElement, quarterX, quarterY);

            var (initialColor, withMouseColor) = RunDragInteractions(
                preAction: () =>
                {
                    // activate zone
                    dragElement.DragAndHold(offSet.Dx, offSet.Dy);
                },
                postAction: () =>
                {
                    // press non-primary mouse button to toggle zones
                    Session.PerformMouseAction(
                nonPrimaryMouseButton == "Right" ? MouseActionType.RightClick : MouseActionType.LeftClick);
                },
                releaseAction: () =>
                {
                    dragElement.ReleaseDrag();
                },
                testCaseName: testCaseName);

            // check the zone color is activated
            Assert.AreEqual(highlightColor, initialColor, $"[{testCaseName}] Zone color did not change.");

            // check the zone color is deactivated
            Assert.AreNotEqual(highlightColor, withMouseColor, $"[{testCaseName}] Zone color did not deactivate.");
        }

        /// <summary>
        /// Test both use Shift and non primary mouse off settings.
        /// <list type="bullet">
        /// <item>
        /// <description>Verifies that pressing the Shift key deactivates zones during a window drag-and-hold action.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod]
        public void TestShowZonesWhenShiftAndMouseOff()
        {
            string testCaseName = nameof(TestToggleZonesWithNonPrimaryMouseClick);
            Element dragElement = Find<Element>(By.Name("Non Client Input Sink Window"));
            var offSet = ZoneSwitchHelper.GetOffset(dragElement, quarterX, quarterY);

            var (initialColor, withShiftColor) = RunDragInteractions(
               preAction: () =>
               {
                   // activate zone
                   dragElement.DragAndHold(offSet.Dx, offSet.Dy);
               },
               postAction: () =>
               {
                   // press Shift Key to deactivate zones
                   Session.PressKey(Key.Shift);
                   Task.Delay(500).Wait();
               },
               releaseAction: () =>
               {
                   dragElement.ReleaseDrag();
                   Session.ReleaseKey(Key.Shift);
               },
               testCaseName: testCaseName);

            Assert.AreEqual(highlightColor, initialColor, $"[{testCaseName}] Zone color did not activate.");
            Assert.AreNotEqual(highlightColor, withShiftColor, $"[{testCaseName}] Zone color did not deactivate.");
        }

        /// <summary>
        /// Test zone visibility when both Shift key and mouse settings are involved.
        /// <list type="bullet">
        /// <item>
        /// <description>Verifies that zones are activated when Shift is pressed during drag, and deactivated by a non-primary mouse click.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod]
        public void TestShowZonesWhenShiftAndMouseOn()
        {
            string testCaseName = nameof(TestShowZonesWhenShiftAndMouseOn);

            var dragElement = Find<Element>(By.Name("Non Client Input Sink Window"));
            var offSet = ZoneSwitchHelper.GetOffset(dragElement, quarterX, quarterY);
            var (initialColor, withShiftColor) = RunDragInteractions(
             preAction: () =>
             {
                 dragElement.DragAndHold(offSet.Dx, offSet.Dy);
             },
             postAction: () =>
             {
                 Session.PressKey(Key.Shift);
             },
             releaseAction: () =>
             {
             },
             testCaseName: testCaseName);

            Assert.AreEqual(primaryColor, withShiftColor, $"[{testCaseName}] zone did not shown.");

            Session.PerformMouseAction(
             nonPrimaryMouseButton == "Right" ? MouseActionType.RightClick : MouseActionType.LeftClick);

            string zoneColorWithMouse = GetOutWindowPixelColor(30);
            Assert.AreEqual(initialColor, zoneColorWithMouse, $"[{nameof(TestShowZonesWhenShiftAndMouseOff)}] Zone color did not activate.");

            Session.ReleaseKey(Key.Shift);
            dragElement.ReleaseDrag();
        }

        /// <summary>
        /// Test that a window becomes transparent during dragging when the transparent window setting is enabled.
        /// <list type="bullet">
        /// <item>
        /// <description>Verifies that the window appears transparent while being dragged.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod]
        public void TestMakeDraggedWindowTransparentOn()
        {
            var pixel = GetPixelWhenMakeDraggedWindow();
            Assert.AreNotEqual(pixel.PixelInWindow, pixel.TransPixel, $"[{nameof(TestMakeDraggedWindowTransparentOff)}] window color is not transparent.");
        }

        /// <summary>
        /// Test that a window remains opaque during dragging when the transparent window setting is disabled.
        /// <list type="bullet">
        /// <item>
        /// <description>Verifies that the window is not transparent while being dragged.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod]
        public void TestMakeDraggedWindowTransparentOff()
        {
            var pixel = GetPixelWhenMakeDraggedWindow();
            Assert.AreEqual(pixel.PixelInWindow, pixel.TransPixel, $"[{nameof(TestMakeDraggedWindowTransparentOff)}] window color is changed.");
        }

        // Setup custom layout with 1 subzones
        private void SetupCustomLayouts()
        {
            var customLayouts = new CustomLayouts();
            var customLayoutListWrapper = CustomLayoutsList;
            Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(customLayoutListWrapper));
        }

        // launch FancyZones settings page
        private void LaunchFancyZones()
        {
            if (this.FindAll<NavigationViewItem>("FancyZones").Count == 0)
            {
                this.Find<NavigationViewItem>("Windowing & Layouts").Click();
            }

            this.Find<NavigationViewItem>("FancyZones").Click();
            this.Find<ToggleSwitch>("Enable FancyZones").Toggle(true);

            this.Session.SetMainWindowSize(WindowSize.Large_Vertical);
            Pull(3, "down"); // Ensure settings are visible
            ZoneBehaviourSettings(TestContext.TestName);
            Pull(10, "up");
            this.Find<Microsoft.PowerToys.UITest.Button>("Launch layout editor").Click(false, 500, 3000);
            this.Session.Attach(PowerToysModule.FancyZone);
            this.Find<Microsoft.PowerToys.UITest.Button>("Maximize").Click();
        }

        // Get the screen margins to calculate the dragged window position
        private void GetScreenMargins()
        {
            var rect = this.Session.GetWindowRect();
            screenMarginTop = rect.Top;
            screenMarginLeft = rect.Left;
            screenMarginRight = rect.Right;
            screenMarginBottom = rect.Bottom;
            quarterX = (rect.Left + rect.Right) / 4;
            quarterY = (rect.Top + rect.Bottom) / 4;
        }

        // Get the mouse color of the pixel when make dragged window
        public (string PixelInWindow, string TransPixel) GetPixelWhenMakeDraggedWindow()
        {
            var dragElement = Find<Element>(By.Name("Non Client Input Sink Window"));
            var offSet = ZoneSwitchHelper.GetOffset(dragElement, quarterX, quarterY);
            dragElement.DragAndHold(offSet.Dx, offSet.Dy);
            Tuple<int, int> pos = GetMousePosition();
            string pixelInWindow = Session.GetPixelColorString(pos.Item1, pos.Item2);
            Session.PressKey(Key.Shift);
            string transPixel = Session.GetPixelColorString(pos.Item1, pos.Item2);
            Session.ReleaseKey(Key.Shift);
            dragElement.ReleaseDrag();
            return (pixelInWindow, transPixel);
        }

        // Get the color of the pixel outside the window
        public string GetOutWindowPixelColor(int spacing)
        {
            var rect = this.Session.GetWindowRect();
            int checkX, checkY;

            if ((rect.Top - screenMarginTop) >= spacing)
            {
                checkX = rect.Left;
                checkY = rect.Top + (spacing / 2);
            }
            else if ((screenMarginBottom - rect.Bottom) >= spacing)
            {
                checkX = rect.Left;
                checkY = rect.Bottom + (spacing / 2);
            }
            else if ((rect.Left - screenMarginLeft) >= spacing)
            {
                checkX = rect.Left - (spacing / 2);
                checkY = rect.Top;
            }
            else if ((screenMarginRight - rect.Right) >= spacing)
            {
                checkX = rect.Right + (spacing / 2);
                checkY = rect.Top;
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(spacing), "No sufficient margin to sample outside the window.");
            }

            Console.WriteLine($"checkX: {checkX}, checkY: {checkY}");
            string zoneColor = this.Session.GetPixelColorString(checkX, checkY);
            Console.WriteLine($"zone color: {zoneColor}");
            return zoneColor;
        }

        // Run drag interactions and return the initial and final zone colors
        public (string InitialZoneColor, string FinalZoneColor) RunDragInteractions(
        Action? preAction,
        Action? postAction,
        Action? releaseAction,
        string testCaseName)
        {
            // Invoke the pre-action
            preAction?.Invoke();

            // Capture initial window state and zone color
            var initialWindowRect = this.Session.GetWindowRect();
            string initialZoneColor = GetOutWindowPixelColor(30);

            // Invoke the post-action
            postAction?.Invoke();

            // Capture final zone color after the interaction
            string finalZoneColor = GetOutWindowPixelColor(30);

            releaseAction?.Invoke();

            // Return initial and final zone colors
            return (initialZoneColor, finalZoneColor);
        }

        // Pull the setting page up or down
        private void Pull(int tries = 5, string direction = "up")
        {
            Key keyToSend = direction == "up" ? Key.Up : Key.Down;
            for (int i = 0; i < tries; i++)
            {
                SendKeys(keyToSend);
            }
        }

        // set the custom layout
        private static readonly CustomLayouts.CustomLayoutListWrapper CustomLayoutsList = new CustomLayouts.CustomLayoutListWrapper
        {
            CustomLayouts = new List<CustomLayouts.CustomLayoutWrapper>
            {
                new CustomLayouts.CustomLayoutWrapper
                {
                    Uuid = "{63F09977-D327-4DAC-98F4-0C886CAE9517}",
                    Type = CustomLayout.Grid.TypeToString(),
                    Name = "Custom Column",
                    Info = new CustomLayouts().ToJsonElement(new CustomLayouts.GridInfoWrapper
                    {
                        Rows = 1,
                        Columns = 1,
                        RowsPercentage = new List<int> { 10000 },
                        ColumnsPercentage = new List<int> { 10000 },
                        CellChildMap = new int[][] { [0] },
                        SensitivityRadius = 20,
                        ShowSpacing = true,
                        Spacing = 0, // set spacing to 0 make sure the zone is full of the screen
                    }),
                },
            },
        };

        // set the zone behaviour settings
        private void ZoneBehaviourSettings(string? testName)
        {
            Microsoft.PowerToys.UITest.CheckBox showZoneNumber = this.Find<Microsoft.PowerToys.UITest.CheckBox>("Show zone number");
            Microsoft.PowerToys.UITest.CheckBox useShiftCheckBox = this.Find<Microsoft.PowerToys.UITest.CheckBox>("Hold Shift key to activate zones while dragging a window");
            Microsoft.PowerToys.UITest.CheckBox useNonPrimaryMouseCheckBox = this.Find<Microsoft.PowerToys.UITest.CheckBox>("Use a non-primary mouse button to toggle zone activation");
            Microsoft.PowerToys.UITest.CheckBox makeDraggedWindowTransparent = this.Find<Microsoft.PowerToys.UITest.CheckBox>("Make dragged window transparent");
            this.Find<Slider>("Opacity (%)").QuickSetValue(100);
            makeDraggedWindowTransparent.SetCheck(false, 500);
            Find<Element>(By.AccessibilityId("HeaderPresenter")).Click();
            showZoneNumber.SetCheck(false, 100);
            switch (testName)
            {
                case "TestShowZonesOnShiftDuringDrag":
                case "TestShowZonesOnDragDuringShift":
                    useShiftCheckBox.SetCheck(true, 500);
                    useNonPrimaryMouseCheckBox.SetCheck(false, 500);
                    Find<Element>(By.AccessibilityId("HeaderPresenter")).Click();
                    break;
                case "TestToggleZonesWithNonPrimaryMouseClick":
                    useShiftCheckBox.SetCheck(false, 500);
                    useNonPrimaryMouseCheckBox.SetCheck(true, 500);
                    Find<Element>(By.AccessibilityId("HeaderPresenter")).Click();
                    break;
                case "TestShowZonesWhenShiftAndMouseOff":
                    useShiftCheckBox.SetCheck(false, 500);
                    useNonPrimaryMouseCheckBox.SetCheck(false, 500);
                    Find<Element>(By.AccessibilityId("HeaderPresenter")).Click();
                    break;
                case "TestShowZonesWhenShiftAndMouseOn":
                    useShiftCheckBox.SetCheck(true, 500);
                    useNonPrimaryMouseCheckBox.SetCheck(true, 500);
                    Find<Element>(By.AccessibilityId("HeaderPresenter")).Click();
                    break;
                case "TestMakeDraggedWindowTransparentOn":
                    makeDraggedWindowTransparent.SetCheck(true, 500);
                    useNonPrimaryMouseCheckBox.SetCheck(false, 500);
                    useShiftCheckBox.SetCheck(true, 500);
                    break; // Added break to prevent fall-through
                case "TestMakeDraggedWindowTransparentOff":
                    useShiftCheckBox.SetCheck(true, 500);
                    useNonPrimaryMouseCheckBox.SetCheck(false, 500);
                    break; // Added break to prevent fall-through
                default:
                    throw new ArgumentException("Unsupported Test Case.", testName);
            }
        }
    }
}
