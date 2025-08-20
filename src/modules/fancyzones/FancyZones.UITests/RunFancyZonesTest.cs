// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.FancyZones.UnitTests.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UITests_FancyZones
{
    [TestClass]
    public class RunFancyZonesTest
    {
        private static FancyZonesSession? _session;

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            _session = new FancyZonesSession(testContext);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            _session?.Close();
        }

        [TestMethod]
        public void RunFancyZones()
        {
            Assert.IsNotNull(_session?.FancyZonesProcess);
        }
    }
}
