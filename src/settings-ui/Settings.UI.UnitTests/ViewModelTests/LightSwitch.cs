// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
    public class LightSwitch
    {
        [TestMethod]
        public async Task IsProfilesLoadingTracksProfileOperationsCoordinator()
        {
            var viewModel = CreateViewModel();
            var coordinator = GetProfileOperationsCoordinator(viewModel);
            var operationStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var completeOperation = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var loadingStateChanges = 0;
            viewModel.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(LightSwitchViewModel.IsProfilesLoading))
                {
                    loadingStateChanges++;
                }
            };

            var operation = coordinator.RunAsync(
                async _ =>
                {
                    operationStarted.SetResult(true);
                    await completeOperation.Task;
                });
            await operationStarted.Task;

            Assert.IsTrue(viewModel.IsProfilesLoading);

            completeOperation.SetResult(true);
            await operation;

            Assert.IsFalse(viewModel.IsProfilesLoading);
            Assert.AreEqual(2, loadingStateChanges);
        }

        private static LightSwitchViewModel CreateViewModel()
        {
            var generalSettingsRepository = new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(
                ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>().Object);

            return new LightSwitchViewModel(generalSettingsRepository, new LightSwitchSettings(), _ => 0);
        }

        private static ProfileOperationCoordinator GetProfileOperationsCoordinator(LightSwitchViewModel viewModel)
        {
            var field = typeof(LightSwitchViewModel).GetField(
                "_profileOperations",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(field, "Could not find the compiled _profileOperations field.");

            return field.GetValue(viewModel) as ProfileOperationCoordinator
                ?? throw new AssertFailedException("The compiled _profileOperations field was not a ProfileOperationCoordinator.");
        }
    }
}
