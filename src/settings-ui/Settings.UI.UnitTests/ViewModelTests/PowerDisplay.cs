// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.UnitTests.BackwardsCompatibility;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace ViewModelTests
{
    [TestClass]
    public class PowerDisplay
    {
        [TestMethod]
        public async Task DisposeShouldDisposeProfileOperationsCoordinator()
        {
            var viewModel = CreateViewModel();
            var coordinator = GetProfileOperationsCoordinator(viewModel);

            viewModel.Dispose();

            await Assert.ThrowsExceptionAsync<ObjectDisposedException>(
                () => coordinator.RunAsync(_ => Task.CompletedTask));
        }

        private static PowerDisplayViewModel CreateViewModel()
        {
            var settingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<LightSwitchSettings>().Object;
            var generalSettingsRepository = new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(
                ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>().Object);
            var powerDisplaySettingsRepository = new BackCompatTestProperties.MockSettingsRepository<PowerDisplaySettings>(
                ISettingsUtilsMocks.GetStubSettingsUtils<PowerDisplaySettings>().Object);

            return new PowerDisplayViewModel(
                settingsUtils,
                generalSettingsRepository,
                powerDisplaySettingsRepository,
                _ => 0,
                static (_, _) => { });
        }

        private static ProfileOperationCoordinator GetProfileOperationsCoordinator(PowerDisplayViewModel viewModel)
        {
            var field = typeof(PowerDisplayViewModel).GetField(
                "_profileOperations",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(field, "Could not find the compiled _profileOperations field.");

            return field.GetValue(viewModel) as ProfileOperationCoordinator
                ?? throw new AssertFailedException("The compiled _profileOperations field was not a ProfileOperationCoordinator.");
        }
    }
}
