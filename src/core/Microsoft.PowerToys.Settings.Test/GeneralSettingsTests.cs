using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace Microsoft.PowerToys.Settings.UnitTest
{
    [TestClass]
    public class GeneralSettingsTests
    {
        [TestMethod]
        public async Task Packaged_UpdatePackakedValueAndSaveSetting_WhenSuccessfulAsync()
        {
            /*
            // Arrange
            GeneralViewModel viewModel = new GeneralViewModel();

            // Act
            GeneralSettings tempSettings = new GeneralSettings();
            tempSettings.Packaged = true; // reset packaged value
            SettingsUtils.SaveSettings(tempSettings.ToJsonString(), string.Empty); // save packaged value to file.
            viewModel.Packaged = false; // update packaged value using the method tested.
            await Task.Delay(2000);
            viewModel = new GeneralViewModel(); // reload the configuration file.

            // Assert
            Assert.IsFalse(viewModel.Packaged);

            */
        }

    }
}
