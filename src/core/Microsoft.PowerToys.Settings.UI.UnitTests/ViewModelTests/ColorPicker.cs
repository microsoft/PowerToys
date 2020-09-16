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
        private const string ModuleName = "ColorPicker";

        [TestInitialize]
        public void Setup()
        {
            var colorPickerSettings = new ColorPickerSettings();

            SettingsUtils.SaveSettings(colorPickerSettings.ToJsonString(), colorPickerSettings.Name, ModuleName + ".json");
        }

        [TestCleanup]
        public void CleanUp()
        {
            string generalSettings_file_name = string.Empty;
            if (SettingsUtils.SettingsFolderExists(generalSettings_file_name))
            {
                DeleteFolder(generalSettings_file_name);
            }

            if (SettingsUtils.SettingsFolderExists(ModuleName))
            {
                DeleteFolder(ModuleName);
            }
        }

        [TestMethod]
        public void ColorPickerIsEnabledByDefault()
        {
            var viewModel = new ColorPickerViewModel(GeneralSettings.Instance, ColorPickerIsEnabledByDefault_IPC);

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
