// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels;
using Microsoft.PowerToys.Settings.UI.UnitTests.BackwardsCompatibility;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    [TestClass]
    public class ShortcutGuide
    {
        public const string ShortCutGuideTestFolderName = "Test\\ShortCutGuide";

        /// <summary>
        /// Test if the original settings files were modified.
        /// </summary>
        [TestMethod]
        [DataRow("v0.18.2", "settings.json")]
        [DataRow("v0.19.2", "settings.json")]
        [DataRow("v0.20.1", "settings.json")]
        [DataRow("v0.21.1", "settings.json")]
        [DataRow("v0.22.0", "settings.json")]
        public void OriginalFilesModificationTest(string version, string fileName)
        {
            var mockIOProvider = BackCompatTestProperties.GetModuleIOProvider(version, ShortcutGuideSettings.ModuleName, fileName);
            var mockSettingsUtils = new SettingsUtils(mockIOProvider.Object);
            ShortcutGuideSettings originalSettings = mockSettingsUtils.GetSettings<ShortcutGuideSettings>(ShortcutGuideSettings.ModuleName);

            var mockGeneralIOProvider = BackCompatTestProperties.GetGeneralSettingsIOProvider(version);
            var mockGeneralSettingsUtils = new SettingsUtils(mockGeneralIOProvider.Object);
            GeneralSettings originalGeneralSettings = mockGeneralSettingsUtils.GetSettings<GeneralSettings>();
            var generalSettingsRepository = new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(mockGeneralSettingsUtils);
            var shortcutSettingsRepository = new BackCompatTestProperties.MockSettingsRepository<ShortcutGuideSettings>(mockSettingsUtils);

            // Initialise View Model with test Config files
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel(generalSettingsRepository, shortcutSettingsRepository, SendMockIPCConfigMSG);

            // Verifiy that the old settings persisted
            Assert.AreEqual(originalGeneralSettings.Enabled.ShortcutGuide, viewModel.IsEnabled);
            Assert.AreEqual(originalSettings.Properties.OverlayOpacity.Value, viewModel.OverlayOpacity);
            Assert.AreEqual(originalSettings.Properties.PressTime.Value, viewModel.PressTime);

            //Verify that the stub file was used
            var expectedCallCount = 2;  //once via the view model, and once by the test (GetSettings<T>)
            BackCompatTestProperties.VerifyModuleIOProviderWasRead(mockIOProvider, ShortcutGuideSettings.ModuleName, expectedCallCount);
            BackCompatTestProperties.VerifyGeneralSettingsIOProviderWasRead(mockGeneralIOProvider, expectedCallCount);
        }

        private Mock<ISettingsUtils> mockGeneralSettingsUtils;

        private Mock<ISettingsUtils> mockShortcutGuideSettingsUtils;

        [TestInitialize]
        public void SetUpStubSettingUtils()
        {
            mockGeneralSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();
            mockShortcutGuideSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<ShortcutGuideSettings>();
        }

        [TestMethod]
        public void IsEnabledShouldEnableModuleWhenSuccessful()
        {
            // Assert
            // Initialize mock function of sending IPC message.
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsTrue(snd.GeneralSettings.Enabled.ShortcutGuide);
                return 0;
            };

            // Arrange
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel(SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<ShortcutGuideSettings>.GetInstance(mockShortcutGuideSettingsUtils.Object), SendMockIPCConfigMSG, ShortCutGuideTestFolderName);

            // Act
            viewModel.IsEnabled = true;
        }

        [TestMethod]
        public void ThemeIndexShouldSetThemeToDarkWhenSuccessful()
        {
            // Assert
            // Initialize mock function of sending IPC message.
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                ShortcutGuideSettingsIPCMessage snd = JsonSerializer.Deserialize<ShortcutGuideSettingsIPCMessage>(msg);
                Assert.AreEqual("dark", snd.Powertoys.ShortcutGuide.Properties.Theme.Value);
                return 0;
            };

            // Arrange
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel(SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<ShortcutGuideSettings>.GetInstance(mockShortcutGuideSettingsUtils.Object), SendMockIPCConfigMSG, ShortCutGuideTestFolderName);
            // Initialize shortcut guide settings theme to 'system' to be in sync with shortcut_guide.h.
            Assert.AreEqual(2, viewModel.ThemeIndex);

            // Act
            viewModel.ThemeIndex = 0;
        }

        [TestMethod]
        public void PressTimeShouldSetPressTimeToOneHundredWhenSuccessful()
        {
            // Assert
            // Initialize mock function of sending IPC message.
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                ShortcutGuideSettingsIPCMessage snd = JsonSerializer.Deserialize<ShortcutGuideSettingsIPCMessage>(msg);
                Assert.AreEqual(100, snd.Powertoys.ShortcutGuide.Properties.PressTime.Value);
                return 0;
            };

            // Arrange
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel(SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<ShortcutGuideSettings>.GetInstance(mockShortcutGuideSettingsUtils.Object), SendMockIPCConfigMSG, ShortCutGuideTestFolderName);
            Assert.AreEqual(900, viewModel.PressTime);

            // Act
            viewModel.PressTime = 100;
        }

        [TestMethod]
        public void OverlayOpacityShouldSeOverlayOpacityToOneHundredWhenSuccessful()
        {
            // Assert
            // Initialize mock function of sending IPC message.
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                ShortcutGuideSettingsIPCMessage snd = JsonSerializer.Deserialize<ShortcutGuideSettingsIPCMessage>(msg);

                // Serialisation not working as expected in the test project:
                Assert.AreEqual(100, snd.Powertoys.ShortcutGuide.Properties.OverlayOpacity.Value);
                return 0;
            };

            // Arrange
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel(SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<ShortcutGuideSettings>.GetInstance(mockShortcutGuideSettingsUtils.Object), SendMockIPCConfigMSG, ShortCutGuideTestFolderName);
            Assert.AreEqual(90, viewModel.OverlayOpacity);

            // Act
            viewModel.OverlayOpacity = 100;
        }
    }
}
