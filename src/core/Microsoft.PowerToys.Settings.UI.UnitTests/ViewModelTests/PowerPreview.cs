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
    public class PowerPreview
    {
        private const string Module = "File Explorer";

        private Mock<ISettingsUtils> mockPowerPreviewSettingsUtils;

        [TestInitialize]
        public void SetUp_StubSettingUtils()
        {
            mockPowerPreviewSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<PowerPreviewSettings>();
        }

        /// <summary>
        /// Test if the original settings files were modified.
        /// </summary>
        [TestMethod]
        public void OriginalFilesModificationTest()
        {
            var mockIOProvider = IIOProviderMocks.GetMockIOProviderForSaveLoadExists();
            var mockSettingsUtils = new SettingsUtils(mockIOProvider.Object);
            // Load Originl Settings Config File
            PowerPreviewSettings originalSettings = mockSettingsUtils.GetSettings<PowerPreviewSettings>(Module);

            // Initialise View Model with test Config files
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            PowerPreviewViewModel viewModel = new PowerPreviewViewModel(SettingsRepository<PowerPreviewSettings>.GetInstance(mockSettingsUtils), SendMockIPCConfigMSG);

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
            PowerPreviewViewModel viewModel = new PowerPreviewViewModel(SettingsRepository<PowerPreviewSettings>.GetInstance(mockPowerPreviewSettingsUtils.Object), SendMockIPCConfigMSG, Module);

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
            PowerPreviewViewModel viewModel = new PowerPreviewViewModel(SettingsRepository<PowerPreviewSettings>.GetInstance(mockPowerPreviewSettingsUtils.Object), SendMockIPCConfigMSG, Module);

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
            PowerPreviewViewModel viewModel = new PowerPreviewViewModel(SettingsRepository<PowerPreviewSettings>.GetInstance(mockPowerPreviewSettingsUtils.Object), SendMockIPCConfigMSG, Module); ;

            // act
            viewModel.MDRenderIsEnabled = true;
        }
    }
}
