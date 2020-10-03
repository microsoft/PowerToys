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
        private bool disposedValue;

        [Fact]
        public void ExecuteCopiesFrameMetadata()
        {
            var operation = new ResizeOperation("Test.jpg", _directory, Settings());

            operation.Execute();

            AssertEx.Image(
                _directory.File(),
                image => Assert.Equal("Test", ((BitmapMetadata)image.Frames[0].Metadata).Comment));
        }

        [Fact]
        public void ExecuteCopiesFrameMetadataExceptWhenMetadataCannotBeCloned()
        {
            var operation = new ResizeOperation("TestMetadataIssue2447.jpg", _directory, Settings());

            operation.Execute();

            AssertEx.Image(
                _directory.File(),
                image => Assert.Null(((BitmapMetadata)image.Frames[0].Metadata).CameraModel));
        }

        [Fact]
        public void ExecuteKeepsDateModified()
        {
            var operation = new ResizeOperation("Test.png", _directory, Settings(s => s.KeepDateModified = true));

            operation.Execute();

            Assert.Equal(File.GetLastWriteTimeUtc("Test.png"), File.GetLastWriteTimeUtc(_directory.File()));
        }

        [Fact]
        public void ExecuteKeepsDateModifiedWhenReplacingOriginals()
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
        public void ExecuteReplacesOriginals()
        {
            var path = Path.Combine(_directory, "Test.png");
            File.Copy("Test.png", path);

            var operation = new ResizeOperation(path, null, Settings(s => s.Replace = true));

            operation.Execute();

            AssertEx.Image(_directory.File(), image => Assert.Equal(96, image.Frames[0].PixelWidth));
        }

        [Fact]
        public void ExecuteTransformsEachFrame()
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
        public void ExecuteUsesFallbackEncoder()
        {
            var operation = new ResizeOperation(
                "Test.ico",
                _directory,
                Settings(s => s.FallbackEncoder = new PngBitmapEncoder().CodecInfo.ContainerFormat));

            operation.Execute();

            Assert.Contains("Test (Test).png", _directory.FileNames);
        }

        [Fact]
        public void TransformIgnoresOrientationWhenLandscapeToPortrait()
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
        public void TransformIgnoresOrientationWhenPortraitToLandscape()
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
        public void TransformIgnoresIgnoreOrientationWhenAuto()
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
        public void TransformIgnoresIgnoreOrientationWhenPercent()
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
        public void TransformHonorsShrinkOnly()
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
        public void TransformIgnoresShrinkOnlyWhenPercent()
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
        public void TransformHonorsShrinkOnlyWhenAutoHeight()
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
        public void TransformHonorsShrinkOnlyWhenAutoWidth()
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
        public void TransformHonorsUnit()
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
        public void TransformHonorsFitWhenFit()
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
        public void TransformHonorsFitWhenFill()
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
        public void TransformHonorsFitWhenStretch()
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
        public void GetDestinationPathUniquifiesOutputFilename()
        {
            File.WriteAllBytes(Path.Combine(_directory, "Test (Test).png"), Array.Empty<byte>());

            var operation = new ResizeOperation("Test.png", _directory, Settings());

            operation.Execute();

            Assert.Contains("Test (Test) (1).png", _directory.FileNames);
        }

        [Fact]
        public void GetDestinationPathUniquifiesOutputFilenameAgain()
        {
            File.WriteAllBytes(Path.Combine(_directory, "Test (Test).png"), Array.Empty<byte>());
            File.WriteAllBytes(Path.Combine(_directory, "Test (Test) (1).png"), Array.Empty<byte>());

            var operation = new ResizeOperation("Test.png", _directory, Settings());

            operation.Execute();

            Assert.Contains("Test (Test) (2).png", _directory.FileNames);
        }

        [Fact]
        public void GetDestinationPathUsesFileNameFormat()
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(s => s.FileName = "%1_%2_%3_%4_%5_%6"));

            operation.Execute();

            Assert.Contains("Test_Test_96_96_96_48.png", _directory.FileNames);
        }

        [Fact]
        public void ExecuteHandlesDirectoriesInFileNameFormat()
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(s => s.FileName = @"Directory\%1 (%2)"));

            operation.Execute();

            Assert.True(File.Exists(_directory + @"\Directory\Test (Test).png"));
        }

        private static Settings Settings(Action<Settings> action = null)
        {
            var settings = new Settings()
            {
                SelectedSizeIndex = 0,
            };
            settings.Sizes.Clear();

            settings.Sizes.Add(new ResizeSize
            {
                Name = "Test",
                Width = 96,
                Height = 96,
            });

            action?.Invoke(settings);

            return settings;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _directory.Dispose();
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
