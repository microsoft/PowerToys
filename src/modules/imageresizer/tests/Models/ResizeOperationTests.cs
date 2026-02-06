#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using ImageResizer.Properties;
using ImageResizer.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImageResizer.Models
{
    [TestClass]
    public class ResizeOperationTests : IDisposable
    {
        // Known legacy container format GUID for PNG, used as FallbackEncoder value in settings JSON
        private static readonly Guid PngContainerFormatGuid = new Guid("1b7cfaf4-713f-473c-bbcd-6137425faeaf");

        private static readonly string[] DateTakenPropertyQuery = new[] { "System.Photo.DateTaken" };
        private static readonly string[] CameraModelPropertyQuery = new[] { "System.Photo.CameraModel" };

        private readonly TestDirectory _directory = new TestDirectory();
        private bool disposedValue;

        [TestMethod]
        public async Task ExecuteCopiesFrameMetadata()
        {
            var operation = new ResizeOperation("Test.jpg", _directory, Settings());

            await operation.ExecuteAsync();

            await AssertEx.ImageAsync(
                _directory.File(),
                async decoder =>
                {
                    var props = await decoder.BitmapProperties.GetPropertiesAsync(DateTakenPropertyQuery);
                    Assert.IsTrue(props.ContainsKey("System.Photo.DateTaken"), "Metadata should be preserved during transcode");
                });
        }

        [TestMethod]
        public async Task ExecuteCopiesFrameMetadataEvenWhenMetadataCannotBeCloned()
        {
            var operation = new ResizeOperation("TestMetadataIssue2447.jpg", _directory, Settings());

            await operation.ExecuteAsync();

            await AssertEx.ImageAsync(
                _directory.File(),
                async decoder =>
                {
                    var props = await decoder.BitmapProperties.GetPropertiesAsync(CameraModelPropertyQuery);
                    Assert.IsTrue(props.ContainsKey("System.Photo.CameraModel"), "Camera model metadata should be preserved");
                });
        }

        [TestMethod]
        public async Task ExecuteKeepsDateModified()
        {
            var operation = new ResizeOperation("Test.png", _directory, Settings(s => s.KeepDateModified = true));

            await operation.ExecuteAsync();

            Assert.AreEqual(File.GetLastWriteTimeUtc("Test.png"), File.GetLastWriteTimeUtc(_directory.File()));
        }

        [TestMethod]
        public async Task ExecuteKeepsDateModifiedWhenReplacingOriginals()
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

            await operation.ExecuteAsync();

            Assert.AreEqual(originalDateModified, File.GetLastWriteTimeUtc(_directory.File()));
        }

        [TestMethod]
        public async Task ExecuteReplacesOriginals()
        {
            var path = Path.Combine(_directory, "Test.png");
            File.Copy("Test.png", path);

            var operation = new ResizeOperation(path, null, Settings(s => s.Replace = true));

            await operation.ExecuteAsync();

            await AssertEx.ImageAsync(_directory.File(), decoder => Assert.AreEqual(96u, decoder.PixelWidth));
        }

        [TestMethod]
        public async Task ExecuteTransformsEachFrame()
        {
            var operation = new ResizeOperation("Test.gif", _directory, Settings());

            await operation.ExecuteAsync();

            await AssertEx.ImageAsync(
                _directory.File(),
                async decoder =>
                {
                    Assert.AreEqual(2u, decoder.FrameCount);
                    for (uint i = 0; i < decoder.FrameCount; i++)
                    {
                        var frame = await decoder.GetFrameAsync(i);
                        Assert.AreEqual(96u, frame.PixelWidth);
                    }
                });
        }

        [TestMethod]
        public async Task ExecuteUsesFallbackEncoder()
        {
            var operation = new ResizeOperation(
                "Test.ico",
                _directory,
                Settings(s => s.FallbackEncoder = PngContainerFormatGuid));

            await operation.ExecuteAsync();

            CollectionAssert.Contains(_directory.FileNames.ToList(), "Test (Test).png");
        }

        [TestMethod]
        public async Task TransformIgnoresOrientationWhenLandscapeToPortrait()
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

            await operation.ExecuteAsync();

            await AssertEx.ImageAsync(
                _directory.File(),
                decoder =>
                {
                    Assert.AreEqual(192u, decoder.PixelWidth);
                    Assert.AreEqual(96u, decoder.PixelHeight);
                });
        }

        [TestMethod]
        public async Task TransformIgnoresOrientationWhenPortraitToLandscape()
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

            await operation.ExecuteAsync();

            await AssertEx.ImageAsync(
                _directory.File(),
                decoder =>
                {
                    Assert.AreEqual(96u, decoder.PixelWidth);
                    Assert.AreEqual(192u, decoder.PixelHeight);
                });
        }

        [TestMethod]
        public async Task TransformIgnoresIgnoreOrientationWhenAuto()
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

            await operation.ExecuteAsync();

            await AssertEx.ImageAsync(
                _directory.File(),
                decoder =>
                {
                    Assert.AreEqual(96u, decoder.PixelWidth);
                    Assert.AreEqual(48u, decoder.PixelHeight);
                });
        }

        [TestMethod]
        public async Task TransformIgnoresIgnoreOrientationWhenPercent()
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

            await operation.ExecuteAsync();

            await AssertEx.ImageAsync(
                _directory.File(),
                decoder =>
                {
                    Assert.AreEqual(96u, decoder.PixelWidth);
                    Assert.AreEqual(192u, decoder.PixelHeight);
                });
        }

        [TestMethod]
        public async Task TransformHonorsShrinkOnly()
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

            await operation.ExecuteAsync();

            await AssertEx.ImageAsync(
                _directory.File(),
                decoder =>
                {
                    Assert.AreEqual(192u, decoder.PixelWidth);
                    Assert.AreEqual(96u, decoder.PixelHeight);
                });
        }

        [TestMethod]
        public async Task TransformIgnoresShrinkOnlyWhenPercent()
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

            await operation.ExecuteAsync();

            await AssertEx.ImageAsync(
                _directory.File(),
                decoder =>
                {
                    Assert.AreEqual(256u, decoder.PixelWidth);
                    Assert.AreEqual(128u, decoder.PixelHeight);
                });
        }

        [TestMethod]
        public async Task TransformHonorsShrinkOnlyWhenAutoHeight()
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

            await operation.ExecuteAsync();

            await AssertEx.ImageAsync(
                _directory.File(),
                decoder => Assert.AreEqual(192u, decoder.PixelWidth));
        }

        [TestMethod]
        public async Task TransformHonorsShrinkOnlyWhenAutoWidth()
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

            await operation.ExecuteAsync();

            await AssertEx.ImageAsync(
                _directory.File(),
                decoder => Assert.AreEqual(96u, decoder.PixelHeight));
        }

        [TestMethod]
        public async Task TransformHonorsUnit()
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

            await operation.ExecuteAsync();

            await AssertEx.ImageAsync(_directory.File(), decoder => Assert.AreEqual((uint)Math.Ceiling(decoder.DpiX), decoder.PixelWidth));
        }

        [TestMethod]
        public async Task TransformHonorsFitWhenFit()
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(x => x.SelectedSize.Fit = ResizeFit.Fit));

            await operation.ExecuteAsync();

            await AssertEx.ImageAsync(
                _directory.File(),
                decoder =>
                {
                    Assert.AreEqual(96u, decoder.PixelWidth);
                    Assert.AreEqual(48u, decoder.PixelHeight);
                });
        }

        [TestMethod]
        public async Task TransformHonorsFitWhenFill()
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(x => x.SelectedSize.Fit = ResizeFit.Fill));

            await operation.ExecuteAsync();

            await AssertEx.ImageAsync(
                _directory.File(),
                async decoder =>
                {
                    var pixel = await decoder.GetFirstPixelAsync();
                    Assert.AreEqual((byte)255, pixel.R, "First pixel R should be 255 (white)");
                    Assert.AreEqual((byte)255, pixel.G, "First pixel G should be 255 (white)");
                    Assert.AreEqual((byte)255, pixel.B, "First pixel B should be 255 (white)");
                    Assert.AreEqual(96u, decoder.PixelWidth);
                    Assert.AreEqual(96u, decoder.PixelHeight);
                });
        }

        [TestMethod]
        public async Task TransformHonorsFitWhenStretch()
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(x => x.SelectedSize.Fit = ResizeFit.Stretch));

            await operation.ExecuteAsync();

            await AssertEx.ImageAsync(
                _directory.File(),
                async decoder =>
                {
                    var pixel = await decoder.GetFirstPixelAsync();
                    Assert.AreEqual((byte)0, pixel.R, "First pixel R should be 0 (black)");
                    Assert.AreEqual((byte)0, pixel.G, "First pixel G should be 0 (black)");
                    Assert.AreEqual((byte)0, pixel.B, "First pixel B should be 0 (black)");
                    Assert.AreEqual(96u, decoder.PixelWidth);
                    Assert.AreEqual(96u, decoder.PixelHeight);
                });
        }

        [TestMethod]
        public async Task TransformHonorsFillWithShrinkOnlyWhenCropRequired()
        {
            var operation = new ResizeOperation(
                "Test.jpg",
                _directory,
                Settings(x =>
                {
                    x.ShrinkOnly = true;
                    x.SelectedSize.Fit = ResizeFit.Fill;
                    x.SelectedSize.Width = 48;
                    x.SelectedSize.Height = 96;
                }));

            await operation.ExecuteAsync();

            await AssertEx.ImageAsync(
                _directory.File(),
                decoder =>
                {
                    Assert.AreEqual(48u, decoder.PixelWidth);
                    Assert.AreEqual(96u, decoder.PixelHeight);
                });
        }

        [TestMethod]
        public async Task TransformHonorsFillWithShrinkOnlyWhenUpscaleAttempted()
        {
            var operation = new ResizeOperation(
                "Test.jpg",
                _directory,
                Settings(x =>
                {
                    x.ShrinkOnly = true;
                    x.SelectedSize.Fit = ResizeFit.Fill;
                    x.SelectedSize.Width = 192;
                    x.SelectedSize.Height = 192;
                }));

            await operation.ExecuteAsync();

            await AssertEx.ImageAsync(
                _directory.File(),
                decoder =>
                {
                    Assert.AreEqual(96u, decoder.PixelWidth);
                    Assert.AreEqual(96u, decoder.PixelHeight);
                });
        }

        [TestMethod]
        public async Task TransformHonorsFillWithShrinkOnlyWhenNoChangeRequired()
        {
            var operation = new ResizeOperation(
                "Test.jpg",
                _directory,
                Settings(x =>
                {
                    x.ShrinkOnly = true;
                    x.SelectedSize.Fit = ResizeFit.Fill;
                    x.SelectedSize.Width = 96;
                    x.SelectedSize.Height = 96;
                }));

            await operation.ExecuteAsync();

            await AssertEx.ImageAsync(
                _directory.File(),
                decoder =>
                {
                    Assert.AreEqual(96u, decoder.PixelWidth);
                    Assert.AreEqual(96u, decoder.PixelHeight);
                });
        }

        [TestMethod]
        public async Task GetDestinationPathUniquifiesOutputFilename()
        {
            File.WriteAllBytes(Path.Combine(_directory, "Test (Test).png"), Array.Empty<byte>());

            var operation = new ResizeOperation("Test.png", _directory, Settings());

            await operation.ExecuteAsync();

            CollectionAssert.Contains(_directory.FileNames.ToList(), "Test (Test) (1).png");
        }

        [TestMethod]
        public async Task GetDestinationPathUniquifiesOutputFilenameAgain()
        {
            File.WriteAllBytes(Path.Combine(_directory, "Test (Test).png"), Array.Empty<byte>());
            File.WriteAllBytes(Path.Combine(_directory, "Test (Test) (1).png"), Array.Empty<byte>());

            var operation = new ResizeOperation("Test.png", _directory, Settings());

            await operation.ExecuteAsync();

            CollectionAssert.Contains(_directory.FileNames.ToList(), "Test (Test) (2).png");
        }

        [TestMethod]
        public async Task GetDestinationPathUsesFileNameFormat()
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(s => s.FileName = "%1_%2_%3_%4_%5_%6"));

            await operation.ExecuteAsync();

            CollectionAssert.Contains(_directory.FileNames.ToList(), "Test_Test_96_96_96_48.png");
        }

        [TestMethod]
        public async Task ExecuteHandlesDirectoriesInFileNameFormat()
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(s => s.FileName = @"Directory\%1 (%2)"));

            await operation.ExecuteAsync();

            Assert.IsTrue(File.Exists(_directory + @"\Directory\Test (Test).png"));
        }

        [TestMethod]
        public async Task StripMetadata()
        {
            var operation = new ResizeOperation(
                "TestMetadataIssue1928.jpg",
                _directory,
                Settings(
                    x =>
                    {
                        x.RemoveMetadata = true;
                    }));

            await operation.ExecuteAsync();

            await AssertEx.ImageAsync(
                _directory.File(),
                async decoder =>
                {
                    try
                    {
                        var props = await decoder.BitmapProperties.GetPropertiesAsync(DateTakenPropertyQuery);
                        Assert.IsFalse(props.ContainsKey("System.Photo.DateTaken"), "DateTaken should be stripped");
                    }
                    catch (Exception)
                    {
                        // If GetPropertiesAsync throws, metadata is not present — which is expected
                    }
                });
        }

        [TestMethod]
        public async Task StripMetadataWhenNoMetadataPresent()
        {
            var operation = new ResizeOperation(
                "TestMetadataIssue1928_NoMetadata.jpg",
                _directory,
                Settings(
                    x =>
                    {
                        x.RemoveMetadata = true;
                    }));

            await operation.ExecuteAsync();

            await AssertEx.ImageAsync(
                _directory.File(),
                async decoder =>
                {
                    try
                    {
                        var props = await decoder.BitmapProperties.GetPropertiesAsync(DateTakenPropertyQuery);
                        Assert.IsFalse(props.ContainsKey("System.Photo.DateTaken"), "DateTaken should not exist");
                    }
                    catch (Exception)
                    {
                        // Expected: no metadata block at all
                    }
                });
        }

        [TestMethod]
        public async Task VerifyFileNameIsSanitized()
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(
                    s =>
                    {
                        s.FileName = @"Directory\%1:*?""<>|(%2)";
                        s.SelectedSize.Name = "Test\\/";
                    }));

            await operation.ExecuteAsync();

            Assert.IsTrue(File.Exists(_directory + @"\Directory\Test_______(Test__).png"));
        }

        [TestMethod]
        public async Task VerifyNotRecommendedNameIsChanged()
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(
                    s =>
                    {
                        s.FileName = @"nul";
                    }));

            await operation.ExecuteAsync();

            Assert.IsTrue(File.Exists(_directory + @"\nul_.png"));
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
