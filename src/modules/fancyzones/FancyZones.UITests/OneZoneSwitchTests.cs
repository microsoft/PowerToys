// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;
using Microsoft.FancyZones.UITests.Utils;
using Microsoft.FancyZonesEditor.UITests.Utils;
using Microsoft.FancyZonesEditor.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
            // kill all processes related to FancyZones Editor to ensure a clean state
            Session.KillAllProcessesByName("PowerToys.FancyZonesEditor");
            AppZoneHistory.DeleteFile();

            RestartScopeExe("Hosts");
            FancyZonesEditorHelper.Files.Restore();

            // Set a custom layout with 1 subzones and clear app zone history
            SetupCustomLayouts();

            // get PowerToys window Name
            powertoysWindowName = ZoneSwitchHelper.GetActiveWindowTitle();

            // Launch FancyZones
            LaunchFancyZones();

            // Launch the Hosts File Editor
            LaunchFromSetting();
        }

        /// <summary>
        /// Test switching between two snapped windows using keyboard shortcuts in FancyZones
        /// <list type="bullet">
        /// <item>
        /// <description>Verifies that after snapping two windows, the active window switches correctly using Win+PageDown.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod]
        [TestCategory("FancyZones #Switch between windows in the current zone #1")]
        public void TestSwitchWindow()
        {
            var (preWindow, postWindow) = SnapToOneZone();

            string? activeWindowTitle = ZoneSwitchHelper.GetActiveWindowTitle();
            Assert.AreEqual(postWindow, activeWindowTitle);

            // switch to the previous window by shortcut win+page down
            SendKeys(Key.Win, Key.PageDown);

            activeWindowTitle = ZoneSwitchHelper.GetActiveWindowTitle();
            Assert.AreEqual(preWindow, activeWindowTitle);

            Clean(); // close the windows
        }

        /// <summary>
        /// Test window switch behavior across virtual desktops in FancyZones
        /// <list type="bullet">
        /// <item>
        /// <description>Verifies that a window remains correctly snapped after switching desktops and can be switched using Win+PageDown.</description>
        /// </item>
        /// </summary>
        [TestMethod("FancyZones.Settings.TestSwitchAfterDesktopChange")]
        [TestCategory("FancyZones #Switch between windows in the current zone #2")]
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

            Clean(); // close the windows
        }

        /// <summary>
        /// Test window switching shortcut behavior when the shortcut is disabled in FancyZones settings
        /// <list type="bullet">
        /// <item>
        /// <description>Verifies that pressing Win+PageDown does not switch to the previously snapped window when the shortcut is disabled.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod("FancyZones.Settings.TestSwitchShortCutDisable")]
        [TestCategory("FancyZones #Switch between windows in the current zone #3")]
        public void TestSwitchShortCutDisable()
        {
            var (preWindow, postWindow) = SnapToOneZone();

            string? activeWindowTitle = ZoneSwitchHelper.GetActiveWindowTitle();
            Assert.AreEqual(postWindow, activeWindowTitle);

            // switch to the previous window by shortcut win+page down
            SendKeys(Key.Win, Key.PageDown);
            Task.Delay(500).Wait(); // Optional: Wait for a moment to ensure window switch

            activeWindowTitle = ZoneSwitchHelper.GetActiveWindowTitle();
            Assert.AreEqual(postWindow, activeWindowTitle);

            Clean(); // close the windows
        }

        private (string PreWindow, string PostWindow) SnapToOneZone()
        {
            this.Session.Attach(PowerToysModule.Hosts, WindowSize.Large_Vertical);

            var hostsView = Find<Pane>(By.Name("Non Client Input Sink Window"));
            hostsView.DoubleClick(); // maximize the window

            var rect = Session.GetMainWindowRect();
            var (targetX, targetY) = ZoneSwitchHelper.GetScreenMargins(rect, 4);

            // Snap first window (Hosts) to left zone using shift+drag with direct mouse movement
            var hostsRect = hostsView.Rect ?? throw new InvalidOperationException("Failed to get hosts window rect");
            int hostsStartX = hostsRect.Left + 70;
            int hostsStartY = hostsRect.Top + 25;

            // For a 2-column layout, left zone is at approximately 1/4 of screen width
            int hostsEndX = rect.Left + (3 * (rect.Right - rect.Left) / 4);
            int hostsEndY = rect.Top + ((rect.Bottom - rect.Top) / 2);

            Session.MoveMouseTo(hostsStartX, hostsStartY);
            Session.PerformMouseAction(MouseActionType.LeftDown);
            Session.PressKey(Key.Shift);
            Session.MoveMouseTo(hostsEndX, hostsEndY);
            Session.PerformMouseAction(MouseActionType.LeftUp);
            Session.ReleaseKey(Key.Shift);
            Task.Delay(500).Wait(); // Wait for snap to complete

            string preWindow = ZoneSwitchHelper.GetActiveWindowTitle();

            // Attach the PowerToys settings window to the front
            Session.Attach(powertoysWindowName, WindowSize.UnSpecified);
            string windowNameFront = ZoneSwitchHelper.GetActiveWindowTitle();
            Pane settingsView = Find<Pane>(By.Name("Non Client Input Sink Window"));
            settingsView.DoubleClick(); // maximize the window

            var windowRect = Session.GetMainWindowRect();
            var settingsRect = settingsView.Rect ?? throw new InvalidOperationException("Failed to get settings window rect");
            int settingsStartX = settingsRect.Left + 70;
            int settingsStartY = settingsRect.Top + 25;

            // For a 2-column layout, right zone is at approximately 3/4 of screen width
            int settingsEndX = windowRect.Left + (3 * (windowRect.Right - windowRect.Left) / 4);
            int settingsEndY = windowRect.Top + ((windowRect.Bottom - windowRect.Top) / 2);

            Session.MoveMouseTo(settingsStartX, settingsStartY);
            Session.PerformMouseAction(MouseActionType.LeftDown);
            Session.PressKey(Key.Shift);
            Session.MoveMouseTo(settingsEndX, settingsEndY);
            Session.PerformMouseAction(MouseActionType.LeftUp);
            Session.ReleaseKey(Key.Shift);
            Task.Delay(500).Wait(); // Wait for snap to complete

            string appZoneHistoryJson = AppZoneHistory.GetData();

            string? zoneIndexOfFileWindow = ZoneSwitchHelper.GetZoneIndexSetByAppName("PowerToys.Hosts.exe", appZoneHistoryJson);
            string? zoneIndexOfPowertoys = ZoneSwitchHelper.GetZoneIndexSetByAppName("PowerToys.Settings.exe", appZoneHistoryJson);

            // check the AppZoneHistory layout is set and in the same zone
            Assert.AreEqual(zoneIndexOfPowertoys, zoneIndexOfFileWindow);

            return (preWindow, powertoysWindowName);
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

        // clean window
        private void Clean()
        {
            // Close First window
            SendKeys(Key.Alt, Key.F4);

            // Close Second window
            SendKeys(Key.Alt, Key.F4);

            // clean app zone history file
            AppZoneHistory.DeleteFile();
        }

        // Setup custom layout with 1 subzones
        private void SetupCustomLayouts()
        {
            var customLayouts = new CustomLayouts();
            var customLayoutListWrapper = CustomLayoutsList;
            FancyZonesEditorHelper.Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(customLayoutListWrapper));
        }

        // launch FancyZones settings page
        private void LaunchFancyZones()
        {
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
            this.Find<CheckBox>("Hold Shift key to activate zones while dragging a window").SetCheck(true, 500);

            // should bind mouse to suitable zone for scrolling
            Find<Element>(By.AccessibilityId("HeaderPresenter")).Click();
            this.Scroll(9, "Down"); // Pull the setting page up to make sure the setting is visible
            bool switchWindowEnable = TestContext.TestName == "TestSwitchShortCutDisable" ? false : true;

            this.Find<ToggleSwitch>("FancyZonesWindowSwitchingToggle").Toggle(switchWindowEnable);

            // Go back and forth to make sure settings applied
            this.Find<NavigationViewItem>("Workspaces").Click();
            Task.Delay(200).Wait();
            this.Find<NavigationViewItem>("FancyZones").Click();

            this.Find<Button>("Open layout editor").Click(false, 500, 5000);
            this.Session.Attach(PowerToysModule.FancyZone);

            // pipeline machine may have an unstable delays, causing the custom layout to be unavailable as we set. then A retry is required.
            // Console.WriteLine($"after launch, Custom layout data: {customLayoutData}");
            try
            {
                // Set the FancyZones layout to a custom layout
                this.Find<Element>(By.Name("Custom Column")).Click();
            }
            catch (Exception)
            {
                // Console.WriteLine($"[Exception] Failed to attach to FancyZones window. Retrying...{ex.Message}");
                this.Find<Microsoft.PowerToys.UITest.Button>("Close").Click();
                this.Session.Attach(PowerToysModule.PowerToysSettings);
                SetupCustomLayouts();
                this.Find<Microsoft.PowerToys.UITest.Button>("Open layout editor").Click(false, 5000, 5000);
                this.Session.Attach(PowerToysModule.FancyZone);

                // customLayoutData = FancyZonesEditorHelper.Files.CustomLayoutsIOHelper.GetData();
                // Console.WriteLine($"after retry, Custom layout data: {customLayoutData}");

                // Set the FancyZones layout to a custom layout
                this.Find<Element>(By.Name("Custom Column")).Click();
            }

            // Close layout editor window
            SendKeys(Key.Alt, Key.F4);
            this.Session.Attach(powertoysWindowName);
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
            this.Find<ToggleSwitch>("Open as administrator").Toggle(launchAsAdmin);
            this.Find<ToggleSwitch>("Show a warning at startup").Toggle(showWarning);

            // launch Hosts File Editor
            this.Find<Button>("Open Hosts File Editor").Click();

            Task.Delay(5000).Wait();
        }
    }
}
