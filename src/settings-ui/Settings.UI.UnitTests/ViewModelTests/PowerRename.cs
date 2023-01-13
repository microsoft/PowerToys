// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.UnitTests.BackwardsCompatibility;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    [TestClass]
    public class PowerRename
    {
        public const string GeneralSettingsFileName = "Test\\PowerRename";

        private Mock<ISettingsUtils> mockGeneralSettingsUtils;

        private Mock<ISettingsUtils> mockPowerRenamePropertiesUtils;

        [TestInitialize]
        public void SetUpStubSettingUtils()
        {
            mockGeneralSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();
            mockPowerRenamePropertiesUtils = ISettingsUtilsMocks.GetStubSettingsUtils<PowerRenameLocalProperties>();
        }

        /// <summary>
        /// Test if the original settings files were modified.
        /// </summary>
        [TestMethod]
        [DataRow("v0.18.2", "power-rename-settings.json")]
        [DataRow("v0.19.2", "power-rename-settings.json")]
        [DataRow("v0.20.1", "power-rename-settings.json")]
        [DataRow("v0.22.0", "power-rename-settings.json")]
        public void OriginalFilesModificationTest(string version, string fileName)
        {
            var settingPathMock = new Mock<ISettingsPath>();
            var mockIOProvider = BackCompatTestProperties.GetModuleIOProvider(version, PowerRenameSettings.ModuleName, fileName);

            var mockSettingsUtils = new SettingsUtils(mockIOProvider.Object, settingPathMock.Object);
            PowerRenameLocalProperties originalSettings = mockSettingsUtils.GetSettingsOrDefault<PowerRenameLocalProperties>(PowerRenameSettings.ModuleName);

            var mockGeneralIOProvider = BackCompatTestProperties.GetGeneralSettingsIOProvider(version);

            var mockGeneralSettingsUtils = new SettingsUtils(mockGeneralIOProvider.Object, settingPathMock.Object);
            GeneralSettings originalGeneralSettings = mockGeneralSettingsUtils.GetSettingsOrDefault<GeneralSettings>();
            var generalSettingsRepository = new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(mockGeneralSettingsUtils);

            // Initialise View Model with test Config files
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockSettingsUtils, generalSettingsRepository, sendMockIPCConfigMSG);

            // Verify that the old settings persisted
            Assert.AreEqual(originalGeneralSettings.Enabled.PowerRename, viewModel.IsEnabled);
            Assert.AreEqual(originalSettings.ExtendedContextMenuOnly, viewModel.EnabledOnContextExtendedMenu);
            Assert.AreEqual(originalSettings.MRUEnabled, viewModel.MRUEnabled);
            Assert.AreEqual(originalSettings.ShowIcon, viewModel.EnabledOnContextMenu);

            // Verify that the stub file was used
            var expectedCallCount = 2;  // once via the view model, and once by the test (GetSettings<T>)
            BackCompatTestProperties.VerifyModuleIOProviderWasRead(mockIOProvider, PowerRenameSettings.ModuleName, expectedCallCount);
            BackCompatTestProperties.VerifyGeneralSettingsIOProviderWasRead(mockGeneralIOProvider, expectedCallCount);
        }

        [TestMethod]
        public void IsEnabledShouldEnableModuleWhenSuccessful()
        {
            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsTrue(snd.GeneralSettings.Enabled.PowerRename);
                return 0;
            };

            // arrange
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, GeneralSettingsFileName);

            // act
            viewModel.IsEnabled = true;
        }

        [TestMethod]
        public void MRUEnabledShouldSetValue2TrueWhenSuccessful()
        {
            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                PowerRenameSettingsIPCMessage snd = JsonSerializer.Deserialize<PowerRenameSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.PowerRename.Properties.MRUEnabled.Value);
                return 0;
            };

            // arrange
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, GeneralSettingsFileName);

            // act
            viewModel.MRUEnabled = true;
        }

        [TestMethod]
        public void WhenIsEnabledIsOffAndMRUEnabledIsOffGlobalAndMruShouldBeOff()
        {
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, GeneralSettingsFileName);

            viewModel.IsEnabled = false;
            viewModel.MRUEnabled = false;

            Assert.IsFalse(viewModel.GlobalAndMruEnabled);
        }

        [TestMethod]
        public void WhenIsEnabledIsOffAndMRUEnabledIsOnGlobalAndMruShouldBeOff()
        {
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, GeneralSettingsFileName);

            viewModel.IsEnabled = false;
            viewModel.MRUEnabled = true;

            Assert.IsFalse(viewModel.GlobalAndMruEnabled);
        }

        [TestMethod]
        public void WhenIsEnabledIsOnAndMRUEnabledIsOffGlobalAndMruShouldBeOff()
        {
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, GeneralSettingsFileName);

            viewModel.IsEnabled = true;
            viewModel.MRUEnabled = false;

            Assert.IsFalse(viewModel.GlobalAndMruEnabled);
        }

        [TestMethod]
        public void WhenIsEnabledIsOnAndMRUEnabledIsOnGlobalAndMruShouldBeOn()
        {
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, GeneralSettingsFileName);

            viewModel.IsEnabled = true;
            viewModel.MRUEnabled = true;

            Assert.IsTrue(viewModel.GlobalAndMruEnabled);
        }

        [TestMethod]
        public void EnabledOnContextMenuShouldSetValue2TrueWhenSuccessful()
        {
            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                PowerRenameSettingsIPCMessage snd = JsonSerializer.Deserialize<PowerRenameSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.PowerRename.Properties.ShowIcon.Value);
                return 0;
            };

            // arrange
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, GeneralSettingsFileName);

            // act
            viewModel.EnabledOnContextMenu = true;
        }

        [TestMethod]
        public void EnabledOnContextExtendedMenuShouldSetValue2TrueWhenSuccessful()
        {
            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                PowerRenameSettingsIPCMessage snd = JsonSerializer.Deserialize<PowerRenameSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.PowerRename.Properties.ShowIcon.Value);
                return 0;
            };

            // arrange
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, GeneralSettingsFileName);

            // act
            viewModel.EnabledOnContextMenu = true;
        }

        [TestMethod]
        public void RestoreFlagsOnLaunchShouldSetValue2TrueWhenSuccessful()
        {
            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                PowerRenameSettingsIPCMessage snd = JsonSerializer.Deserialize<PowerRenameSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.PowerRename.Properties.PersistState.Value);
                return 0;
            };

            // arrange
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, GeneralSettingsFileName);

            // act
            viewModel.RestoreFlagsOnLaunch = true;
        }

        [TestMethod]
        public void MaxDispListNumShouldSetMaxSuggListTo20WhenSuccessful()
        {
            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                PowerRenameSettingsIPCMessage snd = JsonSerializer.Deserialize<PowerRenameSettingsIPCMessage>(msg);
                Assert.AreEqual(20, snd.Powertoys.PowerRename.Properties.MaxMRUSize.Value);
                return 0;
            };

            // arrange
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, GeneralSettingsFileName);

            // act
            viewModel.MaxDispListNum = 20;
        }
    }
}
