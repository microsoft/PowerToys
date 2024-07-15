// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.UnitTests.BackwardsCompatibility;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace ViewModelTests
{
    [TestClass]
    public class General
    {
        public const string GeneralSettingsFileName = "Test\\GeneralSettings";

        private Mock<ISettingsUtils> mockGeneralSettingsUtils;

        [TestInitialize]
        public void SetUpStubSettingUtils()
        {
            mockGeneralSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();
        }

        [TestMethod]
        [DataRow("v0.18.2")]
        [DataRow("v0.19.2")]
        [DataRow("v0.20.1")]
        [DataRow("v0.21.1")]
        [DataRow("v0.22.0")]
        public void OriginalFilesModificationTest(string version)
        {
            var settingPathMock = new Mock<ISettingsPath>();
            var fileMock = BackCompatTestProperties.GetGeneralSettingsIOProvider(version);

            var mockGeneralSettingsUtils = new SettingsUtils(fileMock.Object, settingPathMock.Object);
            GeneralSettings originalGeneralSettings = mockGeneralSettingsUtils.GetSettingsOrDefault<GeneralSettings>();

            var generalSettingsRepository = new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(mockGeneralSettingsUtils);

            // Initialise View Model with test Config files
            // Arrange
            Func<string, int> sendMockIPCConfigMSG = msg => 0;
            Func<string, int> sendRestartAdminIPCMessage = msg => 0;
            Func<string, int> sendCheckForUpdatesIPCMessage = msg => 0;
            var viewModel = new GeneralViewModel(
                settingsRepository: generalSettingsRepository,
                runAsAdminText: "GeneralSettings_RunningAsAdminText",
                runAsUserText: "GeneralSettings_RunningAsUserText",
                isElevated: false,
                isAdmin: false,
                ipcMSGCallBackFunc: sendMockIPCConfigMSG,
                ipcMSGRestartAsAdminMSGCallBackFunc: sendRestartAdminIPCMessage,
                ipcMSGCheckForUpdatesCallBackFunc: sendCheckForUpdatesIPCMessage,
                configFileSubfolder: string.Empty);

            // Verify that the old settings persisted
            Assert.AreEqual(originalGeneralSettings.AutoDownloadUpdates, viewModel.AutoDownloadUpdates);
            Assert.AreEqual(originalGeneralSettings.PowertoysVersion, viewModel.PowerToysVersion);
            Assert.AreEqual(originalGeneralSettings.RunElevated, viewModel.RunElevated);
            Assert.AreEqual(originalGeneralSettings.Startup, viewModel.Startup);

            // Verify that the stub file was used
            var expectedCallCount = 2;  // once via the view model, and once by the test (GetSettings<T>)
            BackCompatTestProperties.VerifyGeneralSettingsIOProviderWasRead(fileMock, expectedCallCount);
        }

        [TestMethod]
        public void IsElevatedShouldUpdateRunasAdminStatusAttrsWhenSuccessful()
        {
            // Arrange
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            Func<string, int> sendRestartAdminIPCMessage = msg => { return 0; };
            Func<string, int> sendCheckForUpdatesIPCMessage = msg => { return 0; };
            GeneralViewModel viewModel = new GeneralViewModel(
                settingsRepository: SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object),
                "GeneralSettings_RunningAsAdminText",
                "GeneralSettings_RunningAsUserText",
                false,
                false,
                sendMockIPCConfigMSG,
                sendRestartAdminIPCMessage,
                sendCheckForUpdatesIPCMessage,
                GeneralSettingsFileName);

            Assert.AreEqual(viewModel.RunningAsUserDefaultText, viewModel.RunningAsText);
            Assert.IsFalse(viewModel.IsElevated);

            // Act
            viewModel.IsElevated = true;

            // Assert
            Assert.AreEqual(viewModel.RunningAsAdminDefaultText, viewModel.RunningAsText);
            Assert.IsTrue(viewModel.IsElevated);
        }

        [TestMethod]
        public void StartupShouldEnableRunOnStartUpWhenSuccessful()
        {
            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsTrue(snd.GeneralSettings.Startup);
                return 0;
            };

            // Arrange
            Func<string, int> sendRestartAdminIPCMessage = msg => { return 0; };
            Func<string, int> sendCheckForUpdatesIPCMessage = msg => { return 0; };
            GeneralViewModel viewModel = new GeneralViewModel(
                settingsRepository: SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object),
                "GeneralSettings_RunningAsAdminText",
                "GeneralSettings_RunningAsUserText",
                false,
                false,
                sendMockIPCConfigMSG,
                sendRestartAdminIPCMessage,
                sendCheckForUpdatesIPCMessage,
                GeneralSettingsFileName);
            Assert.IsFalse(viewModel.Startup);

            // act
            viewModel.Startup = true;
        }

        [TestMethod]
        public void RunElevatedShouldEnableAlwaysRunElevatedWhenSuccessful()
        {
            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsTrue(snd.GeneralSettings.RunElevated);
                return 0;
            };

            Func<string, int> sendRestartAdminIPCMessage = msg => { return 0; };
            Func<string, int> sendCheckForUpdatesIPCMessage = msg => { return 0; };

            // Arrange
            GeneralViewModel viewModel = new GeneralViewModel(
                settingsRepository: SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object),
                "GeneralSettings_RunningAsAdminText",
                "GeneralSettings_RunningAsUserText",
                false,
                false,
                sendMockIPCConfigMSG,
                sendRestartAdminIPCMessage,
                sendCheckForUpdatesIPCMessage,
                GeneralSettingsFileName);

            Assert.IsFalse(viewModel.RunElevated);

            // act
            viewModel.RunElevated = true;
        }

        [TestMethod]
        public void IsLightThemeRadioButtonCheckedShouldThemeToLightWhenSuccessful()
        {
            // Arrange
            GeneralViewModel viewModel = null;

            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.AreEqual("light", snd.GeneralSettings.Theme);
                return 0;
            };

            Func<string, int> sendRestartAdminIPCMessage = msg => { return 0; };
            Func<string, int> sendCheckForUpdatesIPCMessage = msg => { return 0; };
            viewModel = new GeneralViewModel(
                settingsRepository: SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object),
                "GeneralSettings_RunningAsAdminText",
                "GeneralSettings_RunningAsUserText",
                false,
                false,
                sendMockIPCConfigMSG,
                sendRestartAdminIPCMessage,
                sendCheckForUpdatesIPCMessage,
                GeneralSettingsFileName);
            Assert.AreNotEqual(1, viewModel.ThemeIndex);

            // act
            viewModel.ThemeIndex = 1;
        }

        [TestMethod]
        public void IsDarkThemeRadioButtonCheckedShouldThemeToDarkWhenSuccessful()
        {
            // Arrange
            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.AreEqual("dark", snd.GeneralSettings.Theme);
                return 0;
            };

            Func<string, int> sendRestartAdminIPCMessage = msg => { return 0; };
            Func<string, int> sendCheckForUpdatesIPCMessage = msg => { return 0; };
            GeneralViewModel viewModel = new GeneralViewModel(
                settingsRepository: SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object),
                "GeneralSettings_RunningAsAdminText",
                "GeneralSettings_RunningAsUserText",
                false,
                false,
                sendMockIPCConfigMSG,
                sendRestartAdminIPCMessage,
                sendCheckForUpdatesIPCMessage,
                GeneralSettingsFileName);
            Assert.AreNotEqual(0, viewModel.ThemeIndex);

            // act
            viewModel.ThemeIndex = 0;
        }

        [TestMethod]
        public void AllModulesAreEnabledByDefault()
        {
            // arrange
            EnabledModules modules = new EnabledModules();

            // Assert
            Assert.IsTrue(modules.FancyZones);
            Assert.IsTrue(modules.ImageResizer);
            Assert.IsTrue(modules.PowerPreview);
            Assert.IsTrue(modules.ShortcutGuide);
            Assert.IsTrue(modules.PowerRename);
            Assert.IsTrue(modules.PowerLauncher);
            Assert.IsTrue(modules.ColorPicker);
        }
    }
}
