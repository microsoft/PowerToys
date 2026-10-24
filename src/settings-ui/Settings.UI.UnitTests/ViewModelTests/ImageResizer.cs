// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
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
        private Mock<SettingsUtils> _mockGeneralSettingsUtils;

        private Mock<SettingsUtils> _mockImgResizerSettingsUtils;

        public TestContext TestContext { get; set; }

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
            var settingPathMock = new Mock<SettingPath>();

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
        public void AddImageSizeShouldAddNewImageSizeWhenSuccessful()
        {
            // arrange
            var mockSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<ImageResizerSettings>();
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(_mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, (string name) => name);
            int sizeOfOriginalArray = viewModel.Sizes.Count;

            // act
            viewModel.AddImageSize();

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
            viewModel.AddImageSize("New size");

            // Assert
            ImageSize newTestSize = viewModel.Sizes.First(x => x.Id == 0);
            Assert.AreEqual("New size 1", newTestSize.Name);
            Assert.AreEqual(ResizeFit.Fit, newTestSize.Fit);
            Assert.AreEqual(1024, newTestSize.Width);
            Assert.AreEqual(640, newTestSize.Height);
            Assert.AreEqual(ResizeUnit.Pixel, newTestSize.Unit);
        }

        [TestMethod]
        public void CreateNewImageSizeModelShouldNotAddToCollection()
        {
            ImageResizerSettings settings = CreatePresetPersistenceSettings();
            string originalJson = settings.ToJsonString();
            var savedSettings = new List<(string Json, string FileName)>();
            ImageResizerViewModel viewModel = CreatePresetPersistenceViewModel(settings, savedSettings);
            ImageSize[] originalSizes = viewModel.Sizes.ToArray();

            ImageSize workingCopy = viewModel.CreateNewImageSizeModel("Web");
            ImageSize secondWorkingCopy = viewModel.CreateNewImageSizeModel("Web");

            Assert.IsNotNull(workingCopy);
            Assert.AreEqual(4, workingCopy.Id);
            Assert.AreEqual(workingCopy.Id, secondWorkingCopy.Id);
            Assert.AreEqual("Web 1", workingCopy.Name);
            Assert.AreEqual(settings.Properties.ImageresizerCustomSize.Value.Fit, workingCopy.Fit);
            Assert.AreEqual(settings.Properties.ImageresizerCustomSize.Value.Width, workingCopy.Width);
            Assert.AreEqual(settings.Properties.ImageresizerCustomSize.Value.Height, workingCopy.Height);
            Assert.AreEqual(settings.Properties.ImageresizerCustomSize.Value.Unit, workingCopy.Unit);

            workingCopy.Name = "Discarded";
            workingCopy.Width = 999;
            ImageSize editedCopy = originalSizes[0].Clone();
            editedCopy.Name = "Discarded edit";
            editedCopy.Height = 999;

            CollectionAssert.AreEqual(originalSizes, viewModel.Sizes.ToArray());
            Assert.AreEqual(originalJson, settings.ToJsonString());
            Assert.AreEqual(0, savedSettings.Count);
            Assert.IsFalse(viewModel.IsListViewFocusRequested);
        }

        [TestMethod]
        public void AddImageSizeWithModelShouldCommitPreparedSize()
        {
            ImageResizerSettings settings = CreatePresetPersistenceSettings();
            string originalJson = settings.ToJsonString();
            var expectedSizes = settings.Properties.ImageresizerSizes.Value.Select(size => size.Clone()).ToList();
            var savedSettings = new List<(string Json, string FileName)>();
            ImageResizerViewModel viewModel = CreatePresetPersistenceViewModel(settings, savedSettings);
            ImageSize workingCopy = viewModel.CreateNewImageSizeModel("Web");
            workingCopy.Name = "Web";
            workingCopy.Fit = ResizeFit.Fit;
            workingCopy.Width = 1024;
            workingCopy.Height = 768;
            workingCopy.Unit = ResizeUnit.Pixel;

            Assert.AreEqual(0, savedSettings.Count);
            viewModel.DeleteImageSize(3);
            expectedSizes.RemoveAt(3);
            AssertSavedPresetPair(savedSettings, expectedSizes, originalJson);
            string sizesJsonAfterPhoneDeletion = savedSettings[0].Json;
            string settingsJsonAfterPhoneDeletion = savedSettings[1].Json;

            savedSettings.Clear();
            viewModel.AddImageSize(workingCopy);
            expectedSizes.Add(new ImageSize(4, "Web", ResizeFit.Fit, 1024, 768, ResizeUnit.Pixel));

            Assert.AreEqual(4, workingCopy.Id);
            Assert.AreSame(workingCopy, viewModel.Sizes[3]);
            CollectionAssert.AreEqual(expectedSizes.Select(size => size.Id).ToArray(), viewModel.Sizes.Select(size => size.Id).ToArray());
            Assert.IsTrue(viewModel.IsListViewFocusRequested);
            AssertSavedPresetPair(savedSettings, expectedSizes, originalJson);

            ImageResizerSettings reloadedSettings = JsonSerializer.Deserialize<ImageResizerSettings>(savedSettings[1].Json);
            var reloadedWrites = new List<(string Json, string FileName)>();
            ImageResizerViewModel reloadedViewModel = CreatePresetPersistenceViewModel(reloadedSettings, reloadedWrites);
            CollectionAssert.AreEqual(
                expectedSizes.Select(size => size.ToJsonString()).ToArray(),
                reloadedViewModel.Sizes.Select(size => size.ToJsonString()).ToArray());
            Assert.AreEqual(0, reloadedWrites.Count);
            TestContext.WriteLine(
                "ImageResizerProducerPayloads: {0}",
                JsonSerializer.Serialize(new
                {
                    InitialSettingsJson = originalJson,
                    SizesJsonAfterPhoneDeletion = sizesJsonAfterPhoneDeletion,
                    SettingsJsonAfterPhoneDeletion = settingsJsonAfterPhoneDeletion,
                    SizesJson = savedSettings[0].Json,
                    SettingsJson = savedSettings[1].Json,
                }));
        }

        [TestMethod]
        public void UpdateImageSizeShouldApplyWorkingCopyValuesToOriginal()
        {
            ImageResizerSettings settings = CreatePresetPersistenceSettings();
            string originalJson = settings.ToJsonString();
            var expectedSizes = settings.Properties.ImageresizerSizes.Value.Select(size => size.Clone()).ToList();
            var savedSettings = new List<(string Json, string FileName)>();
            ImageResizerViewModel viewModel = CreatePresetPersistenceViewModel(settings, savedSettings);
            ImageSize original = viewModel.Sizes[0];

            ImageSize edited = original.Clone();
            edited.Id = 99;
            edited.Name = "Edited";
            edited.Fit = ResizeFit.Stretch;
            edited.Width = 320;
            edited.Height = 240;
            edited.Unit = ResizeUnit.Percent;

            Assert.AreEqual(originalJson, settings.ToJsonString());
            Assert.AreEqual(0, savedSettings.Count);
            viewModel.UpdateImageSize(original, edited);
            expectedSizes[0] = new ImageSize(0, "Edited", ResizeFit.Stretch, 320, 240, ResizeUnit.Percent);

            Assert.AreEqual("Edited", original.Name);
            Assert.AreEqual(ResizeFit.Stretch, original.Fit);
            Assert.AreEqual(320, original.Width);
            Assert.AreEqual(240, original.Height);
            Assert.AreEqual(ResizeUnit.Percent, original.Unit);
            Assert.AreEqual(0, original.Id);
            Assert.AreSame(original, viewModel.Sizes.First(x => x.Id == 0));
            AssertSavedPresetPair(savedSettings, expectedSizes, originalJson);

            savedSettings.Clear();
            original.Name = "Renamed";
            expectedSizes[0].Name = "Renamed";

            Assert.AreEqual(4, savedSettings.Count);
            AssertSavedPresetPair(savedSettings.Take(2).ToArray(), expectedSizes, originalJson);
            AssertSavedPresetPair(savedSettings.Skip(2).ToArray(), expectedSizes, originalJson);
        }

        [TestMethod]
        public void DeletedPresetChangesShouldNotSaveSettings()
        {
            ImageResizerSettings settings = CreatePresetPersistenceSettings();
            string originalJson = settings.ToJsonString();
            var expectedSizes = settings.Properties.ImageresizerSizes.Value.Select(size => size.Clone()).ToList();
            var savedSettings = new List<(string Json, string FileName)>();
            ImageResizerViewModel viewModel = CreatePresetPersistenceViewModel(settings, savedSettings);
            ImageSize deleted = viewModel.Sizes[3];

            viewModel.DeleteImageSize(deleted.Id);
            expectedSizes.RemoveAt(3);
            AssertSavedPresetPair(savedSettings, expectedSizes, originalJson);

            savedSettings.Clear();
            deleted.Name = "Detached";
            deleted.Width = 999;

            Assert.AreEqual(0, savedSettings.Count);
            Assert.AreEqual(3, viewModel.Sizes.Count);
            Assert.IsFalse(viewModel.Sizes.Contains(deleted));
        }

        [TestMethod]
        public void DiscardedDraftsShouldNotConsumeIdsForPreparedCommits()
        {
            ImageResizerSettings settings = CreatePresetPersistenceSettings();
            settings.Properties.ImageresizerSizes.Value.Clear();
            string originalJson = settings.ToJsonString();
            var savedSettings = new List<(string Json, string FileName)>();
            var expectedSizes = new List<ImageSize>();
            ImageResizerViewModel viewModel = CreatePresetPersistenceViewModel(settings, savedSettings);

            ImageSize discarded = viewModel.CreateNewImageSizeModel("Web");
            ImageSize anotherDiscarded = viewModel.CreateNewImageSizeModel("Web");
            Assert.AreEqual(0, discarded.Id);
            Assert.AreEqual(0, anotherDiscarded.Id);
            discarded.Width = 999;
            anotherDiscarded.Name = "Discarded";
            Assert.AreEqual(0, viewModel.Sizes.Count);
            Assert.AreEqual(0, savedSettings.Count);
            Assert.AreEqual(originalJson, settings.ToJsonString());

            ImageSize first = viewModel.CreateNewImageSizeModel("Web");
            ImageSize second = viewModel.CreateNewImageSizeModel("Web");
            first.Name = "First";
            second.Name = "Second";
            viewModel.AddImageSize(first);
            expectedSizes.Add(new ImageSize(0, "First", ResizeFit.Stretch, 50, 25, ResizeUnit.Percent));

            Assert.AreEqual(0, first.Id);
            Assert.AreEqual(0, second.Id);
            AssertSavedPresetPair(savedSettings, expectedSizes, originalJson);

            savedSettings.Clear();
            viewModel.AddImageSize(second);
            expectedSizes.Add(new ImageSize(1, "Second", ResizeFit.Stretch, 50, 25, ResizeUnit.Percent));

            Assert.AreEqual(1, second.Id);
            Assert.AreSame(first, viewModel.Sizes[0]);
            Assert.AreSame(second, viewModel.Sizes[1]);
            AssertSavedPresetPair(savedSettings, expectedSizes, originalJson);
        }

        [TestMethod]
        public void IdenticalPresetUpdatesShouldEachSaveOnePair()
        {
            ImageResizerSettings settings = CreatePresetPersistenceSettings();
            string originalJson = settings.ToJsonString();
            var expectedSizes = settings.Properties.ImageresizerSizes.Value.Select(size => size.Clone()).ToList();
            var savedSettings = new List<(string Json, string FileName)>();
            ImageResizerViewModel viewModel = CreatePresetPersistenceViewModel(settings, savedSettings);
            ImageSize original = viewModel.Sizes[0];

            viewModel.UpdateImageSize(original, original.Clone());
            AssertSavedPresetPair(savedSettings, expectedSizes, originalJson);

            savedSettings.Clear();
            viewModel.UpdateImageSize(original, original.Clone());

            AssertSavedPresetPair(savedSettings, expectedSizes, originalJson);
            Assert.AreSame(original, viewModel.Sizes[0]);
            Assert.AreEqual(originalJson, settings.ToJsonString());
        }

        [TestMethod]
        public void InvalidPresetCommitsShouldNotSaveSettings()
        {
            ImageResizerSettings settings = CreatePresetPersistenceSettings();
            string originalJson = settings.ToJsonString();
            var savedSettings = new List<(string Json, string FileName)>();
            ImageResizerViewModel viewModel = CreatePresetPersistenceViewModel(settings, savedSettings);
            ImageSize original = viewModel.Sizes[0];

            Assert.ThrowsExactly<ArgumentNullException>(() => viewModel.AddImageSize((ImageSize)null));
            Assert.ThrowsExactly<ArgumentNullException>(() => viewModel.UpdateImageSize(null, original.Clone()));
            Assert.ThrowsExactly<ArgumentNullException>(() => viewModel.UpdateImageSize(original, null));
            Assert.ThrowsExactly<InvalidOperationException>(() => viewModel.DeleteImageSize(99));

            Assert.AreEqual(originalJson, settings.ToJsonString());
            Assert.AreSame(original, viewModel.Sizes[0]);
            Assert.AreEqual(4, viewModel.Sizes.Count);
            Assert.AreEqual(0, savedSettings.Count);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow("%1-custom")]
        public void IgnoredFileNameChangesShouldNotSaveSettings(string fileName)
        {
            ImageResizerSettings settings = CreatePresetPersistenceSettings();
            string originalJson = settings.ToJsonString();
            var savedSettings = new List<(string Json, string FileName)>();
            ImageResizerViewModel viewModel = CreatePresetPersistenceViewModel(settings, savedSettings);

            viewModel.FileName = fileName;

            Assert.AreEqual("%1-custom", viewModel.FileName);
            Assert.AreEqual(originalJson, settings.ToJsonString());
            Assert.AreEqual(0, savedSettings.Count);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void FileNameAndDateChoicesShouldPersistAndReconstruct(bool keepDateModified)
        {
            ImageResizerSettings settings = CreatePresetPersistenceSettings();
            settings.Properties.ImageresizerKeepDateModified.Value = !keepDateModified;
            ImageResizerSettings expectedSettings = JsonSerializer.Deserialize<ImageResizerSettings>(settings.ToJsonString());
            var savedSettings = new List<(string Json, string FileName)>();
            ImageResizerViewModel viewModel = CreatePresetPersistenceViewModel(settings, savedSettings);
            const string fileName = "%1 - %2 - %3 - %4 - %5 - %6";

            viewModel.FileName = fileName;
            expectedSettings.Properties.ImageresizerFileName.Value = fileName;

            Assert.AreEqual(1, savedSettings.Count);
            Assert.AreEqual("settings.json", savedSettings[0].FileName);
            Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(expectedSettings.ToJsonString()), JsonNode.Parse(savedSettings[0].Json)));

            viewModel.FileName = fileName;
            Assert.AreEqual(1, savedSettings.Count);

            viewModel.KeepDateModified = keepDateModified;
            expectedSettings.Properties.ImageresizerKeepDateModified.Value = keepDateModified;

            Assert.AreEqual(2, savedSettings.Count);
            Assert.AreEqual("settings.json", savedSettings[1].FileName);
            Assert.IsTrue(JsonNode.DeepEquals(JsonNode.Parse(expectedSettings.ToJsonString()), JsonNode.Parse(savedSettings[1].Json)));

            viewModel.KeepDateModified = keepDateModified;
            Assert.AreEqual(2, savedSettings.Count);

            ImageResizerSettings reloadedSettings = JsonSerializer.Deserialize<ImageResizerSettings>(savedSettings[1].Json);
            var reloadedWrites = new List<(string Json, string FileName)>();
            ImageResizerViewModel reloadedViewModel = CreatePresetPersistenceViewModel(reloadedSettings, reloadedWrites);
            Assert.AreEqual(fileName, reloadedViewModel.FileName);
            Assert.AreEqual(keepDateModified, reloadedViewModel.KeepDateModified);
            Assert.AreEqual(0, reloadedWrites.Count);
        }

        [TestMethod]
        [DataRow(ResizeFit.Fill, ResizeUnit.Centimeter)]
        [DataRow(ResizeFit.Fill, ResizeUnit.Inch)]
        [DataRow(ResizeFit.Fill, ResizeUnit.Percent)]
        [DataRow(ResizeFit.Fill, ResizeUnit.Pixel)]
        [DataRow(ResizeFit.Fit, ResizeUnit.Centimeter)]
        [DataRow(ResizeFit.Fit, ResizeUnit.Inch)]
        [DataRow(ResizeFit.Fit, ResizeUnit.Percent)]
        [DataRow(ResizeFit.Fit, ResizeUnit.Pixel)]
        [DataRow(ResizeFit.Stretch, ResizeUnit.Centimeter)]
        [DataRow(ResizeFit.Stretch, ResizeUnit.Inch)]
        [DataRow(ResizeFit.Stretch, ResizeUnit.Percent)]
        [DataRow(ResizeFit.Stretch, ResizeUnit.Pixel)]
        public void PresetCommitShouldPreserveFitUnitAndUnicodeName(ResizeFit fit, ResizeUnit unit)
        {
            ImageResizerSettings settings = CreatePresetPersistenceSettings();
            string originalJson = settings.ToJsonString();
            var expectedSizes = settings.Properties.ImageresizerSizes.Value.Take(3).Select(size => size.Clone()).ToList();
            var savedSettings = new List<(string Json, string FileName)>();
            ImageResizerViewModel viewModel = CreatePresetPersistenceViewModel(settings, savedSettings);
            ImageSize workingCopy = viewModel.CreateNewImageSizeModel("Web");
            workingCopy.Name = "Web \u6E2C\u8A66";
            workingCopy.Fit = fit;
            workingCopy.Width = 2.54;
            workingCopy.Height = 1.25;
            workingCopy.Unit = unit;
            Assert.AreEqual(0, savedSettings.Count);

            viewModel.DeleteImageSize(3);
            savedSettings.Clear();
            viewModel.AddImageSize(workingCopy);
            expectedSizes.Add(new ImageSize(4, "Web \u6E2C\u8A66", fit, 2.54, 1.25, unit));

            AssertSavedPresetPair(savedSettings, expectedSizes, originalJson);
            TestContext.WriteLine(
                "ImageResizerProducerMatrixPayloads: {0}",
                JsonSerializer.Serialize(new { SizesJson = savedSettings[0].Json, SettingsJson = savedSettings[1].Json }));
        }

        [TestMethod]
        [DataRow(1)]
        [DataRow(2)]
        public void ThrowingSettingsSaveShouldPropagateWithoutRollingBackPreset(int failingCall)
        {
            ImageResizerSettings settings = CreatePresetPersistenceSettings();
            var savedSettings = new List<(string Json, string FileName)>();
            var saveError = new System.IO.IOException("Recording settings-save override failure.");
            ImageResizerViewModel viewModel = CreatePresetPersistenceViewModel(settings, savedSettings, () =>
            {
                if (savedSettings.Count == failingCall)
                {
                    throw saveError;
                }
            });
            ImageSize original = viewModel.Sizes[0];
            ImageSize edited = original.Clone();
            edited.Name = "Edited";

            System.IO.IOException actualError = Assert.ThrowsExactly<System.IO.IOException>(() => viewModel.UpdateImageSize(original, edited));

            Assert.AreSame(saveError, actualError);
            Assert.AreSame(original, viewModel.Sizes[0]);
            Assert.AreEqual(0, original.Id);
            Assert.AreEqual("Edited", original.Name);
            Assert.AreEqual(failingCall, savedSettings.Count);
            Assert.AreEqual("sizes.json", savedSettings[0].FileName);
            if (failingCall == 2)
            {
                Assert.AreEqual("settings.json", savedSettings[1].FileName);
            }
        }

        [TestMethod]
        public void DeleteImageSizeShouldDeleteImageSizeWhenSuccessful()
        {
            // arrange
            var mockSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<ImageResizerSettings>();
            Func<string, int> sendMockIPCConfigMSG = msg => { return 0; };
            ImageResizerViewModel viewModel = new ImageResizerViewModel(mockSettingsUtils.Object, SettingsRepository<GeneralSettings>.GetInstance(_mockGeneralSettingsUtils.Object), sendMockIPCConfigMSG, (string name) => name);
            viewModel.AddImageSize("New Size");
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
            viewModel.AddImageSize("New size"); // Add: "New size 1"
            viewModel.AddImageSize("New size"); // Add: "New size 2"
            viewModel.AddImageSize("New size"); // Add: "New size 3"
            viewModel.DeleteImageSize(1); // Delete: "New size 2"
            viewModel.AddImageSize("New size"); // Add: "New Size 4"

            // Assert
            Assert.AreEqual("New size 1", viewModel.Sizes[0].Name);
            Assert.AreEqual("New size 3", viewModel.Sizes[1].Name);
            Assert.AreEqual("New size 4", viewModel.Sizes[2].Name);
        }

        [TestMethod]
        [DataRow(-2.0, 0.0)]
        [DataRow(double.NaN, 0.0)]
        [DataRow(0.0, 0.0)]
        [DataRow(2.54, 2.54)]
        [DataRow(50.0, 50.0)]
        public void UpdateWidthAndHeightShouldUpdateSizeWhenCorrectValuesAreProvided(double value, double expected)
        {
            // arrange
            ImageSize imageSize = new ImageSize()
            {
                Id = 0,
                Name = "Test",
                Fit = ResizeFit.Fit,
                Width = 30,
                Height = 40,
                Unit = ResizeUnit.Pixel,
            };

            // Act
            imageSize.Width = value;

            // Assert
            Assert.AreEqual(expected, imageSize.Width);
            Assert.AreEqual(40.0, imageSize.Height);

            // Act
            imageSize.Width = 30;
            imageSize.Height = value;

            // Assert
            Assert.AreEqual(30.0, imageSize.Width);
            Assert.AreEqual(expected, imageSize.Height);

            // Act
            imageSize.Width = 50;

            // Assert
            Assert.AreEqual(50.0, imageSize.Width);
            Assert.AreEqual(expected, imageSize.Height);

            // Act
            imageSize.Height = 50;

            // Assert
            Assert.AreEqual(50.0, imageSize.Width);
            Assert.AreEqual(50.0, imageSize.Height);
        }

        [TestMethod]
        [DataRow(ResizeFit.Fill, ResizeUnit.Centimeter, true)]
        [DataRow(ResizeFit.Fill, ResizeUnit.Inch, true)]
        [DataRow(ResizeFit.Fill, ResizeUnit.Percent, false)]
        [DataRow(ResizeFit.Fill, ResizeUnit.Pixel, true)]
        [DataRow(ResizeFit.Fit, ResizeUnit.Centimeter, true)]
        [DataRow(ResizeFit.Fit, ResizeUnit.Inch, true)]
        [DataRow(ResizeFit.Fit, ResizeUnit.Percent, false)]
        [DataRow(ResizeFit.Fit, ResizeUnit.Pixel, true)]
        [DataRow(ResizeFit.Stretch, ResizeUnit.Centimeter, true)]
        [DataRow(ResizeFit.Stretch, ResizeUnit.Inch, true)]
        [DataRow(ResizeFit.Stretch, ResizeUnit.Percent, true)]
        [DataRow(ResizeFit.Stretch, ResizeUnit.Pixel, true)]
        public void ImageSizeShouldUseHeightUnlessNonStretchPercent(ResizeFit fit, ResizeUnit unit, bool expected)
        {
            ImageSize imageSize = new ImageSize(id: 1, name: "Test", fit: fit, width: 50, height: 30, unit: unit);

            Assert.AreEqual(expected, imageSize.IsHeightUsed);
        }

        [TestMethod]
        public void ImageSizeFitAndUnitChangesShouldNotifyDependentPropertiesOnlyWhenChanged()
        {
            ImageSize imageSize = new ImageSize(id: 1, name: "Test", fit: ResizeFit.Fit, width: 50, height: 30, unit: ResizeUnit.Percent);
            var notifications = new List<string>();
            imageSize.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

            Assert.IsFalse(imageSize.IsHeightUsed);

            AssertChangeAndIdempotent(() => imageSize.Fit = ResizeFit.Stretch, nameof(ImageSize.Fit), true);
            AssertChangeAndIdempotent(() => imageSize.Unit = ResizeUnit.Pixel, nameof(ImageSize.Unit), true);
            AssertChangeAndIdempotent(() => imageSize.Fit = ResizeFit.Fit, nameof(ImageSize.Fit), true);
            AssertChangeAndIdempotent(() => imageSize.Unit = ResizeUnit.Percent, nameof(ImageSize.Unit), false);

            void AssertChangeAndIdempotent(Action change, string propertyName, bool expectedHeightUsed)
            {
                change();

                Assert.AreEqual(expectedHeightUsed, imageSize.IsHeightUsed);
                CollectionAssert.AreEquivalent(
                    new[] { propertyName, nameof(ImageSize.IsHeightUsed), nameof(ImageSize.AccessibleTextHelper) },
                    notifications);

                notifications.Clear();
                change();

                Assert.AreEqual(expectedHeightUsed, imageSize.IsHeightUsed);
                Assert.AreEqual(0, notifications.Count);
            }
        }

        [TestMethod]
        public void ImageSizeCloneEditsCanBeDiscardedWithoutChangingOriginal()
        {
            ImageSize original = new ImageSize(id: 7, name: "Original", fit: ResizeFit.Fit, width: 1024, height: 768, unit: ResizeUnit.Pixel);
            string originalJson = original.ToJsonString();
            var originalNotifications = new List<string>();
            original.PropertyChanged += (_, args) => originalNotifications.Add(args.PropertyName);
            {
                ImageSize workingCopy = original.Clone();

                Assert.AreNotSame(original, workingCopy);
                Assert.AreEqual(original.Id, workingCopy.Id);
                Assert.AreEqual(original.Name, workingCopy.Name);
                Assert.AreEqual(original.Fit, workingCopy.Fit);
                Assert.AreEqual(original.Width, workingCopy.Width);
                Assert.AreEqual(original.Height, workingCopy.Height);
                Assert.AreEqual(original.Unit, workingCopy.Unit);

                workingCopy.Name = "Edited";
                workingCopy.Fit = ResizeFit.Stretch;
                workingCopy.Width = 50;
                workingCopy.Height = 25;
                workingCopy.Unit = ResizeUnit.Percent;

                Assert.AreEqual(7, workingCopy.Id);
                Assert.AreEqual("Edited", workingCopy.Name);
                Assert.AreEqual(ResizeFit.Stretch, workingCopy.Fit);
                Assert.AreEqual(50.0, workingCopy.Width);
                Assert.AreEqual(25.0, workingCopy.Height);
                Assert.AreEqual(ResizeUnit.Percent, workingCopy.Unit);
            }

            Assert.AreEqual(7, original.Id);
            Assert.AreEqual("Original", original.Name);
            Assert.AreEqual(ResizeFit.Fit, original.Fit);
            Assert.AreEqual(1024.0, original.Width);
            Assert.AreEqual(768.0, original.Height);
            Assert.AreEqual(ResizeUnit.Pixel, original.Unit);
            Assert.AreEqual(originalJson, original.ToJsonString());
            Assert.AreEqual(0, originalNotifications.Count);
        }

        [TestMethod]
        public void ImageSizeNameShouldNotBeSetToEmptyNullOrWhitespace()
        {
            // arrange
            ImageSize imageSize = new ImageSize()
            {
                Id = 0,
                Name = "Original Name",
                Fit = ResizeFit.Fit,
                Width = 100,
                Height = 100,
                Unit = ResizeUnit.Pixel,
            };

            // Act - try to set name to empty string
            imageSize.Name = string.Empty;

            // Assert - name should remain unchanged
            Assert.AreEqual("Original Name", imageSize.Name);

            // Act - try to set name to null
            imageSize.Name = null;

            // Assert - name should remain unchanged
            Assert.AreEqual("Original Name", imageSize.Name);

            // Act - try to set name to whitespace only
            imageSize.Name = "   ";

            // Assert - name should remain unchanged
            Assert.AreEqual("Original Name", imageSize.Name);

            // Act - set name to valid value
            imageSize.Name = "New Valid Name";

            // Assert - name should be updated
            Assert.AreEqual("New Valid Name", imageSize.Name);
        }

        [TestMethod]
        public void ImageSizeNameShouldNotBeNullAfterConstructionWithEmptyOrMissingName()
        {
            // Arrange & Act - construct with default (empty) name, simulating deserialization of legacy settings
            ImageSize defaultSize = new ImageSize();

            // Assert - name should be non-null empty string, not null (prevents NullReferenceException in callers)
            Assert.IsNotNull(defaultSize.Name);

            // Arrange & Act - construct with explicit empty name
            ImageSize emptyNameSize = new ImageSize(id: 1, name: string.Empty);

            // Assert - name should remain as the initialized default, not null
            Assert.IsNotNull(emptyNameSize.Name);

            // Arrange & Act - construct with explicit null name
            ImageSize nullNameSize = new ImageSize(id: 2, name: null);

            // Assert - name should remain as the initialized default, not null
            Assert.IsNotNull(nullNameSize.Name);

            // Verify that StartsWith can be called without NullReferenceException
            Assert.IsFalse(defaultSize.Name.StartsWith("Custom", StringComparison.InvariantCulture));
            Assert.IsFalse(emptyNameSize.Name.StartsWith("Custom", StringComparison.InvariantCulture));
            Assert.IsFalse(nullNameSize.Name.StartsWith("Custom", StringComparison.InvariantCulture));
        }

        private static ImageResizerSettings CreatePresetPersistenceSettings()
        {
            var settings = new ImageResizerSettings();
            settings.Properties.ImageresizerSizes = new ImageResizerSizes(new ObservableCollection<ImageSize>
            {
                new ImageSize(0, "Small", ResizeFit.Fit, 854, 480, ResizeUnit.Pixel),
                new ImageSize(1, "Medium", ResizeFit.Fit, 1366, 768, ResizeUnit.Pixel),
                new ImageSize(2, "Large", ResizeFit.Fit, 1920, 1080, ResizeUnit.Pixel),
                new ImageSize(3, "Phone", ResizeFit.Fit, 320, 568, ResizeUnit.Pixel),
            });
            settings.Properties.ImageresizerSelectedSizeIndex.Value = 2;
            settings.Properties.ImageresizerFileName.Value = "%1-custom";
            settings.Properties.ImageresizerKeepDateModified.Value = true;
            settings.Properties.ImageresizerCustomSize.Value = new ImageSize(4, "Custom", ResizeFit.Stretch, 50, 25, ResizeUnit.Percent);
            return settings;
        }

        private static ImageResizerViewModel CreatePresetPersistenceViewModel(
            ImageResizerSettings settings,
            List<(string Json, string FileName)> savedSettings,
            Action onSave = null)
        {
            var settingsUtils = new Mock<SettingsUtils>(new MockFileSystem(), null);
            settingsUtils.Setup(utils => utils.GetSettings<ImageResizerSettings>(ImageResizerSettings.ModuleName, "settings.json")).Returns(settings);
            settingsUtils.Setup(utils => utils.SaveSettings(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, string>((json, module, fileName) =>
                {
                    Assert.AreEqual(ImageResizerSettings.ModuleName, module);
                    savedSettings.Add((json, fileName));
                    onSave?.Invoke();
                });
            var generalSettings = new Mock<ISettingsRepository<GeneralSettings>>();
            generalSettings.SetupGet(repository => repository.SettingsConfig).Returns(new GeneralSettings());
            return new ImageResizerViewModel(
                settingsUtils.Object,
                generalSettings.Object,
                _ => throw new AssertFailedException("Preset edits must not send general-settings IPC."),
                resourceName => resourceName);
        }

        private static void AssertSavedPresetPair(
            IReadOnlyList<(string Json, string FileName)> savedSettings,
            IReadOnlyCollection<ImageSize> expectedSizes,
            string originalSettingsJson)
        {
            Assert.AreEqual(2, savedSettings.Count);
            Assert.AreEqual("sizes.json", savedSettings[0].FileName);
            Assert.AreEqual("settings.json", savedSettings[1].FileName);

            ImageResizerSizes sidecar = JsonSerializer.Deserialize<ImageResizerSizes>(savedSettings[0].Json);
            ImageResizerSettings fullSettings = JsonSerializer.Deserialize<ImageResizerSettings>(savedSettings[1].Json);
            string[] expected = expectedSizes.Select(size => size.ToJsonString()).ToArray();
            CollectionAssert.AreEqual(expected, sidecar.Value.Select(size => size.ToJsonString()).ToArray());
            CollectionAssert.AreEqual(expected, fullSettings.Properties.ImageresizerSizes.Value.Select(size => size.ToJsonString()).ToArray());

            JsonObject originalProperties = JsonNode.Parse(originalSettingsJson)["properties"].AsObject();
            JsonObject savedProperties = JsonNode.Parse(savedSettings[1].Json)["properties"].AsObject();
            foreach (var property in originalProperties)
            {
                if (property.Key != "imageresizer_sizes")
                {
                    Assert.IsTrue(JsonNode.DeepEquals(property.Value, savedProperties[property.Key]), property.Key);
                }
            }
        }
    }
}
