// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ImageResizer.Properties;
using ImageResizer.Test;
using Xunit;

namespace ImageResizer.Models
{
    public class ResizeOperationTests : IDisposable
    {
        private readonly TestDirectory _directory = new TestDirectory();

        [Fact]
        public void Execute_copies_frame_metadata()
        {
            var operation = new ResizeOperation("Test.jpg", _directory, Settings());

            operation.Execute();

            AssertEx.Image(
                _directory.File(),
                image => Assert.Equal("Test", ((BitmapMetadata)image.Frames[0].Metadata).Comment));
        }

        [Fact]
        public void Execute_keeps_date_modified()
        {
            var operation = new ResizeOperation("Test.png", _directory, Settings(s => s.KeepDateModified = true));

            operation.Execute();

            Assert.Equal(File.GetLastWriteTimeUtc("Test.png"), File.GetLastWriteTimeUtc(_directory.File()));
        }

        [Fact]
        public void Execute_keeps_date_modified_when_replacing_originals()
        {
            var path = Path.Combine(_directory, "Test.png");
            File.Copy("Test.png", path);

            var originalDateModified = File.GetLastWriteTimeUtc(path);

            var operation = new ResizeOperation(
                path,
                null,
                Settings(
                    s =>
                    {
                        s.KeepDateModified = true;
                        s.Replace = true;
                    }));

            operation.Execute();

            Assert.Equal(originalDateModified, File.GetLastWriteTimeUtc(_directory.File()));
        }

        [Fact]
        public void Execute_replaces_originals()
        {
            var path = Path.Combine(_directory, "Test.png");
            File.Copy("Test.png", path);

            var operation = new ResizeOperation(path, null, Settings(s => s.Replace = true));

            operation.Execute();

            AssertEx.Image(_directory.File(), image => Assert.Equal(96, image.Frames[0].PixelWidth));
        }

        [Fact]
        public void Execute_transforms_each_frame()
        {
            var operation = new ResizeOperation("Test.gif", _directory, Settings());

            operation.Execute();

            AssertEx.Image(
                _directory.File(),
                image =>
                {
                    Assert.Equal(2, image.Frames.Count);
                    AssertEx.All(image.Frames, frame => Assert.Equal(96, frame.PixelWidth));
                });
        }

        [Fact]
        public void Execute_uses_fallback_encoder()
        {
            var operation = new ResizeOperation(
                "Test.ico",
                _directory,
                Settings(s => s.FallbackEncoder = new PngBitmapEncoder().CodecInfo.ContainerFormat));

            operation.Execute();

            Assert.Contains("Test (Test).png", _directory.FileNames);
        }

        [Fact]
        public void Transform_ignores_orientation_when_landscape_to_portrait()
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(
                    x =>
                    {
                        x.IgnoreOrientation = true;
                        x.SelectedSize.Width = 96;
                        x.SelectedSize.Height = 192;
                    }));

            operation.Execute();

            AssertEx.Image(
                _directory.File(),
                image =>
                {
                    Assert.Equal(192, image.Frames[0].PixelWidth);
                    Assert.Equal(96, image.Frames[0].PixelHeight);
                });
        }

        [Fact]
        public void Transform_ignores_orientation_when_portrait_to_landscape()
        {
            var operation = new ResizeOperation(
                "TestPortrait.png",
                _directory,
                Settings(
                    x =>
                    {
                        x.IgnoreOrientation = true;
                        x.SelectedSize.Width = 192;
                        x.SelectedSize.Height = 96;
                    }));

            operation.Execute();

            AssertEx.Image(
                _directory.File(),
                image =>
                {
                    Assert.Equal(96, image.Frames[0].PixelWidth);
                    Assert.Equal(192, image.Frames[0].PixelHeight);
                });
        }

        [Fact]
        public void Transform_ignores_ignore_orientation_when_auto()
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(
                    x =>
                    {
                        x.IgnoreOrientation = true;
                        x.SelectedSize.Width = 96;
                        x.SelectedSize.Height = 0;
                    }));

            operation.Execute();

            AssertEx.Image(
                _directory.File(),
                image =>
                {
                    Assert.Equal(96, image.Frames[0].PixelWidth);
                    Assert.Equal(48, image.Frames[0].PixelHeight);
                });
        }

        [Fact]
        public void Transform_ignores_ignore_orientation_when_percent()
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(
                    x =>
                    {
                        x.IgnoreOrientation = true;
                        x.SelectedSize.Width = 50;
                        x.SelectedSize.Height = 200;
                        x.SelectedSize.Unit = ResizeUnit.Percent;
                        x.SelectedSize.Fit = ResizeFit.Stretch;
                    }));

            operation.Execute();

            AssertEx.Image(
                _directory.File(),
                image =>
                {
                    Assert.Equal(96, image.Frames[0].PixelWidth);
                    Assert.Equal(192, image.Frames[0].PixelHeight);
                });
        }

        [Fact]
        public void Transform_honors_shrink_only()
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(
                    x =>
                    {
                        x.ShrinkOnly = true;
                        x.SelectedSize.Width = 288;
                        x.SelectedSize.Height = 288;
                    }));

            operation.Execute();

            AssertEx.Image(
                _directory.File(),
                image =>
                {
                    Assert.Equal(192, image.Frames[0].PixelWidth);
                    Assert.Equal(96, image.Frames[0].PixelHeight);
                });
        }

        [Fact]
        public void Transform_ignores_shrink_only_when_percent()
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(
                    x =>
                    {
                        x.ShrinkOnly = true;
                        x.SelectedSize.Width = 133.3;
                        x.SelectedSize.Unit = ResizeUnit.Percent;
                    }));

            operation.Execute();

            AssertEx.Image(
                _directory.File(),
                image =>
                {
                    Assert.Equal(256, image.Frames[0].PixelWidth);
                    Assert.Equal(128, image.Frames[0].PixelHeight);
                });
        }

        [Fact]
        public void Transform_honors_shrink_only_when_auto_height()
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(
                    x =>
                    {
                        x.ShrinkOnly = true;
                        x.SelectedSize.Width = 288;
                        x.SelectedSize.Height = 0;
                    }));

            operation.Execute();

            AssertEx.Image(
                _directory.File(),
                image => Assert.Equal(192, image.Frames[0].PixelWidth));
        }

        [Fact]
        public void Transform_honors_shrink_only_when_auto_width()
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(
                    x =>
                    {
                        x.ShrinkOnly = true;
                        x.SelectedSize.Width = 0;
                        x.SelectedSize.Height = 288;
                    }));

            operation.Execute();

            AssertEx.Image(
                _directory.File(),
                image => Assert.Equal(96, image.Frames[0].PixelHeight));
        }

        [Fact]
        public void Transform_honors_unit()
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(
                    x =>
                    {
                        x.SelectedSize.Width = 1;
                        x.SelectedSize.Height = 1;
                        x.SelectedSize.Unit = ResizeUnit.Inch;
                    }));

            operation.Execute();

            AssertEx.Image(_directory.File(), image => Assert.Equal(image.Frames[0].DpiX, image.Frames[0].PixelWidth, 0));
        }

        [Fact]
        public void Transform_honors_fit_when_Fit()
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(x => x.SelectedSize.Fit = ResizeFit.Fit));

            operation.Execute();

            AssertEx.Image(
                _directory.File(),
                image =>
                {
                    Assert.Equal(96, image.Frames[0].PixelWidth);
                    Assert.Equal(48, image.Frames[0].PixelHeight);
                });
        }

        [Fact]
        public void Transform_honors_fit_when_Fill()
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(x => x.SelectedSize.Fit = ResizeFit.Fill));

            operation.Execute();

            AssertEx.Image(
                _directory.File(),
                image =>
                {
                    Assert.Equal(Colors.White, image.Frames[0].GetFirstPixel());
                    Assert.Equal(96, image.Frames[0].PixelWidth);
                    Assert.Equal(96, image.Frames[0].PixelHeight);
                });
        }

        [Fact]
        public void Transform_honors_fit_when_Stretch()
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(x => x.SelectedSize.Fit = ResizeFit.Stretch));

            operation.Execute();

            AssertEx.Image(
                _directory.File(),
                image =>
                {
                    Assert.Equal(Colors.Black, image.Frames[0].GetFirstPixel());
                    Assert.Equal(96, image.Frames[0].PixelWidth);
                    Assert.Equal(96, image.Frames[0].PixelHeight);
                });
        }

        [Fact]
        public void GetDestinationPath_uniquifies_output_filename()
        {
            File.WriteAllBytes(Path.Combine(_directory, "Test (Test).png"), new byte[0]);

            var operation = new ResizeOperation("Test.png", _directory, Settings());

            operation.Execute();

            Assert.Contains("Test (Test) (1).png", _directory.FileNames);
        }

        [Fact]
        public void GetDestinationPath_uniquifies_output_filename_again()
        {
            File.WriteAllBytes(Path.Combine(_directory, "Test (Test).png"), new byte[0]);
            File.WriteAllBytes(Path.Combine(_directory, "Test (Test) (1).png"), new byte[0]);

            var operation = new ResizeOperation("Test.png", _directory, Settings());

            operation.Execute();

            Assert.Contains("Test (Test) (2).png", _directory.FileNames);
        }

        [Fact]
        public void GetDestinationPath_uses_fileName_format()
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(s => s.FileName = "%1_%2_%3_%4_%5_%6"));

            operation.Execute();

            Assert.Contains("Test_Test_96_96_96_48.png", _directory.FileNames);
        }

        [Fact]
        public void Execute_handles_directories_in_fileName_format()
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(s => s.FileName = @"Directory\%1 (%2)"));

            operation.Execute();

            Assert.True(File.Exists(_directory + @"\Directory\Test (Test).png"));
        }

        public void Dispose()
            => _directory.Dispose();

        private Settings Settings(Action<Settings> action = null)
        {
            var settings = new Settings
            {
                Sizes = new ObservableCollection<ResizeSize>
                {
                    new ResizeSize
                    {
                        Name = "Test",
                        Width = 96,
                        Height = 96
                    },
                },
                SelectedSizeIndex = 0,
            };
            action?.Invoke(settings);

            return settings;
        }
    }
}
