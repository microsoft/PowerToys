// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ViewModelTests
{
    [TestClass]
    public class ColorPicker
    {
        private const string TestModuleName = "Test\\ColorPicker";

        // This should not be changed. 
        // Changing it will causes user's to lose their local settings configs.
        private const string OriginalModuleName = "ColorPicker";

        [TestInitialize]
        public void Setup()
        {
            var generalSettings = new GeneralSettings();
            var colorPickerSettings = new ColorPickerSettings();

            SettingsUtils.SaveSettings(generalSettings.ToJsonString(), "Test");
            SettingsUtils.SaveSettings(colorPickerSettings.ToJsonString(), TestModuleName, TestModuleName + ".json");

        }

        [TestCleanup]
        public void CleanUp()
        {
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

        /// <summary>
        /// Test if the original settings files were modified.
        /// </summary>
        [TestMethod]
        public void OriginalFilesModificationTest()
        {
            SettingsUtils.IsTestMode = true;

            // Load Originl Settings Config File
            ColorPickerSettings originalSettings = SettingsUtils.GetSettings<ColorPickerSettings>(OriginalModuleName);
            GeneralSettings originalGeneralSettings = SettingsUtils.GetSettings<GeneralSettings>();

            // Initialise View Model with test Config files
            ColorPickerViewModel viewModel = new ColorPickerViewModel(ColorPickerIsEnabledByDefault_IPC);

            // Verifiy that the old settings persisted
            Assert.AreEqual(originalGeneralSettings.Enabled.ColorPicker, viewModel.IsEnabled);
            Assert.AreEqual(originalSettings.Properties.ActivationShortcut.ToString(), viewModel.ActivationShortcut.ToString());
            Assert.AreEqual(originalSettings.Properties.ChangeCursor, viewModel.ChangeCursor);
        }

        [TestMethod]
        public void ColorPickerIsEnabledByDefault()
        {
            SettingsUtils.IsTestMode = true;

            var viewModel = new ColorPickerViewModel(ColorPickerIsEnabledByDefault_IPC);

            Assert.IsTrue(viewModel.IsEnabled);
        }

        public int ColorPickerIsEnabledByDefault_IPC(string msg)
        {
            OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
            Assert.IsTrue(snd.GeneralSettings.Enabled.ColorPicker);
            return 0;
        }

        private static void DeleteFolder(string powertoy)
        {
            Directory.Delete(Path.Combine(SettingsUtils.LocalApplicationDataFolder(), $"Microsoft\\PowerToys\\{powertoy}"), true);
        }
    }
}
