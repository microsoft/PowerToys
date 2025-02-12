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
            UITestManager.Init();
            Debug.WriteLine("ClassInitialize executed");
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            UITestManager.Close();
            if (_context != null)
            {
                _context = null;
            }

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
            var session = UITestManager.GetSession();

            UITestManager.Enable_Module_from_Dashboard("Keyboard Manager");

            // Launch to KeyboardManagerEditor
            // session.FindElementByName<Button>("Remap a key").Click();
            session.FindElement<Button>(By.Name("Remap a key")).Click();
            Thread.Sleep(3000);
            UITestManager.LaunchModuleWithWindowName(PowerToysModuleWindow.KeyboardManagerKeys);
            session = UITestManager.GetSession();

            // Maximize window
            session.FindElementByName<Window>("Remap keys").Maximize();

            // Add Key Remapping
            session.FindElementByName<Button>("Add key remapping").Click();
            session.FindElementByName<Button>("Select").Click();
            Thread.Sleep(3000);
            session.FindElementByName<Button>("Cancel").Click();

            session.FindElement<Button>(By.Name("Cancel")).Click();
            UITestManager.CloseModule(PowerToysModuleWindow.KeyboardManagerKeys);
            UITestManager.Disable_Module_from_Dashboard("Keyboard Manager");
        }
    }
}
