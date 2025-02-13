// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UITests_KeyboardManager
{
    [TestClass]
    public class RunKeyboardManagerUITests : UITestBase
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
        public static void ClassInit(TestContext testContext)
        {
            Debug.WriteLine("ClassInitialize executed");
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (_context != null)
            {
                _context = null;
            }

            Debug.WriteLine("ClassCleanup executed");
        }

        [TestMethod]
        public void EnableKeyboardManager() // verify the session is initialized
        {
            Debug.WriteLine("Test method executed");
            Session = SessionManager.Current;

            UITestBase.Enable_Module_from_Dashboard("Keyboard Manager");

            // Launch to KeyboardManagerEditor
            // session.FindElementByName<Button>("Remap a key").Click();
            Session?.FindElement<Button>(By.Name("Remap a key")).Click();
            Thread.Sleep(3000);
            Session = SessionManager.AttachSession(PowerToysModuleWindow.KeyboardManagerKeys);

            // Maximize window
            Session?.FindElementByName<Window>("Remap keys").Maximize();

            // Add Key Remapping
            Session?.FindElementByName<Button>("Add key remapping").Click();
            Session?.FindElementByName<Button>("Select").Click();
            Thread.Sleep(3000);
            Session?.FindElementByName<Button>("Cancel").Click();

            Session?.FindElement<Button>(By.Name("Cancel")).Click();
            UITestBase.Disable_Module_from_Dashboard("Keyboard Manager");
        }
    }
}
