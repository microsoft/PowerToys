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
        private static int checkPositionX; // set check position
        private static int checkPositionY; // set check position
        private static int centerX; // set check position
        private static int centerY; // set check position
        private static int minTop;
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
            Console.WriteLine($"PowerToys window name: {powertoysWindowName}");

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

            int tries = 3;
            Pull(tries, "down"); // Pull the setting page up to make sure the setting is visible
            ZoneBehaviourSettings(TestContext.TestName);

            Pull(tries, "up");
            this.Find<Microsoft.PowerToys.UITest.Button>("Launch layout editor").Click(false, 500, 4000);
            this.Session.Attach(PowerToysModule.FancyZone);
            this.Find<Element>(By.Name("Custom Column")).Click();
            this.Find<Microsoft.PowerToys.UITest.Button>("Maximize").Click();
            var windowCenter = this.Session.GetWindowCenter();
            centerX = windowCenter.CenterX;
            centerY = windowCenter.CenterY;
            minTop = this.Session.GetWindowRect().Top;
            this.Find<Microsoft.PowerToys.UITest.Button>("Close").Click();
        }

        public void ZoneBehaviourSettings(string? testName)
        {
            Microsoft.PowerToys.UITest.CheckBox useShiftCheckBox = this.Find<Microsoft.PowerToys.UITest.CheckBox>("Hold Shift key to activate zones while dragging a window");
            Microsoft.PowerToys.UITest.CheckBox useNonPrimaryMouseCheckBox = this.Find<Microsoft.PowerToys.UITest.CheckBox>("Use a non-primary mouse button to toggle zone activation");
            switch (testName)
            {
                case "TestShowZonesOnShiftDuringDrag":
                    useShiftCheckBox.SetCheck(true, 500);
                    useNonPrimaryMouseCheckBox.SetCheck(false, 500);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// Test  in the FancyZones
        /// <list type="bullet">
        /// <item>
        /// <description>Validating Adding-entry Button works correctly.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod]
        public void TestShowZonesOnShiftDuringDrag()
        {
            // assert the AppZoneHistory layout is set
            // Start Windows Explorer process
            Session.Attach(powertoysWindowName, WindowSize.Small); // display window1
            GetOutWindowPixelColor(10);

            Element settingsView = Find<Element>(By.Name("Non Client Input Sink Window"));
            if (settingsView?.Rect is not { } rect)
            {
                throw new InvalidOperationException("Element 'settingsView' does not have a valid Rect. Cannot perform drag operation.");
            }

            settingsView.DragAndHold(centerX, centerY);
            var windowRect = this.Session.GetWindowRect();
            GetOutWindowPixelColor(20);

            // Tests shift work or not
            Session.PressKey(Key.Shift);
            string zoneColor = this.Session.GetPixelColorString(checkPositionX, checkPositionY); // Removed trailing whitespace

            // relese mouse and shift key
            Session.ReleaseKey(Key.Shift);
            settingsView.ReleaseDrag();

            var intoZonewindowRect = this.Session.GetWindowRect();
            Assert.AreNotEqual(windowRect, intoZonewindowRect, "Window rects are equal");
            Console.WriteLine($"intoZonewindowRect: {intoZonewindowRect}");
        }

        public void GetOutWindowPixelColor(int offset)
        {
            var windowRect = this.Session.GetWindowRect(); // Get the window rectangle
            int top = windowRect.Top; // Extract the 'Top' value from the tuple
            int left = windowRect.Left; // Extract the 'Left' value from the tuple
            checkPositionY = top - offset;
            checkPositionX = left;
            Assert.IsTrue(checkPositionY > minTop, "checkPositionY is not greater than minTop");

            string originalZoneColor = this.Session.GetPixelColorString(checkPositionX, checkPositionY); // Removed trailing whitespace
            Console.WriteLine($"Original zone color: {originalZoneColor}");
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
                        Spacing = 10,
                    }),
                },
            },
        };
    }
}
