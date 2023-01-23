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
            var settingPathMock = new Mock<ISettingsPath>();
            var mockIOProvider = BackCompatTestProperties.GetModuleIOProvider(version, ShortcutGuideSettings.ModuleName, fileName);
            var mockSettingsUtils = new SettingsUtils(mockIOProvider.Object, settingPathMock.Object);
            ShortcutGuideSettings originalSettings = mockSettingsUtils.GetSettingsOrDefault<ShortcutGuideSettings>(ShortcutGuideSettings.ModuleName);

            var mockGeneralIOProvider = BackCompatTestProperties.GetGeneralSettingsIOProvider(version);
            var mockGeneralSettingsUtils = new SettingsUtils(mockGeneralIOProvider.Object, settingPathMock.Object);
            GeneralSettings originalGeneralSettings = mockGeneralSettingsUtils.GetSettingsOrDefault<GeneralSettings>();
            var generalSettingsRepository = new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(mockGeneralSettingsUtils);
            var shortcutSettingsRepository = new BackCompatTestProperties.MockSettingsRepository<ShortcutGuideSettings>(mockSettingsUtils);

            // Initialise View Model with test Config files
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel(mockSettingsUtils, generalSettingsRepository, shortcutSettingsRepository, sendMockIPCConfigMSG);

            // Verify that the old settings persisted
            Assert.AreEqual(originalGeneralSettings.Enabled.ShortcutGuide, viewModel.IsEnabled);
            Assert.AreEqual(originalSettings.Properties.OverlayOpacity.Value, viewModel.OverlayOpacity);

            // Verify that the stub file was used
            var expectedCallCount = 2;  // once via the view model, and once by the test (GetSettings<T>)
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
            var settingsUtilsMock = new Mock<SettingsUtils>();

            // Assert
            // Initialize mock function of sending IPC message.
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsTrue(snd.GeneralSettings.Enabled.ShortcutGuide);
                return 0;
            };

            // Arrange
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel(settingsUtilsMock.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<ShortcutGuideSettings>.GetInstance(mockShortcutGuideSettingsUtils.Object), sendMockIPCConfigMSG, ShortCutGuideTestFolderName);

            // Act
            viewModel.IsEnabled = true;
        }

        [TestMethod]
        public void ThemeIndexShouldSetThemeToDarkWhenSuccessful()
        {
            // Arrange
            var settingsUtilsMock = new Mock<ISettingsUtils>();
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel(settingsUtilsMock.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<ShortcutGuideSettings>.GetInstance(mockShortcutGuideSettingsUtils.Object), msg => { return 0; }, ShortCutGuideTestFolderName);

            // Initialize shortcut guide settings theme to 'system' to be in sync with shortcut_guide.h.
            Assert.AreEqual(2, viewModel.ThemeIndex);

            // Act
            viewModel.ThemeIndex = 0;

            // Assert
            Func<string, bool> isDark = s => JsonSerializer.Deserialize<ShortcutGuideSettings>(s).Properties.Theme.Value == "dark";
            settingsUtilsMock.Verify(x => x.SaveSettings(It.Is<string>(y => isDark(y)), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        public void OverlayOpacityShouldSeOverlayOpacityToOneHundredWhenSuccessful()
        {
            // Arrange
            var settingsUtilsMock = new Mock<ISettingsUtils>();
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel(settingsUtilsMock.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SettingsRepository<ShortcutGuideSettings>.GetInstance(mockShortcutGuideSettingsUtils.Object), msg => { return 0; }, ShortCutGuideTestFolderName);
            Assert.AreEqual(90, viewModel.OverlayOpacity);

            // Act
            viewModel.OverlayOpacity = 100;

            // Assert
            Func<string, bool> equal100 = s => JsonSerializer.Deserialize<ShortcutGuideSettings>(s).Properties.OverlayOpacity.Value == 100;
            settingsUtilsMock.Verify(x => x.SaveSettings(It.Is<string>(y => equal100(y)), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }
    }
}
