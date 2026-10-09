// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Wox.Test.Plugins
{
    [TestClass]
    public class PluginInitTest
    {
        [TestMethod]
        public void PublicAPIIsNullTest()
        {
            // Assert.Throws(typeof(WoxFatalException), () => PluginManager.Initialize(null));
        }
    }
}
