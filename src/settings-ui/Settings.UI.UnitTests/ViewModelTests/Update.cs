// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class Update
    {
        private sealed class TestSettingsRepository : ISettingsRepository<GeneralSettings>
        {
            public TestSettingsRepository(GeneralSettings settings)
            {
                SettingsConfig = settings;
            }

            public GeneralSettings SettingsConfig { get; set; }

            public event Action<GeneralSettings> SettingsChanged;

            public bool ReloadSettings()
            {
                SettingsChanged?.Invoke(SettingsConfig);
                return true;
            }
        }

        [DataTestMethod]
        [DataRow(UpdatingSettings.UpdatingState.UpToDate, (int)UpdateViewModel.TransientUpdateOperation.None, UpdateViewModel.UpdateUIState.UpToDate)]
        [DataRow(UpdatingSettings.UpdatingState.NetworkError, (int)UpdateViewModel.TransientUpdateOperation.None, UpdateViewModel.UpdateUIState.NetworkError)]
        [DataRow(UpdatingSettings.UpdatingState.ReadyToDownload, (int)UpdateViewModel.TransientUpdateOperation.None, UpdateViewModel.UpdateUIState.ReadyToDownload)]
        [DataRow(UpdatingSettings.UpdatingState.ReadyToInstall, (int)UpdateViewModel.TransientUpdateOperation.None, UpdateViewModel.UpdateUIState.ReadyToInstall)]
        [DataRow(UpdatingSettings.UpdatingState.ErrorDownloading, (int)UpdateViewModel.TransientUpdateOperation.None, UpdateViewModel.UpdateUIState.ErrorDownloading)]
        [DataRow(UpdatingSettings.UpdatingState.UpToDate, (int)UpdateViewModel.TransientUpdateOperation.Checking, UpdateViewModel.UpdateUIState.Checking)]
        [DataRow(UpdatingSettings.UpdatingState.ReadyToDownload, (int)UpdateViewModel.TransientUpdateOperation.Checking, UpdateViewModel.UpdateUIState.Checking)]
        [DataRow(UpdatingSettings.UpdatingState.ReadyToInstall, (int)UpdateViewModel.TransientUpdateOperation.Checking, UpdateViewModel.UpdateUIState.Checking)]
        [DataRow(UpdatingSettings.UpdatingState.UpToDate, (int)UpdateViewModel.TransientUpdateOperation.Downloading, UpdateViewModel.UpdateUIState.Downloading)]
        [DataRow(UpdatingSettings.UpdatingState.ReadyToDownload, (int)UpdateViewModel.TransientUpdateOperation.Downloading, UpdateViewModel.UpdateUIState.Downloading)]
        [DataRow(UpdatingSettings.UpdatingState.ReadyToInstall, (int)UpdateViewModel.TransientUpdateOperation.Downloading, UpdateViewModel.UpdateUIState.Downloading)]
        [DataRow(UpdatingSettings.UpdatingState.ReadyToInstall, (int)UpdateViewModel.TransientUpdateOperation.Installing, UpdateViewModel.UpdateUIState.ReadyToInstall)]
        public void GetUpdateUIStateShouldMapPersistentAndTransientStates(
            UpdatingSettings.UpdatingState updatingState,
            int activeUpdateOperation,
            UpdateViewModel.UpdateUIState expected)
        {
            Assert.AreEqual(
                expected,
                UpdateViewModel.GetUpdateUIState(
                    updatingState,
                    (UpdateViewModel.TransientUpdateOperation)activeUpdateOperation));
        }

        [TestMethod]
        public void CheckForUpdatesShouldShowProgressAndSendActionOnly()
        {
            string sentMessage = null;
            var generalSettings = new GeneralSettings
            {
                IncludePrereleaseUpdates = true,
            };
            generalSettings.Enabled.AlwaysOnTop = false;
            generalSettings.Enabled.FancyZones = false;
            generalSettings.Enabled.PowerLauncher = true;
            var settingsBeforeCheck = generalSettings.ToJsonString();
            var viewModel = CreateViewModel(
                new TestSettingsRepository(generalSettings),
                new UpdatingSettings(),
                message =>
                {
                    sentMessage = message;
                    return 0;
                });

            viewModel.CheckForUpdates();

            Assert.AreEqual(UpdateViewModel.UpdateUIState.Checking, viewModel.CurrentUpdateUIState);
            Assert.IsTrue(viewModel.IsActivityVisible);
            Assert.IsFalse(viewModel.CanStartAction);

            using var message = JsonDocument.Parse(sentMessage);
            var generalAction = message.RootElement.GetProperty("action").GetProperty("general");
            Assert.AreEqual("check_for_updates", generalAction.GetProperty("action_name").GetString());
            Assert.IsFalse(generalAction.TryGetProperty("enabled", out _));
            Assert.IsFalse(generalAction.TryGetProperty("include_prerelease_updates", out _));
            Assert.AreEqual(settingsBeforeCheck, generalSettings.ToJsonString());
        }

        [TestMethod]
        public void CheckForUpdatesShouldUseCheckingStateForAnExistingUpdate()
        {
            var updatingSettings = new UpdatingSettings
            {
                State = UpdatingSettings.UpdatingState.ReadyToInstall,
                DownloadedInstallerFilename = "PowerToysSetup.exe",
            };
            var viewModel = CreateViewModel(
                new TestSettingsRepository(new GeneralSettings()),
                updatingSettings,
                message => 0);

            viewModel.CheckForUpdates();

            Assert.AreEqual(UpdateViewModel.UpdateUIState.Checking, viewModel.CurrentUpdateUIState);
            Assert.IsFalse(viewModel.CanStartAction);
        }

        [TestMethod]
        public void CheckForUpdatesShouldRecoverWhenIpcDeliveryFails()
        {
            var viewModel = CreateViewModel(
                new TestSettingsRepository(new GeneralSettings()),
                new UpdatingSettings(),
                message => 1);

            viewModel.CheckForUpdates();

            Assert.AreEqual(UpdateViewModel.UpdateUIState.NetworkError, viewModel.CurrentUpdateUIState);
            Assert.IsTrue(viewModel.CanStartAction);
            Assert.IsTrue(viewModel.IsActivityVisible);
        }

        [TestMethod]
        public void CheckForUpdatesShouldRecoverWhenIpcDeliveryThrows()
        {
            var viewModel = CreateViewModel(
                new TestSettingsRepository(new GeneralSettings()),
                new UpdatingSettings(),
                message => throw new InvalidOperationException());

            viewModel.CheckForUpdates();

            Assert.AreEqual(UpdateViewModel.UpdateUIState.NetworkError, viewModel.CurrentUpdateUIState);
            Assert.IsTrue(viewModel.CanStartAction);
        }

        [TestMethod]
        public void RefreshUpdatingStateShouldCompleteTransientOperation()
        {
            var currentSettings = new UpdatingSettings();
            var viewModel = CreateViewModel(
                new TestSettingsRepository(new GeneralSettings()),
                currentSettings,
                message => 0,
                () => currentSettings);

            viewModel.CheckForUpdates();
            currentSettings = new UpdatingSettings
            {
                State = UpdatingSettings.UpdatingState.NetworkError,
            };

            viewModel.RefreshUpdatingState();

            Assert.AreEqual(UpdateViewModel.UpdateUIState.NetworkError, viewModel.CurrentUpdateUIState);
            Assert.IsTrue(viewModel.CanStartAction);
        }

        [TestMethod]
        public void RefreshUpdatingStateShouldRecoverWhenStateCannotBeLoaded()
        {
            var viewModel = CreateViewModel(
                new TestSettingsRepository(new GeneralSettings()),
                new UpdatingSettings(),
                message => 0,
                () => null);

            viewModel.CheckForUpdates();
            viewModel.RefreshUpdatingState();

            Assert.AreEqual(UpdateViewModel.UpdateUIState.NetworkError, viewModel.CurrentUpdateUIState);
            Assert.IsTrue(viewModel.CanStartAction);
        }

        [TestMethod]
        public void UpdateNowShouldShowDownloadingUntilUpdaterStateChanges()
        {
            bool updateStarted = false;
            var viewModel = CreateViewModel(
                new TestSettingsRepository(new GeneralSettings()),
                new UpdatingSettings
                {
                    State = UpdatingSettings.UpdatingState.ReadyToDownload,
                },
                message => 0,
                startUpdate: () => updateStarted = true);

            viewModel.UpdateNow();

            Assert.IsTrue(updateStarted);
            Assert.AreEqual(UpdateViewModel.UpdateUIState.Downloading, viewModel.CurrentUpdateUIState);
            Assert.IsTrue(viewModel.IsActivityVisible);
        }

        [TestMethod]
        public void UpdateNowShouldPreventStartingDownloadedInstallerTwice()
        {
            int updateStartCount = 0;
            var viewModel = CreateViewModel(
                new TestSettingsRepository(new GeneralSettings()),
                new UpdatingSettings
                {
                    State = UpdatingSettings.UpdatingState.ReadyToInstall,
                    DownloadedInstallerFilename = "PowerToysSetup.exe",
                },
                message => 0,
                startUpdate: () => updateStartCount++);

            viewModel.UpdateNow();
            viewModel.UpdateNow();

            Assert.AreEqual(1, updateStartCount);
            Assert.AreEqual(UpdateViewModel.UpdateUIState.ReadyToInstall, viewModel.CurrentUpdateUIState);
            Assert.IsFalse(viewModel.CanStartAction);
        }

        [TestMethod]
        public void UpdateNowShouldRecoverWhenStartingUpdaterFails()
        {
            var viewModel = CreateViewModel(
                new TestSettingsRepository(new GeneralSettings()),
                new UpdatingSettings
                {
                    State = UpdatingSettings.UpdatingState.ReadyToDownload,
                },
                message => 0,
                startUpdate: () => throw new InvalidOperationException());

            viewModel.UpdateNow();

            Assert.AreEqual(UpdateViewModel.UpdateUIState.ErrorDownloading, viewModel.CurrentUpdateUIState);
            Assert.IsTrue(viewModel.CanStartAction);
            Assert.IsTrue(viewModel.IsActivityVisible);
        }

        [TestMethod]
        public void UpdateNowShouldClearFailureWhenRetryingDownloadedInstaller()
        {
            int updateStartCount = 0;
            var viewModel = CreateViewModel(
                new TestSettingsRepository(new GeneralSettings()),
                new UpdatingSettings
                {
                    State = UpdatingSettings.UpdatingState.ReadyToInstall,
                    DownloadedInstallerFilename = "PowerToysSetup.exe",
                },
                message => 0,
                startUpdate: () =>
                {
                    updateStartCount++;
                    if (updateStartCount == 1)
                    {
                        throw new InvalidOperationException();
                    }
                });

            viewModel.UpdateNow();
            Assert.AreEqual(UpdateViewModel.UpdateUIState.ErrorDownloading, viewModel.CurrentUpdateUIState);

            viewModel.UpdateNow();

            Assert.AreEqual(2, updateStartCount);
            Assert.AreEqual(UpdateViewModel.UpdateUIState.ReadyToInstall, viewModel.CurrentUpdateUIState);
            Assert.IsFalse(viewModel.CanStartAction);
        }

#if DEBUG
        [TestMethod]
        public void UpdateNowShouldNotStartUpdaterWhilePreviewing()
        {
            bool updateStarted = false;
            var viewModel = CreateViewModel(
                new TestSettingsRepository(new GeneralSettings()),
                new UpdatingSettings(),
                message => 0,
                startUpdate: () => updateStarted = true);
            viewModel.SetDebugPreviewState(UpdateViewModel.UpdateUIState.ReadyToDownload);

            viewModel.UpdateNow();

            Assert.IsFalse(updateStarted);
            Assert.AreEqual(UpdateViewModel.UpdateUIState.Downloading, viewModel.CurrentUpdateUIState);
        }
#endif

        [TestMethod]
        public void DismissingActivityShouldHideSurfaceButKeepUpdateBadge()
        {
            var viewModel = CreateViewModel(
                new TestSettingsRepository(new GeneralSettings()),
                new UpdatingSettings
                {
                    State = UpdatingSettings.UpdatingState.ReadyToDownload,
                },
                message => 0);

            viewModel.RequestActivity();
            viewModel.DismissActivity();

            Assert.IsFalse(viewModel.IsActivityVisible);
            Assert.IsTrue(viewModel.ShowUpdateBadge);
        }

        [TestMethod]
        public void UpToDateActivityShouldRemainHiddenAtStartOfWindowSession()
        {
            var viewModel = CreateViewModel(
                new TestSettingsRepository(new GeneralSettings()),
                new UpdatingSettings(),
                message => 0);

            viewModel.RequestActivity();
            Assert.IsTrue(viewModel.IsActivityVisible);

            viewModel.DismissActivity();

            Assert.IsFalse(viewModel.IsActivityVisible);

            viewModel.BeginWindowSession();

            Assert.IsFalse(viewModel.IsActivityVisible);
        }

        [TestMethod]
        public void AvailableUpdateShouldShowActivityAtStartOfWindowSession()
        {
            var viewModel = CreateViewModel(
                new TestSettingsRepository(new GeneralSettings()),
                new UpdatingSettings
                {
                    State = UpdatingSettings.UpdatingState.ReadyToDownload,
                },
                message => 0);

            Assert.IsTrue(viewModel.IsActivityVisible);

            viewModel.DismissActivity();
            Assert.IsFalse(viewModel.IsActivityVisible);

            viewModel.BeginWindowSession();
            Assert.IsTrue(viewModel.IsActivityVisible);
        }

        private static UpdateViewModel CreateViewModel(
            ISettingsRepository<GeneralSettings> settingsRepository,
            UpdatingSettings initialSettings,
            Func<string, int> sendMessage,
            Func<UpdatingSettings> loadSettings = null,
            Action startUpdate = null)
        {
            loadSettings ??= () => initialSettings;
            startUpdate ??= () => { };

            return new UpdateViewModel(
                settingsRepository,
                sendMessage,
                loadSettings,
                startUpdate,
                false,
                false,
                null);
        }
    }
}
