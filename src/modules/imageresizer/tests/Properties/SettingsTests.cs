// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using ImageResizer.Models;
using ImageResizer.Test;
using Xunit;
using Xunit.Extensions;

namespace ImageResizer.Properties
{
    public class SettingsTests : IClassFixture<AppFixture>
    {
        [Fact]
        public void AllSizes_propagates_Sizes_collection_events()
        {
            var settings = new Settings
            {
                Sizes = new ObservableCollection<ResizeSize>(),
                CustomSize = new CustomSize(),
            };
            var ncc = (INotifyCollectionChanged)settings.AllSizes;

            var result = AssertEx.Raises<NotifyCollectionChangedEventArgs>(
                h => ncc.CollectionChanged += h,
                h => ncc.CollectionChanged -= h,
                () => settings.Sizes.Add(new ResizeSize()));

            Assert.Equal(NotifyCollectionChangedAction.Add, result.Arguments.Action);
        }

        [Fact]
        public void AllSizes_propagates_Sizes_property_events()
        {
            var settings = new Settings
            {
                Sizes = new ObservableCollection<ResizeSize>(),
                CustomSize = new CustomSize(),
            };

            Assert.PropertyChanged(
                (INotifyPropertyChanged)settings.AllSizes,
                "Item[]",
                () => settings.Sizes.Add(new ResizeSize()));
        }

        [Fact]
        public void AllSizes_contains_Sizes()
        {
            var settings = new Settings
            {
                Sizes = new ObservableCollection<ResizeSize> { new ResizeSize() },
                CustomSize = new CustomSize(),
            };

            Assert.Contains(settings.Sizes[0], settings.AllSizes);
        }

        [Fact]
        public void AllSizes_contains_CustomSize()
        {
            var settings = new Settings
            {
                Sizes = new ObservableCollection<ResizeSize>(),
                CustomSize = new CustomSize(),
            };

            Assert.Contains(settings.CustomSize, settings.AllSizes);
        }

        [Fact]
        public void AllSizes_handles_property_events_for_CustomSize()
        {
            var originalCustomSize = new CustomSize();
            var settings = new Settings
            {
                Sizes = new ObservableCollection<ResizeSize>(),
                CustomSize = originalCustomSize,
            };
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
        public void FileNameFormat_works()
        {
            var settings = new Settings { FileName = "{T}%1e%2s%3t%4%5%6%7" };

            var result = settings.FileNameFormat;

            Assert.Equal("{{T}}{0}e{1}s{2}t{3}{4}{5}%7", result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void SelectedSize_returns_CustomSize_when_out_of_range(int index)
        {
            var settings = new Settings
            {
                SelectedSizeIndex = index,
                Sizes = new ObservableCollection<ResizeSize>(),
                CustomSize = new CustomSize(),
            };

            var result = settings.SelectedSize;

            Assert.Same(settings.CustomSize, result);
        }

        [Fact]
        public void SelectedSize_returns_Size_when_in_range()
        {
            var settings = new Settings
            {
                SelectedSizeIndex = 0,
                Sizes = new ObservableCollection<ResizeSize>
                {
                    new ResizeSize(),
                },
            };

            var result = settings.SelectedSize;

            Assert.Same(settings.Sizes[0], result);
        }

        [Fact]
        public void IDataErrorInfo_Error_returns_empty()
        {
            var settings = new Settings();

            var result = ((IDataErrorInfo)settings).Error;

            Assert.Empty(result);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(101)]
        public void IDataErrorInfo_Item_JpegQualityLevel_returns_error_when_out_of_range(int value)
        {
            var settings = new Settings { JpegQualityLevel = value };

            var result = ((IDataErrorInfo)settings)["JpegQualityLevel"];

            Assert.Equal(
                string.Format(Resources.ValueMustBeBetween, 1, 100),
                result);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(100)]
        public void IDataErrorInfo_Item_JpegQualityLevel_returns_empty_when_in_range(int value)
        {
            var settings = new Settings { JpegQualityLevel = value };

            var result = ((IDataErrorInfo)settings)["JpegQualityLevel"];

            Assert.Empty(result);
        }

        [Fact]
        public void IDataErrorInfo_Item_returns_empty_when_not_JpegQualityLevel()
        {
            var settings = new Settings();

            var result = ((IDataErrorInfo)settings)["Unknown"];

            Assert.Empty(result);
        }

        private Settings SettingsWithTempFile()
        {
            var settings = new Settings();
            Settings.SettingsPath = ".\\test_settings.json";
            return settings;
        }

        private void DeleteSettingsFileIfExists(Settings settings)
        {
            if (System.IO.File.Exists(Settings.SettingsPath))
            {
                System.IO.File.Delete(Settings.SettingsPath);
            }
        }

        [Fact]
        public void Reload_createsFile_when_FileNotFound()
        {
            // Arrange
            var settings = SettingsWithTempFile();
            DeleteSettingsFileIfExists(settings);

            // Assert
            Assert.False(System.IO.File.Exists(Settings.SettingsPath));

            // Act
            settings.Reload();

            // Assert
            Assert.True(System.IO.File.Exists(Settings.SettingsPath));

            // Arrange
            DeleteSettingsFileIfExists(settings);
        }

        [Fact]
        public void Save_creates_file()
        {
            // Arrange
            var settings = SettingsWithTempFile();
            DeleteSettingsFileIfExists(settings);

            // Assert
            Assert.False(System.IO.File.Exists(Settings.SettingsPath));

            // Act
            settings.Save();

            // Assert
            Assert.True(System.IO.File.Exists(Settings.SettingsPath));

            // Arrange
            DeleteSettingsFileIfExists(settings);
        }

        [Fact]
        public void Save_json_is_readable_by_Reload()
        {
            // Arrange
            var settings = SettingsWithTempFile();
            DeleteSettingsFileIfExists(settings);

            // Assert
            Assert.False(System.IO.File.Exists(Settings.SettingsPath));

            // Act
            settings.Save();
            settings.Reload();  // If the JSON file created by Save() is not readable this function will throw an error

            // Assert
            Assert.True(System.IO.File.Exists(Settings.SettingsPath));

            // Arrange
            DeleteSettingsFileIfExists(settings);
        }

        [Fact]
        public void Reload_raises_PropertyChanged_()
        {
            // Arrange
            var settings = SettingsWithTempFile();
            DeleteSettingsFileIfExists(settings);
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
            Assert.PropertyChanged(settings, "Sizes", action);
            Assert.PropertyChanged(settings, "KeepDateModified", action);
            Assert.PropertyChanged(settings, "FallbackEncoder", action);
            Assert.PropertyChanged(settings, "CustomSize", action);
            Assert.PropertyChanged(settings, "SelectedSizeIndex", action);

            // Arrange
            DeleteSettingsFileIfExists(settings);
        }
    }
}
