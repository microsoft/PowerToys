// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.UITests.API;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UITests_KeyboardManager
{
    [TestClass]
    public class RunKeyboardManagerUITests
    {
        private const string PowerToysSettingsPath = @"\..\..\..\WinUI3Apps\PowerToys.Settings.exe";
        private static UITestAPI? mUITestAPI;

        private static TestContext? _context;

        [AssemblyInitialize]
        public static void SetupAll(TestContext context)
        {
            Debug.WriteLine("AssemblyInitialize executed");
        }

        [AssemblyCleanup]
        public static void CleanupAll()
        {
            Debug.WriteLine("AssemblyCleanup executed");
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            mUITestAPI = new UITestAPI();
            mUITestAPI.Init("PowerToys.Settings", PowerToysSettingsPath, "PowerToys.Settings");
            Debug.WriteLine("ClassInitialize executed");
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (mUITestAPI != null && _context != null)
            {
                mUITestAPI.Close(_context);
            }

            _context = null;
            Debug.WriteLine("ClassCleanup executed");
        }

        [TestInitialize]
        public void TestInitialize()
        {
            Debug.WriteLine("TestInitialize executed");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            Debug.WriteLine("TestCleanup executed");
        }

        [TestMethod]
        public void EnableKeyboardManager() // verify the session is initialized
        {
            Debug.WriteLine("Test method executed");
            Assert.IsNotNull(mUITestAPI);

            mUITestAPI.Enable_Module_from_Dashboard("Keyboard Manager");
            mUITestAPI.Click_Element("Remap a key");
            Thread.Sleep(5000);
            mUITestAPI.LaunchApp("PowerToys.KeyboardManagerEditor", "Remap keys");
            mUITestAPI.Click_Element("Add key remapping");
            mUITestAPI.Click_Element("Cancel");
            mUITestAPI.CloseApp("PowerToys.KeyboardManagerEditor");
            mUITestAPI.Disable_Module_from_Dashboard("Keyboard Manager");
        }
    }
}
