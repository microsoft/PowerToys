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
        public const string Module = "Test\\File Explorer";

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
            PowerPreviewViewModel viewModel = new PowerPreviewViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SettingsRepository<PowerPreviewSettings>.GetInstance(ISettingsUtilsMocks.GetStubSettingsUtils().Object), SendMockIPCConfigMSG, Module);

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
            PowerPreviewViewModel viewModel = new PowerPreviewViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SettingsRepository<PowerPreviewSettings>.GetInstance(ISettingsUtilsMocks.GetStubSettingsUtils().Object), SendMockIPCConfigMSG, Module);

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
            PowerPreviewViewModel viewModel = new PowerPreviewViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, SettingsRepository<PowerPreviewSettings>.GetInstance(ISettingsUtilsMocks.GetStubSettingsUtils().Object), SendMockIPCConfigMSG, Module);;

            // act
            viewModel.MDRenderIsEnabled = true;
        }
    }
}
