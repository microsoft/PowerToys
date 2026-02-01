#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

using ImageResizer.Models;
using ImageResizer.Test;
using ManagedCommon;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageResizer.Properties
{
    [TestClass]
    public class SettingsTests
    {
        private static readonly JsonSerializerOptions _serializerOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
        };

        private static readonly CompositeFormat ValueMustBeBetween = System.Text.CompositeFormat.Parse(Properties.Resources.ValueMustBeBetween);

        private static App _imageResizerApp;

        public SettingsTests()
        {
            // Change settings.json path to a temp file
            Settings.SettingsPath = ".\\test_settings.json";
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            // new App() needs to be created since Settings.Reload() uses App.Current to update properties on the UI thread. App() can be created only once otherwise it results in System.InvalidOperationException : Cannot create more than one System.Windows.Application instance in the same AppDomain.
            _imageResizerApp = new App();
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
        public void AllSizesHandlesPropertyEventsForCustomSize()
        {
            var originalCustomSize = new CustomSize();
            var settings = new Settings
            {
                CustomSize = originalCustomSize,
            };

            settings.Sizes.Clear();
            var ncc = (INotifyCollectionChanged)settings.AllSizes;

            var result = AssertEx.Raises<NotifyCollectionChangedEventArgs>(
                h => ncc.CollectionChanged += h,
                h => ncc.CollectionChanged -= h,
                () => settings.CustomSize = new CustomSize());

            // Reset is used instead of Replace to avoid ArgumentOutOfRangeException
            // when notifying changes for virtual items (CustomSize/AiSize) that exist
            // outside the bounds of the underlying _sizes collection.
            Assert.AreEqual(NotifyCollectionChangedAction.Reset, result.Arguments.Action);
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

            // Using InvariantCulture since this is used internally
            Assert.AreEqual(
                string.Format(CultureInfo.InvariantCulture, ValueMustBeBetween, 1, 100),
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

        [TestMethod]
        public void ReloadRaisesPropertyChanged()
        {
            // Arrange
            var settings = new Settings();
            settings.Save();    // To create the settings file

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
            wrapper.Properties.SelectedSizeIndex = 0; // Simulate fallback to first preset.
            json = JsonSerializer.Serialize(wrapper, _serializerOptions);
            System.IO.File.WriteAllText(Settings.SettingsPath, json);

            settings.Reload();

            Assert.AreEqual(0, settings.SelectedSizeIndex, "Selection should fall back to first preset after deletion.");
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

            bool isNotified = false;
            settings.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(settings.SelectedSizeIndex))
                {
                    isNotified = true;
                }
            };

            settings.SelectedSizeIndex = 0; // Setting to the same value

            Assert.IsFalse(isNotified, "PropertyChanged should not be raised when setting the same value.");
        }

        [TestMethod]
        public void SelectedSizeIndexSetterNotifiesWhenValueChanged()
        {
            var settings = new Settings();
            settings.SelectedSizeIndex = 0;

            bool isNotified = false;
            settings.PropertyChanged += (sender, e) =>
            {
                if (e.PropertyName == nameof(settings.SelectedSizeIndex))
                {
                    isNotified = true;
                }
            };

            settings.SelectedSizeIndex = 1; // Setting to a different value

            Assert.IsTrue(isNotified, "PropertyChanged should be raised when setting a different value.");
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
            _imageResizerApp.Dispose();
            _imageResizerApp = null;
        }

        [TestCleanup]
        public void TestCleanUp()
        {
            if (System.IO.File.Exists(Settings.SettingsPath))
            {
                System.IO.File.Delete(Settings.SettingsPath);
            }
        }
    }
}
