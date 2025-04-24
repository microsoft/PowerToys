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
        private static int screenMarginTop; // set check position
        private static int screenMarginLeft; // set check position
        private static int screenMarginRight; // set check position
        private static int screenMarginBottom; // set check position

        private static int centerX;
        private static int centerY;
        private static string powertoysWindowName = "PowerToys Settings"; // set powertoys settings window name

        public DragingWindowTestssingle()
            : base(PowerToysModule.PowerToysSettings, WindowSize.Medium)
        {
        }

        [TestInitialize]
        public void TestInitialize()
        {
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

            int tries = 2;
            Pull(tries, "down"); // Pull the setting page up to make sure the setting is visible
            ZoneBehaviourSettings(TestContext.TestName);
            this.Find<Slider>("Opacity (%)").QuickSetValue(100);

            Pull(tries, "up");
            this.Find<Microsoft.PowerToys.UITest.Button>("Launch layout editor").Click(false, 500, 4000);
            this.Session.Attach(PowerToysModule.FancyZone);
            this.Find<Microsoft.PowerToys.UITest.Button>("Maximize").Click();
            var rect = this.Session.GetWindowRect();
            screenMarginTop = rect.Top; // set check position
            screenMarginLeft = rect.Left; // set check position
            screenMarginRight = rect.Right; // set check position
            screenMarginBottom = rect.Bottom; // set check position

            centerX = (rect.Left + rect.Right) / 2;
            centerY = (rect.Top + rect.Bottom) / 2;

            this.Find<Element>(By.Name("Custom Column")).Click();
            this.Find<Microsoft.PowerToys.UITest.Button>("Close").Click();
        }

        public void ZoneBehaviourSettings(string? testName)
        {
            Microsoft.PowerToys.UITest.CheckBox useShiftCheckBox = this.Find<Microsoft.PowerToys.UITest.CheckBox>("Hold Shift key to activate zones while dragging a window");
            Microsoft.PowerToys.UITest.CheckBox useNonPrimaryMouseCheckBox = this.Find<Microsoft.PowerToys.UITest.CheckBox>("Use a non-primary mouse button to toggle zone activation");
            switch (testName)
            {
                case "TestShowZonesOnShiftDuringDrag":
                case "TestShowZonesOnDragDuringShift":
                    useShiftCheckBox.SetCheck(true, 500);
                    useNonPrimaryMouseCheckBox.SetCheck(false, 500);
                    break;
                default:
                    break;
            }
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
            Session.Attach(powertoysWindowName, WindowSize.Small); // display window1
            var settingsView = Find<Element>(By.Name("Non Client Input Sink Window"));
            RunDragInteractions(
                preAction: () =>
                {
                    settingsView.DragAndHold(centerX, centerY);
                },
                postAction: () =>
                {
                    Session.PressKey(Key.Shift);
                },
                releaseAction: () =>
                {
                    settingsView.ReleaseDrag();
                    Session.ReleaseKey(Key.Shift);
                },
                testCaseName: nameof(TestShowZonesOnShiftDuringDrag));
        }

        [TestMethod]
        public void TestShowZonesOnDragDuringShift()
        {
            Session.Attach(powertoysWindowName, WindowSize.Small); // display window1
            var settingsView = Find<Element>(By.Name("Non Client Input Sink Window"));
            var offSet = ZoneSwitchHelper.GetOffset(settingsView, centerX, centerY);
            settingsView.Drag(offSet.Dx, offSet.Dy);
            RunDragInteractions(
                preAction: () =>
                {
                    Session.PressKey(Key.Shift);
                },
                postAction: () =>
                {
                    settingsView.DragAndHold(centerX + 20, centerY + 20);
                    settingsView.DragAndHold(centerX - 20, centerY - 20);
                },
                releaseAction: () =>
                {
                    Session.ReleaseKey(Key.Shift);
                },
                testCaseName: nameof(TestShowZonesOnDragDuringShift));
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

            releaseAction?.Invoke();
            var windowRectAfter = this.Session.GetWindowRect();
            Assert.AreNotEqual(windowRectBefore, windowRectAfter, $"[{testCaseName}] Window rect did not change.");

            // Assert the AppZoneHistory layout is set
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
    }
}
