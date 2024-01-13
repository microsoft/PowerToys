// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using ImageResizer.Models;
using ImageResizer.Test;
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

            Assert.AreEqual(NotifyCollectionChangedAction.Replace, result.Arguments.Action);
            Assert.AreEqual(1, result.Arguments.NewItems.Count);
            Assert.AreEqual(settings.CustomSize, result.Arguments.NewItems[0]);
            Assert.AreEqual(0, result.Arguments.NewStartingIndex);
            Assert.AreEqual(1, result.Arguments.OldItems.Count);
            Assert.AreEqual(originalCustomSize, result.Arguments.OldItems[0]);
            Assert.AreEqual(0, result.Arguments.OldStartingIndex);
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
