// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.IO;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.ViewModels;
using Microsoft.PowerToys.Settings.UI.UnitTests.BackwardsCompatibility;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class ColorPicker
    {
        private const string OriginalModuleName = "ColorPicker";


        /// <summary>
        /// Test if the original settings files were modified.
        /// </summary>
        [TestMethod]
        [DataRow("v0.20.2", "settings.json")]
        [DataRow("v0.18.2", "settings.json")]
        public void OriginalFilesModificationTest(string version, string fileName)
        {
            //Arrange
            var mockIOProvider = BackCompatTestProperties.GetModuleIOProvider(version, OriginalModuleName, fileName);
            var mockSettingsUtils = new SettingsUtils(mockIOProvider.Object);
            ColorPickerSettings originalSettings = mockSettingsUtils.GetSettings<ColorPickerSettings>(OriginalModuleName);

            var mockGeneralIOProvider = BackCompatTestProperties.GetGeneralSettingsIOProvider(version);
            var mockGeneralSettingsUtils = new SettingsUtils(mockGeneralIOProvider.Object);
            GeneralSettings originalGeneralSettings = mockGeneralSettingsUtils.GetSettings<GeneralSettings>();
            var generalSettingsRepository = new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(mockGeneralSettingsUtils);

            //Act
            // Initialise View Model with test Config files
            ColorPickerViewModel viewModel = new ColorPickerViewModel(mockSettingsUtils, generalSettingsRepository, ColorPickerIsEnabledByDefault_IPC);

            //Assert
            // Verifiy that the old settings persisted
            Assert.AreEqual(originalGeneralSettings.Enabled.ColorPicker, viewModel.IsEnabled);
            Assert.AreEqual(originalSettings.Properties.ActivationShortcut.ToString(), viewModel.ActivationShortcut.ToString());
            Assert.AreEqual(originalSettings.Properties.ChangeCursor, viewModel.ChangeCursor);

            //Verify that the stub file was used
            var expectedCallCount = 2;  //once via the view model, and once by the test (GetSettings<T>)
            BackCompatTestProperties.VerifyModuleIOProviderWasRead(mockIOProvider, OriginalModuleName, expectedCallCount);
            BackCompatTestProperties.VerifyGeneralSettingsIOProviderWasRead(mockGeneralIOProvider, expectedCallCount);
        }

        [TestMethod]
        public void ColorPickerIsEnabledByDefault()
        {
            var mockSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<ColorPickerSettings>();
            var viewModel = new ColorPickerViewModel(ISettingsUtilsMocks.GetStubSettingsUtils<ColorPickerSettings>().Object, SettingsRepository<GeneralSettings>.GetInstance(ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>().Object), ColorPickerIsEnabledByDefault_IPC);

            Assert.IsTrue(viewModel.IsEnabled);
        }

        public int ColorPickerIsEnabledByDefault_IPC(string msg)
        {
            OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
            Assert.IsTrue(snd.GeneralSettings.Enabled.ColorPicker);
            return 0;
        }

    }
}
