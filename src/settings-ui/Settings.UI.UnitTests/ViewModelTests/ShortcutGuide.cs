// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions;
using System.Text.Json;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
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
        [DataRow("v0.18.2", "settings.json", 100)]
        [DataRow("v0.19.2", "settings.json", 1150)]
        [DataRow("v0.20.1", "settings.json", 650)]
        [DataRow("v0.21.1", "settings.json", 1050)]
        [DataRow("v0.22.0", "settings.json", 850)]
        public void OriginalFilesModificationTest(string version, string fileName, int expectedPressTime)
        {
            var settingPathMock = new Mock<SettingPath>();
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
            Assert.AreEqual(expectedPressTime, viewModel.PressTime);
            Assert.AreEqual((int)ShortcutGuideWindowsKeyAction.TaskbarIndicators, viewModel.WindowsKeyActionIndex);
            Assert.IsTrue(viewModel.CloseOnWindowsKeyRelease);

            // Verify that the stub file was used
            var expectedCallCount = 2;  // once via the view model, and once by the test (GetSettings<T>)
            BackCompatTestProperties.VerifyModuleIOProviderWasRead(mockIOProvider, ShortcutGuideSettings.ModuleName, expectedCallCount);
            BackCompatTestProperties.VerifyGeneralSettingsIOProviderWasRead(mockGeneralIOProvider, expectedCallCount);
        }

        private Mock<SettingsUtils> mockGeneralSettingsUtils;

        private Mock<SettingsUtils> mockShortcutGuideSettingsUtils;

        [TestInitialize]
        public void SetUpStubSettingUtils()
        {
            mockGeneralSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();
            mockShortcutGuideSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<ShortcutGuideSettings>();
        }

        [TestMethod]
        public void IsEnabledShouldEnableModuleWhenSuccessful()
        {
            var settingsUtilsMock = new Mock<SettingsUtils>(new FileSystem(), null);

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
            var settingsUtilsMock = new Mock<SettingsUtils>(new FileSystem(), null);
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
        public void WindowsKeySettingsShouldUseExpectedDefaults()
        {
            var properties = new ShortcutGuideProperties();

            Assert.AreEqual((int)ShortcutGuideWindowsKeyAction.TaskbarIndicators, properties.WindowsKeyAction.Value);
            Assert.AreEqual(ShortcutGuideProperties.DefaultPressTimeMs, properties.PressTime.Value);
            Assert.IsTrue(properties.CloseOnWindowsKeyRelease.Value);
        }

        [TestMethod]
        public void InvalidWindowsKeySettingsShouldBeNormalizedOnInitialization()
        {
            var settings = new ShortcutGuideSettings();
            settings.Properties.WindowsKeyAction.Value = 42;
            settings.Properties.PressTime.Value = ShortcutGuideProperties.MaximumPressTimeMs + 1;

            int ipcMessageCount = 0;
            var viewModel = CreateViewModel(settings, out var settingsUtilsMock, _ => ipcMessageCount++);

            Assert.AreEqual((int)ShortcutGuideWindowsKeyAction.TaskbarIndicators, viewModel.WindowsKeyActionIndex);
            Assert.AreEqual(ShortcutGuideProperties.MaximumPressTimeMs, viewModel.PressTime);
            Assert.AreEqual((int)ShortcutGuideWindowsKeyAction.TaskbarIndicators, settings.Properties.WindowsKeyAction.Value);
            Assert.AreEqual(ShortcutGuideProperties.MaximumPressTimeMs, settings.Properties.PressTime.Value);
            Assert.IsTrue(viewModel.IsWindowsKeyHoldEnabled);
            Assert.IsFalse(viewModel.IsOpenShortcutGuideWindowsKeyAction);
            Assert.AreEqual(1, ipcMessageCount);
            settingsUtilsMock.Verify(
                x => x.SaveSettings(
                    It.Is<string>(json => JsonSerializer.Deserialize<ShortcutGuideSettings>(json).Properties.WindowsKeyAction.Value == (int)ShortcutGuideWindowsKeyAction.TaskbarIndicators),
                    ShortcutGuideSettings.ModuleName,
                    It.IsAny<string>()),
                Times.Once);
        }

        [TestMethod]
        public void WindowsKeyActionShouldUpdateConditionalStateAndPersist()
        {
            var settings = new ShortcutGuideSettings();
            int ipcMessageCount = 0;
            var viewModel = CreateViewModel(settings, out var settingsUtilsMock, _ => ipcMessageCount++);

            viewModel.WindowsKeyActionIndex = (int)ShortcutGuideWindowsKeyAction.Off;

            Assert.IsFalse(viewModel.IsWindowsKeyHoldEnabled);
            Assert.IsFalse(viewModel.IsOpenShortcutGuideWindowsKeyAction);
            Assert.AreEqual((int)ShortcutGuideWindowsKeyAction.Off, settings.Properties.WindowsKeyAction.Value);
            Assert.AreEqual(1, ipcMessageCount);
            settingsUtilsMock.Verify(
                x => x.SaveSettings(
                    It.Is<string>(json => JsonSerializer.Deserialize<ShortcutGuideSettings>(json).Properties.WindowsKeyAction.Value == (int)ShortcutGuideWindowsKeyAction.Off),
                    ShortcutGuideSettings.ModuleName,
                    It.IsAny<string>()),
                Times.Once);

            viewModel.WindowsKeyActionIndex = (int)ShortcutGuideWindowsKeyAction.OpenShortcutGuide;

            Assert.IsTrue(viewModel.IsWindowsKeyHoldEnabled);
            Assert.IsTrue(viewModel.IsOpenShortcutGuideWindowsKeyAction);
        }

        [TestMethod]
        public void PressTimeShouldClampAtBothBoundariesAndPersist()
        {
            var settings = new ShortcutGuideSettings();
            var viewModel = CreateViewModel(settings, out var settingsUtilsMock);

            viewModel.PressTime = ShortcutGuideProperties.MinimumPressTimeMs - 1;

            Assert.AreEqual(ShortcutGuideProperties.MinimumPressTimeMs, viewModel.PressTime);
            Assert.AreEqual(ShortcutGuideProperties.MinimumPressTimeMs, settings.Properties.PressTime.Value);

            viewModel.PressTime = ShortcutGuideProperties.MaximumPressTimeMs + 1;

            Assert.AreEqual(ShortcutGuideProperties.MaximumPressTimeMs, viewModel.PressTime);
            Assert.AreEqual(ShortcutGuideProperties.MaximumPressTimeMs, settings.Properties.PressTime.Value);
            settingsUtilsMock.Verify(
                x => x.SaveSettings(
                    It.Is<string>(json => JsonSerializer.Deserialize<ShortcutGuideSettings>(json).Properties.PressTime.Value == ShortcutGuideProperties.MaximumPressTimeMs),
                    ShortcutGuideSettings.ModuleName,
                    It.IsAny<string>()),
                Times.Once);
        }

        [TestMethod]
        public void CloseOnWindowsKeyReleaseShouldPersist()
        {
            var settings = new ShortcutGuideSettings();
            var viewModel = CreateViewModel(settings, out var settingsUtilsMock);

            viewModel.CloseOnWindowsKeyRelease = false;

            Assert.IsFalse(settings.Properties.CloseOnWindowsKeyRelease.Value);
            settingsUtilsMock.Verify(
                x => x.SaveSettings(
                    It.Is<string>(json => JsonSerializer.Deserialize<ShortcutGuideSettings>(json).Properties.CloseOnWindowsKeyRelease.Value == false),
                    ShortcutGuideSettings.ModuleName,
                    It.IsAny<string>()),
                Times.Once);
        }

        private static ShortcutGuideViewModel CreateViewModel(
            ShortcutGuideSettings settings,
            out Mock<SettingsUtils> settingsUtilsMock,
            Action<string> ipcMessageReceived = null)
        {
            settingsUtilsMock = new Mock<SettingsUtils>(new FileSystem(), null);

            var generalSettingsRepository = new Mock<ISettingsRepository<GeneralSettings>>();
            generalSettingsRepository.SetupGet(x => x.SettingsConfig).Returns(new GeneralSettings());

            var shortcutGuideSettingsRepository = new Mock<ISettingsRepository<ShortcutGuideSettings>>();
            shortcutGuideSettingsRepository.SetupGet(x => x.SettingsConfig).Returns(settings);

            return new ShortcutGuideViewModel(
                settingsUtilsMock.Object,
                generalSettingsRepository.Object,
                shortcutGuideSettingsRepository.Object,
                message =>
                {
                    ipcMessageReceived?.Invoke(message);
                    return 0;
                },
                ShortCutGuideTestFolderName);
        }
    }
}
