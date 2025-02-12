// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.FancyZones.UnitTests.Utils;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UITests_FancyZones
{
    [TestClass]
    public class RunFancyZonesTest : UITestBase
    {
        private static TestContext? _context;

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            UITestBase.ClassInit(testContext);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            UITestBase.ClassClean();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            this.TestInit();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            this.TestClean();
        }

        [TestMethod]
        public void RunFancyZones()
        {
            Thread.Sleep(2000);
            Session = SessionManager.Current;
            if (Session == null)
            {
                Assert.IsNull(Session);
            }

            Session?.FindElementByName<Element>("Launch layout editor").Click();
            Thread.Sleep(4000);
            Session = SessionManager.AttachSession(PowerToysModuleWindow.Fancyzone);
        }
    }
}
