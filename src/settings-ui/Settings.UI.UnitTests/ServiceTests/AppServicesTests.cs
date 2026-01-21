// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.PowerToys.Settings.UI.UnitTests.ServiceTests
{
    [TestClass]
    public class AppServicesTests
    {
        [TestMethod]
        public void AppServices_Configure_SetsIsConfiguredToTrue()
        {
            // Note: AppServices is static and may already be configured from other tests
            // This test verifies the configuration doesn't throw
            AppServices.Configure();

            Assert.IsTrue(AppServices.IsConfigured);
        }

        [TestMethod]
        public void AppServices_GetService_ReturnsNavigationService()
        {
            AppServices.Configure();

            var navigationService = AppServices.GetService<INavigationService>();

            Assert.IsNotNull(navigationService);
        }

        [TestMethod]
        public void AppServices_GetService_ReturnsSettingsService()
        {
            AppServices.Configure();

            var settingsService = AppServices.GetService<ISettingsService>();

            Assert.IsNotNull(settingsService);
        }

        [TestMethod]
        public void AppServices_GetService_ReturnsIPCService()
        {
            AppServices.Configure();

            var ipcService = AppServices.GetService<IIPCService>();

            Assert.IsNotNull(ipcService);
        }
    }
}
