#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

using ImageResizer.Helpers;
using ImageResizer.Models;
using ImageResizer.Test;
using ManagedCommon;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageResizer.Properties
{
    [TestClass]
    [DoNotParallelize]
    public sealed class SettingsTests : IDisposable
    {
        private static readonly Func<string, string> _getString = key => key;
        private static readonly string[] _defaultPresetNames = { "Small", "Medium", "Large", "Phone" };
        private static readonly string[] _phoneToWebPresetNames = { "Small", "Medium", "Large", "Web" };
        private static readonly int[] _phoneToWebPresetIds = { 0, 1, 2, 4 };
        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        private static Func<string, string> _originalGetString;
        private readonly string _originalSettingsPath;
        private readonly TestDirectory _directory;
        private readonly string _settingsPath;

        public SettingsTests()
        {
            _originalSettingsPath = Settings.SettingsPath;
            _directory = new TestDirectory();
            _settingsPath = Path.Combine(_directory, "settings.json");
            Settings.SettingsPath = _settingsPath;
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _originalGetString = ResourceLoaderInstance.GetString;
            ResourceLoaderInstance.GetString = _getString;
        }

        [TestMethod]
        public void AllSizesPropagatesSizesCollectionEvents()
        {
            var settings = new Settings
            {
                CustomSize = new CustomSize(),
            };

            settings.Sizes.Clear();
            var ncc = (INotifyCollectionChanged)settings.AllSizes;

            var result = AssertEx.Raises<NotifyCollectionChangedEventArgs>(
                h => ncc.CollectionChanged += h,
                h => ncc.CollectionChanged -= h,
                () => settings.Sizes.Add(new ResizeSize()));

            Assert.AreEqual(NotifyCollectionChangedAction.Add, result.Arguments.Action);
        }

        [TestMethod]
        public void SelectedSizeSetterSelectsContainedPreset()
        {
            var settings = new Settings
            {
                SelectedSizeIndex = 0,
            };
            var expectedPreset = settings.Sizes[1];

            settings.SelectedSize = expectedPreset;

            Assert.AreEqual(1, settings.SelectedSizeIndex);
            Assert.AreSame(expectedPreset, settings.SelectedSize);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void SelectedSizeSetterSelectsCustomForAbsentSize(bool useNullSize)
        {
            var settings = new Settings
            {
                SelectedSizeIndex = 0,
            };
            var expectedCustomSize = settings.CustomSize;
            var expectedCustomIndex = settings.Sizes.Count;
            var requestedSize = useNullSize ? null : new ResizeSize();
            Assert.IsFalse(settings.Sizes.Contains(requestedSize));

            settings.SelectedSize = requestedSize;

            Assert.AreEqual(expectedCustomIndex, settings.SelectedSizeIndex);
            Assert.AreSame(expectedCustomSize, settings.SelectedSize);
        }

        [TestMethod]
        public void AllSizesPropagatesSizesPropertyEvents()
        {
            var settings = new Settings
            {
                CustomSize = new CustomSize(),
            };

            settings.Sizes.Clear();

            var result = false;
            ((INotifyPropertyChanged)settings.AllSizes).PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == "Item[]")
                {
                    result = true;
                }
            };

            settings.Sizes.Add(new ResizeSize());

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void AllSizesContainsSizes()
        {
            var settings = new Settings
            {
                CustomSize = new CustomSize(),
            };

            settings.Sizes.Add(new ResizeSize());
            CollectionAssert.Contains(settings.AllSizes.ToList(), settings.Sizes[0]);
        }

        [TestMethod]
        public void AllSizesContainsCustomSize()
        {
            var settings = new Settings
            {
                CustomSize = new CustomSize(),
            };
            settings.Sizes.Clear();

            CollectionAssert.Contains(settings.AllSizes.ToList(), settings.CustomSize);
        }

        [TestMethod]
        public void AllSizesReflectsPhoneRemovalWebAdditionAndClear()
        {
            var settings = new Settings();
            var expected = settings.AllSizes.ToList();
            CollectionAssert.AreEqual(
                _defaultPresetNames,
                expected.Take(4).Select(size => size.Name).ToArray());
            Assert.AreSame(settings.CustomSize, expected[4]);
            var phone = settings.Sizes[3];
            var web = new ResizeSize(4, "Web", ResizeFit.Fit, 1024, 768, ResizeUnit.Pixel);
            var changes = new List<NotifyCollectionChangedEventArgs>();
            var properties = new List<string>();
            ((INotifyCollectionChanged)settings.AllSizes).CollectionChanged += (sender, e) => changes.Add(e);
            ((INotifyPropertyChanged)settings.AllSizes).PropertyChanged += (sender, e) => properties.Add(e.PropertyName);

            settings.Sizes.RemoveAt(3);
            expected.RemoveAt(3);
            CollectionAssert.AreEqual(expected, settings.AllSizes.ToList());
            Assert.AreEqual(NotifyCollectionChangedAction.Remove, changes[0].Action);
            Assert.AreSame(phone, changes[0].OldItems[0]);
            Assert.AreEqual(3, changes[0].OldStartingIndex);

            settings.Sizes.Add(web);
            expected.Insert(3, web);
            CollectionAssert.AreEqual(expected, settings.AllSizes.ToList());
            Assert.AreEqual(NotifyCollectionChangedAction.Add, changes[1].Action);
            Assert.AreSame(web, changes[1].NewItems[0]);
            Assert.AreEqual(3, changes[1].NewStartingIndex);
            Assert.AreEqual(2, properties.Count(name => name == "Count"));
            Assert.AreEqual(2, properties.Count(name => name == "Item[]"));

            settings.Sizes.Clear();

            CollectionAssert.AreEqual(expected.Skip(4).ToList(), settings.AllSizes.ToList());
            Assert.AreSame(settings.CustomSize, settings.AllSizes.First());
            CollectionAssert.AreEqual(
                new[] { NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Add, NotifyCollectionChangedAction.Reset },
                changes.Select(change => change.Action).ToArray());
            Assert.AreEqual(3, properties.Count(name => name == "Count"));
            Assert.AreEqual(3, properties.Count(name => name == "Item[]"));
        }

        [TestMethod]
        public void AllSizesForwardsPresetReplacement()
        {
            var settings = new Settings();
            var allSizes = settings.AllSizes;
            var originalPresets = settings.Sizes.ToArray();
            var originalView = allSizes.ToList();
            var replacement = new ResizeSize(4, "Web", ResizeFit.Fit, 1024, 768, ResizeUnit.Pixel);
            var expected = new[] { originalPresets[0], replacement, originalPresets[2], originalPresets[3] }
                .Concat(originalView.Skip(originalPresets.Length)).ToList();
            var sourceChanges = new List<NotifyCollectionChangedEventArgs>();
            var changes = new List<NotifyCollectionChangedEventArgs>();
            var collectionSenders = new List<object>();
            var sourceProperties = new List<PropertyChangedEventArgs>();
            var properties = new List<PropertyChangedEventArgs>();
            var propertySenders = new List<object>();
            var chronology = new List<string>();
            settings.Sizes.CollectionChanged += (sender, e) => sourceChanges.Add(e);
            ((INotifyPropertyChanged)settings.Sizes).PropertyChanged += (sender, e) => sourceProperties.Add(e);
            ((INotifyCollectionChanged)allSizes).CollectionChanged += (sender, e) =>
            {
                changes.Add(e);
                collectionSenders.Add(sender);
                chronology.Add("collection");
            };
            ((INotifyPropertyChanged)allSizes).PropertyChanged += (sender, e) =>
            {
                properties.Add(e);
                propertySenders.Add(sender);
                chronology.Add("property");
            };

            settings.Sizes[1] = replacement;

            Assert.AreEqual(1, sourceChanges.Count);
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(1, collectionSenders.Count);
            Assert.AreSame(allSizes, collectionSenders[0]);
            Assert.AreSame(sourceChanges[0], changes[0]);
            Assert.AreEqual(NotifyCollectionChangedAction.Replace, changes[0].Action);
            Assert.AreEqual(1, changes[0].OldItems.Count);
            Assert.AreEqual(1, changes[0].NewItems.Count);
            Assert.AreSame(originalPresets[1], changes[0].OldItems[0]);
            Assert.AreSame(replacement, changes[0].NewItems[0]);
            Assert.AreEqual(1, changes[0].OldStartingIndex);
            Assert.AreEqual(1, changes[0].NewStartingIndex);
            Assert.AreEqual(1, sourceProperties.Count);
            Assert.AreEqual(1, properties.Count);
            Assert.AreEqual(1, propertySenders.Count);
            Assert.AreSame(allSizes, propertySenders[0]);
            Assert.AreSame(sourceProperties[0], properties[0]);
            Assert.AreEqual("Item[]", properties[0].PropertyName);
            Assert.AreEqual(2, chronology.Count);
            Assert.AreEqual("property", chronology[0]);
            Assert.AreEqual("collection", chronology[1]);
            var actual = allSizes.ToList();
            Assert.AreEqual(originalView.Count, actual.Count);
            CollectionAssert.AreEqual(expected, actual);
            CollectionAssert.DoesNotContain(actual, originalPresets[1]);
        }

        [DataTestMethod]
        [DataRow(0, 3)]
        [DataRow(3, 0)]
        public void AllSizesForwardsPresetMove(int oldIndex, int newIndex)
        {
            var settings = new Settings();
            var allSizes = settings.AllSizes;
            var originalPresets = settings.Sizes.ToArray();
            var originalView = allSizes.ToList();
            var movedPreset = originalPresets[oldIndex];
            var expectedPresets = oldIndex == 0
                ? new[] { originalPresets[1], originalPresets[2], originalPresets[3], originalPresets[0] }
                : new[] { originalPresets[3], originalPresets[0], originalPresets[1], originalPresets[2] };
            var expected = expectedPresets.Concat(originalView.Skip(originalPresets.Length)).ToList();
            var sourceChanges = new List<NotifyCollectionChangedEventArgs>();
            var changes = new List<NotifyCollectionChangedEventArgs>();
            var collectionSenders = new List<object>();
            var sourceProperties = new List<PropertyChangedEventArgs>();
            var properties = new List<PropertyChangedEventArgs>();
            var propertySenders = new List<object>();
            var chronology = new List<string>();
            settings.Sizes.CollectionChanged += (sender, e) => sourceChanges.Add(e);
            ((INotifyPropertyChanged)settings.Sizes).PropertyChanged += (sender, e) => sourceProperties.Add(e);
            ((INotifyCollectionChanged)allSizes).CollectionChanged += (sender, e) =>
            {
                changes.Add(e);
                collectionSenders.Add(sender);
                chronology.Add("collection");
            };
            ((INotifyPropertyChanged)allSizes).PropertyChanged += (sender, e) =>
            {
                properties.Add(e);
                propertySenders.Add(sender);
                chronology.Add("property");
            };

            settings.Sizes.Move(oldIndex, newIndex);

            Assert.AreEqual(1, sourceChanges.Count);
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(1, collectionSenders.Count);
            Assert.AreSame(allSizes, collectionSenders[0]);
            Assert.AreSame(sourceChanges[0], changes[0]);
            Assert.AreEqual(NotifyCollectionChangedAction.Move, changes[0].Action);
            Assert.AreEqual(1, changes[0].OldItems.Count);
            Assert.AreEqual(1, changes[0].NewItems.Count);
            Assert.AreSame(movedPreset, changes[0].OldItems[0]);
            Assert.AreSame(movedPreset, changes[0].NewItems[0]);
            Assert.AreEqual(oldIndex, changes[0].OldStartingIndex);
            Assert.AreEqual(newIndex, changes[0].NewStartingIndex);
            Assert.AreEqual(1, sourceProperties.Count);
            Assert.AreEqual(1, properties.Count);
            Assert.AreEqual(1, propertySenders.Count);
            Assert.AreSame(allSizes, propertySenders[0]);
            Assert.AreSame(sourceProperties[0], properties[0]);
            Assert.AreEqual("Item[]", properties[0].PropertyName);
            Assert.AreEqual(2, chronology.Count);
            Assert.AreEqual("property", chronology[0]);
            Assert.AreEqual("collection", chronology[1]);
            var actual = allSizes.ToList();
            Assert.AreEqual(originalView.Count, actual.Count);
            CollectionAssert.AreEqual(expected, actual);
        }

        [TestMethod]
        public void AllSizesHandlesPropertyEventsForCustomSize()
        {
            var originalCustomSize = new CustomSize();
            var replacementCustomSize = new CustomSize(ResizeFit.Stretch, 500, 300, ResizeUnit.Pixel);
            var settings = new Settings
            {
                CustomSize = originalCustomSize,
            };

            settings.Sizes.Clear();
            var ncc = (INotifyCollectionChanged)settings.AllSizes;

            var changes = new List<NotifyCollectionChangedEventArgs>();
            var callbackSizes = new List<ResizeSize>();
            ncc.CollectionChanged += (sender, e) =>
            {
                changes.Add(e);
                callbackSizes.Add(settings.AllSizes.First());
            };

            settings.CustomSize = replacementCustomSize;

            // Reset is used instead of Replace to avoid ArgumentOutOfRangeException
            // when notifying changes for virtual items (CustomSize/AiSize) that exist
            // outside the bounds of the underlying _sizes collection.
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(NotifyCollectionChangedAction.Reset, changes[0].Action);
            Assert.AreEqual(1, callbackSizes.Count);
            Assert.AreSame(replacementCustomSize, callbackSizes[0]);
            Assert.AreSame(replacementCustomSize, settings.AllSizes.First());
            CollectionAssert.DoesNotContain(settings.AllSizes.ToList(), originalCustomSize);
        }

        [TestMethod]
        public void AllSizesResetsWhenSameCustomSizeIsAssigned()
        {
            var settings = new Settings();
            var currentCustomSize = settings.CustomSize;
            settings.Sizes.Clear();
            var changes = new List<NotifyCollectionChangedEventArgs>();
            var callbackSizes = new List<ResizeSize>();
            ((INotifyCollectionChanged)settings.AllSizes).CollectionChanged += (sender, e) =>
            {
                changes.Add(e);
                callbackSizes.Add(settings.AllSizes.First());
            };

            settings.CustomSize = currentCustomSize;

            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(NotifyCollectionChangedAction.Reset, changes[0].Action);
            Assert.AreEqual(1, callbackSizes.Count);
            Assert.AreSame(currentCustomSize, callbackSizes[0]);
            Assert.AreSame(currentCustomSize, settings.AllSizes.First());
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow(" ")]
        public void FileNameRejectsBlankValuesWithoutChangingStateOrNotifying(string invalidFileName)
        {
            var settings = new Settings { FileName = "retained-%1" };
            var notifications = new List<string>();
            settings.PropertyChanged += (sender, e) => notifications.Add(e.PropertyName);

            var exception = Assert.ThrowsExactly<ArgumentNullException>(() => settings.FileName = invalidFileName);

            Assert.AreEqual(nameof(Settings.FileName), exception.ParamName);
            Assert.AreEqual("retained-%1", settings.FileName);
            Assert.AreEqual(0, notifications.Count);
        }

        [TestMethod]
        public void FileNameFormatWorks()
        {
            var settings = new Settings { FileName = "{T}%1e%2s%3t%4%5%6%7" };

            var result = settings.FileNameFormat;

            Assert.AreEqual("{{T}}{0}e{1}s{2}t{3}{4}{5}%7", result);
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        public void SelectedSizeReturnsCustomSizeWhenOutOfRange(int index)
        {
            var settings = new Settings
            {
                SelectedSizeIndex = index,
                CustomSize = new CustomSize(),
            };
            settings.Sizes.Clear();

            var result = settings.SelectedSize;

            Assert.AreEqual(settings.CustomSize, result);
        }

        [TestMethod]
        public void SelectedSizeReturnsSizeWhenInRange()
        {
            var settings = new Settings
            {
                SelectedSizeIndex = 0,
            };

            settings.Sizes.Add(new ResizeSize());
            var result = settings.SelectedSize;

            Assert.AreEqual(settings.Sizes[0], result);
        }

        [TestMethod]
        public void IDataErrorInfoErrorReturnsEmpty()
        {
            var settings = new Settings();

            var result = ((IDataErrorInfo)settings).Error;

            Assert.AreEqual(result, string.Empty);
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(101)]
        public void IDataErrorInfoItemJpegQualityLevelReturnsErrorWhenOutOfRange(int value)
        {
            var settings = new Settings { JpegQualityLevel = value };

            var result = ((IDataErrorInfo)settings)["JpegQualityLevel"];

            // With test ResourceLoaderInstance, GetString returns the key itself ("ValueMustBeBetween")
            // which becomes the CompositeFormat. Format it the same way the production code does.
            var expectedFormat = CompositeFormat.Parse(ResourceLoaderInstance.GetString("ValueMustBeBetween"));
            Assert.AreEqual(
                string.Format(CultureInfo.InvariantCulture, expectedFormat, 1, 100),
                result);
        }

        [DataTestMethod]
        [DataRow(1)]
        [DataRow(100)]
        public void IDataErrorInfoItemJpegQualityLevelReturnsEmptyWhenInRange(int value)
        {
            var settings = new Settings { JpegQualityLevel = value };

            var result = ((IDataErrorInfo)settings)["JpegQualityLevel"];

            Assert.AreEqual(result, string.Empty);
        }

        [TestMethod]
        public void IDataErrorInfoItemReturnsEmptyWhenNotJpegQualityLevel()
        {
            var settings = new Settings();

            var result = ((IDataErrorInfo)settings)["Unknown"];

            Assert.AreEqual(result, string.Empty);
        }

        [TestMethod]
        public void ReloadCreatesFileWhenFileNotFound()
        {
            // Arrange
            var settings = new Settings();

            // Assert
            Assert.IsFalse(System.IO.File.Exists(Settings.SettingsPath));

            // Act
            settings.Reload();

            // Assert
            Assert.IsTrue(System.IO.File.Exists(Settings.SettingsPath));
        }

        [TestMethod]
        public void SaveCreatesFile()
        {
            // Arrange
            var settings = new Settings();

            // Assert
            Assert.IsFalse(System.IO.File.Exists(Settings.SettingsPath));

            // Act
            settings.Save();

            // Assert
            Assert.IsTrue(System.IO.File.Exists(Settings.SettingsPath));
        }

        [TestMethod]
        public void SaveJsonIsReadableByReload()
        {
            // Arrange
            var settings = new Settings();

            // Assert
            Assert.IsFalse(System.IO.File.Exists(Settings.SettingsPath));

            // Act
            settings.Save();
            settings.Reload();  // If the JSON file created by Save() is not readable this function will throw an error

            // Assert
            Assert.IsTrue(System.IO.File.Exists(Settings.SettingsPath));
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void SaveReloadConsumesWrappedOptionsFileNameAndSelection(bool keepDateModified)
        {
            const string fileName = "%1 - %2 - %3 - %4 - %5 - %6";
            var saved = new Settings
            {
                SelectedSizeIndex = 3,
                ShrinkOnly = true,
                Replace = true,
                IgnoreOrientation = false,
                RemoveMetadata = true,
                JpegQualityLevel = 73,
                PngInterlaceOption = PngInterlaceOption.On,
                TiffCompressOption = TiffCompressOption.None,
                FileName = fileName,
                KeepDateModified = keepDateModified,
                FallbackEncoder = new Guid("1b7cfaf4-713f-473c-bbcd-6137425faeaf"),
                CustomSize = new CustomSize(ResizeFit.Stretch, 500, 300, ResizeUnit.Pixel),
            };

            saved.Save();

            using var document = JsonDocument.Parse(System.IO.File.ReadAllBytes(_settingsPath));
            var root = document.RootElement;
            Assert.AreEqual("Image Resizer", root.GetProperty("name").GetString());
            Assert.AreEqual("1", root.GetProperty("version").GetString());
            var properties = root.GetProperty("properties");
            Assert.AreEqual(3, properties.GetProperty("imageresizer_selectedSizeIndex").GetProperty("value").GetInt32());
            Assert.IsTrue(properties.GetProperty("imageresizer_shrinkOnly").GetProperty("value").GetBoolean());
            Assert.IsTrue(properties.GetProperty("imageresizer_replace").GetProperty("value").GetBoolean());
            Assert.IsFalse(properties.GetProperty("imageresizer_ignoreOrientation").GetProperty("value").GetBoolean());
            Assert.IsTrue(properties.GetProperty("imageresizer_removeMetadata").GetProperty("value").GetBoolean());
            Assert.AreEqual(73, properties.GetProperty("imageresizer_jpegQualityLevel").GetProperty("value").GetInt32());
            Assert.AreEqual(1, properties.GetProperty("imageresizer_pngInterlaceOption").GetProperty("value").GetInt32());
            Assert.AreEqual(1, properties.GetProperty("imageresizer_tiffCompressOption").GetProperty("value").GetInt32());
            Assert.AreEqual(fileName, properties.GetProperty("imageresizer_fileName").GetProperty("value").GetString());
            Assert.AreEqual(keepDateModified, properties.GetProperty("imageresizer_keepDateModified").GetProperty("value").GetBoolean());
            Assert.AreEqual(saved.FallbackEncoder, properties.GetProperty("imageresizer_fallbackEncoder").GetProperty("value").GetGuid());

            var consumed = new Settings { KeepDateModified = !keepDateModified };
            consumed.Reload();

            Assert.IsTrue(consumed.ShrinkOnly);
            Assert.IsTrue(consumed.Replace);
            Assert.IsFalse(consumed.IgnoreOrientation);
            Assert.IsTrue(consumed.RemoveMetadata);
            Assert.AreEqual(73, consumed.JpegQualityLevel);
            Assert.AreEqual(saved.PngInterlaceOption, consumed.PngInterlaceOption);
            Assert.AreEqual(saved.TiffCompressOption, consumed.TiffCompressOption);
            Assert.AreEqual(fileName, consumed.FileName);
            Assert.AreEqual("{0} - {1} - {2} - {3} - {4} - {5}", consumed.FileNameFormat);
            Assert.AreEqual(keepDateModified, consumed.KeepDateModified);
            Assert.AreEqual(saved.FallbackEncoder, consumed.FallbackEncoder);
            Assert.AreEqual(3, consumed.SelectedSizeIndex);
            Assert.AreSame(consumed.Sizes[3], consumed.SelectedSize);
            Assert.AreEqual("Phone", consumed.SelectedSize.Name);
            Assert.AreEqual(ResizeFit.Stretch, consumed.CustomSize.Fit);
            Assert.AreEqual(500d, consumed.CustomSize.Width);
            Assert.AreEqual(300d, consumed.CustomSize.Height);
            Assert.AreEqual(ResizeUnit.Pixel, consumed.CustomSize.Unit);
        }

        [TestMethod]
        public void ReloadConsumesPhoneToWebEditWhileRetainingActiveSelection()
        {
            var settings = new Settings();
            settings.Save();
            settings.Reload();
            settings.SelectedSizeIndex = 2;
            var notifications = new List<string>();
            settings.PropertyChanged += (sender, e) => notifications.Add(e.PropertyName);
            var allSizes = settings.AllSizes;
            var edited = new Settings();
            edited.Sizes.RemoveAt(3);
            edited.Sizes.Add(new ResizeSize(4, "Web", ResizeFit.Fit, 1024, 768, ResizeUnit.Pixel));
            edited.Save();

            using var document = JsonDocument.Parse(System.IO.File.ReadAllBytes(_settingsPath));
            var presets = document.RootElement.GetProperty("properties").GetProperty("imageresizer_sizes").GetProperty("value");
            CollectionAssert.AreEqual(
                _phoneToWebPresetNames,
                presets.EnumerateArray().Select(size => size.GetProperty("name").GetString()).ToArray());
            var web = presets[3];
            Assert.AreEqual(4, web.GetProperty("Id").GetInt32());
            Assert.AreEqual(1, web.GetProperty("fit").GetInt32());
            Assert.AreEqual(1024d, web.GetProperty("width").GetDouble());
            Assert.AreEqual(768d, web.GetProperty("height").GetDouble());
            Assert.AreEqual(3, web.GetProperty("unit").GetInt32());

            settings.Reload();

            Assert.AreSame(allSizes, settings.AllSizes);
            CollectionAssert.AreEqual(
                _phoneToWebPresetNames,
                allSizes.Take(4).Select(size => size.Name).ToArray());
            CollectionAssert.AreEqual(_phoneToWebPresetIds, settings.Sizes.Select(size => size.Id).ToArray());
            Assert.IsFalse(allSizes.Any(size => size.Name == "Phone"));
            Assert.AreSame(settings.CustomSize, allSizes.ElementAt(4));
            Assert.AreEqual(ResizeFit.Fit, settings.Sizes[3].Fit);
            Assert.AreEqual(1024d, settings.Sizes[3].Width);
            Assert.AreEqual(768d, settings.Sizes[3].Height);
            Assert.AreEqual(ResizeUnit.Pixel, settings.Sizes[3].Unit);
            Assert.AreEqual(2, settings.SelectedSizeIndex);
            Assert.AreSame(settings.Sizes[2], settings.SelectedSize);
            Assert.AreEqual(2, settings.SelectedSize.Id);
            CollectionAssert.Contains(notifications, nameof(Settings.SelectedSizeIndex));
            CollectionAssert.Contains(notifications, nameof(Settings.SelectedSize));
        }

        [DataTestMethod]
        [DataRow(false, 2)]
        [DataRow(true, 0)]
        public void ReloadRetainsActivePresetIdWhenItsIndexChanges(bool reorder, int expectedIndex)
        {
            var settings = new Settings();
            settings.Save();
            settings.Reload();
            settings.SelectedSizeIndex = 3;
            var edited = new Settings();
            if (reorder)
            {
                edited.Sizes.Move(3, 0);
            }
            else
            {
                edited.Sizes.RemoveAt(0);
            }

            edited.Save();

            settings.Reload();

            Assert.AreEqual(expectedIndex, settings.SelectedSizeIndex);
            Assert.AreSame(settings.Sizes[expectedIndex], settings.SelectedSize);
            Assert.AreEqual(3, settings.SelectedSize.Id);
            Assert.AreEqual("Phone", settings.SelectedSize.Name);
            CollectionAssert.AreEqual(
                edited.Sizes.Select(size => size.Id).ToArray(),
                settings.AllSizes.Take(edited.Sizes.Count).Select(size => size.Id).ToArray());
            Assert.AreSame(settings.CustomSize, settings.AllSizes.ElementAt(edited.Sizes.Count));
        }

        [TestMethod]
        public void ReloadRaisesPropertyChanged()
        {
            // Arrange
            var settings = new Settings();
            settings.SelectedSizeIndex = 2;
            settings.Save();

            // Reset to default so Reload will trigger a real change
            settings.SelectedSizeIndex = 0;

            var shrinkOnlyChanged = false;
            var replaceChanged = false;
            var ignoreOrientationChanged = false;
            var jpegQualityLevelChanged = false;
            var pngInterlaceOptionChanged = false;
            var tiffCompressOptionChanged = false;
            var fileNameChanged = false;
            var keepDateModifiedChanged = false;
            var fallbackEncoderChanged = false;
            var customSizeChanged = false;
            var selectedSizeIndexChanged = false;

            settings.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == "ShrinkOnly")
                {
                    shrinkOnlyChanged = true;
                }
                else if (e.PropertyName == "Replace")
                {
                    replaceChanged = true;
                }
                else if (e.PropertyName == "IgnoreOrientation")
                {
                    ignoreOrientationChanged = true;
                }
                else if (e.PropertyName == "JpegQualityLevel")
                {
                    jpegQualityLevelChanged = true;
                }
                else if (e.PropertyName == "PngInterlaceOption")
                {
                    pngInterlaceOptionChanged = true;
                }
                else if (e.PropertyName == "TiffCompressOption")
                {
                    tiffCompressOptionChanged = true;
                }
                else if (e.PropertyName == "FileName")
                {
                    fileNameChanged = true;
                }
                else if (e.PropertyName == "KeepDateModified")
                {
                    keepDateModifiedChanged = true;
                }
                else if (e.PropertyName == "FallbackEncoder")
                {
                    fallbackEncoderChanged = true;
                }
                else if (e.PropertyName == "CustomSize")
                {
                    customSizeChanged = true;
                }
                else if (e.PropertyName == "SelectedSizeIndex")
                {
                    selectedSizeIndexChanged = true;
                }
            };

            // Act
            settings.Reload();

            // Assert
            Assert.IsTrue(shrinkOnlyChanged);
            Assert.IsTrue(replaceChanged);
            Assert.IsTrue(ignoreOrientationChanged);
            Assert.IsTrue(jpegQualityLevelChanged);
            Assert.IsTrue(pngInterlaceOptionChanged);
            Assert.IsTrue(tiffCompressOptionChanged);
            Assert.IsTrue(fileNameChanged);
            Assert.IsTrue(keepDateModifiedChanged);
            Assert.IsTrue(fallbackEncoderChanged);
            Assert.IsTrue(customSizeChanged);
            Assert.IsTrue(selectedSizeIndexChanged);
        }

        [TestMethod]
        public void SystemTextJsonDeserializesCorrectly()
        {
            // Generated Settings file in 0.72
            var defaultInput =
                "{\r\n  \"properties\": {\r\n    \"imageresizer_selectedSizeIndex\": {\r\n      \"value\": 1\r\n    },\r\n    \"imageresizer_shrinkOnly\": {\r\n      \"value\": true\r\n    },\r\n    \"imageresizer_replace\": {\r\n      \"value\": true\r\n    },\r\n    \"imageresizer_ignoreOrientation\": {\r\n      \"value\": false\r\n    },\r\n    \"imageresizer_jpegQualityLevel\": {\r\n      \"value\": 91\r\n    },\r\n    \"imageresizer_pngInterlaceOption\": {\r\n      \"value\": 1\r\n    },\r\n    \"imageresizer_tiffCompressOption\": {\r\n      \"value\": 1\r\n    },\r\n    \"imageresizer_fileName\": {\r\n      \"value\": \"%1 %1 (%2)\"\r\n    },\r\n    \"imageresizer_sizes\": {\r\n      \"value\": [\r\n        {\r\n          \"Id\": 0,\r\n          \"ExtraBoxOpacity\": 100,\r\n          \"EnableEtraBoxes\": true,\r\n          \"name\": \"Small-NotDefault\",\r\n          \"fit\": 1,\r\n          \"width\": 854,\r\n          \"height\": 480,\r\n          \"unit\": 3\r\n        },\r\n        {\r\n          \"Id\": 3,\r\n          \"ExtraBoxOpacity\": 100,\r\n          \"EnableEtraBoxes\": true,\r\n          \"name\": \"Phone\",\r\n          \"fit\": 1,\r\n          \"width\": 320,\r\n          \"height\": 568,\r\n          \"unit\": 3\r\n        }\r\n      ]\r\n    },\r\n    \"imageresizer_keepDateModified\": {\r\n      \"value\": false\r\n    },\r\n    \"imageresizer_fallbackEncoder\": {\r\n      \"value\": \"19e4a5aa-5662-4fc5-a0c0-1758028e1057\"\r\n    },\r\n    \"imageresizer_customSize\": {\r\n      \"value\": {\r\n        \"Id\": 4,\r\n        \"ExtraBoxOpacity\": 100,\r\n        \"EnableEtraBoxes\": true,\r\n        \"name\": \"custom\",\r\n        \"fit\": 1,\r\n        \"width\": 1024,\r\n        \"height\": 640,\r\n        \"unit\": 3\r\n      }\r\n    }\r\n  },\r\n  \"name\": \"Image Resizer\",\r\n  \"version\": \"1\"\r\n}";

            // Execute readFile/writefile twice and see if serialized string is still correct
            var resultWrapper = JsonSerializer.Deserialize<SettingsWrapper>(defaultInput);
            var serializedInput = JsonSerializer.Serialize(resultWrapper, _serializerOptions);
            var resultWrapper2 = JsonSerializer.Deserialize<SettingsWrapper>(serializedInput);
            var serializedInput2 = JsonSerializer.Serialize(resultWrapper2, _serializerOptions);

            Assert.AreEqual(serializedInput, serializedInput2);
            Assert.AreEqual("Image Resizer", resultWrapper2.Name);
            Assert.AreEqual("1", resultWrapper2.Version);
            Assert.IsNotNull(resultWrapper2.Properties);
            Assert.IsTrue(resultWrapper2.Properties.ShrinkOnly);
            Assert.IsTrue(resultWrapper2.Properties.Replace);
            Assert.AreEqual(91, resultWrapper2.Properties.JpegQualityLevel);
            Assert.AreEqual(1, (int)resultWrapper2.Properties.PngInterlaceOption);
            Assert.AreEqual(1, (int)resultWrapper2.Properties.TiffCompressOption);
            Assert.AreEqual("%1 %1 (%2)", resultWrapper2.Properties.FileName);
            Assert.AreEqual(2, resultWrapper2.Properties.Sizes.Count);
            Assert.IsFalse(resultWrapper2.Properties.KeepDateModified);
            Assert.AreEqual("Small-NotDefault", resultWrapper2.Properties.Sizes[0].Name);
        }

        [TestMethod]
        public void ReloadPreservesSelectionByIdWhenPresetEdited()
        {
            var settings = new Settings();
            settings.Sizes.Clear();
            settings.Sizes.Add(new ResizeSize(0, "Small", ResizeFit.Fit, 640, 480, ResizeUnit.Pixel));
            settings.Sizes.Add(new ResizeSize(1, "Medium", ResizeFit.Fit, 1280, 720, ResizeUnit.Pixel));
            settings.SelectedSizeIndex = 1; // Select "Medium"
            settings.Save();

            var json = System.IO.File.ReadAllText(Settings.SettingsPath);
            json = json.Replace("\"Medium\"", "\"Medium-Edited\"");
            System.IO.File.WriteAllText(Settings.SettingsPath, json);

            settings.Reload();

            Assert.AreEqual(1, settings.SelectedSizeIndex, "Selection should be preserved after name edit.");
            Assert.AreEqual("Medium-Edited", settings.SelectedSize.Name, "Selected size name should reflect edited name.");
        }

        [TestMethod]
        public void ReloadFallsBackWhenSelectedPresetIsDeleted()
        {
            var settings = new Settings();
            settings.Sizes.Clear();
            settings.Sizes.Add(new ResizeSize(0, "Small", ResizeFit.Fit, 640, 480, ResizeUnit.Pixel));
            settings.Sizes.Add(new ResizeSize(1, "Medium", ResizeFit.Fit, 1280, 720, ResizeUnit.Pixel));
            settings.SelectedSizeIndex = 1; // Select "Medium"
            settings.Save();

            // Remove the "Medium" preset from the saved JSON.
            var json = System.IO.File.ReadAllText(Settings.SettingsPath);
            var wrapper = JsonSerializer.Deserialize<SettingsWrapper>(json);
            wrapper.Properties.Sizes.RemoveAt(1);
            wrapper.Properties.SelectedSizeIndex = 0; // Simulate fall back to the first preset.
            json = JsonSerializer.Serialize(wrapper, _serializerOptions);
            System.IO.File.WriteAllText(Settings.SettingsPath, json);

            settings.Reload();

            Assert.AreEqual(0, settings.SelectedSizeIndex, "Selection should fall back to first preset after deletion.");
        }

        /// <summary>
        /// Verifies the dynamic fallback path (_isFirstLoad == false): when the currently-selected
        /// preset is deleted by an external save while the app is running, a subsequent reload
        /// should fall back to CustomSize rather than using the stale index from the file.
        /// </summary>
        [TestMethod]
        public void ReloadFallsBackToCustomSizeWhenSelectedPresetDeletedDuringSession()
        {
            // Arrange: perform the first load to transition _isFirstLoad → false.
            var settings = new Settings();
            settings.Sizes.Clear();
            settings.Sizes.Add(new ResizeSize(0, "Small", ResizeFit.Fit, 640, 480, ResizeUnit.Pixel));
            settings.Sizes.Add(new ResizeSize(1, "Medium", ResizeFit.Fit, 1280, 720, ResizeUnit.Pixel));
            settings.Save();
            settings.Reload(); // first load: _isFirstLoad true → false

            // Select "Medium" after first load (simulates user interaction).
            settings.SelectedSizeIndex = 1;

            // Simulate an external save that removes the "Medium" preset.
            var json = System.IO.File.ReadAllText(Settings.SettingsPath);
            var wrapper = JsonSerializer.Deserialize<SettingsWrapper>(json);
            wrapper.Properties.Sizes.RemoveAt(1);
            System.IO.File.WriteAllText(Settings.SettingsPath, JsonSerializer.Serialize(wrapper, _serializerOptions));

            // Act: second reload (_isFirstLoad == false) – ID-based matching path.
            settings.Reload();

            // Assert: id=1 (Medium) no longer exists → fall back to CustomSize.
            Assert.AreEqual(
                settings.Sizes.Count,
                settings.SelectedSizeIndex,
                "Should fall back to CustomSize when the selected preset is deleted during an active session.");
            Assert.IsInstanceOfType<CustomSize>(
                settings.SelectedSize,
                "SelectedSize should be CustomSize after the selected preset is deleted.");
        }

        [TestMethod]
        public void ReloadPreservesCustomSizeSelection()
        {
            var settings = new Settings();
            settings.SelectedSizeIndex = settings.Sizes.Count;  // Select Custom Size
            settings.Save();

            // Modify a different setting in the saved file.
            var json = System.IO.File.ReadAllText(Settings.SettingsPath);
            var wrapper = JsonSerializer.Deserialize<SettingsWrapper>(json);
            wrapper.Properties.ShrinkOnly = false;
            json = JsonSerializer.Serialize(wrapper, _serializerOptions);
            System.IO.File.WriteAllText(Settings.SettingsPath, json);

            settings.Reload();

            Assert.AreEqual(settings.Sizes.Count, settings.SelectedSizeIndex, "Custom size selection should be preserved after reload.");
            Assert.IsInstanceOfType<CustomSize>(settings.SelectedSize, "Selected size should be of type CustomSize.");
        }

        [TestMethod]
        public void ReloadSelectsLoadedCustomWhenAllPresetsRemovedDuringSession()
        {
            Assert.AreEqual(AiAvailabilityState.NotSupported, App.AiAvailabilityState);
            var settings = new Settings();
            settings.Save();
            settings.Reload();
            settings.SelectedSizeIndex = 1;
            var selectedPreset = settings.Sizes[1];
            Assert.AreSame(selectedPreset, settings.SelectedSize);
            var originalCustomSize = settings.CustomSize;

            var wrapper = JsonSerializer.Deserialize<SettingsWrapper>(System.IO.File.ReadAllText(_settingsPath));
            wrapper.Properties.Sizes.Clear();
            wrapper.Properties.CustomSize = new CustomSize(ResizeFit.Stretch, 500, 300, ResizeUnit.Pixel);
            wrapper.Properties.SelectedSizeIndex = 1;
            System.IO.File.WriteAllText(_settingsPath, JsonSerializer.Serialize(wrapper, _serializerOptions));

            settings.Reload();

            Assert.AreEqual(0, settings.Sizes.Count);
            Assert.AreEqual(0, settings.SelectedSizeIndex);
            Assert.AreSame(settings.CustomSize, settings.SelectedSize);
            Assert.AreNotSame(originalCustomSize, settings.CustomSize);
            Assert.AreNotSame(selectedPreset, settings.SelectedSize);
            Assert.AreEqual(ResizeFit.Stretch, settings.SelectedSize.Fit);
            Assert.AreEqual(500d, settings.SelectedSize.Width);
            Assert.AreEqual(300d, settings.SelectedSize.Height);
            Assert.AreEqual(ResizeUnit.Pixel, settings.SelectedSize.Unit);
            var allSizes = settings.AllSizes.ToList();
            Assert.AreEqual(1, allSizes.Count);
            Assert.AreSame(settings.CustomSize, allSizes[0]);
        }

        [TestMethod]
        public void ReloadRetainsActiveCustomSelectionWhenPresetCountChanges()
        {
            var settings = new Settings();
            settings.Save();
            settings.Reload();
            var oldPresetCount = settings.Sizes.Count;
            var oldAllSizesCount = settings.AllSizes.Count();
            var originalCustomSize = settings.CustomSize;
            settings.SelectedSizeIndex = oldPresetCount;
            Assert.AreSame(originalCustomSize, settings.SelectedSize);
            var removedPreset = settings.Sizes.Last();
            var remainingIds = settings.Sizes.Take(oldPresetCount - 1).Select(size => size.Id).ToArray();
            var remainingNames = settings.Sizes.Take(oldPresetCount - 1).Select(size => size.Name).ToArray();

            var wrapper = JsonSerializer.Deserialize<SettingsWrapper>(System.IO.File.ReadAllText(_settingsPath));
            wrapper.Properties.Sizes.RemoveAt(oldPresetCount - 1);
            wrapper.Properties.CustomSize = new CustomSize(ResizeFit.Stretch, 500, 300, ResizeUnit.Pixel);
            wrapper.Properties.SelectedSizeIndex = 0;
            System.IO.File.WriteAllText(_settingsPath, JsonSerializer.Serialize(wrapper, _serializerOptions));

            settings.Reload();

            Assert.AreEqual(oldPresetCount - 1, settings.Sizes.Count);
            Assert.AreEqual(settings.Sizes.Count, settings.SelectedSizeIndex);
            Assert.AreNotEqual(oldPresetCount, settings.SelectedSizeIndex);
            Assert.AreNotEqual(0, settings.SelectedSizeIndex);
            Assert.AreNotSame(originalCustomSize, settings.CustomSize);
            Assert.AreSame(settings.CustomSize, settings.SelectedSize);
            Assert.AreEqual(ResizeFit.Stretch, settings.SelectedSize.Fit);
            Assert.AreEqual(500d, settings.SelectedSize.Width);
            Assert.AreEqual(300d, settings.SelectedSize.Height);
            Assert.AreEqual(ResizeUnit.Pixel, settings.SelectedSize.Unit);
            CollectionAssert.AreEqual(remainingIds, settings.Sizes.Select(size => size.Id).ToArray());
            CollectionAssert.AreEqual(remainingNames, settings.Sizes.Select(size => size.Name).ToArray());
            var expectedAllSizes = settings.Sizes.ToList();
            expectedAllSizes.Add(settings.CustomSize);
            var allSizes = settings.AllSizes.ToList();
            CollectionAssert.AreEqual(expectedAllSizes, allSizes.Take(settings.Sizes.Count + 1).ToList());
            Assert.AreEqual(oldAllSizesCount - 1, allSizes.Count);
            Assert.IsFalse(allSizes.Any(size => size.Name == removedPreset.Name));
            CollectionAssert.DoesNotContain(allSizes, originalCustomSize);
        }

        [TestMethod]
        public void ReloadHandlesInvalidSelectedSizeIndex()
        {
            var settings = new Settings();
            settings.Save();

            var json = System.IO.File.ReadAllText(Settings.SettingsPath);
            var wrapper = JsonSerializer.Deserialize<SettingsWrapper>(json);
            wrapper.Properties.SelectedSizeIndex = 999; // Invalid index
            json = JsonSerializer.Serialize(wrapper, _serializerOptions);
            System.IO.File.WriteAllText(Settings.SettingsPath, json);

            settings.Reload();

            Assert.AreEqual(settings.Sizes.Count, settings.SelectedSizeIndex, "Invalid SelectedSizeIndex should fall back to Custom Size.");
        }

        [TestMethod]
        public void ReloadHandlesNegativeSelectedSizeIndex()
        {
            var settings = new Settings();
            settings.Save();

            var json = System.IO.File.ReadAllText(Settings.SettingsPath);
            var wrapper = JsonSerializer.Deserialize<SettingsWrapper>(json);
            wrapper.Properties.SelectedSizeIndex = -5; // Invalid negative index
            json = JsonSerializer.Serialize(wrapper, _serializerOptions);
            System.IO.File.WriteAllText(Settings.SettingsPath, json);

            settings.Reload();

            Assert.AreEqual(settings.Sizes.Count, settings.SelectedSizeIndex, "Negative SelectedSizeIndex should fall back to Custom Size.");
        }

        [TestMethod]
        public void ReloadMalformedJsonRestoresDefaultsAndRetainsActiveCustomSelection()
        {
            var settings = new Settings();
            settings.Save();
            settings.Reload();
            settings.Sizes.Clear();
            settings.Sizes.Add(new ResizeSize(99, "Edited", ResizeFit.Stretch, 500, 300, ResizeUnit.Pixel));
            settings.CustomSize = new CustomSize(ResizeFit.Stretch, 500, 300, ResizeUnit.Pixel);
            settings.SelectedSizeIndex = settings.Sizes.Count;
            var originalCustomSize = settings.CustomSize;
            Assert.AreSame(originalCustomSize, settings.SelectedSize);
            settings.ShrinkOnly = true;
            settings.Replace = true;
            settings.IgnoreOrientation = false;
            settings.RemoveMetadata = true;
            settings.JpegQualityLevel = 17;
            settings.PngInterlaceOption = PngInterlaceOption.On;
            settings.TiffCompressOption = TiffCompressOption.None;
            settings.FileName = "changed-%1";
            settings.KeepDateModified = true;
            settings.FallbackEncoder = new Guid("1b7cfaf4-713f-473c-bbcd-6137425faeaf");
            Assert.IsNull(Logger.CurrentLogFile, $"Unexpected logging destination: {Logger.CurrentLogFile}");
            System.IO.File.WriteAllText(_settingsPath, "{");

            settings.Reload();

            CollectionAssert.AreEqual(_defaultPresetNames, settings.Sizes.Select(size => size.Name).ToArray());
            CollectionAssert.AreEqual(Enumerable.Range(0, 4).ToArray(), settings.Sizes.Select(size => size.Id).ToArray());
            Assert.AreEqual(4, settings.SelectedSizeIndex);
            Assert.AreSame(settings.CustomSize, settings.SelectedSize);
            Assert.AreNotSame(originalCustomSize, settings.CustomSize);
            Assert.AreEqual(ResizeFit.Fit, settings.SelectedSize.Fit);
            Assert.AreEqual(1024d, settings.SelectedSize.Width);
            Assert.AreEqual(640d, settings.SelectedSize.Height);
            Assert.AreEqual(ResizeUnit.Pixel, settings.SelectedSize.Unit);
            CollectionAssert.AreEqual(_defaultPresetNames, settings.AllSizes.Take(4).Select(size => size.Name).ToArray());
            Assert.AreSame(settings.CustomSize, settings.AllSizes.ElementAt(4));
            Assert.IsFalse(settings.ShrinkOnly);
            Assert.IsFalse(settings.Replace);
            Assert.IsTrue(settings.IgnoreOrientation);
            Assert.IsFalse(settings.RemoveMetadata);
            Assert.AreEqual(90, settings.JpegQualityLevel);
            Assert.AreEqual(PngInterlaceOption.Default, settings.PngInterlaceOption);
            Assert.AreEqual(TiffCompressOption.Default, settings.TiffCompressOption);
            Assert.AreEqual("%1 (%2)", settings.FileName);
            Assert.IsFalse(settings.KeepDateModified);
            Assert.AreEqual(new Guid("19e4a5aa-5662-4fc5-a0c0-1758028e1057"), settings.FallbackEncoder);
            Assert.IsNull(Logger.CurrentLogFile);
        }

        [DataTestMethod]
        [DataRow("null")]
        [DataRow("{\"properties\":null}")]
        public void ReloadNullSnapshotThrowsWithoutChangingLoadedState(string json)
        {
            var settings = new Settings
            {
                ShrinkOnly = true,
                JpegQualityLevel = 73,
                FileName = "retained-%1",
                CustomSize = new CustomSize(ResizeFit.Stretch, 500, 300, ResizeUnit.Pixel),
            };
            settings.Save();
            settings.Reload();
            settings.SelectedSizeIndex = 1;
            var originalSizes = settings.Sizes;
            var originalPresets = settings.Sizes.ToList();
            var originalCustomSize = settings.CustomSize;
            var originalSelectedSize = settings.SelectedSize;
            var originalAllSizes = settings.AllSizes;
            var originalAllSizesItems = settings.AllSizes.ToList();
            var notifications = new List<string>();
            settings.PropertyChanged += (sender, e) => notifications.Add(e.PropertyName);
            System.IO.File.WriteAllText(_settingsPath, json);
            var originalBytes = System.IO.File.ReadAllBytes(_settingsPath);

            Assert.ThrowsExactly<NullReferenceException>(() => settings.Reload());

            Assert.AreSame(originalSizes, settings.Sizes);
            CollectionAssert.AreEqual(originalPresets, settings.Sizes.ToList());
            Assert.AreSame(originalCustomSize, settings.CustomSize);
            Assert.AreEqual(ResizeFit.Stretch, settings.CustomSize.Fit);
            Assert.AreEqual(500d, settings.CustomSize.Width);
            Assert.AreEqual(300d, settings.CustomSize.Height);
            Assert.AreEqual(ResizeUnit.Pixel, settings.CustomSize.Unit);
            Assert.AreEqual(1, settings.SelectedSizeIndex);
            Assert.AreSame(originalSelectedSize, settings.SelectedSize);
            Assert.AreSame(originalAllSizes, settings.AllSizes);
            CollectionAssert.AreEqual(originalAllSizesItems, settings.AllSizes.ToList());
            Assert.IsTrue(settings.ShrinkOnly);
            Assert.AreEqual(73, settings.JpegQualityLevel);
            Assert.AreEqual("retained-%1", settings.FileName);
            Assert.AreEqual(0, notifications.Count);
            CollectionAssert.AreEqual(originalBytes, System.IO.File.ReadAllBytes(_settingsPath));
        }

        [TestMethod]
        public void IdRecoveryHelperRecoversDuplicateIds()
        {
            var sizes = new List<ResizeSize>
            {
                new(0, "Size1", ResizeFit.Fit, 800, 600, ResizeUnit.Pixel),
                new(0, "Size2", ResizeFit.Fit, 1024, 768, ResizeUnit.Pixel), // Duplicate ID
                new(2, "Size3", ResizeFit.Fit, 1920, 1080, ResizeUnit.Pixel),
            };

            IdRecoveryHelper.RecoverInvalidIds(sizes);

            Assert.AreEqual(0, sizes[0].Id, "First item should keep its original ID.");
            Assert.AreEqual(1, sizes[1].Id, "Second item should have been assigned a new unique ID.");
            Assert.AreEqual(2, sizes[2].Id, "Third item should keep its original ID.");
        }

        [TestMethod]
        public void IdRecoveryHelperPreservesOrder()
        {
            var sizes = new List<ResizeSize>
            {
                new(5, "Size1", ResizeFit.Fit, 800, 600, ResizeUnit.Pixel),
                new(2, "Size2", ResizeFit.Fit, 1024, 768, ResizeUnit.Pixel),
                new(4, "Size3", ResizeFit.Fit, 1920, 1080, ResizeUnit.Pixel),
            };

            IdRecoveryHelper.RecoverInvalidIds(sizes);

            Assert.AreEqual("Size1", sizes[0].Name, "Items should retain the same order.");
            Assert.AreEqual("Size2", sizes[1].Name, "Items should retain the same order.");
            Assert.AreEqual("Size3", sizes[2].Name, "Items should retain the same order.");
        }

        [TestMethod]
        public void IdRecoveryHelperHandlesMultipleDuplicates()
        {
            var sizes = new List<ResizeSize>
            {
                new(1, "Size1", ResizeFit.Fit, 800, 600, ResizeUnit.Pixel),
                new(1, "Size2", ResizeFit.Fit, 1024, 768, ResizeUnit.Pixel), // Duplicate ID
                new(1, "Size3", ResizeFit.Fit, 1920, 1080, ResizeUnit.Pixel), // Duplicate ID
            };

            IdRecoveryHelper.RecoverInvalidIds(sizes);

            Assert.AreEqual(1, sizes[0].Id, "First item should keep its original ID.");
            Assert.AreEqual(0, sizes[1].Id, "Second item should have been assigned a new unique ID.");
            Assert.AreEqual(2, sizes[2].Id, "Third item should have been assigned a new unique ID.");
        }

        [TestMethod]
        public void IdRecoveryHelperFillsGaps()
        {
            var sizes = new List<ResizeSize>
            {
                new(0, "Size1", ResizeFit.Fit, 800, 600, ResizeUnit.Pixel),
                new(4, "Size2", ResizeFit.Fit, 1024, 768, ResizeUnit.Pixel),
                new(4, "Size3", ResizeFit.Fit, 1920, 1080, ResizeUnit.Pixel), // Duplicate ID
            };

            IdRecoveryHelper.RecoverInvalidIds(sizes);

            Assert.AreEqual(0, sizes[0].Id, "First item should keep its original ID.");
            Assert.AreEqual(4, sizes[1].Id, "Second item should keep its original ID.");
            Assert.AreEqual(1, sizes[2].Id, "Third item should have been assigned the first available ID.");
        }

        [TestMethod]
        public void SelectedSizeIndexSetterDoesNotNotifyWhenValueUnchanged()
        {
            var settings = new Settings();
            settings.SelectedSizeIndex = 0;
            var expectedSize = settings.Sizes[0];
            var notifications = new List<string>();
            settings.PropertyChanged += (sender, e) => notifications.Add(e.PropertyName);

            settings.SelectedSizeIndex = 0;

            Assert.AreEqual(0, notifications.Count);
            Assert.AreEqual(0, settings.SelectedSizeIndex);
            Assert.AreSame(expectedSize, settings.SelectedSize);
        }

        [TestMethod]
        public void SelectedSizeIndexSetterNotifiesWhenValueChanged()
        {
            var settings = new Settings();
            settings.SelectedSizeIndex = 0;
            var expectedSize = settings.Sizes[1];
            var notifications = new List<(object Sender, string Name, int Index, ResizeSize Size)>();
            settings.PropertyChanged += (sender, e) =>
                notifications.Add((sender, e.PropertyName, settings.SelectedSizeIndex, settings.SelectedSize));

            settings.SelectedSizeIndex = 1;

            Assert.AreEqual(2, notifications.Count);
            CollectionAssert.AreEqual(
                new[] { nameof(Settings.SelectedSizeIndex), nameof(Settings.SelectedSize) },
                notifications.Select(notification => notification.Name).ToArray());
            foreach (var notification in notifications)
            {
                Assert.AreSame(settings, notification.Sender);
                Assert.AreEqual(1, notification.Index);
                Assert.AreSame(expectedSize, notification.Size);
            }

            Assert.AreEqual(1, settings.SelectedSizeIndex);
            Assert.AreSame(expectedSize, settings.SelectedSize);
        }

        [TestMethod]
        public void SelectedSizeReturnsCorrectItemAfterReload()
        {
            var settings = new Settings();
            settings.Sizes.Clear();
            settings.Sizes.Add(new ResizeSize(0, "Small", ResizeFit.Fit, 640, 480, ResizeUnit.Pixel));
            settings.Sizes.Add(new ResizeSize(1, "Medium", ResizeFit.Fit, 1280, 720, ResizeUnit.Pixel));
            settings.SelectedSizeIndex = 1;
            settings.Save();

            settings.Reload();

            Assert.AreEqual("Medium", settings.SelectedSize.Name, "SelectedSize should return the correct item after reload.");
            Assert.AreEqual(1, (settings.SelectedSize as IHasId)?.Id, "SelectedSize should have the correct ID after reload.");
        }

        [TestMethod]
        public void ReloadUpdatesCustomSizeInstance()
        {
            var settings = new Settings();
            var originalCustomSize = settings.CustomSize;
            settings.CustomSize.Width = 500;
            settings.Save();

            // Modify the saved file to change CustomSize width.
            var json = System.IO.File.ReadAllText(Settings.SettingsPath);
            var wrapper = JsonSerializer.Deserialize<SettingsWrapper>(json);
            wrapper.Properties.CustomSize.Width = 999;
            json = JsonSerializer.Serialize(wrapper, _serializerOptions);
            System.IO.File.WriteAllText(Settings.SettingsPath, json);

            settings.Reload();

            Assert.AreNotSame(originalCustomSize, settings.CustomSize, "CustomSize instance should be updated after reload.");
            Assert.AreEqual(999, settings.CustomSize.Width, "CustomSize width should reflect the reloaded value.");
        }

        [TestMethod]
        public void ReloadRecoversDuplicateIdsBeforeMatching()
        {
            var settings = new Settings();
            settings.Sizes.Clear();
            settings.Sizes.Add(new ResizeSize(0, "Size1", ResizeFit.Fit, 800, 600, ResizeUnit.Pixel));
            settings.SelectedSizeIndex = 0;
            settings.Save();

            // Manually create the file with duplicate IDs.
            var json = System.IO.File.ReadAllText(Settings.SettingsPath);
            var wrapper = JsonSerializer.Deserialize<SettingsWrapper>(json);
            wrapper.Properties.Sizes.Add(new ResizeSize(0, "Size2", ResizeFit.Fit, 1024, 768, ResizeUnit.Pixel)); // Duplicate ID
            json = JsonSerializer.Serialize(wrapper, _serializerOptions);
            System.IO.File.WriteAllText(Settings.SettingsPath, json);

            settings.Reload();

            Assert.AreEqual(2, settings.Sizes.Count, "There should be two sizes after reload.");
            Assert.AreEqual(0, settings.Sizes[0].Id, "First size should keep its original ID.");
            Assert.AreEqual(1, settings.Sizes[1].Id, "Second size should have been assigned a new unique ID.");
            Assert.AreEqual(0, settings.SelectedSizeIndex, "SelectedSizeIndex should still refer to the first size.");
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (ReferenceEquals(ResourceLoaderInstance.GetString, _getString))
            {
                ResourceLoaderInstance.GetString = _originalGetString;
            }
        }

        [TestCleanup]
        public void TestCleanUp()
        {
            Dispose();
        }

        public void Dispose()
        {
            try
            {
                _directory.Dispose();
            }
            finally
            {
                if (Settings.SettingsPath == _settingsPath)
                {
                    Settings.SettingsPath = _originalSettingsPath;
                }
            }

            GC.SuppressFinalize(this);
        }
    }
}
