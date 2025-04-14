// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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

        [TestMethod]
        public void TestLaunchFileExplore()
        {
            KillAllExplorerWindows();

            // Start Windows Explorer process
            var explorerProcessInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe", // This launches Windows Explorer
                Arguments = "C:\\",  // You can specify any path you want Explorer to open, like C:\, D:\, or any directory
            };

            Process.Start(explorerProcessInfo);  // Start the explorer.exe process
            Task.Delay(2000).Wait(); // Optional: Wait for a moment to ensure Explorer is up and running

            string windowName = "Windows (C:) - File Explorer";
            this.Session.Attach(windowName, WindowSize.Medium);

            var tabView = this.Find<Element>(Microsoft.PowerToys.UITest.By.AccessibilityId("TabView"));
            int offsetX = 50;
            int offsetY = 50;
            tabView.KeyDownAndDrag(Keys.Shift, offsetX, offsetY);

            // Start Windows Explorer process
            var explorerProcessInfo_2 = new ProcessStartInfo
            {
                FileName = "explorer.exe", // This launches Windows Explorer
                Arguments = "C:\\Program Files (x86)",  // You can specify any path you want Explorer to open, like C:\, D:\, or any directory
            };

            Process.Start(explorerProcessInfo_2);  // Start the explorer.exe process
            Task.Delay(2000).Wait(); // Optional: Wait for a moment to ensure Explorer is up and running

            string windowName_file = "Program Files (x86) - File Explorer";
            this.Session.Attach(windowName_file, WindowSize.Medium);

            var filetabView = this.Find<Element>(Microsoft.PowerToys.UITest.By.AccessibilityId("TabView"));
            filetabView.KeyDownAndDrag(Keys.Shift, offsetX,setY = 50);

            filetabView.SendKeys(Keys.Alt, Keys.Tab);
            Task.Delay(2000).Wait(); // Optional: Wait for a moment to ensure Explorer is up and running

            // filetabView.SendKeys(Keys.Alt, Keys.Tab);
            // Task.Delay(2000).Wait();
        }
    }
}
