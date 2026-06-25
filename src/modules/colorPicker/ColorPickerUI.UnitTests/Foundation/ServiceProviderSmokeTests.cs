// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ColorPicker.Foundation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ColorPicker.UnitTests.Foundation
{
    [TestClass]
    public class ServiceProviderSmokeTests
    {
        [TestMethod]
        public void Container_resolves_the_exit_token_source_as_a_singleton()
        {
            var provider = AppServices.Configure();

            var a = provider.GetRequiredService<System.Threading.CancellationTokenSource>();
            var b = provider.GetRequiredService<System.Threading.CancellationTokenSource>();

            Assert.IsNotNull(a);
            Assert.AreSame(a, b);
        }
    }
}
