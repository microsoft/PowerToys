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
    public class DragingWindowTestssingle : UITestBase
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

        public DragingWindowTestssingle()
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

            // set a custom layout with 2 subzones
            CustomLayouts customLayouts = new CustomLayouts();
            CustomLayouts.CustomLayoutListWrapper customLayoutListWrapper = CustomLayoutsList;
            Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(customLayoutListWrapper));

            // clear the app zone history
            AppZoneHistory.DeleteFile();

            // Goto Hosts File Editor setting page
            if (this.FindAll<NavigationViewItem>("FancyZones").Count == 0)
            {
                // Expand Advanced list-group if needed
                this.Find<NavigationViewItem>("Windowing & Layouts").Click();
            }

            this.Find<NavigationViewItem>("FancyZones").Click();
            this.Find<ToggleSwitch>("Enable FancyZones").Toggle(true);
            this.Session.SetMainWindowSize(WindowSize.Large_Vertical);

            int tries = 3;
            Pull(tries, "down"); // Pull the setting page up to make sure the setting is visible
            ZoneBehaviourSettings(TestContext.TestName);

            Pull(tries, "up");
            this.Find<Microsoft.PowerToys.UITest.Button>("Launch layout editor").Click(false, 500, 4000);
            this.Session.Attach(PowerToysModule.FancyZone);
            this.Find<Microsoft.PowerToys.UITest.Button>("Maximize").Click();
            var rect = this.Session.GetWindowRect();
            screenMarginTop = rect.Top; // set check position
            screenMarginLeft = rect.Left; // set check position
            screenMarginRight = rect.Right; // set check position
            screenMarginBottom = rect.Bottom; // set check position

            quarterX = (rect.Left + rect.Right) / 4;
            quarterY = (rect.Top + rect.Bottom) / 4;

            this.Find<Element>(By.Name("Custom Column")).Click();
            this.Find<Microsoft.PowerToys.UITest.Button>("Close").Click();
            Session.Attach(powertoysWindowName, WindowSize.Small); // display window1
        }

        /// <summary>
        /// Test Use Shift key to activate zones while dragging a window in FancyZones Zone Behaviour Settings
        /// <list type="bullet">
        /// <item>
        /// <description>Verifies that holding Shift while dragging activates zones as expected.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod]
        public void TestShowZonesOnShiftDuringDrag()
        {
            var dragElement = Find<Element>(By.Name("Non Client Input Sink Window"));
            var offSet = ZoneSwitchHelper.GetOffset(dragElement, quarterX, quarterY);
            dragElement.DragAndHold(offSet.Dx, offSet.Dy);
            string zoneColorBefore = GetOutWindowPixelColor(30);
            Session.PressKey(Key.Shift);
            string zoneColorwithShift = GetOutWindowPixelColor(30);
            Assert.AreNotEqual(zoneColorwithShift, zoneColorBefore, $"[{nameof(TestShowZonesOnShiftDuringDrag)}] Check color did not change.");
            Assert.AreEqual(primaryColor, zoneColorwithShift, $"[{nameof(TestShowZonesOnShiftDuringDrag)}] Zones did not shown.");
            Session.ReleaseKey(Key.Shift);
            string zoneColorcurrent = GetOutWindowPixelColor(30);
            Assert.AreEqual(zoneColorcurrent, zoneColorBefore, $"[{nameof(TestShowZonesOnShiftDuringDrag)}] Zone color did not activate.");
            dragElement.ReleaseDrag();
        }

        [TestMethod]
        public void TestShowZonesOnDragDuringShift()
        {
            var dragElement = Find<Element>(By.Name("Non Client Input Sink Window"));
            var offSet = ZoneSwitchHelper.GetOffset(dragElement, quarterX, quarterY);

            RunDragInteractions(
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
        }

        [TestMethod]
        public void TestToggleZonesWithNonPrimaryMouseClick()
        {
            var dragElement = Find<Element>(By.Name("Non Client Input Sink Window"));
            var offSet = ZoneSwitchHelper.GetOffset(dragElement, quarterX, quarterY);

            // activate zone
            dragElement.DragAndHold(offSet.Dx, offSet.Dy);
            string zoneColorcurrent = GetOutWindowPixelColor(30);

            // press non-primary mouse button
            Session.PerformMouseAction(
                nonPrimaryMouseButton == "Right" ? MouseActionType.RightClick : MouseActionType.LeftClick);

            // check the zone color is not activated
            string zoneColorAfter = GetOutWindowPixelColor(30);
            Assert.AreNotEqual(zoneColorcurrent, zoneColorAfter, $"[{nameof(TestToggleZonesWithNonPrimaryMouseClick)}] Zone color did not deactivate.");
            highlightColor = zoneColorcurrent;

            // release drag
            dragElement.ReleaseDrag();
        }

        [TestMethod]
        public void TestShowZonesWhenShiftAndMouseOff()
        {
            var dragElement = Find<Element>(By.Name("Non Client Input Sink Window"));
            var offSet = ZoneSwitchHelper.GetOffset(dragElement, quarterX, quarterY);
            dragElement.DragAndHold(offSet.Dx, offSet.Dy);
            Task.Delay(1000).Wait();
            string currentzoneColor = GetOutWindowPixelColor(30);
            Assert.AreEqual(highlightColor, currentzoneColor, $"[{nameof(TestShowZonesWhenShiftAndMouseOff)}] Zone color did not activate.");
            Session.PressKey(Key.Shift);
            string zoneColor = GetOutWindowPixelColor(30);
            Assert.AreNotEqual(highlightColor, zoneColor, $"[{nameof(TestShowZonesWhenShiftAndMouseOff)}] Zone color did not deactivate.");
            dragElement.ReleaseDrag();
            Session.ReleaseKey(Key.Shift);
        }

        [TestMethod]
        public void TestShowZonesWhenShiftAndMouseOn()
        {
            var dragElement = Find<Element>(By.Name("Non Client Input Sink Window"));
            var offSet = ZoneSwitchHelper.GetOffset(dragElement, quarterX, quarterY);
            dragElement.DragAndHold(offSet.Dx, offSet.Dy);
            Task.Delay(1000).Wait();
            string currentzoneColor = GetOutWindowPixelColor(30);
            Session.PressKey(Key.Shift);
            string zoneColorwithShift = GetOutWindowPixelColor(30);
            Assert.AreEqual(primaryColor, zoneColorwithShift, $"[{nameof(TestShowZonesWhenShiftAndMouseOff)}] Zone color did not activate.");
            Session.PerformMouseAction(
             nonPrimaryMouseButton == "Right" ? MouseActionType.RightClick : MouseActionType.LeftClick);
            string zoneColorwithMouse = GetOutWindowPixelColor(30);
            Assert.AreEqual(currentzoneColor, zoneColorwithMouse, $"[{nameof(TestShowZonesWhenShiftAndMouseOff)}] Zone color did not activate.");

            Session.ReleaseKey(Key.Shift);
            dragElement.ReleaseDrag();
        }

        [TestMethod]
        public void TestMakeDraggedWindowTransparentOn()
        {
            var pixel = GetPixelWhenMakeDraggedWindow();
            Assert.AreNotEqual(pixel.PixelInwindow, pixel.TransPixel, $"[{nameof(TestMakeDraggedWindowTransparentOff)}] window color is not transparent.");
        }

        [TestMethod]
        public void TestMakeDraggedWindowTransparentOff()
        {
            var pixel = GetPixelWhenMakeDraggedWindow();
            Assert.AreEqual(pixel.PixelInwindow, pixel.TransPixel, $"[{nameof(TestMakeDraggedWindowTransparentOff)}] window color is not transparent.");
        }

        public (string PixelInwindow, string TransPixel) GetPixelWhenMakeDraggedWindow()
        {
            Element dragElement = Find<Element>(By.Name("Non Client Input Sink Window"));
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

        public void RunDragInteractions(
        Action? preAction,
        Action? postAction,
        Action? releaseAction,
        string testCaseName)
        {
            preAction?.Invoke();

            // Drag PowerToys Window
            var windowRectBefore = this.Session.GetWindowRect();
            string zoneColorBefore = GetOutWindowPixelColor(30);

            postAction?.Invoke();
            string zoneColorAfter = GetOutWindowPixelColor(30);
            Assert.AreNotEqual(zoneColorBefore, zoneColorAfter, $"[{testCaseName}] Zone color did not change.");
            Assert.AreEqual(highlightColor, zoneColorAfter, $"[{testCaseName}] Zone color did not activate.");

            releaseAction?.Invoke();
            string appZoneHistoryJson = AppZoneHistory.GetData();
            string? zonenumber = ZoneSwitchHelper.GetZoneIndexSetByAppName(powertoysWindowName, appZoneHistoryJson);
            Assert.IsNull(zonenumber, $"[{testCaseName}] AppZoneHistory layout is not set.");
        }

        private bool FindGroup(string groupName)
        {
            try
            {
                var foundElements = this.FindAll<Element>(groupName);
                foreach (var element in foundElements)
                {
                    string className = element.ClassName;
                    string name = element.Name;
                    string text = element.Text;
                    string helptext = element.HelpText;
                    string controlType = element.ControlType;
                }

                if (foundElements.Count == 0)
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                // Validate if group is not found by checking exception.Message
                return ex.Message.Contains("No element found");
            }

            return true;
        }

        private void Pull(int tries = 5, string direction = "up")
        {
            Key keyToSend = direction == "up" ? Key.Up : Key.Down;
            for (int i = 0; i < tries; i++)
            {
                SendKeys(keyToSend);
            }
        }

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

        private void ZoneBehaviourSettings(string? testName)
        {
            Microsoft.PowerToys.UITest.CheckBox showZoneNumber = this.Find<Microsoft.PowerToys.UITest.CheckBox>("Show zone number");
            Microsoft.PowerToys.UITest.CheckBox useShiftCheckBox = this.Find<Microsoft.PowerToys.UITest.CheckBox>("Hold Shift key to activate zones while dragging a window");
            Microsoft.PowerToys.UITest.CheckBox useNonPrimaryMouseCheckBox = this.Find<Microsoft.PowerToys.UITest.CheckBox>("Use a non-primary mouse button to toggle zone activation");
            Microsoft.PowerToys.UITest.CheckBox makeDraggedWindowTransparent = this.Find<Microsoft.PowerToys.UITest.CheckBox>("Make dragged window transparent");
            this.Find<Slider>("Opacity (%)").QuickSetValue(100);
            showZoneNumber.SetCheck(false, 100);
            switch (testName)
            {
                case "TestShowZonesOnShiftDuringDrag":
                case "TestShowZonesOnDragDuringShift":
                    useShiftCheckBox.SetCheck(true, 500);
                    useNonPrimaryMouseCheckBox.SetCheck(false, 500);
                    break;
                case "TestToggleZonesWithNonPrimaryMouseClick":
                    useShiftCheckBox.SetCheck(false, 500);
                    useNonPrimaryMouseCheckBox.SetCheck(true, 500);
                    break;
                case "TestShowZonesWhenShiftAndMouseOff":
                    useShiftCheckBox.SetCheck(false, 500);
                    useNonPrimaryMouseCheckBox.SetCheck(false, 500);
                    break;
                case "TestShowZonesWhenShiftAndMouseOn":
                    useShiftCheckBox.SetCheck(true, 500);
                    useNonPrimaryMouseCheckBox.SetCheck(true, 500);
                    break;
                case "TestMakeDraggedWindowTransparentOn":
                    useShiftCheckBox.SetCheck(true, 500);
                    useNonPrimaryMouseCheckBox.SetCheck(false, 500);
                    makeDraggedWindowTransparent.SetCheck(true, 500);
                    break; // Added break to prevent fall-through
                case "TestMakeDraggedWindowTransparentOff":
                    useShiftCheckBox.SetCheck(true, 500);
                    useNonPrimaryMouseCheckBox.SetCheck(false, 500);
                    makeDraggedWindowTransparent.SetCheck(false, 500);
                    Find<Element>(By.AccessibilityId("HeaderPresenter")).Click(); // make mouse on the windows settings then scroll up can work.
                    break; // Added break to prevent fall-through
                default:
                    throw new ArgumentException("Unsupported Test Case.", testName);
            }
        }
    }
}
