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
    public class PowerPreview
    {
        public const string TestModuleName = "Test\\File Explorer";

        // This should not be changed. 
        // Changing it will causes user's to lose their local settings configs.
        private const string OriginalModuleName = "File Explorer";

        [TestInitialize]
        public void Setup()
        {
            // initialize creation of test settings file.
            GeneralSettings generalSettings = new GeneralSettings();
            PowerPreviewSettings powerpreview = new PowerPreviewSettings();

            SettingsUtils.SaveSettings(generalSettings.ToJsonString(), "Test");
            SettingsUtils.SaveSettings(powerpreview.ToJsonString(), TestModuleName);
        }

        [TestCleanup]
        public void CleanUp()
        {
            // delete folder created.
            string generalSettings_file_name = "Test";
            if (SettingsUtils.SettingsFolderExists(generalSettings_file_name))
            {
                DeleteFolder(generalSettings_file_name);
            }

            if (SettingsUtils.SettingsFolderExists(TestModuleName))
            {
                DeleteFolder(TestModuleName);
            }
        }

        public void DeleteFolder(string powertoy)
        {
            Directory.Delete(Path.Combine(SettingsUtils.LocalApplicationDataFolder(), $"Microsoft\\PowerToys\\{powertoy}"), true);
        }

        /// <summary>
        /// Test if the original settings files were modified.
        /// </summary>
        [TestMethod]
        public void OriginalFilesModificationTest()
        {
            SettingsUtils.IsTestMode = true;

            // Load Originl Settings Config File
            PowerPreviewSettings originalSettings = SettingsUtils.GetSettings<PowerPreviewSettings>(OriginalModuleName);

            // Initialise View Model with test Config files
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            PowerPreviewViewModel viewModel = new PowerPreviewViewModel(SendMockIPCConfigMSG);

            // Verifiy that the old settings persisted
            Assert.AreEqual(originalSettings.Properties.EnableMdPreview, viewModel.MDRenderIsEnabled);
            Assert.AreEqual(originalSettings.Properties.EnableSvgPreview, viewModel.SVGRenderIsEnabled);
            Assert.AreEqual(originalSettings.Properties.EnableSvgThumbnail, viewModel.SVGThumbnailIsEnabled);
        }

        [TestMethod]
        public void SVGRenderIsEnabled_ShouldPrevHandler_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                SndModuleSettings<SndPowerPreviewSettings> snd = JsonSerializer.Deserialize<SndModuleSettings<SndPowerPreviewSettings>>(msg);
                Assert.IsTrue(snd.PowertoysSetting.FileExplorerPreviewSettings.Properties.EnableSvgPreview);
                return 0;
            };

            // arrange
            PowerPreviewViewModel viewModel = new PowerPreviewViewModel(SendMockIPCConfigMSG, TestModuleName);

            // act
            viewModel.SVGRenderIsEnabled = true;
        }

        [TestMethod]
        public void SVGThumbnailIsEnabled_ShouldPrevHandler_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                SndModuleSettings<SndPowerPreviewSettings> snd = JsonSerializer.Deserialize<SndModuleSettings<SndPowerPreviewSettings>>(msg);
                Assert.IsTrue(snd.PowertoysSetting.FileExplorerPreviewSettings.Properties.EnableSvgThumbnail);
                return 0;
            };

            // arrange
            PowerPreviewViewModel viewModel = new PowerPreviewViewModel(SendMockIPCConfigMSG, TestModuleName);

            // act
            viewModel.SVGThumbnailIsEnabled = true;
        }

        [TestMethod]
        public void MDRenderIsEnabled_ShouldPrevHandler_WhenSuccessful()
        {
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                SndModuleSettings<SndPowerPreviewSettings> snd = JsonSerializer.Deserialize<SndModuleSettings<SndPowerPreviewSettings>>(msg);
                Assert.IsTrue(snd.PowertoysSetting.FileExplorerPreviewSettings.Properties.EnableMdPreview);
                return 0;
            };

            // arrange
            PowerPreviewViewModel viewModel = new PowerPreviewViewModel(SendMockIPCConfigMSG, TestModuleName);;

            // act
            viewModel.MDRenderIsEnabled = true;
        }
    }
}
