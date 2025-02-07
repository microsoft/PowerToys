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
            mUITestAPI.Init();
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

        [TestMethod]
        public void RunFancyZones()
        {
            Assert.IsNotNull(mUITestAPI);
            Thread.Sleep(2000);
            mUITestAPI.TestCode("Launch layout editor");
            Thread.Sleep(2000);
            mUITestAPI.Click_Element("Launch layout editor");
            Thread.Sleep(4000);
            mUITestAPI.LaunchModuleWithWindowName(PowerToysModuleWindow.Fancyzone);
            mUITestAPI?.Click_CreateNewLayout();
        }
    }
}
