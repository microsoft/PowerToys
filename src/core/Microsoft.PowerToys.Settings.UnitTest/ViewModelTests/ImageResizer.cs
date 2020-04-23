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
        public void JPEGQualityLevel_ShouldSetValueToTen_WhenSuccefull()
        {
            // arrange
            ImageResizerViewModel viewModel = new ImageResizerViewModel();

            // act
            viewModel.JPEGQualityLevel = 10;

            // Assert
            ImageResizerSettings settings = SettingsUtils.GetSettings<ImageResizerSettings>(Module);
            Assert.AreEqual(10, settings.Properties.ImageresizerJpegQualityLevel.Value);
        }

        [TestMethod]
        public void PngInterlaceOption_ShouldSetValueToTen_WhenSuccefull()
        {
            // arrange
            ImageResizerViewModel viewModel = new ImageResizerViewModel();

            // act
            viewModel.PngInterlaceOption = 10;

            // Assert
            ImageResizerSettings settings = SettingsUtils.GetSettings<ImageResizerSettings>(Module);
            Assert.AreEqual(10, settings.Properties.ImageresizerPngInterlaceOption.Value);
        }

        [TestMethod]
        public void TiffCompressOption_ShouldSetValueToTen_WhenSuccefull()
        {
            // arrange
            ImageResizerViewModel viewModel = new ImageResizerViewModel();

            // act
            viewModel.TiffCompressOption = 10;

            // Assert
            ImageResizerSettings settings = SettingsUtils.GetSettings<ImageResizerSettings>(Module);
            Assert.AreEqual(10, settings.Properties.ImageresizerTiffCompressOption.Value);
        }

        [TestMethod]
        public void FileName_ShouldUpdateValue_WhenSuccefull()
        {
            // arrange
            ImageResizerViewModel viewModel = new ImageResizerViewModel();
            string exptectedValue = "%1 (%3)";

            // act
            viewModel.FileName = exptectedValue;

            // Assert
            ImageResizerSettings settings = SettingsUtils.GetSettings<ImageResizerSettings>(Module);
            Assert.AreEqual(exptectedValue, settings.Properties.ImageresizerFileName.Value);
        }

        [TestMethod]
        public void FileName_ShouldNOTUpdateValue_WhenNameIsInValid ()
        {
            // arrange
            ImageResizerViewModel viewModel = new ImageResizerViewModel();
            string[] invalidNames =
            {
                string.Empty,
                " ",            // no name.
                "%1",           // single name value.
                "%7 (%5)",      // name max index exceeded.
                "%8 (%8)",      // name max index exceeded.
                "%5 (%3 )",     // name contains extra spaces.
                "%5  (%3)",     // name contains extra spaces.
                "%5 ( %3)",     // name contains extra spaces.
                "% 5 ( %3)",     // name contains extra spaces.
                "%5 (% 3)",     // name contains extra spaces.
                "%5 ( %3 )",     // name contains extra spaces.
            };

            // act and Assert
            foreach (string invalidName in invalidNames)
            {
                viewModel.FileName = invalidName;
                Assert.AreNotEqual(invalidName, viewModel.FileName);
            }
        }

        [TestMethod]
        public void KeepDateModified_ShouldUpdateValue_WhenSuccefull()
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
        public void Encoder_ShouldUpdateValue_WhenSuccefull()
        {
            // arrange
            ImageResizerViewModel viewModel = new ImageResizerViewModel();

            // act
            viewModel.Encoder = 3;

            // Assert
            ImageResizerSettings settings = SettingsUtils.GetSettings<ImageResizerSettings>(Module);
            Assert.AreEqual("163bcc30-e2e9-4f0b-961d-a3e9fdb788a3", settings.Properties.ImageresizerFallbackEncoder.Value);
            Assert.AreEqual(3, viewModel.Encoder);
        }

        [TestMethod]
        public void AddRow_ShouldAddEmptyImageSize_WhenSuccefull()
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
        public void DeleteImageSize_ShouldDeleteImageSize_WhenSuccefull()
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
