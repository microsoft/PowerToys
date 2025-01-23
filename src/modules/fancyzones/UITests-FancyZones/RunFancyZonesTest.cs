// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.FancyZones.UnitTests.Utils;
using Microsoft.UITests.API;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UITests_FancyZones
{
    [TestClass]
    public class RunFancyZonesTest
    {
        private const string PowerToysPath = @"\..\..\..\WinUI3Apps\PowerToys.Settings.exe";
        private static UITestAPI? mUITestAPI;

        private static TestContext? _context;

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
        }

        [TestInitialize]
        public void TestInitialize()
        {
            mUITestAPI = new UITestAPI();
            mUITestAPI.Init("PowerToys.Settings", PowerToysPath, "PowerToys.Settings");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (mUITestAPI != null && _context != null)
            {
                mUITestAPI.Close(_context);
            }

            _context = null;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [TestMethod]
        public void RunFancyZones()
        {
            List<string> windowTitles = new List<string>();
            _ = EnumWindows(
                (hWnd, lParam) =>
            {
                if (IsWindowVisible(hWnd))
                {
                    StringBuilder windowText = new StringBuilder(256);
                    _ = GetWindowText(hWnd, windowText, windowText.Capacity);
                    if (windowText.Length > 0)
                    {
                        windowTitles.Add(windowText.ToString());
                    }
                }

                return true;
            },
                IntPtr.Zero);

            foreach (string title in windowTitles)
            {
                Console.WriteLine(title);
            }

            Assert.IsNotNull(mUITestAPI);

            mUITestAPI.LuanchApp("PowerToys.FancyZonesEditor", "FancyZones Editor");
            mUITestAPI?.Click_CreateNewLayout();
        }
    }
}
