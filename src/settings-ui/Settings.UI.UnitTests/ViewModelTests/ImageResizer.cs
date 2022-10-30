// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.UnitTests.BackwardsCompatibility;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    [TestClass]
    public class ImageResizer
    {
        private Mock<ISettingsUtils> _mockGeneralSettingsUtils;

        private Mock<ISettingsUtils> _mockImgResizerSettingsUtils;

        [TestInitialize]
        public void SetUpStubSettingUtils()
        {
            _mockGeneralSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();
            _mockImgResizerSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<ImageResizerSettings>();
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
            var settingPathMock = new Mock<ISettingsPath>();

            var fileMock = BackCompatTestProperties.GetModuleIOProvider(version, ImageResizerSettings.ModuleName, fileName);
            var mockSettingsUtils = new SettingsUtils(fileMock.Object, settingPathMock.Object);

            ImageResizerSettings originalSettings = mockSettingsUtils.GetSettingsOrDefault<ImageResizerSettings>(ImageResizerSettings.ModuleName);

            var mockGeneralFileMock = BackCompatTestProperties.GetGeneralSettingsIOProvider(version);
            var mockGeneralSettingsUtils = new SettingsUtils(mockGeneralFileMock.Object, settingPathMock.Object);
            GeneralSettings originalGeneralSettings = mockGeneralSettingsUtils.GetSettingsOrDefault<GeneralSettings>();

            var generalSettingsRepository = new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(mockGeneralSettingsUtils);

            // Initialise View Model with test Config files
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(mockSettingsUtils, generalSettingsRepository, sendMockIPCConfigMSG, (string name) => name);

            // Verify that the old settings persisted
            Assert.AreEqual(originalGeneralSettings.Enabled.ImageResizer, viewModel.IsEnabled);
            Assert.AreEqual(ImageResizerViewModel.GetEncoderIndex(originalSettings.Properties.ImageresizerFallbackEncoder.Value), viewModel.Encoder);
            Assert.AreEqual(originalSettings.Properties.ImageresizerFileName.Value, viewModel.FileName);
            Assert.AreEqual(originalSettings.Properties.ImageresizerJpegQualityLevel.Value, viewModel.JPEGQualityLevel);
            Assert.AreEqual(originalSettings.Properties.ImageresizerKeepDateModified.Value, viewModel.KeepDateModified);
            Assert.AreEqual(originalSettings.Properties.ImageresizerPngInterlaceOption.Value, viewModel.PngInterlaceOption);
            Assert.AreEqual(originalSettings.Properties.ImageresizerSizes.Value.Count, viewModel.Sizes.Count);
            Assert.AreEqual(originalSettings.Properties.ImageresizerTiffCompressOption.Value, viewModel.TiffCompressOption);

            // Verify that the stub file was used
            var expectedCallCount = 2;  // once via the view model, and once by the test (GetSettings<T>)
            BackCompatTestProperties.VerifyModuleIOProviderWasRead(fileMock, ImageResizerSettings.ModuleName, expectedCallCount);
            BackCompatTestProperties.VerifyGeneralSettingsIOProviderWasRead(mockGeneralFileMock, expectedCallCount);
        }

        [TestMethod]
        public void IsEnabledShouldEnableModuleWhenSuccessful()
        {
            // Assert
            Func<string, int> sendMockIPCConfigMSG = msg =>
            {
                OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
                Assert.IsTrue(snd.GeneralSettings.Enabled.ImageResizer);
                return 0;
            };

            // arrange
            ImageResizerViewModel viewModel = new ImageResizerViewModel(_mockImgResizerSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(_mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, (string name) => name);

            // act
            viewModel.IsEnabled = true;
        }

        [TestMethod]
        public void JPEGQualityLevelShouldSetValueToTenWhenSuccessful()
        {
            // arrange
            var fileSystemMock = new MockFileSystem();
            var mockSettingsUtils = new SettingsUtils(fileSystemMock);
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(mockSettingsUtils, SettingsRepository<GeneralSettings>.GetInstance(_mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, (string name) => name);

            // act
            viewModel.JPEGQualityLevel = 10;

            // Assert
            viewModel = new ImageResizerViewModel(mockSettingsUtils, SettingsRepository<GeneralSettings>.GetInstance(_mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, (string name) => name);
            Assert.AreEqual(10, viewModel.JPEGQualityLevel);
        }

        [TestMethod]
        public void PngInterlaceOptionShouldSetValueToTenWhenSuccessful()
        {
            // arrange
            var fileSystemMock = new MockFileSystem();
            var mockSettingsUtils = new SettingsUtils(fileSystemMock);
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(mockSettingsUtils, SettingsRepository<GeneralSettings>.GetInstance(_mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, (string name) => name);

            // act
            viewModel.PngInterlaceOption = 10;

            // Assert
            viewModel = new ImageResizerViewModel(mockSettingsUtils, SettingsRepository<GeneralSettings>.GetInstance(_mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, (string name) => name);
            Assert.AreEqual(10, viewModel.PngInterlaceOption);
        }

        [TestMethod]
        public void TiffCompressOptionShouldSetValueToTenWhenSuccessful()
        {
            // arrange
            var fileSystemMock = new MockFileSystem();
            var mockSettingsUtils = new SettingsUtils(fileSystemMock);
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(mockSettingsUtils, SettingsRepository<GeneralSettings>.GetInstance(_mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, (string name) => name);

            // act
            viewModel.TiffCompressOption = 10;

            // Assert
            viewModel = new ImageResizerViewModel(mockSettingsUtils, SettingsRepository<GeneralSettings>.GetInstance(_mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, (string name) => name);
            Assert.AreEqual(10, viewModel.TiffCompressOption);
        }

        [TestMethod]
        public void FileNameShouldUpdateValueWhenSuccessful()
        {
            // arrange
            var fileSystemMock = new MockFileSystem();
            var mockSettingsUtils = new SettingsUtils(fileSystemMock);
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(mockSettingsUtils, SettingsRepository<GeneralSettings>.GetInstance(_mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, (string name) => name);
            string expectedValue = "%1 (%3)";

            // act
            viewModel.FileName = expectedValue;

            // Assert
            viewModel = new ImageResizerViewModel(mockSettingsUtils, SettingsRepository<GeneralSettings>.GetInstance(_mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, (string name) => name);
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

            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(settingUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(_mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, (string name) => name);

            // act
            viewModel.KeepDateModified = true;

            // Assert
            settingUtils.Verify();
        }

        [TestMethod]
        public void EncoderShouldUpdateValueWhenSuccessful()
        {
            // arrange
            var fileSystemMock = new MockFileSystem();
            var mockSettingsUtils = new SettingsUtils(fileSystemMock);
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(mockSettingsUtils, SettingsRepository<GeneralSettings>.GetInstance(_mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, (string name) => name);

            // act
            viewModel.Encoder = 3;

            // Assert
            viewModel = new ImageResizerViewModel(mockSettingsUtils, SettingsRepository<GeneralSettings>.GetInstance(_mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, (string name) => name);
            Assert.AreEqual("163bcc30-e2e9-4f0b-961d-a3e9fdb788a3", viewModel.EncoderGuid);
            Assert.AreEqual(3, viewModel.Encoder);
        }

        [TestMethod]
        public void AddRowShouldAddNewImageSizeWhenSuccessful()
        {
            // arrange
            var mockSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<ImageResizerSettings>();
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(_mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, (string name) => name);
            int sizeOfOriginalArray = viewModel.Sizes.Count;

            // act
            viewModel.AddRow("New size");

            // Assert
            Assert.AreEqual(sizeOfOriginalArray + 1, viewModel.Sizes.Count);
        }

        [TestMethod]
        public void NewlyAddedImageSizeHasCorrectValues()
        {
            // arrange
            var mockSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<ImageResizerSettings>();
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(_mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, (string name) => name);

            // act
            viewModel.AddRow("New size");

            // Assert
            ImageSize newTestSize = viewModel.Sizes.First(x => x.Id == 0);
            Assert.AreEqual(newTestSize.Name, "New size 1");
            Assert.AreEqual(newTestSize.Fit, (int)ResizeFit.Fit);
            Assert.AreEqual(newTestSize.Width, 854);
            Assert.AreEqual(newTestSize.Height, 480);
            Assert.AreEqual(newTestSize.Unit, (int)ResizeUnit.Pixel);
        }

        [TestMethod]
        public void DeleteImageSizeShouldDeleteImageSizeWhenSuccessful()
        {
            // arrange
            var mockSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<ImageResizerSettings>();
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(_mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, (string name) => name);
            viewModel.AddRow("New Size");
            int sizeOfOriginalArray = viewModel.Sizes.Count;
            ImageSize deleteCandidate = viewModel.Sizes.First(x => x.Id == 0);

            // act
            viewModel.DeleteImageSize(0);

            // Assert
            Assert.AreEqual(sizeOfOriginalArray - 1, viewModel.Sizes.Count);
            Assert.IsFalse(viewModel.Sizes.Contains(deleteCandidate));
        }

        [TestMethod]
        public void NameOfNewImageSizesIsIncrementedCorrectly()
        {
            // arrange
            var mockSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<ImageResizerSettings>();
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(_mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, (string name) => name);

            // act
            viewModel.AddRow("New size"); // Add: "New size 1"
            viewModel.AddRow("New size"); // Add: "New size 2"
            viewModel.AddRow("New size"); // Add: "New size 3"
            viewModel.DeleteImageSize(1); // Delete: "New size 2"
            viewModel.AddRow("New size"); // Add: "New Size 4"

            // Assert
            Assert.AreEqual(viewModel.Sizes[0].Name, "New size 1");
            Assert.AreEqual(viewModel.Sizes[1].Name, "New size 3");
            Assert.AreEqual(viewModel.Sizes[2].Name, "New size 4");
        }

        [TestMethod]
        public void UpdateWidthAndHeightShouldUpdateSizeWhenCorrectValuesAreProvided()
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
