// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
    public class ColorPicker
    {
        private const string TestModuleName = "Test\\ColorPicker";

        // This should not be changed. 
        // Changing it will causes user's to lose their local settings configs.
        private const string OriginalModuleName = "ColorPicker";
     

        /// <summary>
        /// Test if the original settings files were modified.
        /// </summary>
        [TestMethod]
        public void OriginalFilesModificationTest()
        {
            var mockIOProvider = IIOProviderMocks.GetMockIOProviderForSaveLoadExists();
            var mockSettingsUtils = new SettingsUtils(mockIOProvider.Object);
            // Load Originl Settings Config File
            ColorPickerSettings originalSettings = mockSettingsUtils.GetSettings<ColorPickerSettings>(OriginalModuleName);
            GeneralSettings originalGeneralSettings = mockSettingsUtils.GetSettings<GeneralSettings>();


            // Initialise View Model with test Config files
            ColorPickerViewModel viewModel = new ColorPickerViewModel(mockSettingsUtils, ColorPickerIsEnabledByDefault_IPC);

            // Verifiy that the old settings persisted
            Assert.AreEqual(originalGeneralSettings.Enabled.ColorPicker, viewModel.IsEnabled);
            Assert.AreEqual(originalSettings.Properties.ActivationShortcut.ToString(), viewModel.ActivationShortcut.ToString());
            Assert.AreEqual(originalSettings.Properties.ChangeCursor, viewModel.ChangeCursor);
        }

        [TestMethod]
        public void ColorPickerIsEnabledByDefault()
        {
            
            var viewModel = new ColorPickerViewModel(ISettingsUtilsMocks.GetStubSettingsUtils().Object, ColorPickerIsEnabledByDefault_IPC);

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
