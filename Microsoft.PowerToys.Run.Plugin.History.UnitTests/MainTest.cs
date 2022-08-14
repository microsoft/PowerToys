// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Wox.Plugin;

namespace Microsoft.PowerToys.Run.Plugin.History.UnitTests
{
    [TestClass]
    public class MainTest
    {
        [TestMethod]
        [DataRow(@"abcd")]
        public void PlaceHolderTest(string input)
        {
            Assert.AreEqual(input, "abcd");
        }
    }
}