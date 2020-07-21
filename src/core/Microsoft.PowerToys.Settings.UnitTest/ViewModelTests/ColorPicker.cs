using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Text.Json;

namespace ViewModelTests
{
    [TestClass]
    public class ColorPicker
    {
        private const string ModuleName = "ColorPicker";

        [TestInitialize]
        public void Setup()
        {
            var generalSettings = new GeneralSettings();
            var colorPickerSettings = new ColorPickerSettings();

            SettingsUtils.SaveSettings(generalSettings.ToJsonString());
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
            var viewModel = new ColorPickerViewModel();

            ShellPage.DefaultSndMSGCallback = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsTrue(snd.GeneralSettings.Enabled.ColorPicker);
            };

            Assert.IsTrue(viewModel.IsEnabled);
        }

        private static void DeleteFolder(string powertoy)
        {
            Directory.Delete(Path.Combine(SettingsUtils.LocalApplicationDataFolder(), $"Microsoft\\PowerToys\\{powertoy}"), true);
        }
    }
}
