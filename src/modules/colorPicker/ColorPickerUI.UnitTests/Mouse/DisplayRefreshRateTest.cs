// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ColorPicker.Mouse;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Mouse
{
    [TestClass]
    public class DisplayRefreshRateTest
    {
        [TestMethod]
        [DataRow(0, 60.0)]
        [DataRow(1, 60.0)]
        [DataRow(60, 60.0)]
        [DataRow(144, 144.0)]
        public void GetDisplayRefreshRateOrDefault_HandlesDefaultRefreshRateSentinels(int displayFrequency, double expectedRefreshRate)
        {
            Assert.AreEqual(expectedRefreshRate, MouseInfoProvider.GetDisplayRefreshRateOrDefault((uint)displayFrequency));
        }
    }
}
