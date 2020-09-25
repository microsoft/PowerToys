// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.ViewModels;
using Microsoft.PowerToys.Settings.UI.UnitTests.BackwardsCompatibility;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    [TestClass]
    public class PowerRename
    {
        // This should not be changed. 
        // Changing it will cause user's to lose their local settings configs.
        public const string ModuleName = PowerRenameSettings.ModuleName;

        public const string generalSettings_file_name = "Test\\PowerRename";

        private Mock<ISettingsUtils> mockGeneralSettingsUtils;

        private Mock<ISettingsUtils> mockPowerRenamePropertiesUtils;

        [TestInitialize]
        public void SetUp_StubSettingUtils()
        {
            mockGeneralSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();
            mockPowerRenamePropertiesUtils = ISettingsUtilsMocks.GetStubSettingsUtils<PowerRenameLocalProperties>();
        }

        /// <summary>
        /// Test if the original settings files were modified.
        /// </summary>
        [TestMethod]
        [DataRow("v0.20.2", "power-rename-settings.json")]
        public void OriginalFilesModificationTest(string version, string fileName)
        {
            var mockIOProvider = BackCompatTestProperties.GetModuleIOProvider(version, ModuleName, fileName);
            var mockSettingsUtils = new SettingsUtils(mockIOProvider.Object);
            PowerRenameLocalProperties originalSettings = mockSettingsUtils.GetSettings<PowerRenameLocalProperties>(ModuleName);

            var mockGeneralIOProvider = BackCompatTestProperties.GetGeneralSettingsIOProvider(version);
            var mockGeneralSettingsUtils = new SettingsUtils(mockGeneralIOProvider.Object);
            GeneralSettings originalGeneralSettings = mockGeneralSettingsUtils.GetSettings<GeneralSettings>();
            var generalSettingsRepository = new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(mockGeneralSettingsUtils);

            // Initialise View Model with test Config files
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockSettingsUtils, generalSettingsRepository, SendMockIPCConfigMSG);

            // Verifiy that the old settings persisted
            Assert.AreEqual(originalGeneralSettings.Enabled.PowerRename, viewModel.IsEnabled);
            Assert.AreEqual(originalSettings.ExtendedContextMenuOnly, viewModel.EnabledOnContextExtendedMenu);
            Assert.AreEqual(originalSettings.MRUEnabled, viewModel.MRUEnabled);
            Assert.AreEqual(originalSettings.ShowIcon, viewModel.EnabledOnContextMenu);

            //Verify that the stub file was used
            var expectedCallCount = 2;  //once via the view model, and once by the test (GetSettings<T>)
            BackCompatTestProperties.VerifyModuleIOProviderWasRead(mockIOProvider, ModuleName, expectedCallCount);
            BackCompatTestProperties.VerifyGeneralSettingsIOProviderWasRead(mockGeneralIOProvider, expectedCallCount);
        }

        [TestMethod]
        public void IsEnabled_ShouldEnableModule_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsTrue(snd.GeneralSettings.Enabled.PowerRename);
                return 0;
            };

            // arrange
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG, generalSettings_file_name);

            // act
            viewModel.IsEnabled = true;
        }

        [TestMethod]
        public void MRUEnabled_ShouldSetValue2True_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                PowerRenameSettingsIPCMessage snd = JsonSerializer.Deserialize<PowerRenameSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.PowerRename.Properties.MRUEnabled.Value);
                return 0;
            };

            // arrange
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG, generalSettings_file_name);

            // act
            viewModel.MRUEnabled = true;
        }

        [TestMethod]
        public void WhenIsEnabledIsOffAndMRUEnabledIsOffGlobalAndMruShouldBeOff()
        {
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG, generalSettings_file_name);

            viewModel.IsEnabled = false;
            viewModel.MRUEnabled = false;

            Assert.IsFalse(viewModel.GlobalAndMruEnabled);
        }

        [TestMethod]
        public void WhenIsEnabledIsOffAndMRUEnabledIsOnGlobalAndMruShouldBeOff()
        {
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG, generalSettings_file_name);

            viewModel.IsEnabled = false;
            viewModel.MRUEnabled = true;

            Assert.IsFalse(viewModel.GlobalAndMruEnabled);
        }

        [TestMethod]
        public void WhenIsEnabledIsOnAndMRUEnabledIsOffGlobalAndMruShouldBeOff()
        {
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG, generalSettings_file_name);

            viewModel.IsEnabled = true;
            viewModel.MRUEnabled = false;

            Assert.IsFalse(viewModel.GlobalAndMruEnabled);
        }

        [TestMethod]
        public void WhenIsEnabledIsOnAndMRUEnabledIsOnGlobalAndMruShouldBeOn()
        {
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG, generalSettings_file_name);

            viewModel.IsEnabled = true;
            viewModel.MRUEnabled = true;

            Assert.IsTrue(viewModel.GlobalAndMruEnabled);
        }

        [TestMethod]
        public void EnabledOnContextMenu_ShouldSetValue2True_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                PowerRenameSettingsIPCMessage snd = JsonSerializer.Deserialize<PowerRenameSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.PowerRename.Properties.ShowIcon.Value);
                return 0;
            };

            // arrange
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG, generalSettings_file_name);

            // act
            viewModel.EnabledOnContextMenu = true;
        }

        [TestMethod]
        public void EnabledOnContextExtendedMenu_ShouldSetValue2True_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                PowerRenameSettingsIPCMessage snd = JsonSerializer.Deserialize<PowerRenameSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.PowerRename.Properties.ShowIcon.Value);
                return 0;
            };

            // arrange
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG, generalSettings_file_name);

            // act
            viewModel.EnabledOnContextMenu = true;
        }

        [TestMethod]
        public void RestoreFlagsOnLaunch_ShouldSetValue2True_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                PowerRenameSettingsIPCMessage snd = JsonSerializer.Deserialize<PowerRenameSettingsIPCMessage>(msg);
                Assert.IsTrue(snd.Powertoys.PowerRename.Properties.PersistState.Value);
                return 0;
            };

            // arrange
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG, generalSettings_file_name);

            // act
            viewModel.RestoreFlagsOnLaunch = true;
        }

        [TestMethod]
        public void MaxDispListNum_ShouldSetMaxSuggListTo20_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                PowerRenameSettingsIPCMessage snd = JsonSerializer.Deserialize<PowerRenameSettingsIPCMessage>(msg);
                Assert.AreEqual(20, snd.Powertoys.PowerRename.Properties.MaxMRUSize.Value);
                return 0;
            };

            // arrange
            PowerRenameViewModel viewModel = new PowerRenameViewModel(mockPowerRenamePropertiesUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG, generalSettings_file_name);

            // act
            viewModel.MaxDispListNum = 20;
        }
    }
}
