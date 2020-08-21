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
        private const string ModuleName = "Test\\ColorPicker";
        private const string GeneralSettingsFolderName = "Test";

        [TestInitialize]
        public void Setup()
        {
            var generalSettings = new GeneralSettings();
            var colorPickerSettings = new ColorPickerSettings();

            SettingsUtils.SaveSettings(generalSettings.ToJsonString(), GeneralSettingsFolderName);
            SettingsUtils.SaveSettings(colorPickerSettings.ToJsonString(), ModuleName);
        }

        [TestCleanup]
        public void CleanUp()
        {
            if (SettingsUtils.SettingsFolderExists(GeneralSettingsFolderName))
            {
                DeleteFolder(GeneralSettingsFolderName);
            }

            if (SettingsUtils.SettingsFolderExists(ModuleName))
            {
                DeleteFolder(ModuleName);
            }
        }

        [TestMethod]
        public void ColorPickerIsEnabledByDefault()
        {
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
