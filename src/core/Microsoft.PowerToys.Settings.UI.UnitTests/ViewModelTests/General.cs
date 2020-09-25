// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.ViewModels;
using Microsoft.PowerToys.Settings.UI.UnitTests.BackwardsCompatibility;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NuGet.Frameworks;

namespace ViewModelTests
{
    [TestClass]
    public class General
    {
        public const string generalSettings_file_name = "Test\\GenealSettings";

        private Mock<ISettingsUtils> mockGeneralSettingsUtils;



        [TestInitialize]
        public void SetUp_StubSettingUtils()
        {
            mockGeneralSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();
        }
		
        /// </summary>
        [TestMethod]
        [DataRow("v0.20.2", "settings.json")]
        [DataRow("v0.18.2", "settings.json")]
        public void OriginalFilesModificationTest(string version, string fileName)
        {
            var mockGeneralIOProvider = BackCompatTestProperties.GetGeneralSettingsIOProvider(version);
            var mockGeneralSettingsUtils = new SettingsUtils(mockGeneralIOProvider.Object);
            GeneralSettings originalGeneralSettings = mockGeneralSettingsUtils.GetSettings<GeneralSettings>();
            var generalSettingsRepository = new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(mockGeneralSettingsUtils);

            // Initialise View Model with test Config files
            // Arrange
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            Func<string, int> SendRestartAdminIPCMessage = msg => { return 0; };
            Func<string, int> SendCheckForUpdatesIPCMessage = msg => { return 0; };
            GeneralViewModel viewModel = new GeneralViewModel(
                mockGeneralSettingsUtils,
                generalSettingsRepository,
                "GeneralSettings_RunningAsAdminText",
                "GeneralSettings_RunningAsUserText",
                false,
                false,
                UpdateUIThemeMethod,
                SendMockIPCConfigMSG,
                SendRestartAdminIPCMessage,
                SendCheckForUpdatesIPCMessage,
                string.Empty);

            // Verifiy that the old settings persisted
            Assert.AreEqual(originalGeneralSettings.AutoDownloadUpdates, viewModel.AutoDownloadUpdates);
            Assert.AreEqual(originalGeneralSettings.Packaged, viewModel.Packaged);
            Assert.AreEqual(originalGeneralSettings.PowertoysVersion, viewModel.PowerToysVersion);
            Assert.AreEqual(originalGeneralSettings.RunElevated, viewModel.RunElevated);
            Assert.AreEqual(originalGeneralSettings.Startup, viewModel.Startup);

            //Verify that the stub file was used
            var expectedCallCount = 2;  //once via the view model, and once by the test (GetSettings<T>)
            BackCompatTestProperties.VerifyGeneralSettingsIOProviderWasRead(mockGeneralIOProvider, expectedCallCount);
        }

        [TestMethod]
        public void IsElevated_ShouldUpdateRunasAdminStatusAttrs_WhenSuccessful()
        {
            // Arrange
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            Func<string, int> SendRestartAdminIPCMessage = msg => { return 0; };
            Func<string, int> SendCheckForUpdatesIPCMessage = msg => { return 0; };
            GeneralViewModel viewModel = new GeneralViewModel(
                new Mock<ISettingsUtils>().Object,
                SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object),
                "GeneralSettings_RunningAsAdminText",
                "GeneralSettings_RunningAsUserText",
                false,
                false,
                UpdateUIThemeMethod,
                SendMockIPCConfigMSG,
                SendRestartAdminIPCMessage,
                SendCheckForUpdatesIPCMessage,
                generalSettings_file_name);

            Assert.AreEqual(viewModel.RunningAsUserDefaultText, viewModel.RunningAsText);
            Assert.IsFalse(viewModel.IsElevated);

            // Act
            viewModel.IsElevated = true;

            // Assert
            Assert.AreEqual(viewModel.RunningAsAdminDefaultText, viewModel.RunningAsText);
            Assert.IsTrue(viewModel.IsElevated);
        }

        [TestMethod]
        public void Startup_ShouldEnableRunOnStartUp_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsTrue(snd.GeneralSettings.Startup);
                return 0;
            };

            // Arrange
            Func<string, int> SendRestartAdminIPCMessage = msg => { return 0; };
            Func<string, int> SendCheckForUpdatesIPCMessage = msg => { return 0; };
            GeneralViewModel viewModel = new GeneralViewModel(
                new Mock<ISettingsUtils>().Object,
                SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object),
                "GeneralSettings_RunningAsAdminText",
                "GeneralSettings_RunningAsUserText",
                false,
                false,
                UpdateUIThemeMethod,
                SendMockIPCConfigMSG,
                SendRestartAdminIPCMessage,
                SendCheckForUpdatesIPCMessage,
                generalSettings_file_name);
            Assert.IsFalse(viewModel.Startup);

            // act
            viewModel.Startup = true;
        }

        [TestMethod]
        public void RunElevated_ShouldEnableAlwaysRunElevated_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsTrue(snd.GeneralSettings.RunElevated);
                return 0;
            };

            Func<string, int> SendRestartAdminIPCMessage = msg => { return 0; };
            Func<string, int> SendCheckForUpdatesIPCMessage = msg => { return 0; };

            // Arrange
            GeneralViewModel viewModel = new GeneralViewModel(
                new Mock<ISettingsUtils>().Object,
                SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object),
                "GeneralSettings_RunningAsAdminText",
                "GeneralSettings_RunningAsUserText",
                false,
                false,
                UpdateUIThemeMethod,
                SendMockIPCConfigMSG,
                SendRestartAdminIPCMessage,
                SendCheckForUpdatesIPCMessage,
                generalSettings_file_name);

            Assert.IsFalse(viewModel.RunElevated);

            // act
            viewModel.RunElevated = true;
        }

        [TestMethod]
        public void IsLightThemeRadioButtonChecked_ShouldThemeToLight_WhenSuccessful()
        {
            // Arrange
            GeneralViewModel viewModel = null;
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.AreEqual("light", snd.GeneralSettings.Theme);
                return 0;
            };

            Func<string, int> SendRestartAdminIPCMessage = msg => { return 0; };
            Func<string, int> SendCheckForUpdatesIPCMessage = msg => { return 0; };
            viewModel = new GeneralViewModel(
                new Mock<ISettingsUtils>().Object,
                SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object),
                "GeneralSettings_RunningAsAdminText",
                "GeneralSettings_RunningAsUserText",
                false,
                false,
                UpdateUIThemeMethod,
                SendMockIPCConfigMSG,
                SendRestartAdminIPCMessage,
                SendCheckForUpdatesIPCMessage,
                generalSettings_file_name);
            Assert.IsFalse(viewModel.IsLightThemeRadioButtonChecked);

            // act
            viewModel.IsLightThemeRadioButtonChecked = true;
        }

        [TestMethod]
        public void IsDarkThemeRadioButtonChecked_ShouldThemeToDark_WhenSuccessful()
        {
            // Arrange
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.AreEqual("dark", snd.GeneralSettings.Theme);
                return 0;
            };

            Func<string, int> SendRestartAdminIPCMessage = msg => { return 0; };
            Func<string, int> SendCheckForUpdatesIPCMessage = msg => { return 0; };
            GeneralViewModel viewModel = new GeneralViewModel(
                new Mock<ISettingsUtils>().Object,
                SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object),
                "GeneralSettings_RunningAsAdminText",
                "GeneralSettings_RunningAsUserText",
                false,
                false,
                UpdateUIThemeMethod,
                SendMockIPCConfigMSG,
                SendRestartAdminIPCMessage,
                SendCheckForUpdatesIPCMessage,
                generalSettings_file_name);
            Assert.IsFalse(viewModel.IsDarkThemeRadioButtonChecked);



            // act
            viewModel.IsDarkThemeRadioButtonChecked = true;
        }

        [TestMethod]
        public void AllModulesAreEnabledByDefault()
        {
            //arrange 
            EnabledModules modules = new EnabledModules();


            //Assert
            Assert.IsTrue(modules.FancyZones);
            Assert.IsTrue(modules.ImageResizer);
            Assert.IsTrue(modules.FileExplorerPreview);
            Assert.IsTrue(modules.ShortcutGuide);
            Assert.IsTrue(modules.PowerRename);
            Assert.IsTrue(modules.KeyboardManager);
            Assert.IsTrue(modules.PowerLauncher);
            Assert.IsTrue(modules.ColorPicker);
        }

        public int UpdateUIThemeMethod(string themeName)
        {
            return 0;
        }
    }
}
