// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using ImageResizer.Models;
using ImageResizer.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageResizer.Properties
{
    [TestClass]
    public class SettingsTests
    {
        private static App _imageResizerApp;

        public SettingsTests()
        {
            // Change settings.json path to a temp file
            Settings.SettingsPath = ".\\test_settings.json";
        }

        [ClassInitialize]
#pragma warning disable CA1801 // Review unused parameters
        public static void ClassInitialize(TestContext context)
#pragma warning restore CA1801 // Review unused parameters
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
                string.Format(CultureInfo.InvariantCulture, Resources.ValueMustBeBetween, 1, 100),
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
