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
using System.Xml.Linq;
using FancyZonesEditor.Models;
using FancyZonesEditorCommon.Data;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;

namespace UITests_FancyZones
{
    [TestClass]
    public class OneZoneSwitchTests : UITestBase
    {
        public OneZoneSwitchTests()
            : base(PowerToysModule.PowerToysSettings, WindowSize.Medium)
        {
        }

        [TestMethod]
        public void TestLaunchFileExplore()
        {
            KillAllExplorerWindows();

            // Start Windows Explorer process
            LaunchExplorer("C:\\");
            string windowName = "Windows (C:) - File Explorer";
            this.Session.Attach(windowName, WindowSize.Medium);

            DragTabViewWithShift();

            // Start Windows Explorer process
            LaunchExplorer("C:\\Program Files (x86)");

            string windowName_file = "Program Files (x86) - File Explorer";
            this.Session.Attach(windowName_file, WindowSize.Medium);

            var filetabView = DragTabViewWithShift();
            filetabView.SendKeys(Keys.Alt, Keys.Tab);
            Task.Delay(1000).Wait(); // Optional: Wait for a moment to ensure window switch

            string? activeWindowTitle = GetActiveWindowTitle();
            Console.WriteLine($"Active Window Title: {activeWindowTitle}");
            Assert.AreEqual("Windows (C:) - File Explorer", activeWindowTitle);
        }

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
            Task.Delay(500).Wait(); // Wait for the Explorer window to fully launch
        }

        private Element DragTabViewWithShift()
        {
            var tabView = this.Find<Element>(Microsoft.PowerToys.UITest.By.AccessibilityId("TabView"));

            int offsetX = 50;
            int offsetY = 50;

            tabView.KeyDownAndDrag(Keys.Shift, offsetX, offsetY);

            return tabView;
        }
    }
}
