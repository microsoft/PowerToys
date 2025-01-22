// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.FancyZones.UnitTests.Utils;
using Microsoft.UITests.API;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UITests_FancyZones
{
    [TestClass]
    public class RunFancyZonesTest
    {
        private const string PowerToysPath = @"\..\..\..\WinUI3Apps\PowerToys.Settings.exe";
        private static UITestAPI? _uITestAPI;

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
            _uITestAPI = new UITestAPI();
            _uITestAPI.Init(PowerToysPath);
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (_uITestAPI != null && _context != null)
            {
                _uITestAPI.Close(_context);
            }

            _context = null;
        }

        [TestMethod]
        public void RunFancyZones()
        {
            Assert.IsNotNull(_uITestAPI);
        }
    }
}
