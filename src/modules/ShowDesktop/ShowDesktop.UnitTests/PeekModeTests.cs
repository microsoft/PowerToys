// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ShowDesktop.UnitTests
{
    [TestClass]
    public class PeekModeTests
    {
        [TestMethod]
        public void Native_HasValue_Zero()
        {
            Assert.AreEqual(0, (int)PeekMode.Native);
        }

        [TestMethod]
        public void Minimize_HasValue_One()
        {
            Assert.AreEqual(1, (int)PeekMode.Minimize);
        }

        [TestMethod]
        public void FlyAway_HasValue_Two()
        {
            Assert.AreEqual(2, (int)PeekMode.FlyAway);
        }

        [TestMethod]
        public void PeekMode_HasExactlyThreeValues()
        {
            var values = System.Enum.GetValues<PeekMode>();
            Assert.AreEqual(3, values.Length);
        }

        [TestMethod]
        [DataRow(0, PeekMode.Native)]
        [DataRow(1, PeekMode.Minimize)]
        [DataRow(2, PeekMode.FlyAway)]
        public void PeekMode_CastsFromInt_Correctly(int intValue, PeekMode expected)
        {
            Assert.AreEqual(expected, (PeekMode)intValue);
        }
    }
}
