// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class ShortcutGuide
    {
        public const string ShortCutGuideTestFolderName = "Test\\ShortCutGuide";

        [TestInitialize]
        public void Setup()
        {
            // initialize creation of test settings file.
            // Test base path:
            // C:\Users\<user name>\AppData\Local\Packages\08e1807b-8b6d-4bfa-adc4-79c64aae8e78_9abkseg265h2m\LocalState\Microsoft\PowerToys\
            GeneralSettings generalSettings = new GeneralSettings();
            ShortcutGuideSettings shortcutGuide = new ShortcutGuideSettings();

            SettingsUtils.SaveSettings(generalSettings.ToJsonString());
            SettingsUtils.SaveSettings(shortcutGuide.ToJsonString(), ShortCutGuideTestFolderName);
        }

        [TestCleanup]
        public void CleanUp()
        {
            // delete folder created.
            // delete general settings folder.
            string ShortCutGuideTestFolderName = string.Empty;
            if (SettingsUtils.SettingsFolderExists(string.Empty))
            {
                DeleteFolder(string.Empty);
            }

            // delete power rename folder.
            if (SettingsUtils.SettingsFolderExists(ShortCutGuideTestFolderName))
            {
                DeleteFolder(ShortCutGuideTestFolderName);
            }
        }

        public void DeleteFolder(string powertoy)
        {
            Directory.Delete(Path.Combine(SettingsUtils.LocalApplicationDataFolder(), $"Microsoft\\PowerToys\\{powertoy}"), true);
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
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel(SendMockIPCConfigMSG, ShortCutGuideTestFolderName);

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
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel(SendMockIPCConfigMSG, ShortCutGuideTestFolderName);
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
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel(SendMockIPCConfigMSG, ShortCutGuideTestFolderName);
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
            ShortcutGuideViewModel viewModel = new ShortcutGuideViewModel(SendMockIPCConfigMSG, ShortCutGuideTestFolderName);
            Assert.AreEqual(90, viewModel.OverlayOpacity);

            // Act
            viewModel.OverlayOpacity = 100;
        }
    }
}
