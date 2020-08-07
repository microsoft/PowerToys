// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NUnit.Framework;
using Wox.Core.Plugin;
using Wox.Infrastructure.Exception;

namespace Wox.Test.Plugins
{
    [TestFixture]
    public class PluginInitTest
    {
        [Test]
        public void PublicAPIIsNullTest()
        {
            // Assert.Throws(typeof(WoxFatalException), () => PluginManager.Initialize(null));
        }
    }
}
