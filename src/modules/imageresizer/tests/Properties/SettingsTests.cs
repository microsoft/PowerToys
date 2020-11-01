// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
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
