// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Settings.UI.Lib.Utilities;
using Microsoft.PowerToys.Settings.UI.Lib.ViewModels;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    [TestClass]
    public class ImageResizer
    {
        // To have a consistent name.
        public const string Module = ImageResizerSettings.ModuleName;

        private Mock<ISettingsUtils> mockGeneralSettingsUtils;

        private Mock<ISettingsUtils> mockImgResizerSettingsUtils;

        [TestInitialize]
        public void SetUp_StubSettingUtils()
        {
            mockGeneralSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();
            mockImgResizerSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<ImageResizerSettings>();
        }


        /// <summary>
        /// Test if the original settings files were modified.
        /// </summary>
        [TestMethod]
        public void OriginalFilesModificationTest()
        {
            var mockIOProvider = IIOProviderMocks.GetMockIOProviderForSaveLoadExists();
            var mockSettingsUtils = new SettingsUtils(mockIOProvider.Object);
            // Load Originl Settings Config File
            ImageResizerSettings originalSettings = mockSettingsUtils.GetSettings<ImageResizerSettings>(Module);
            GeneralSettings originalGeneralSettings = mockSettingsUtils.GetSettings<GeneralSettings>();

            // Initialise View Model with test Config files
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(mockSettingsUtils, SettingsRepository<GeneralSettings>.GetInstance(mockSettingsUtils), SendMockIPCConfigMSG);

            // Verifiy that the old settings persisted
            Assert.AreEqual(originalGeneralSettings.Enabled.ImageResizer, viewModel.IsEnabled);
            Assert.AreEqual(viewModel.GetEncoderIndex(originalSettings.Properties.ImageresizerFallbackEncoder.Value), viewModel.Encoder);
            Assert.AreEqual(originalSettings.Properties.ImageresizerFileName.Value, viewModel.FileName);
            Assert.AreEqual(originalSettings.Properties.ImageresizerJpegQualityLevel.Value, viewModel.JPEGQualityLevel);
            Assert.AreEqual(originalSettings.Properties.ImageresizerKeepDateModified.Value, viewModel.KeepDateModified);
            Assert.AreEqual(originalSettings.Properties.ImageresizerPngInterlaceOption.Value, viewModel.PngInterlaceOption);
            Assert.AreEqual(originalSettings.Properties.ImageresizerSizes.Value.Count, viewModel.Sizes.Count);
            Assert.AreEqual(originalSettings.Properties.ImageresizerTiffCompressOption.Value, viewModel.TiffCompressOption);
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
            ImageResizerViewModel viewModel = new ImageResizerViewModel(mockImgResizerSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG);

            // act
            viewModel.IsEnabled = true;
        }

        [TestMethod]
        public void JPEGQualityLevel_ShouldSetValueToTen_WhenSuccessful()
        {
            // arrange
            var mockIOProvider = IIOProviderMocks.GetMockIOProviderForSaveLoadExists();
            var mockSettingsUtils = new SettingsUtils(mockIOProvider.Object);
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(mockSettingsUtils, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG);

            // act
            viewModel.JPEGQualityLevel = 10;

            // Assert
            viewModel = new ImageResizerViewModel(mockSettingsUtils, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG);
            Assert.AreEqual(10, viewModel.JPEGQualityLevel);
        }

        [TestMethod]
        public void PngInterlaceOption_ShouldSetValueToTen_WhenSuccessful()
        {
            // arrange
            var mockIOProvider = IIOProviderMocks.GetMockIOProviderForSaveLoadExists();
            var mockSettingsUtils = new SettingsUtils(mockIOProvider.Object);
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(mockSettingsUtils, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG);

            // act
            viewModel.PngInterlaceOption = 10;

            // Assert
            viewModel = new ImageResizerViewModel(mockSettingsUtils, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG);
            Assert.AreEqual(10, viewModel.PngInterlaceOption);
        }

        [TestMethod]
        public void TiffCompressOption_ShouldSetValueToTen_WhenSuccessful()
        {
            // arrange
            var mockIOProvider = IIOProviderMocks.GetMockIOProviderForSaveLoadExists();
            var mockSettingsUtils = new SettingsUtils(mockIOProvider.Object);
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(mockSettingsUtils, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG);

            // act
            viewModel.TiffCompressOption = 10;

            // Assert
            viewModel = new ImageResizerViewModel(mockSettingsUtils, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG);
            Assert.AreEqual(10, viewModel.TiffCompressOption);
        }

        [TestMethod]
        public void FileName_ShouldUpdateValue_WhenSuccessful()
        {
            // arrange
            var mockIOProvider = IIOProviderMocks.GetMockIOProviderForSaveLoadExists();
            var mockSettingsUtils = new SettingsUtils(mockIOProvider.Object);
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(mockSettingsUtils, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG);
            string expectedValue = "%1 (%3)";

            // act
            viewModel.FileName = expectedValue;

            // Assert
            viewModel = new ImageResizerViewModel(mockSettingsUtils, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG);
            Assert.AreEqual(expectedValue, viewModel.FileName);
        }

        [TestMethod]
        public void KeepDateModified_ShouldUpdateValue_WhenSuccessful()
        {
            // arrange
            var settingUtils = ISettingsUtilsMocks.GetStubSettingsUtils<ImageResizerSettings>();

            var expectedSettingsString = new ImageResizerSettings() { Properties = new ImageResizerProperties() { ImageresizerKeepDateModified = new BoolProperty() { Value = true } } }.ToJsonString();
            settingUtils.Setup(x => x.SaveSettings(
                                        It.Is<string>(content => content.Equals(expectedSettingsString, StringComparison.Ordinal)),
                                        It.Is<string>(module => module.Equals(Module, StringComparison.Ordinal)),
                                        It.IsAny<string>()))
                                     .Verifiable();

            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(settingUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG);

            // act
            viewModel.KeepDateModified = true;

            // Assert
            settingUtils.Verify();
        }

        [TestMethod]
        public void Encoder_ShouldUpdateValue_WhenSuccessful()
        {
            // arrange
            var mockIOProvider = IIOProviderMocks.GetMockIOProviderForSaveLoadExists();
            var mockSettingsUtils = new SettingsUtils(mockIOProvider.Object);
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(mockSettingsUtils, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG);

            // act
            viewModel.Encoder = 3;

            // Assert
            viewModel = new ImageResizerViewModel(mockSettingsUtils, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG);
            Assert.AreEqual("163bcc30-e2e9-4f0b-961d-a3e9fdb788a3", viewModel.GetEncoderGuid(viewModel.Encoder));
            Assert.AreEqual(3, viewModel.Encoder);
        }

        [TestMethod]
        public void AddRow_ShouldAddEmptyImageSize_WhenSuccessful()
        {
            // arrange
            var mockSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<ImageResizerSettings>();
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG);
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
            var mockSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<ImageResizerSettings>();
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG);
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
