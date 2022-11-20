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
    public class PowerPreview
    {
        private Mock<ISettingsUtils> mockPowerPreviewSettingsUtils;

        private Mock<ISettingsUtils> mockGeneralSettingsUtils;

        [TestInitialize]
        public void SetUpStubSettingUtils()
        {
            mockPowerPreviewSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<PowerPreviewSettings>();
            mockGeneralSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();
        }

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
            var fileMock = BackCompatTestProperties.GetModuleIOProvider(version, PowerPreviewSettings.ModuleName, fileName);

            var mockSettingsUtils = new SettingsUtils(fileMock.Object, settingPathMock.Object);
            PowerPreviewSettings originalSettings = mockSettingsUtils.GetSettingsOrDefault<PowerPreviewSettings>(PowerPreviewSettings.ModuleName);
            var repository = new BackCompatTestProperties.MockSettingsRepository<PowerPreviewSettings>(mockSettingsUtils);

            var mockGeneralIOProvider = BackCompatTestProperties.GetGeneralSettingsIOProvider(version);
            var mockGeneralSettingsUtils = new SettingsUtils(mockGeneralIOProvider.Object, settingPathMock.Object);
            GeneralSettings originalGeneralSettings = mockGeneralSettingsUtils.GetSettingsOrDefault<GeneralSettings>();
            var generalSettingsRepository = new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(mockGeneralSettingsUtils);

            // Initialise View Model with test Config files
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            PowerPreviewViewModel viewModel = new PowerPreviewViewModel(repository, generalSettingsRepository, sendMockIPCConfigMSG);

            // Verify that the old settings persisted
            Assert.AreEqual(originalGeneralSettings.IsElevated, viewModel.IsElevated);
            Assert.AreEqual(originalSettings.Properties.EnableMdPreview, viewModel.MDRenderIsEnabled);
            Assert.AreEqual(originalSettings.Properties.EnableMonacoPreview, viewModel.MonacoRenderIsEnabled);
            Assert.AreEqual(originalSettings.Properties.EnablePdfPreview, viewModel.PDFRenderIsEnabled);
            Assert.AreEqual(originalSettings.Properties.EnableGcodePreview, viewModel.GCODERenderIsEnabled);
            Assert.AreEqual(originalSettings.Properties.EnableSvgPreview, viewModel.SVGRenderIsEnabled);
            Assert.AreEqual(originalSettings.Properties.EnableSvgThumbnail, viewModel.SVGThumbnailIsEnabled);
            Assert.AreEqual(originalSettings.Properties.EnablePdfThumbnail, viewModel.PDFThumbnailIsEnabled);
            Assert.AreEqual(originalSettings.Properties.EnableGcodeThumbnail, viewModel.GCODEThumbnailIsEnabled);
            Assert.AreEqual(originalSettings.Properties.EnableStlThumbnail, viewModel.STLThumbnailIsEnabled);

            // Verify that the stub file was used
            var expectedCallCount = 2;  // once via the view model, and once by the test (GetSettings<T>)
            BackCompatTestProperties.VerifyModuleIOProviderWasRead(fileMock, PowerPreviewSettings.ModuleName, expectedCallCount);
        }

        [TestMethod]
        public void SVGRenderIsEnabledShouldPrevHandlerWhenSuccessful()
        {
            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                SndModuleSettings<SndPowerPreviewSettings> snd = JsonSerializer.Deserialize<SndModuleSettings<SndPowerPreviewSettings>>(msg);
                Assert.IsTrue(snd.PowertoysSetting.FileExplorerPreviewSettings.Properties.EnableSvgPreview);
                return 0;
            };

            // arrange
            PowerPreviewViewModel viewModel = new PowerPreviewViewModel(SettingsRepository<PowerPreviewSettings>.GetInstance(mockPowerPreviewSettingsUtils.Object), SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, PowerPreviewSettings.ModuleName);

            // act
            viewModel.SVGRenderIsEnabled = true;
        }

        [TestMethod]
        public void SVGThumbnailIsEnabledShouldPrevHandlerWhenSuccessful()
        {
            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                SndModuleSettings<SndPowerPreviewSettings> snd = JsonSerializer.Deserialize<SndModuleSettings<SndPowerPreviewSettings>>(msg);
                Assert.IsTrue(snd.PowertoysSetting.FileExplorerPreviewSettings.Properties.EnableSvgThumbnail);
                return 0;
            };

            // arrange
            PowerPreviewViewModel viewModel = new PowerPreviewViewModel(SettingsRepository<PowerPreviewSettings>.GetInstance(mockPowerPreviewSettingsUtils.Object), SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, PowerPreviewSettings.ModuleName);

            // act
            viewModel.SVGThumbnailIsEnabled = true;
        }

        [TestMethod]
        public void PDFThumbnailIsEnabledShouldPrevHandlerWhenSuccessful()
        {
            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                SndModuleSettings<SndPowerPreviewSettings> snd = JsonSerializer.Deserialize<SndModuleSettings<SndPowerPreviewSettings>>(msg);
                Assert.IsTrue(snd.PowertoysSetting.FileExplorerPreviewSettings.Properties.EnablePdfThumbnail);
                return 0;
            };

            // arrange
            PowerPreviewViewModel viewModel = new PowerPreviewViewModel(SettingsRepository<PowerPreviewSettings>.GetInstance(mockPowerPreviewSettingsUtils.Object), SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, PowerPreviewSettings.ModuleName);

            // act
            viewModel.PDFThumbnailIsEnabled = true;
        }

        [TestMethod]
        public void GCODEThumbnailIsEnabledShouldPrevHandlerWhenSuccessful()
        {
            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                SndModuleSettings<SndPowerPreviewSettings> snd = JsonSerializer.Deserialize<SndModuleSettings<SndPowerPreviewSettings>>(msg);
                Assert.IsTrue(snd.PowertoysSetting.FileExplorerPreviewSettings.Properties.EnableGcodeThumbnail);
                return 0;
            };

            // arrange
            PowerPreviewViewModel viewModel = new PowerPreviewViewModel(SettingsRepository<PowerPreviewSettings>.GetInstance(mockPowerPreviewSettingsUtils.Object), SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, PowerPreviewSettings.ModuleName);

            // act
            viewModel.GCODEThumbnailIsEnabled = true;
        }

        [TestMethod]
        public void STLThumbnailIsEnabledShouldPrevHandlerWhenSuccessful()
        {
            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                SndModuleSettings<SndPowerPreviewSettings> snd = JsonSerializer.Deserialize<SndModuleSettings<SndPowerPreviewSettings>>(msg);
                Assert.IsTrue(snd.PowertoysSetting.FileExplorerPreviewSettings.Properties.EnableStlThumbnail);
                return 0;
            };

            // arrange
            PowerPreviewViewModel viewModel = new PowerPreviewViewModel(SettingsRepository<PowerPreviewSettings>.GetInstance(mockPowerPreviewSettingsUtils.Object), SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, PowerPreviewSettings.ModuleName);

            // act
            viewModel.STLThumbnailIsEnabled = true;
        }

        [TestMethod]
        public void MDRenderIsEnabledShouldPrevHandlerWhenSuccessful()
        {
            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                SndModuleSettings<SndPowerPreviewSettings> snd = JsonSerializer.Deserialize<SndModuleSettings<SndPowerPreviewSettings>>(msg);
                Assert.IsTrue(snd.PowertoysSetting.FileExplorerPreviewSettings.Properties.EnableMdPreview);
                return 0;
            };

            // arrange
            PowerPreviewViewModel viewModel = new PowerPreviewViewModel(SettingsRepository<PowerPreviewSettings>.GetInstance(mockPowerPreviewSettingsUtils.Object), SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, PowerPreviewSettings.ModuleName);

            // act
            viewModel.MDRenderIsEnabled = true;
        }

        [TestMethod]
        public void MonacoRenderIsEnabledShouldPrevHandlerWhenSuccessful()
        {
            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                SndModuleSettings<SndPowerPreviewSettings> snd = JsonSerializer.Deserialize<SndModuleSettings<SndPowerPreviewSettings>>(msg);
                Assert.IsTrue(snd.PowertoysSetting.FileExplorerPreviewSettings.Properties.EnableMonacoPreview);
                return 0;
            };

            // arrange
            PowerPreviewViewModel viewModel = new PowerPreviewViewModel(SettingsRepository<PowerPreviewSettings>.GetInstance(mockPowerPreviewSettingsUtils.Object), SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, PowerPreviewSettings.ModuleName);

            // act
            viewModel.MonacoRenderIsEnabled = true;
        }

        [TestMethod]
        public void PDFRenderIsEnabledShouldPrevHandlerWhenSuccessful()
        {
            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                SndModuleSettings<SndPowerPreviewSettings> snd = JsonSerializer.Deserialize<SndModuleSettings<SndPowerPreviewSettings>>(msg);
                Assert.IsTrue(snd.PowertoysSetting.FileExplorerPreviewSettings.Properties.EnablePdfPreview);
                return 0;
            };

            // arrange
            PowerPreviewViewModel viewModel = new PowerPreviewViewModel(SettingsRepository<PowerPreviewSettings>.GetInstance(mockPowerPreviewSettingsUtils.Object), SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, PowerPreviewSettings.ModuleName);

            // act
            viewModel.PDFRenderIsEnabled = true;
        }

        [TestMethod]
        public void GCODERenderIsEnabledShouldPrevHandlerWhenSuccessful()
        {
            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                SndModuleSettings<SndPowerPreviewSettings> snd = JsonSerializer.Deserialize<SndModuleSettings<SndPowerPreviewSettings>>(msg);
                Assert.IsTrue(snd.PowertoysSetting.FileExplorerPreviewSettings.Properties.EnableGcodePreview);
                return 0;
            };

            // arrange
            PowerPreviewViewModel viewModel = new PowerPreviewViewModel(SettingsRepository<PowerPreviewSettings>.GetInstance(mockPowerPreviewSettingsUtils.Object), SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, PowerPreviewSettings.ModuleName);

            // act
            viewModel.GCODERenderIsEnabled = true;
        }
    }
}
