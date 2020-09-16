// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
            ImageResizerSettings imageResizer = new ImageResizerSettings();
            GeneralSettingsCache<GeneralSettings>.Instance.CommonSettingsConfig = new GeneralSettings();
            SettingsUtils.SaveSettings(GeneralSettingsCache<GeneralSettings>.Instance.CommonSettingsConfig.ToJsonString());
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
            // Assert
            Func<string, int> SendMockIPCConfigMSG = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsTrue(snd.GeneralSettings.Enabled.ImageResizer);
                return 0;
            };

            // arrange
            ImageResizerViewModel viewModel = new ImageResizerViewModel(GeneralSettingsCache<GeneralSettings>.Instance, SendMockIPCConfigMSG);

            // act
            viewModel.IsEnabled = true;
        }

        [TestMethod]
        public void JPEGQualityLevel_ShouldSetValueToTen_WhenSuccessful()
        {
            // arrange
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(GeneralSettingsCache<GeneralSettings>.Instance, SendMockIPCConfigMSG);

            // act
            viewModel.JPEGQualityLevel = 10;

            // Assert
            viewModel = new ImageResizerViewModel(GeneralSettingsCache<GeneralSettings>.Instance, SendMockIPCConfigMSG);
            Assert.AreEqual(10, viewModel.JPEGQualityLevel);
        }

        [TestMethod]
        public void PngInterlaceOption_ShouldSetValueToTen_WhenSuccessful()
        {
            // arrange
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(GeneralSettingsCache<GeneralSettings>.Instance, SendMockIPCConfigMSG);

            // act
            viewModel.PngInterlaceOption = 10;

            // Assert
            viewModel = new ImageResizerViewModel(GeneralSettingsCache<GeneralSettings>.Instance, SendMockIPCConfigMSG);
            Assert.AreEqual(10, viewModel.PngInterlaceOption);
        }

        [TestMethod]
        public void TiffCompressOption_ShouldSetValueToTen_WhenSuccessful()
        {
            // arrange
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(GeneralSettingsCache<GeneralSettings>.Instance, SendMockIPCConfigMSG);

            // act
            viewModel.TiffCompressOption = 10;

            // Assert
            viewModel = new ImageResizerViewModel(GeneralSettingsCache<GeneralSettings>.Instance, SendMockIPCConfigMSG);
            Assert.AreEqual(10, viewModel.TiffCompressOption);
        }

        [TestMethod]
        public void FileName_ShouldUpdateValue_WhenSuccessful()
        {
            // arrange
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(GeneralSettingsCache<GeneralSettings>.Instance, SendMockIPCConfigMSG);
            string expectedValue = "%1 (%3)";

            // act
            viewModel.FileName = expectedValue;

            // Assert
            viewModel = new ImageResizerViewModel(GeneralSettingsCache<GeneralSettings>.Instance, SendMockIPCConfigMSG);
            Assert.AreEqual(expectedValue, viewModel.FileName);
        }

        [TestMethod]
        public void KeepDateModified_ShouldUpdateValue_WhenSuccessful()
        {
            // arrange
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(GeneralSettingsCache<GeneralSettings>.Instance, SendMockIPCConfigMSG);

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
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(GeneralSettingsCache<GeneralSettings>.Instance, SendMockIPCConfigMSG);

            // act
            viewModel.Encoder = 3;

            // Assert
            viewModel = new ImageResizerViewModel(GeneralSettingsCache<GeneralSettings>.Instance, SendMockIPCConfigMSG);
            Assert.AreEqual("163bcc30-e2e9-4f0b-961d-a3e9fdb788a3", viewModel.GetEncoderGuid(viewModel.Encoder));
            Assert.AreEqual(3, viewModel.Encoder);
        }

        [TestMethod]
        public void AddRow_ShouldAddEmptyImageSize_WhenSuccessful()
        {
            // arrange
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(GeneralSettingsCache<GeneralSettings>.Instance, SendMockIPCConfigMSG);
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
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(GeneralSettingsCache<GeneralSettings>.Instance, SendMockIPCConfigMSG);
            int sizeOfOriginalArray = viewModel.Sizes.Count;
            ImageSize deleteCandidate = viewModel.Sizes.Where<ImageSize>(x => x.Id == 0).First();

            // act
            viewModel.DeleteImageSize(0);

            // Assert
            Assert.AreEqual(viewModel.Sizes.Count, sizeOfOriginalArray - 1);
            Assert.IsFalse(viewModel.Sizes.Contains(deleteCandidate));
        }

        [TestMethod]
        public void UpdateWidthAndHeight_ShouldUpateSize_WhenCorrectValuesAreProvided()
        {
            // arrange
            ImageSize imageSize = new ImageSize()
            {
                Id = 0,
                Name = "Test",
                Fit = (int)ResizeFit.Fit,
                Width = 30,
                Height = 30,
                Unit = (int)ResizeUnit.Pixel,
            };

            double negativeWidth = -2.0;
            double negativeHeight = -2.0;

            // Act
            imageSize.Width = negativeWidth;
            imageSize.Height = negativeHeight;

            // Assert
            Assert.AreEqual(0, imageSize.Width);
            Assert.AreEqual(0, imageSize.Height);

            // Act
            imageSize.Width = 50;
            imageSize.Height = 50;

            // Assert
            Assert.AreEqual(50, imageSize.Width);
            Assert.AreEqual(50, imageSize.Height);
        }
    }
}
