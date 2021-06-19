// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImageResizer.Models;
using ImageResizer.Test;
using Xunit;
using Xunit.Abstractions;
using Xunit.Extensions;

namespace ImageResizer.Properties
{
    public class SettingsTests : IClassFixture<AppFixture>, IDisposable
    {
        private bool disposedValue;

        public SettingsTests()
        {
            // Change settings.json path to a temp file
            Settings.SettingsPath = ".\\test_settings.json";
        }

        [Fact]
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

            Assert.Equal(NotifyCollectionChangedAction.Add, result.Arguments.Action);
        }

        [Fact]
        public void AllSizesPropagatesSizesPropertyEvents()
        {
            var settings = new Settings
            {
                CustomSize = new CustomSize(),
            };

            settings.Sizes.Clear();
            Assert.PropertyChanged(
                (INotifyPropertyChanged)settings.AllSizes,
                "Item[]",
                () => settings.Sizes.Add(new ResizeSize()));
        }

        [Fact]
        public void AllSizesContainsSizes()
        {
            var settings = new Settings
            {
                CustomSize = new CustomSize(),
            };

            settings.Sizes.Add(new ResizeSize());
            Assert.Contains(settings.Sizes[0], settings.AllSizes);
        }

        [Fact]
        public void AllSizesContainsCustomSize()
        {
            var settings = new Settings
            {
                CustomSize = new CustomSize(),
            };
            settings.Sizes.Clear();

            Assert.Contains(settings.CustomSize, settings.AllSizes);
        }

        [Fact]
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

            Assert.Equal(NotifyCollectionChangedAction.Replace, result.Arguments.Action);
            Assert.Equal(1, result.Arguments.NewItems.Count);
            Assert.Equal(settings.CustomSize, result.Arguments.NewItems[0]);
            Assert.Equal(0, result.Arguments.NewStartingIndex);
            Assert.Equal(1, result.Arguments.OldItems.Count);
            Assert.Equal(originalCustomSize, result.Arguments.OldItems[0]);
            Assert.Equal(0, result.Arguments.OldStartingIndex);
        }

        [Fact]
        public void FileNameFormatWorks()
        {
            var settings = new Settings { FileName = "{T}%1e%2s%3t%4%5%6%7" };

            var result = settings.FileNameFormat;

            Assert.Equal("{{T}}{0}e{1}s{2}t{3}{4}{5}%7", result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void SelectedSizeReturnsCustomSizeWhenOutOfRange(int index)
        {
            var settings = new Settings
            {
                SelectedSizeIndex = index,
                CustomSize = new CustomSize(),
            };
            settings.Sizes.Clear();

            var result = settings.SelectedSize;

            Assert.Same(settings.CustomSize, result);
        }

        [Fact]
        public void SelectedSizeReturnsSizeWhenInRange()
        {
            var settings = new Settings
            {
                SelectedSizeIndex = 0,
            };

            settings.Sizes.Add(new ResizeSize());
            var result = settings.SelectedSize;

            Assert.Same(settings.Sizes[0], result);
        }

        [Fact]
        public void IDataErrorInfoErrorReturnsEmpty()
        {
            var settings = new Settings();

            var result = ((IDataErrorInfo)settings).Error;

            Assert.Empty(result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(101)]
        public void IDataErrorInfoItemJpegQualityLevelReturnsErrorWhenOutOfRange(int value)
        {
            var settings = new Settings { JpegQualityLevel = value };

            var result = ((IDataErrorInfo)settings)["JpegQualityLevel"];

            // Using InvariantCulture since this is used internally
            Assert.Equal(
                string.Format(CultureInfo.InvariantCulture, Resources.ValueMustBeBetween, 1, 100),
                result);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(100)]
        public void IDataErrorInfoItemJpegQualityLevelReturnsEmptyWhenInRange(int value)
        {
            var settings = new Settings { JpegQualityLevel = value };

            var result = ((IDataErrorInfo)settings)["JpegQualityLevel"];

            Assert.Empty(result);
        }

        [Fact]
        public void IDataErrorInfoItemReturnsEmptyWhenNotJpegQualityLevel()
        {
            var settings = new Settings();

            var result = ((IDataErrorInfo)settings)["Unknown"];

            Assert.Empty(result);
        }

        [Fact]
        public void ReloadCreatesFileWhenFileNotFound()
        {
            // Arrange
            var settings = new Settings();

            // Assert
            Assert.False(System.IO.File.Exists(Settings.SettingsPath));

            // Act
            settings.Reload();

            // Assert
            Assert.True(System.IO.File.Exists(Settings.SettingsPath));
        }

        [Fact]
        public void SaveCreatesFile()
        {
            // Arrange
            var settings = new Settings();

            // Assert
            Assert.False(System.IO.File.Exists(Settings.SettingsPath));

            // Act
            settings.Save();

            // Assert
            Assert.True(System.IO.File.Exists(Settings.SettingsPath));
        }

        [Fact]
        public void SaveJsonIsReadableByReload()
        {
            // Arrange
            var settings = new Settings();

            // Assert
            Assert.False(System.IO.File.Exists(Settings.SettingsPath));

            // Act
            settings.Save();
            settings.Reload();  // If the JSON file created by Save() is not readable this function will throw an error

            // Assert
            Assert.True(System.IO.File.Exists(Settings.SettingsPath));
        }

        [Fact]
        public void ReloadRaisesPropertyChanged()
        {
            // Arrange
            var settings = new Settings();
            settings.Save();    // To create the settings file

            // Act
            var action = new System.Action(settings.Reload);

            // Assert
            Assert.PropertyChanged(settings, "ShrinkOnly", action);
            Assert.PropertyChanged(settings, "Replace", action);
            Assert.PropertyChanged(settings, "IgnoreOrientation", action);
            Assert.PropertyChanged(settings, "JpegQualityLevel", action);
            Assert.PropertyChanged(settings, "PngInterlaceOption", action);
            Assert.PropertyChanged(settings, "TiffCompressOption", action);
            Assert.PropertyChanged(settings, "FileName", action);
            Assert.PropertyChanged(settings, "KeepDateModified", action);
            Assert.PropertyChanged(settings, "FallbackEncoder", action);
            Assert.PropertyChanged(settings, "CustomSize", action);
            Assert.PropertyChanged(settings, "SelectedSizeIndex", action);
        }

        [Fact]
        public void SystemTextJsonDeserializesCorrectly()
        {
            // Generated Settings file in 0.72
            var defaultInput =
                "{\r\n  \"properties\": {\r\n    \"imageresizer_selectedSizeIndex\": {\r\n      \"value\": 1\r\n    },\r\n    \"imageresizer_shrinkOnly\": {\r\n      \"value\": true\r\n    },\r\n    \"imageresizer_replace\": {\r\n      \"value\": true\r\n    },\r\n    \"imageresizer_ignoreOrientation\": {\r\n      \"value\": false\r\n    },\r\n    \"imageresizer_jpegQualityLevel\": {\r\n      \"value\": 91\r\n    },\r\n    \"imageresizer_pngInterlaceOption\": {\r\n      \"value\": 1\r\n    },\r\n    \"imageresizer_tiffCompressOption\": {\r\n      \"value\": 1\r\n    },\r\n    \"imageresizer_fileName\": {\r\n      \"value\": \"%1 %1 (%2)\"\r\n    },\r\n    \"imageresizer_sizes\": {\r\n      \"value\": [\r\n        {\r\n          \"Id\": 0,\r\n          \"ExtraBoxOpacity\": 100,\r\n          \"EnableEtraBoxes\": true,\r\n          \"name\": \"Small-NotDefault\",\r\n          \"fit\": 1,\r\n          \"width\": 854,\r\n          \"height\": 480,\r\n          \"unit\": 3\r\n        },\r\n        {\r\n          \"Id\": 3,\r\n          \"ExtraBoxOpacity\": 100,\r\n          \"EnableEtraBoxes\": true,\r\n          \"name\": \"Phone\",\r\n          \"fit\": 1,\r\n          \"width\": 320,\r\n          \"height\": 568,\r\n          \"unit\": 3\r\n        }\r\n      ]\r\n    },\r\n    \"imageresizer_keepDateModified\": {\r\n      \"value\": false\r\n    },\r\n    \"imageresizer_fallbackEncoder\": {\r\n      \"value\": \"19e4a5aa-5662-4fc5-a0c0-1758028e1057\"\r\n    },\r\n    \"imageresizer_customSize\": {\r\n      \"value\": {\r\n        \"Id\": 4,\r\n        \"ExtraBoxOpacity\": 100,\r\n        \"EnableEtraBoxes\": true,\r\n        \"name\": \"custom\",\r\n        \"fit\": 1,\r\n        \"width\": 1024,\r\n        \"height\": 640,\r\n        \"unit\": 3\r\n      }\r\n    }\r\n  },\r\n  \"name\": \"ImageResizer\",\r\n  \"version\": \"1\"\r\n}";

            // Execute readFile/writefile twice and see if serialized string is still correct
            var resultWrapper = JsonSerializer.Deserialize<SettingsWrapper>(defaultInput);
            var serializedInput = JsonSerializer.Serialize(resultWrapper, new JsonSerializerOptions() { WriteIndented = true });
            var resultWrapper2 = JsonSerializer.Deserialize<SettingsWrapper>(serializedInput);
            var serializedInput2 = JsonSerializer.Serialize(resultWrapper2, new JsonSerializerOptions() { WriteIndented = true });

            Assert.Equal(serializedInput, serializedInput2);
            Assert.Equal("ImageResizer", resultWrapper2.Name);
            Assert.Equal("1", resultWrapper2.Version);
            Assert.NotNull(resultWrapper2.Properties);
            Assert.True(resultWrapper2.Properties.ShrinkOnly);
            Assert.True(resultWrapper2.Properties.Replace);
            Assert.Equal(91, resultWrapper2.Properties.JpegQualityLevel);
            Assert.Equal(1, (int)resultWrapper2.Properties.PngInterlaceOption);
            Assert.Equal(1, (int)resultWrapper2.Properties.TiffCompressOption);
            Assert.Equal("%1 %1 (%2)", resultWrapper2.Properties.FileName);
            Assert.Equal(2, resultWrapper2.Properties.Sizes.Count);
            Assert.False(resultWrapper2.Properties.KeepDateModified);
            Assert.Equal("Small-NotDefault", resultWrapper2.Properties.Sizes[0].Name);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (System.IO.File.Exists(Settings.SettingsPath))
                    {
                        System.IO.File.Delete(Settings.SettingsPath);
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
