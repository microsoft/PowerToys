// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Utilities;
using Microsoft.PowerToys.Settings.UI.Library.ViewModels;
using Microsoft.PowerToys.Settings.UI.UnitTests.BackwardsCompatibility;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    [TestClass]
    public class ImageResizer
    {

        private Mock<ISettingsUtils> mockGeneralSettingsUtils;

        private Mock<ISettingsUtils> mockImgResizerSettingsUtils;

        [TestInitialize]
        public void SetUpStubSettingUtils()
        {
            mockGeneralSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();
            mockImgResizerSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<ImageResizerSettings>();
        }


        /// <summary>
        /// Test if the original settings files were modified.
        /// </summary>
        [TestMethod]
        [DataRow("v0.18.2", "settings.json")]
        [DataRow("v0.19.2", "settings.json")]
        [DataRow("v0.20.1", "settings.json")]
        [DataRow("v0.21.1", "settings.json")]
        [DataRow("v0.22.0", "settings.json")]
        public void OriginalFilesModificationTest(string version, string fileName)
        {
            var mockIOProvider = BackCompatTestProperties.GetModuleIOProvider(version, ImageResizerSettings.ModuleName, fileName);
            var mockSettingsUtils = new SettingsUtils(mockIOProvider.Object);
            ImageResizerSettings originalSettings = mockSettingsUtils.GetSettings<ImageResizerSettings>(ImageResizerSettings.ModuleName);

            var mockGeneralIOProvider = BackCompatTestProperties.GetGeneralSettingsIOProvider(version);
            var mockGeneralSettingsUtils = new SettingsUtils(mockGeneralIOProvider.Object);
            GeneralSettings originalGeneralSettings = mockGeneralSettingsUtils.GetSettings<GeneralSettings>();
            var generalSettingsRepository = new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(mockGeneralSettingsUtils);

            // Initialise View Model with test Config files
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(mockSettingsUtils, generalSettingsRepository, SendMockIPCConfigMSG);

            // Verifiy that the old settings persisted
            Assert.AreEqual(originalGeneralSettings.Enabled.ImageResizer, viewModel.IsEnabled);
            Assert.AreEqual(ImageResizerViewModel.GetEncoderIndex(originalSettings.Properties.ImageresizerFallbackEncoder.Value), viewModel.Encoder);
            Assert.AreEqual(originalSettings.Properties.ImageresizerFileName.Value, viewModel.FileName);
            Assert.AreEqual(originalSettings.Properties.ImageresizerJpegQualityLevel.Value, viewModel.JPEGQualityLevel);
            Assert.AreEqual(originalSettings.Properties.ImageresizerKeepDateModified.Value, viewModel.KeepDateModified);
            Assert.AreEqual(originalSettings.Properties.ImageresizerPngInterlaceOption.Value, viewModel.PngInterlaceOption);
            Assert.AreEqual(originalSettings.Properties.ImageresizerSizes.Value.Count, viewModel.Sizes.Count);
            Assert.AreEqual(originalSettings.Properties.ImageresizerTiffCompressOption.Value, viewModel.TiffCompressOption);

            //Verify that the stub file was used
            var expectedCallCount = 2;  //once via the view model, and once by the test (GetSettings<T>)
            BackCompatTestProperties.VerifyModuleIOProviderWasRead(mockIOProvider, ImageResizerSettings.ModuleName, expectedCallCount);
            BackCompatTestProperties.VerifyGeneralSettingsIOProviderWasRead(mockGeneralIOProvider, expectedCallCount);
        }

        [TestMethod]
        public void IsEnabledShouldEnableModuleWhenSuccessful()
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
        public void JPEGQualityLevelShouldSetValueToTenWhenSuccessful()
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
        public void PngInterlaceOptionShouldSetValueToTenWhenSuccessful()
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
        public void TiffCompressOptionShouldSetValueToTenWhenSuccessful()
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
        public void FileNameShouldUpdateValueWhenSuccessful()
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
        public void KeepDateModifiedShouldUpdateValueWhenSuccessful()
        {
            // arrange
            var settingUtils = ISettingsUtilsMocks.GetStubSettingsUtils<ImageResizerSettings>();

            var expectedSettingsString = new ImageResizerSettings() { Properties = new ImageResizerProperties() { ImageresizerKeepDateModified = new BoolProperty() { Value = true } } }.ToJsonString();
            // Using Ordinal since this is used internally
            settingUtils.Setup(x => x.SaveSettings(
                                        It.Is<string>(content => content.Equals(expectedSettingsString, StringComparison.Ordinal)),
                                        It.Is<string>(module => module.Equals(ImageResizerSettings.ModuleName, StringComparison.Ordinal)),
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
        public void EncoderShouldUpdateValueWhenSuccessful()
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
            Assert.AreEqual("163bcc30-e2e9-4f0b-961d-a3e9fdb788a3", viewModel.EncoderGuid);
            Assert.AreEqual(3, viewModel.Encoder);
        }

        [TestMethod]
        public void AddRowShouldAddEmptyImageSizeWhenSuccessful()
        {
            // arrange
            var mockSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<ImageResizerSettings>();
            Func<string, int> SendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(mockGeneralSettingsUtils.Object), SendMockIPCConfigMSG);
            int sizeOfOriginalArray = viewModel.Sizes.Count;

            // act
            viewModel.AddRow();

            // Assert
            Assert.AreEqual(sizeOfOriginalArray + 1, viewModel.Sizes.Count);
        }

        [TestMethod]
        public void DeleteImageSizeShouldDeleteImageSizeWhenSuccessful()
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
            Assert.AreEqual(sizeOfOriginalArray - 1, viewModel.Sizes.Count);
            Assert.IsFalse(viewModel.Sizes.Contains(deleteCandidate));
        }

        [TestMethod]
        public void UpdateWidthAndHeightShouldUpateSizeWhenCorrectValuesAreProvided()
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
