using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ViewModelTests
{
    [TestClass]
    public class ImageResizer
    {
        public const string Module = "ImageResizer";
        [TestInitialize]
        public void Setup()
        {
            // initialize creation of test settings file.
            // Test base path:
            // C:\Users\<user name>\AppData\Local\Packages\08e1807b-8b6d-4bfa-adc4-79c64aae8e78_9abkseg265h2m\LocalState\Microsoft\PowerToys\
            GeneralSettings generalSettings = new GeneralSettings();
            ImageResizerSettings imageResizer = new ImageResizerSettings();

            SettingsUtils.SaveSettings(generalSettings.ToJsonString());
            SettingsUtils.SaveSettings(imageResizer.ToJsonString(), imageResizer.Name);
        }

        [TestCleanup]
        public void CleanUp()
        {
            // delete folder created.
            string generalSettings_file_name = string.Empty;
            if (SettingsUtils.SettingsFolderExists(generalSettings_file_name))
            {
                DeleteFolder(generalSettings_file_name);
            }

            if (SettingsUtils.SettingsFolderExists(Module))
            {
                DeleteFolder(Module);
            }
        }

        public void DeleteFolder(string powertoy)
        {
            Directory.Delete(Path.Combine(SettingsUtils.LocalApplicationDataFolder(), $"Microsoft\\PowerToys\\{powertoy}"), true);
        }

        [TestMethod]
        public void IsEnabled_ShouldEnableModule_WhenSuccessful()
        {
            // arrange
            ImageResizerViewModel viewModel = new ImageResizerViewModel();

            // Assert
            ShellPage.DefaultSndMSGCallback = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsTrue(snd.GeneralSettings.Enabled.ImageResizer);
            };

            // act
            viewModel.IsEnabled = true;
        }

        [TestMethod]
        public void JPEGQualityLevel_ShouldSetValueToTen_WhenSuccessful()
        {
            // arrange
            ImageResizerViewModel viewModel = new ImageResizerViewModel();

            // act
            viewModel.JPEGQualityLevel = 10;

            // Assert
            viewModel = new ImageResizerViewModel();
            Assert.AreEqual(10, viewModel.JPEGQualityLevel);
        }

        [TestMethod]
        public void PngInterlaceOption_ShouldSetValueToTen_WhenSuccessful()
        {
            // arrange
            ImageResizerViewModel viewModel = new ImageResizerViewModel();

            // act
            viewModel.PngInterlaceOption = 10;

            // Assert
            viewModel = new ImageResizerViewModel();
            Assert.AreEqual(10, viewModel.PngInterlaceOption);
        }

        [TestMethod]
        public void TiffCompressOption_ShouldSetValueToTen_WhenSuccessful()
        {
            // arrange
            ImageResizerViewModel viewModel = new ImageResizerViewModel();

            // act
            viewModel.TiffCompressOption = 10;

            // Assert
            viewModel = new ImageResizerViewModel();
            Assert.AreEqual(10, viewModel.TiffCompressOption);
        }

        [TestMethod]
        public void FileName_ShouldUpdateValue_WhenSuccessful()
        {
            // arrange
            ImageResizerViewModel viewModel = new ImageResizerViewModel();
            string expectedValue = "%1 (%3)";

            // act
            viewModel.FileName = expectedValue;

            // Assert
            viewModel = new ImageResizerViewModel();
            Assert.AreEqual(expectedValue, viewModel.FileName);
        }

        [TestMethod]
        public void KeepDateModified_ShouldUpdateValue_WhenSuccessful()
        {
            // arrange
            ImageResizerViewModel viewModel = new ImageResizerViewModel();

            // act
            viewModel.KeepDateModified = true;

            // Assert
            ImageResizerSettings settings = SettingsUtils.GetSettings<ImageResizerSettings>(Module);
            Assert.AreEqual(true, settings.Properties.ImageresizerKeepDateModified.Value);
        }


        [TestMethod]
        public void Encoder_ShouldUpdateValue_WhenSuccessful()
        {
            // arrange
            ImageResizerViewModel viewModel = new ImageResizerViewModel();

            // act
            viewModel.Encoder = 3;

            // Assert
            viewModel = new ImageResizerViewModel();
            Assert.AreEqual("163bcc30-e2e9-4f0b-961d-a3e9fdb788a3", viewModel.GetEncoderGuid(viewModel.Encoder));
            Assert.AreEqual(3, viewModel.Encoder);
        }

        [TestMethod]
        public void AddRow_ShouldAddEmptyImageSize_WhenSuccessful()
        {
            // arrange
            ImageResizerViewModel viewModel = new ImageResizerViewModel();
            int sizeOfOriginalArray = viewModel.Sizes.Count;

            // act
            viewModel.AddRow();

            // Assert
            Assert.AreEqual(viewModel.Sizes.Count, sizeOfOriginalArray + 1);
        }

        [TestMethod]
        public void DeleteImageSize_ShouldDeleteImageSize_WhenSuccessful()
        {
            // arrange
            ImageResizerViewModel viewModel = new ImageResizerViewModel();
            int sizeOfOriginalArray = viewModel.Sizes.Count;
            ImageSize deleteCandidate = viewModel.Sizes.Where<ImageSize>(x => x.Id == 0).First();

            // act
            viewModel.DeleteImageSize(0);

            // Assert
            Assert.AreEqual(viewModel.Sizes.Count, sizeOfOriginalArray - 1);
            Assert.IsFalse(viewModel.Sizes.Contains(deleteCandidate));
        }
    }
}
