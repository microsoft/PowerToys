// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.FancyZones.UnitTests.Utils;
using Microsoft.UITests.API;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UITests_FancyZones
{
    [TestClass]
    public class RunFancyZonesTest
    {
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
            UITestManager.Init();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            UITestManager.Close();
            if (_context != null)
            {
                _context = null;
            }
        }

        [TestMethod]
        public void RunFancyZones()
        {
            Thread.Sleep(2000);
            UITestManager.TestCode("Launch layout editor");
            Thread.Sleep(2000);
            var session = UITestManager.GetSession();
            session?.FindElementByName<Element>("Launch layout editor")?.Click();
            Thread.Sleep(4000);
            UITestManager.LaunchModuleWithWindowName(PowerToysModuleWindow.Fancyzone);
        }
    }
}
