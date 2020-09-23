// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.ViewModels;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    [TestClass]
    public class ShortcutGuide
    {
        public const string ShortCutGuideTestFolderName = "Test\\ShortCutGuide";
        // This should not be changed. 
        // Changing it will cause user's to lose their local settings configs.
        public const string ModuleName = "Shortcut Guide";

        /// <summary>
        /// Test if the original settings files were modified.
        /// </summary>
        [TestMethod]
        public void OriginalFilesModificationTest()
        {
            var mockIOProvider = IIOProviderMocks.GetMockIOProviderForSaveLoadExists();
            var mockSettingsUtils = new SettingsUtils(mockIOProvider.Object);
            // Load Originl Settings Config File
            ShortcutGuideSettings originalSettings = mockSettingsUtils.GetSettings<ShortcutGuideSettings>(ModuleName);
            GeneralSettings originalGeneralSettings = mockSettingsUtils.GetSettings<GeneralSettings>();

            // Initialise View Model with test Config files
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel(mockSettingsUtils, SendMockIPCConfigMSG);

            // Verifiy that the old settings persisted
            Assert.AreEqual(originalGeneralSettings.Enabled.ShortcutGuide, viewModel.IsEnabled);
            Assert.AreEqual(originalSettings.Properties.OverlayOpacity.Value, viewModel.OverlayOpacity);
            Assert.AreEqual(originalSettings.Properties.PressTime.Value, viewModel.PressTime);
        }

        [TestMethod]
        public void IsEnabled_ShouldEnableModule_WhenSuccessful()
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
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SendMockIPCConfigMSG, ShortCutGuideTestFolderName);

            // Act
            viewModel.IsEnabled = true;
        }

        [TestMethod]
        public void ThemeIndex_ShouldSetThemeToDark_WhenSuccessful()
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
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SendMockIPCConfigMSG, ShortCutGuideTestFolderName);
            Assert.AreEqual(1, viewModel.ThemeIndex);

            // Act
            viewModel.ThemeIndex = 0;
        }

        [TestMethod]
        public void PressTime_ShouldSetPressTimeToOneHundred_WhenSuccessful()
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
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SendMockIPCConfigMSG, ShortCutGuideTestFolderName);
            Assert.AreEqual(900, viewModel.PressTime);

            // Act
            viewModel.PressTime = 100;
        }

        [TestMethod]
        public void OverlayOpacity_ShouldSeOverlayOpacityToOneHundred_WhenSuccessful()
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
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SendMockIPCConfigMSG, ShortCutGuideTestFolderName);
            Assert.AreEqual(90, viewModel.OverlayOpacity);

            // Act
            viewModel.OverlayOpacity = 100;
        }
    }
}
