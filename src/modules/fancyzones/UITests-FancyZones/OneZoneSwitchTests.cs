// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZones.UITests.Utils;
using Microsoft.FancyZonesEditor.UITests.Utils;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Windows;
using static Microsoft.FancyZonesEditor.UnitTests.Utils.FancyZonesEditorHelper;

namespace UITests_FancyZones
{
    [TestClass]
    public class OneZoneSwitchTests : UITestBase
    {
        private static readonly string WindowName = "Windows (C:) - File Explorer"; // set lauch explorer window name
        private static readonly string PowertoysWindowName = "PowerToys Settings"; // set powertoys settings window name
        private static readonly int SubZones = 2;
        private static readonly IOTestHelper AppZoneHistory = new FancyZonesEditorFiles().AppZoneHistoryIOHelper;

        public OneZoneSwitchTests()
            : base(PowerToysModule.PowerToysSettings, WindowSize.Medium)
        {
        }

        [TestInitialize]
        public void TestInitialize()
        {
            // set a custom layout with 2 subzones
            CustomLayouts customLayouts = new CustomLayouts();
            CustomLayouts.CustomLayoutListWrapper customLayoutListWrapper = CustomLayoutsList;
            Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(customLayoutListWrapper));

            // clear the app zone history
            AppZoneHistory.DeleteFile();
            string data = AppZoneHistory.GetData();
            Console.WriteLine($"1App zone history data: {data}");

            this.RestartScopeExe();
        }

        [TestMethod]
        public void TestLaunchFileExplore()
        {
            SnapWindowstoOneZone();

            string? activeWindowTitle = ZoneSwitchHelper.GetActiveWindowTitle();
            Assert.AreEqual(PowertoysWindowName, activeWindowTitle);

            // switch to the previous window by shortcut win+page down
            SendKeys(Key.Win, Key.PageDown);
            Task.Delay(1000).Wait(); // Optional: Wait for a moment to ensure window switch

            activeWindowTitle = ZoneSwitchHelper.GetActiveWindowTitle();
            Assert.AreEqual(WindowName, activeWindowTitle);
        }

        private void SnapWindowstoOneZone()
        {
            // assert the appzonehistory layout is set
            ZoneSwitchHelper.KillAllExplorerWindows();

            // Start Windows Explorer process
            ZoneSwitchHelper.LaunchExplorer("C:\\");
            Session.Attach(WindowName, WindowSize.UnSpecified); // display window1
            var tabView = Find<Element>(Microsoft.PowerToys.UITest.By.AccessibilityId("TabView"));
            tabView.DoubleClick(); // maximize the window

            // Set drag position of target zone
            int screenWidth = Screen.PrimaryScreen?.Bounds.Width ?? 1920;  // default 1920
            int screenHeight = Screen.PrimaryScreen?.Bounds.Height ?? 1080;

            int targetX = screenWidth / SubZones / 3;
            int targetY = screenWidth / SubZones / 2;

            // Drag the tab view to the target zone
            tabView.KeyDownAndDrag(Key.Shift, targetX, targetY);

            Session.Attach(PowertoysWindowName, WindowSize.UnSpecified);

            // Attach the PowerToys settings window to the front
            string name = "Non Client Input Sink Window";
            Element settingsView = Find<Element>(Microsoft.PowerToys.UITest.By.Name(name));
            settingsView.DoubleClick(); // maximize the window
            settingsView.KeyDownAndDrag(Key.Shift, targetX, targetY);

            // check the appzonehistory layout is set and in the same zone
            string appZoneHistoryJson = AppZoneHistory.GetData();
            Assert.AreEqual(
                ZoneSwitchHelper.GetZoneSetUuidByAppName(WindowName, appZoneHistoryJson),
                ZoneSwitchHelper.GetZoneSetUuidByAppName(PowertoysWindowName, appZoneHistoryJson));
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
    }
}
