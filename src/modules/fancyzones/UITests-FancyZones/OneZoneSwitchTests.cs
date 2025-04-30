// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.DirectoryServices;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;
using System.Xml.Linq;
using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;
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
    public class OneZoneSwitchTests : UITestBase
    {
        private static readonly int SubZones = 2;
        private static readonly IOTestHelper AppZoneHistory = new FancyZonesEditorFiles().AppZoneHistoryIOHelper;
        private static string powertoysWindowName = "PowerToys Settings"; // set powertoys settings window name

        public OneZoneSwitchTests()
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

            // Goto FancyZones setting page
            if (this.FindAll<NavigationViewItem>("FancyZones").Count == 0)
            {
                // Expand Advanced list-group if needed
                this.Find<NavigationViewItem>("Windowing & Layouts").Click();
            }

            this.Find<NavigationViewItem>("FancyZones").Click();
            this.Find<ToggleSwitch>("Enable FancyZones").Toggle(true);
            this.Session.SetMainWindowSize(WindowSize.Large);

            // fixed settings
            this.Find<Microsoft.PowerToys.UITest.CheckBox>("Hold Shift key to activate zones while dragging a window").SetCheck(true, 500);

            // should bind mouse to suitable zone for scrolling
            Find<Element>(By.AccessibilityId("HeaderPresenter")).Click();
            Scroll(9, "Down"); // Pull the setting page up to make sure the setting is visible
            bool switchWindowEnable = TestContext.TestName == "TestSwitchShortCutDisable" ? false : true;

            this.Find<ToggleSwitch>("Switch between windows in the current zone").Toggle(switchWindowEnable);

            Console.WriteLine($"Switch between windows in the current zone: {Find<ToggleSwitch>("Switch between windows in the current zone").IsOn}");
            Task.Delay(500).Wait(); // Wait for the setting to be applied
            Scroll(9, "Up"); // Pull the setting page down to make sure the setting is visible
            this.Find<Microsoft.PowerToys.UITest.Button>("Launch layout editor").Click(false, 500, 5000);
            this.Session.Attach(PowerToysModule.FancyZone);
            this.Find<Element>(By.Name("Custom Column")).Click();
            this.Find<Microsoft.PowerToys.UITest.Button>("Close").Click();
            this.Session.Attach(PowerToysModule.PowerToysSettings);
            LaunchFromSetting();
            this.RestartScopeExe();
        }

        // [TestMethod]
        public void TestSwitchWindow()
        {
            var (preWindow, postWindow) = SnapToOneZone();

            string? activeWindowTitle = ZoneSwitchHelper.GetActiveWindowTitle();
            Assert.AreEqual(postWindow, activeWindowTitle);

            // switch to the previous window by shortcut win+page down
            SendKeys(Key.Win, Key.PageDown);

            activeWindowTitle = ZoneSwitchHelper.GetActiveWindowTitle();
            Assert.AreEqual(preWindow, activeWindowTitle);

            // Clean settings
            Clean();
        }

        // [TestMethod]
        public void TestSwitchAfterDesktopChange()
        {
            var (preWindow, postWindow) = SnapToOneZone();

            string? windowTitle = ZoneSwitchHelper.GetActiveWindowTitle();
            Assert.AreEqual(postWindow, windowTitle);

            // Add virtual desktop
            SendKeys(Key.Ctrl, Key.Win, Key.D);

            // return back
            SendKeys(Key.Ctrl, Key.Win, Key.Left);
            Task.Delay(500).Wait(); // Optional: Wait for a moment to ensure window switch
            string? returnWindowTitle = ZoneSwitchHelper.GetActiveWindowTitle();
            Assert.AreEqual(postWindow, returnWindowTitle);

            // check shortcut
            SendKeys(Key.Win, Key.PageDown);
            string? activeWindowTitle = ZoneSwitchHelper.GetActiveWindowTitle();
            Assert.AreEqual(preWindow, activeWindowTitle);

            // close the virtual desktop
            SendKeys(Key.Ctrl, Key.Win, Key.Right);
            Task.Delay(500).Wait(); // Optional: Wait for a moment to ensure window switch
            SendKeys(Key.Ctrl, Key.Win, Key.F4);
            Task.Delay(500).Wait(); // Optional: Wait for a moment to ensure window switch

            // Clean settings
            Clean();
        }

        // [TestMethod]
        public void TestSwitchShortCutDisable()
        {
            var (preWindow, postWindow) = SnapToOneZone();

            string? activeWindowTitle = ZoneSwitchHelper.GetActiveWindowTitle();
            Assert.AreEqual(postWindow, activeWindowTitle);

            // switch to the previous window by shortcut win+page down
            SendKeys(Key.Win, Key.PageDown);
            Task.Delay(500).Wait(); // Optional: Wait for a moment to ensure window switch

            activeWindowTitle = ZoneSwitchHelper.GetActiveWindowTitle();
            Assert.AreNotEqual(preWindow, activeWindowTitle);

            // Clean Setting
            Clean();
        }

        private (string PreWindow, string PostWindow) SnapToOneZone()
        {
            this.Session.Attach(PowerToysModule.Hosts, WindowSize.Large_Vertical);

            var hostsView = Find<Element>(By.Name("Non Client Input Sink Window"));
            hostsView.DoubleClick(); // maximize the window

            var rect = Session.GetWindowRect();
            var (targetX, targetY) = ZoneSwitchHelper.GetScreenMargins(rect, 4);
            var offSet = ZoneSwitchHelper.GetOffset(hostsView, targetX, targetY);

            DragWithShift(hostsView, offSet);

            string preWindow = ZoneSwitchHelper.GetActiveWindowTitle();
            Console.WriteLine($"Window name: {preWindow}");

            // Attach the PowerToys settings window to the front
            Session.Attach(powertoysWindowName, WindowSize.UnSpecified);
            string windowNameFront = ZoneSwitchHelper.GetActiveWindowTitle();
            Console.WriteLine($"Window name: {windowNameFront}");
            Element settingsView = Find<Element>(By.Name("Non Client Input Sink Window"));
            settingsView.DoubleClick(); // maximize the window

            DragWithShift(settingsView, offSet);

            // Session.PressKey(Key.Shift);
            // settingsView.DragAndHold(offSet.Dx, offSet.Dy);
            // Task.Delay(1000).Wait(); // Optional: Wait for a moment to ensure the drag is in progress
            // settingsView.ReleaseDrag();
            // Task.Delay(1000).Wait();
            // Session.ReleaseKey(Key.Shift);

            // Assert.IsNotNull(zoneIndexOfPowertoys, "Powertoys Drag to zone Failed");

            // start exe
            // Session.KillAllProcessesByName("explorer");
            // Session.StartExe("explorer.exe", "C:\\");
            // Task.Delay(1000).Wait(); // Optional: Wait for a moment to ensure the window is open

            // Start Windows Explorer process
            // Session.Attach(windowName, WindowSize.Large_Vertical); // display window1
            // tabView.KeyDownAndDrag(Key.Shift, targetX, targetY);
            string appZoneHistoryJson = AppZoneHistory.GetData();

            string? zoneIndexOfFileWindow = ZoneSwitchHelper.GetZoneIndexSetByAppName("PowerToys.Hosts.exe", appZoneHistoryJson); // explorer.exe
            string? zoneIndexOfPowertoys = ZoneSwitchHelper.GetZoneIndexSetByAppName("PowerToys.Settings.exe", appZoneHistoryJson);

            Console.WriteLine($"zoneIndexOfFileWindow: {zoneIndexOfFileWindow}, zoneIndexOfPowertoys {zoneIndexOfPowertoys}");

            // check the AppZoneHistory layout is set and in the same zone
            Assert.AreEqual(zoneIndexOfPowertoys, zoneIndexOfFileWindow);

            return (preWindow, powertoysWindowName);
        }

        private void DragWithShift(Element settingsView, (int Dx, int Dy) offSet)
        {
            Session.PressKey(Key.Shift);
            settingsView.DragAndHold(offSet.Dx, offSet.Dy);
            Task.Delay(1000).Wait(); // Wait for drag to start (optional)
            settingsView.ReleaseDrag();
            Task.Delay(1000).Wait(); // Wait after drag (optional)
            Session.ReleaseKey(Key.Shift);
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
                        Columns = SubZones,
                        RowsPercentage = new List<int> { 10000 },
                        ColumnsPercentage = new List<int> { 5000, 5000 },
                        CellChildMap = new int[][] { [0, 1] },
                        SensitivityRadius = 20,
                        ShowSpacing = true,
                        Spacing = 10,
                    }),
                },
            },
        };

        // Pull the setting page up or down
        private void Scroll(int tries = 5, string direction = "Up")
        {
            MouseActionType mouseAction = direction == "Up" ? MouseActionType.ScrollUp : MouseActionType.ScrollDown;
            for (int i = 0; i < tries; i++)
            {
                Session.PerformMouseAction(mouseAction, 100, 1000); // Ensure settings are visible
            }
        }

        private void Clean()
        {
            Session.KillAllProcessesByName("explorer");
        }

        private void LaunchFromSetting(bool showWarning = false, bool launchAsAdmin = false)
        {
            // Goto Hosts File Editor setting page
            if (this.FindAll<NavigationViewItem>("Hosts File Editor").Count == 0)
            {
                // Expand Advanced list-group if needed
                this.Find<NavigationViewItem>("Advanced").Click();
            }

            this.Find<NavigationViewItem>("Hosts File Editor").Click();
            Task.Delay(1000).Wait();

            this.Find<ToggleSwitch>("Enable Hosts File Editor").Toggle(true);
            this.Find<ToggleSwitch>("Launch as administrator").Toggle(launchAsAdmin);
            this.Find<ToggleSwitch>("Show a warning at startup").Toggle(showWarning);

            // launch Hosts File Editor
            this.Find<Microsoft.PowerToys.UITest.Button>("Launch Hosts File Editor").Click();

            Task.Delay(1000).Wait();
        }
    }
}
