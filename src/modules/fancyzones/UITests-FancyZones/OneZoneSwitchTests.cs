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
        private static readonly int SubZones = 2;

        public OneZoneSwitchTests()
            : base(PowerToysModule.PowerToysSettings, WindowSize.Medium)
        {
        }

        [TestInitialize]
        public void TestInitialize()
        {
            CustomLayouts customLayouts = new CustomLayouts();

            CustomLayouts.CustomLayoutListWrapper customLayoutListWrapper = CustomLayoutsList;

            Files.CustomLayoutsIOHelper.WriteData(customLayouts.Serialize(customLayoutListWrapper));
            this.RestartScopeExe();
        }

        [TestMethod]
        public void TestLaunchFileExplore()
        {
            KillAllExplorerWindows();

            // Start Windows Explorer process
            LaunchExplorer("C:\\");
            string windowName = "Windows (C:) - File Explorer";
            this.Session.Attach(windowName, WindowSize.Large); // display window1

            int screenWidth = Screen.PrimaryScreen?.Bounds.Width ?? 1920;  // default 1920
            int screenHeight = Screen.PrimaryScreen?.Bounds.Height ?? 1080;

            int targetX = screenWidth / SubZones / 3;
            int targetY = screenWidth / SubZones / 2;

            var tabView = DragTabViewWithShift(targetX, targetY);

            // Start Windows Explorer process
            LaunchExplorer("C:\\Program Files (x86)");

            string windowName_file = "Program Files (x86) - File Explorer";
            this.Session.Attach(windowName_file, WindowSize.Large);

            var filetabView = DragTabViewWithShift(targetX, targetY);
            this.Session.KeyboardAction(OpenQA.Selenium.Keys.LeftControl, OpenQA.Selenium.Keys.Escape, OpenQA.Selenium.Keys.PageDown);
            Task.Delay(1000).Wait(); // Optional: Wait for a moment to ensure window switch

            string? activeWindowTitle = GetActiveWindowTitle();
            Console.WriteLine($"Active Window Title: {activeWindowTitle}");
            Assert.AreEqual("Windows (C:) - File Explorer", activeWindowTitle);
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

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        private static string? GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            if (GetWindowText(handle, buff, nChars) > 0)
            {
                return buff.ToString();
            }

            return null;
        }

        private static void KillAllExplorerWindows()
        {
            foreach (var process in Process.GetProcessesByName("explorer"))
            {
                try
                {
                    process.Kill();
                    process.WaitForExit();
                    Console.WriteLine($"Killed explorer.exe (PID: {process.Id})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to kill explorer.exe (PID: {process.Id}): {ex.Message}");
                }
            }
        }

        private void LaunchExplorer(string path)
        {
            var explorerProcessInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = path,
            };

            Process.Start(explorerProcessInfo);
            Task.Delay(2000).Wait(); // Wait for the Explorer window to fully launch
        }

        private Element DragTabViewWithShift(int targetX = 50, int targetY = 50)
        {
            var tabView = this.Find<Element>(Microsoft.PowerToys.UITest.By.AccessibilityId("TabView"));
            Assert.IsTrue(tabView.Rect.HasValue, "TabView rectangle should have a value.");

            int dx = targetX - tabView.Rect.Value.X;
            int dy = targetY - tabView.Rect.Value.Y;
            Console.WriteLine($"dx: {dx}, dy: {dy}");

            tabView.KeyDownAndDrag(OpenQA.Selenium.Keys.Shift, dx, dy);

            return tabView;
        }
    }
}
