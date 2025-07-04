// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.TimeDate.UnitTests
{
    [TestClass]
    public class BasicTests
    {
        [TestMethod]
        public void BasicTest()
        {
            // This is a basic test to verify the test project can run
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void DateTimeTest()
        {
            // Test basic DateTime functionality
            var now = DateTime.Now;
            Assert.IsNotNull(now);
            Assert.IsTrue(now > DateTime.MinValue);
        }
    }
}
