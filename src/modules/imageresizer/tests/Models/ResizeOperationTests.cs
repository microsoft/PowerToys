#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ImageResizer.Properties;
using ImageResizer.Test;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Graphics.Imaging;

namespace ImageResizer.Models
{
    [TestClass]
    public class ResizeOperationTests : IDisposable
    {
        // Known legacy container format GUID for PNG, used as FallbackEncoder value in settings JSON
        private static readonly Guid PngContainerFormatGuid = new Guid("1b7cfaf4-713f-473c-bbcd-6137425faeaf");

        private static readonly string[] CommentPropertyQuery = new[] { "System.Comment" };
        private static readonly string[] DateTakenPropertyQuery = new[] { "System.Photo.DateTaken" };
        private static readonly string[] CameraModelPropertyQuery = new[] { "System.Photo.CameraModel" };
        private static readonly string[] OriginalPhotoFileNames = new[] { "photo.jpg" };
        private static readonly string[] BatchSourceFileNames = new[] { "A.jpg", "B.jpg", "C.jpg" };
        private static readonly string[] BatchOutputFileNames = new[] { "A (Small).jpg", "B (Small).jpg", "C (Small).jpg" };
        private static readonly string[] CorruptBatchSourceFileNames = new[] { "corrupt.jpg", "good.jpg" };
        private static readonly string[] CorruptBatchOutputFileNames = new[] { "good (Small).jpg" };
        private static readonly string[] InchTokenOutputFileNames = new[] { "4_3_384_288.png" };
        private static readonly string[] OriginalPngPhotoFileNames = new[] { "photo.png" };

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
                    var props = await decoder.BitmapProperties.GetPropertiesAsync(CommentPropertyQuery);
                    Assert.IsTrue(props.ContainsKey("System.Comment"), "Comment metadata should be preserved during transcode");
                    Assert.AreEqual("Test", (string)props["System.Comment"].Value, "Comment value should be preserved");
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
            var expectedDateModified = new DateTime(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(path, expectedDateModified);
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

            Assert.AreEqual(expectedDateModified, originalDateModified);
            Assert.AreEqual(originalDateModified, File.GetLastWriteTimeUtc(_directory.File()));
            await AssertEx.ImageAsync(_directory.File(), decoder => Assert.AreEqual(96u, decoder.PixelWidth));
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
        public async Task TransformRejectsScaledDimensionsAboveInt32RangeBeforeWriting()
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(
                    settings =>
                    {
                        settings.SelectedSize.Fit = ResizeFit.Stretch;
                        settings.SelectedSize.Width = (double)int.MaxValue + 1;
                        settings.SelectedSize.Height = 100;
                    }));

            await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => operation.ExecuteAsync());

            Assert.AreEqual(0, _directory.FileNames.Count());
        }

        [TestMethod]
        public async Task TransformRejectsNegativeFractionalDimensionsBeforeRounding()
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(
                    settings =>
                    {
                        settings.SelectedSize.Fit = ResizeFit.Stretch;
                        settings.SelectedSize.Width = -0.4;
                        settings.SelectedSize.Height = 100;
                    }));

            await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => operation.ExecuteAsync());

            Assert.AreEqual(0, _directory.FileNames.Count());
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
        public async Task TransformRoundsPositiveFractionalFillDimensionsToAtLeastOnePixel()
        {
            var operation = new ResizeOperation(
                "TestPortrait.png",
                _directory,
                Settings(
                    settings =>
                    {
                        settings.SelectedSize.Fit = ResizeFit.Fill;
                        settings.SelectedSize.Width = 0.4;
                        settings.SelectedSize.Height = 100;
                    }));

            await operation.ExecuteAsync();

            await AssertEx.ImageAsync(
                _directory.File(),
                decoder =>
                {
                    Assert.AreEqual(1u, decoder.PixelWidth);
                    Assert.AreEqual(100u, decoder.PixelHeight);
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
            var basePath = Path.Combine(_directory, "Test (Test).png");
            var firstPath = Path.Combine(_directory, "Test (Test) (1).png");
            var baseBytes = new byte[] { 1, 2, 3 };
            var firstBytes = new byte[] { 4, 5, 6 };
            File.WriteAllBytes(basePath, baseBytes);
            File.WriteAllBytes(firstPath, firstBytes);

            var operation = new ResizeOperation("Test.png", _directory, Settings());

            await operation.ExecuteAsync();

            CollectionAssert.Contains(_directory.FileNames.ToList(), "Test (Test) (2).png");
            Assert.AreEqual(3, _directory.FileNames.Count());
            CollectionAssert.AreEqual(baseBytes, File.ReadAllBytes(basePath));
            CollectionAssert.AreEqual(firstBytes, File.ReadAllBytes(firstPath));
            await AssertEx.ImageAsync(
                Path.Combine(_directory, "Test (Test) (2).png"),
                decoder =>
                {
                    Assert.AreEqual(BitmapDecoder.PngDecoderId, decoder.DecoderInformation.CodecId);
                    Assert.AreEqual(96u, decoder.PixelWidth);
                    Assert.AreEqual(48u, decoder.PixelHeight);
                });
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
        [DataRow(false, false)]
        [DataRow(true, false)]
        [DataRow(false, true)]
        [DataRow(true, true)]
        public async Task ExecuteNamesCopyWithSelectedAndActualDimensionsWithoutChangingSource(bool useSourceDirectory, bool keepDateModified)
        {
            using var sourceDirectory = new TestDirectory();
            var source = Path.Combine(sourceDirectory, "photo.jpg");
            await CreateJpegFixtureAsync(source, 1200, 800);
            var expectedSourceTime = new DateTime(2020, 1, 15, 8, 30, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(source, expectedSourceTime);
            var sourceTime = File.GetLastWriteTimeUtc(source);
            Assert.AreEqual(expectedSourceTime, sourceTime, "Fixture qualification: fixed source date.");
            var sourceBytes = File.ReadAllBytes(source);
            var settings = Settings(s =>
            {
                s.SelectedSize.Name = "Small";
                s.SelectedSize.Width = 854;
                s.SelectedSize.Height = 480;
                s.SelectedSize.Fit = ResizeFit.Fit;
                s.IgnoreOrientation = false;
                s.Replace = false;
                s.KeepDateModified = keepDateModified;
                s.FileName = "%1 - %2 - %3 - %4 - %5 - %6";
            });
            var destination = useSourceDirectory ? sourceDirectory : _directory;
            var before = DateTime.UtcNow;

            await new ResizeOperation(source, useSourceDirectory ? null : destination, settings).ExecuteAsync();

            var after = DateTime.UtcNow;
            var output = Path.Combine(destination, "photo - Small - 854 - 480 - 720 - 480.jpg");
            CollectionAssert.AreEquivalent(
                useSourceDirectory ? new[] { "photo.jpg", Path.GetFileName(output) } : new[] { Path.GetFileName(output) },
                destination.FileNames.ToArray());
            if (!useSourceDirectory)
            {
                Assert.AreEqual(1, sourceDirectory.FileNames.Count());
            }

            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(source));
            Assert.AreEqual(sourceTime, File.GetLastWriteTimeUtc(source));
            var outputTime = File.GetLastWriteTimeUtc(output);
            if (keepDateModified)
            {
                Assert.AreEqual(sourceTime, outputTime);
            }
            else
            {
                // Retain the existing two-second filesystem timestamp-resolution allowance.
                Assert.IsTrue(outputTime >= before.AddSeconds(-2) && outputTime <= after.AddSeconds(2));
                Assert.AreNotEqual(sourceTime, outputTime);
            }

            await AssertEx.ImageAsync(
                output,
                decoder =>
                {
                    Assert.AreEqual(BitmapDecoder.JpegDecoderId, decoder.DecoderInformation.CodecId);
                    Assert.AreEqual(1u, decoder.FrameCount);
                    Assert.AreEqual(720u, decoder.PixelWidth);
                    Assert.AreEqual(480u, decoder.PixelHeight);
                });
        }

        [TestMethod]
        public async Task ProcessDefaultSmallBatchCreatesThreeLiteralSquareCopiesWithoutChangingSources()
        {
            using var sourceDirectory = new TestDirectory();
            var batch = new ResizeBatch
            {
                DestinationDirectory = _directory,
            };
            var sourceTime = new DateTime(2020, 1, 15, 8, 30, 0, DateTimeKind.Utc);
            foreach (var fileName in BatchSourceFileNames)
            {
                var source = Path.Combine(sourceDirectory, fileName);
                await CreateJpegFixtureAsync(source, 1000, 1000);
                File.SetLastWriteTimeUtc(source, sourceTime);
                batch.Files.Add(source);
            }

            var sources = batch.Files.ToArray();
            var sourceBytes = sources.Select(File.ReadAllBytes).ToArray();
            var sourceTimes = sources.Select(File.GetLastWriteTimeUtc).ToArray();
            Assert.IsTrue(sourceTimes.All(time => time == sourceTime), "Fixture qualification: fixed source dates.");
            var progress = new ConcurrentBag<(int Completed, double Total)>();

            var errors = await batch.ProcessAsync(
                (completed, total) => progress.Add((completed, total)),
                new Settings(),
                CancellationToken.None);

            Assert.AreEqual(0, errors.Count(), "The real three-image batch must not report errors.");
            CollectionAssert.AreEquivalent(BatchOutputFileNames, _directory.FileNames.ToArray(), "Exact default output names/count.");
            CollectionAssert.AreEquivalent(BatchSourceFileNames, sourceDirectory.FileNames.ToArray(), "No source may be removed or added.");
            Assert.AreEqual(3, progress.Count);
            Assert.IsTrue(progress.All(report => report.Total == 3 && report.Completed >= 1 && report.Completed <= 3));
            Assert.IsTrue(progress.Any(report => report.Completed == 3));
            for (var index = 0; index < sources.Length; index++)
            {
                CollectionAssert.AreEqual(sourceBytes[index], File.ReadAllBytes(sources[index]), "Product must not change source bytes.");
                Assert.AreEqual(sourceTimes[index], File.GetLastWriteTimeUtc(sources[index]), "Product must not change source dates.");
                await AssertEx.ImageAsync(Path.Combine(_directory, BatchOutputFileNames[index]), decoder =>
                {
                    Assert.AreEqual(BitmapDecoder.JpegDecoderId, decoder.DecoderInformation.CodecId);
                    Assert.AreEqual(1u, decoder.FrameCount);
                    Assert.AreEqual(480u, decoder.PixelWidth);
                    Assert.AreEqual(480u, decoder.PixelHeight);
                });
            }
        }

        [TestMethod]
        public async Task ProcessDefaultSmallBatchReportsCorruptInputAndCreatesValidCopyWithoutChangingSources()
        {
            using var sourceDirectory = new TestDirectory();
            var goodSource = Path.Combine(sourceDirectory, "good.jpg");
            var corruptSource = Path.Combine(sourceDirectory, "corrupt.jpg");
            await CreateJpegFixtureAsync(goodSource, 1000, 1000);
            File.WriteAllBytes(corruptSource, new byte[] { 0x4E, 0x4F, 0x54, 0x2D, 0x41, 0x4E, 0x2D, 0x49, 0x4D, 0x41, 0x47, 0x45 });
            var batch = new ResizeBatch
            {
                DestinationDirectory = _directory,
            };
            batch.Files.Add(corruptSource);
            batch.Files.Add(goodSource);
            var sources = batch.Files.ToArray();
            var expectedSourceTime = new DateTime(2020, 1, 15, 8, 30, 0, DateTimeKind.Utc);
            foreach (var source in sources)
            {
                File.SetLastWriteTimeUtc(source, expectedSourceTime);
            }

            var sourceBytes = sources.Select(File.ReadAllBytes).ToArray();
            var sourceTimes = sources.Select(File.GetLastWriteTimeUtc).ToArray();
            Assert.IsTrue(sourceTimes.All(time => time == expectedSourceTime), "Fixture qualification: fixed source dates.");
            var progress = new ConcurrentBag<(int Completed, double Total)>();
            var settings = new Settings
            {
                IgnoreOrientation = false,
                Replace = false,
                ShrinkOnly = false,
            };

            var errors = (await batch.ProcessAsync(
                (completed, total) => progress.Add((completed, total)),
                settings,
                CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(10))).ToArray();

            Assert.AreEqual(1, errors.Length, "Only the corrupt input must report an error.");
            Assert.AreEqual("corrupt.jpg", errors[0].File);
            Assert.IsFalse(string.IsNullOrWhiteSpace(errors[0].Error), "The product must provide a nonempty error message.");
            CollectionAssert.AreEqual(CorruptBatchOutputFileNames, _directory.FileNames.ToArray(), "Only the valid output may be created.");
            CollectionAssert.AreEquivalent(CorruptBatchSourceFileNames, sourceDirectory.FileNames.ToArray(), "No source may be removed or added.");
            Assert.AreEqual(2, progress.Count);
            Assert.IsTrue(progress.All(report => report.Total == 2 && report.Completed >= 1 && report.Completed <= 2));
            Assert.IsTrue(progress.Any(report => report.Completed == 2));
            for (var index = 0; index < sources.Length; index++)
            {
                CollectionAssert.AreEqual(sourceBytes[index], File.ReadAllBytes(sources[index]), "Product must not change source bytes.");
                Assert.AreEqual(sourceTimes[index], File.GetLastWriteTimeUtc(sources[index]), "Product must not change source dates.");
            }

            await AssertEx.ImageAsync(Path.Combine(_directory, "good (Small).jpg"), decoder =>
            {
                Assert.AreEqual(BitmapDecoder.JpegDecoderId, decoder.DecoderInformation.CodecId);
                Assert.AreEqual(1u, decoder.FrameCount);
                Assert.AreEqual(480u, decoder.PixelWidth);
                Assert.AreEqual(480u, decoder.PixelHeight);
            });
        }

        [TestMethod]
        [DataRow(1200, 800, false, 720, 480)]
        [DataRow(1200, 800, true, 720, 480)]
        [DataRow(100, 100, true, 100, 100)]
        [DataRow(100, 100, false, 480, 480)]
        public async Task ExecuteDefaultSmallCopiesLiteralPixelGeometry(
            int sourceWidth, int sourceHeight, bool shrinkOnly, int expectedWidth, int expectedHeight)
        {
            var settings = new Settings
            {
                ShrinkOnly = shrinkOnly,
            };

            await AssertLiteralCopyAsync(
                sourceWidth, sourceHeight, settings, "photo (Small).jpg", expectedWidth, expectedHeight);
        }

        [TestMethod]
        [DataRow(ResizeFit.Fit, ResizeUnit.Pixel, 400, 400, false, 400, 267)]
        [DataRow(ResizeFit.Fill, ResizeUnit.Pixel, 400, 400, false, 400, 400)]
        [DataRow(ResizeFit.Stretch, ResizeUnit.Pixel, 400, 400, false, 400, 400)]
        [DataRow(ResizeFit.Fit, ResizeUnit.Percent, 50, 200, false, 600, 400)]
        [DataRow(ResizeFit.Stretch, ResizeUnit.Pixel, 500, 300, false, 500, 300)]
        [DataRow(ResizeFit.Fit, ResizeUnit.Pixel, 100, 200, false, 100, 67)]
        [DataRow(ResizeFit.Fit, ResizeUnit.Pixel, 100, 200, true, 150, 100)]
        public async Task ExecuteCustomCopiesLiteralPixelGeometry(
            ResizeFit fit, ResizeUnit unit, int width, int height, bool ignoreOrientation, int expectedWidth, int expectedHeight)
        {
            var settings = new Settings
            {
                CustomSize = new CustomSize(fit, width, height, unit),
                IgnoreOrientation = ignoreOrientation,
                ShrinkOnly = false,
                Replace = false,
                KeepDateModified = false,
            };
            settings.SelectedSizeIndex = settings.Sizes.Count;

            await AssertLiteralCopyAsync(
                1200, 800, settings, "photo (Input_Custom).jpg", expectedWidth, expectedHeight);
        }

        [TestMethod]
        [DataRow(ResizeFit.Stretch, ResizeUnit.Centimeter, 10, 5, 96, 378, 189)]
        [DataRow(ResizeFit.Stretch, ResizeUnit.Centimeter, 10, 5, 120, 472, 236)]
        [DataRow(ResizeFit.Stretch, ResizeUnit.Inch, 4, 3, 96, 384, 288)]
        [DataRow(ResizeFit.Stretch, ResizeUnit.Inch, 4, 3, 120, 480, 360)]
        [DataRow(ResizeFit.Stretch, ResizeUnit.Pixel, 500, 300, 120, 500, 300)]
        [DataRow(ResizeFit.Fit, ResizeUnit.Percent, 50, 200, 120, 600, 400)]
        [DataRow(ResizeFit.Fill, ResizeUnit.Percent, 50, 200, 96, 600, 400)]
        [DataRow(ResizeFit.Fill, ResizeUnit.Percent, 50, 200, 120, 600, 400)]
        [DataRow(ResizeFit.Stretch, ResizeUnit.Percent, 50, 25, 96, 600, 200)]
        [DataRow(ResizeFit.Stretch, ResizeUnit.Percent, 50, 25, 120, 600, 200)]
        public async Task ExecuteCustomCopiesLiteralUnitGeometryAtDeclaredDpi(
            ResizeFit fit, ResizeUnit unit, int width, int height, int sourceDpi, int expectedWidth, int expectedHeight)
        {
            var settings = new Settings
            {
                CustomSize = new CustomSize(fit, width, height, unit),
                IgnoreOrientation = false,
                ShrinkOnly = false,
                Replace = false,
                KeepDateModified = false,
            };
            settings.SelectedSizeIndex = settings.Sizes.Count;

            await AssertLiteralCopyAsync(
                1200, 800, settings, "photo (Input_Custom).jpg", expectedWidth, expectedHeight, sourceDpi);
        }

        [TestMethod]
        public async Task ExecuteCustomCopiesLiteralInchGeometryAtIndependentAxisDpi()
        {
            var settings = new Settings
            {
                CustomSize = new CustomSize(ResizeFit.Stretch, 4, 3, ResizeUnit.Inch),
                IgnoreOrientation = false,
                ShrinkOnly = false,
                Replace = false,
                KeepDateModified = false,
            };
            settings.SelectedSizeIndex = settings.Sizes.Count;

            await AssertLiteralCopyAsync(
                1200, 800, settings, "photo (Input_Custom).jpg", 384, 360, sourceDpi: 96, sourceVerticalDpi: 120);
        }

        [TestMethod]
        [DataRow(ResizeFit.Fill, 192, 96, 96, 96, 96, 96)]
        [DataRow(ResizeFit.Fill, 100, 100, 200, 200, 100, 100)]
        [DataRow(ResizeFit.Stretch, 100, 100, 50, 200, 100, 100)]
        [DataRow(ResizeFit.Fit, 100, 100, 100, 100, 100, 100)]
        public async Task ExecuteCustomCopiesLiteralShrinkOnlyPixelGeometry(
            ResizeFit fit, int sourceWidth, int sourceHeight, int width, int height, int expectedWidth, int expectedHeight)
        {
            var settings = new Settings
            {
                CustomSize = new CustomSize(fit, width, height, ResizeUnit.Pixel),
                IgnoreOrientation = false,
                ShrinkOnly = true,
                Replace = false,
                KeepDateModified = false,
            };
            settings.SelectedSizeIndex = settings.Sizes.Count;

            await AssertLiteralCopyAsync(
                sourceWidth, sourceHeight, settings, "photo (Input_Custom).jpg", expectedWidth, expectedHeight);
        }

        [TestMethod]
        [DataRow(800, 1200, 200, 100, 100, 150)]
        [DataRow(100, 100, 100, 200, 100, 100)]
        public async Task ExecuteCustomCopiesLiteralOrientationPixelGeometry(
            int sourceWidth, int sourceHeight, int width, int height, int expectedWidth, int expectedHeight)
        {
            var settings = new Settings
            {
                CustomSize = new CustomSize(ResizeFit.Fit, width, height, ResizeUnit.Pixel),
                IgnoreOrientation = true,
                ShrinkOnly = false,
                Replace = false,
                KeepDateModified = false,
            };
            settings.SelectedSizeIndex = settings.Sizes.Count;

            await AssertLiteralCopyAsync(
                sourceWidth, sourceHeight, settings, "photo (Input_Custom).jpg", expectedWidth, expectedHeight);
        }

        [TestMethod]
        [DataRow(ResizeFit.Fit, 400d, 0d, true, 400, 267)]
        [DataRow(ResizeFit.Fit, 400d, double.NaN, true, 400, 267)]
        [DataRow(ResizeFit.Fit, 0d, 200d, true, 300, 200)]
        [DataRow(ResizeFit.Fit, double.NaN, 200d, true, 300, 200)]
        [DataRow(ResizeFit.Fill, 0d, 200d, false, 1200, 200)]
        [DataRow(ResizeFit.Stretch, 0d, 200d, false, 1200, 200)]
        [DataRow(ResizeFit.Stretch, 0.4, 100d, false, 1, 100)]
        public async Task ExecuteCustomCopiesLiteralAutoAndMinimumPixelGeometry(
            ResizeFit fit, double width, double height, bool ignoreOrientation, int expectedWidth, int expectedHeight)
        {
            var settings = new Settings
            {
                CustomSize = new CustomSize(fit, width, height, ResizeUnit.Pixel),
                IgnoreOrientation = ignoreOrientation,
                ShrinkOnly = false,
                Replace = false,
                KeepDateModified = false,
            };
            settings.SelectedSizeIndex = settings.Sizes.Count;

            await AssertLiteralCopyAsync(
                1200, 800, settings, "photo (Input_Custom).jpg", expectedWidth, expectedHeight);
        }

        [TestMethod]
        [DataRow(ResizeFit.Stretch, double.PositiveInfinity, 100d, false)]
        [DataRow(ResizeFit.Fit, 0d, 0d, false)]
        [DataRow(ResizeFit.Stretch, double.PositiveInfinity, 100d, true)]
        [DataRow(ResizeFit.Fit, 0d, 0d, true)]
        public async Task ExecuteCustomRejectsNonfinitePixelGeometryBeforeWriting(
            ResizeFit fit, double width, double height, bool replace)
        {
            using var sourceDirectory = new TestDirectory();
            var source = Path.Combine(sourceDirectory, "photo.jpg");
            await CreateJpegFixtureAsync(source, 1200, 800);
            File.SetLastWriteTimeUtc(source, new DateTime(2020, 1, 15, 8, 30, 0, DateTimeKind.Utc));
            var sourceTime = File.GetLastWriteTimeUtc(source);
            var sourceBytes = File.ReadAllBytes(source);
            var settings = new Settings
            {
                CustomSize = new CustomSize(fit, width, height, ResizeUnit.Pixel),
                IgnoreOrientation = false,
                ShrinkOnly = false,
                Replace = replace,
                KeepDateModified = false,
            };
            settings.SelectedSizeIndex = settings.Sizes.Count;
            var operation = new ResizeOperation(source, _directory, settings);

            await Assert.ThrowsExactlyAsync<ArgumentOutOfRangeException>(() => operation.ExecuteAsync());

            Assert.AreEqual(0, _directory.FileNames.Count(), "Product must reject dimensions before creating output.");
            CollectionAssert.AreEqual(OriginalPhotoFileNames, sourceDirectory.FileNames.ToArray(), "Product must retain only the original in the source folder.");
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(source), "Product must not change source bytes.");
            Assert.AreEqual(sourceTime, File.GetLastWriteTimeUtc(source), "Product must not change the source date.");
        }

        [TestMethod]
        [DataRow("en-US", "2.5_3.5_2_4.png")]
        [DataRow("de-DE", "2,5_3,5_2_4.png")]
        public async Task ExecuteFormatsFractionalDimensionsUsingCurrentCulture(string culture, string expectedName)
        {
            var originalCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var settings = Settings(s =>
                {
                    s.FileName = "%3_%4_%5_%6";
                    s.IgnoreOrientation = false;
                    s.SelectedSize.Fit = ResizeFit.Stretch;
                    s.SelectedSize.Unit = ResizeUnit.Pixel;
                    s.SelectedSize.Width = 2.5;
                    s.SelectedSize.Height = 3.5;
                });

                await new ResizeOperation("Test.png", _directory, settings).ExecuteAsync();

                CollectionAssert.AreEqual(new[] { expectedName }, _directory.FileNames.ToArray());
                await AssertEx.ImageAsync(
                    _directory.File(),
                    decoder =>
                    {
                        Assert.AreEqual(BitmapDecoder.PngDecoderId, decoder.DecoderInformation.CodecId);
                        Assert.AreEqual(2u, decoder.PixelWidth);
                        Assert.AreEqual(4u, decoder.PixelHeight);
                    });
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        [TestMethod]
        [DataRow("photo.edit.jpg", "%1_%1_%2", "photo.edit_photo.edit_Small.jpg")]
        [DataRow("photo.jpg", "%2", "Small.jpg")]
        [DataRow("photo.JPG", "%1", "photo.JPG")]
        public async Task ExecutePreservesBasenameTokensAndSupportedExtension(string sourceFileName, string format, string expectedName)
        {
            var settings = Settings(s =>
            {
                s.SelectedSize.Name = "Small";
                s.SelectedSize.Width = 854;
                s.SelectedSize.Height = 480;
                s.SelectedSize.Fit = ResizeFit.Fit;
                s.SelectedSize.Unit = ResizeUnit.Pixel;
                s.IgnoreOrientation = false;
                s.ShrinkOnly = false;
                s.Replace = false;
                s.FileName = format;
            });

            await AssertLiteralCopyAsync(1200, 800, settings, expectedName, 720, 480, sourceFileName: sourceFileName);
        }

        [TestMethod]
        public async Task ExecuteNamesInchesSeparatelyFromActualPixels()
        {
            using var sourceDirectory = new TestDirectory();
            var source = Path.Combine(sourceDirectory, "photo.png");
            await CreateCoordinateFixtureAsync(source, 1200, 800);
            var sourceBytes = File.ReadAllBytes(source);
            var sourceTime = File.GetLastWriteTimeUtc(source);
            var settings = Settings(s =>
            {
                s.SelectedSize.Name = "Print";
                s.SelectedSize.Width = 4;
                s.SelectedSize.Height = 3;
                s.SelectedSize.Unit = ResizeUnit.Inch;
                s.SelectedSize.Fit = ResizeFit.Stretch;
                s.IgnoreOrientation = false;
                s.ShrinkOnly = false;
                s.Replace = false;
                s.FileName = "%3_%4_%5_%6";
            });

            await new ResizeOperation(source, _directory, settings).ExecuteAsync();

            CollectionAssert.AreEqual(InchTokenOutputFileNames, _directory.FileNames.ToArray());
            await ReadPixels(_directory.File(), 384, 288);
            CollectionAssert.AreEqual(OriginalPngPhotoFileNames, sourceDirectory.FileNames.ToArray());
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(source));
            Assert.AreEqual(sourceTime, File.GetLastWriteTimeUtc(source));
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task ExecuteKeepsNativeEncoderDespiteConflictingFallback(bool pngSource)
        {
            using var sourceDirectory = new TestDirectory();
            var sourceFileName = pngSource ? "photo.png" : "photo.jpeg";
            var source = Path.Combine(sourceDirectory, sourceFileName);
            if (pngSource)
            {
                await CreateCoordinateFixtureAsync(source, 1200, 800);
            }
            else
            {
                await CreateJpegFixtureAsync(source, 1200, 800);
            }

            var sourceBytes = File.ReadAllBytes(source);
            var sourceTime = File.GetLastWriteTimeUtc(source);
            var settings = Settings(s =>
            {
                s.SelectedSize.Name = "Small";
                s.SelectedSize.Width = 854;
                s.SelectedSize.Height = 480;
                s.SelectedSize.Unit = ResizeUnit.Pixel;
                s.SelectedSize.Fit = ResizeFit.Fit;
                s.IgnoreOrientation = false;
                s.ShrinkOnly = false;
                s.Replace = false;
                s.FallbackEncoder = pngSource
                    ? new Guid("19e4a5aa-5662-4fc5-a0c0-1758028e1057")
                    : PngContainerFormatGuid;
            });

            await new ResizeOperation(source, _directory, settings).ExecuteAsync();

            var expectedName = pngSource ? "photo (Small).png" : "photo (Small).jpeg";
            CollectionAssert.AreEqual(new[] { expectedName }, _directory.FileNames.ToArray());
            await AssertEx.ImageAsync(_directory.File(), decoder =>
            {
                Assert.AreEqual(pngSource ? BitmapDecoder.PngDecoderId : BitmapDecoder.JpegDecoderId, decoder.DecoderInformation.CodecId);
                Assert.AreEqual(1u, decoder.FrameCount);
                Assert.AreEqual(720u, decoder.PixelWidth);
                Assert.AreEqual(480u, decoder.PixelHeight);
            });
            CollectionAssert.AreEqual(new[] { sourceFileName }, sourceDirectory.FileNames.ToArray());
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(source));
            Assert.AreEqual(sourceTime, File.GetLastWriteTimeUtc(source));
        }

        [TestMethod]
        public async Task GetDestinationPathFillsFirstSuffixHoleWithoutOverwritingFiles()
        {
            var basePath = Path.Combine(_directory, "Test (Test).png");
            var laterPath = Path.Combine(_directory, "Test (Test) (2).png");
            var baseBytes = new byte[] { 1, 2, 3 };
            var laterBytes = new byte[] { 4, 5, 6 };
            File.WriteAllBytes(basePath, baseBytes);
            File.WriteAllBytes(laterPath, laterBytes);

            await new ResizeOperation("Test.png", _directory, Settings()).ExecuteAsync();

            Assert.AreEqual(3, _directory.FileNames.Count());
            CollectionAssert.AreEqual(baseBytes, File.ReadAllBytes(basePath));
            CollectionAssert.AreEqual(laterBytes, File.ReadAllBytes(laterPath));
            await AssertEx.ImageAsync(
                Path.Combine(_directory, "Test (Test) (1).png"),
                decoder =>
                {
                    Assert.AreEqual(96u, decoder.PixelWidth);
                    Assert.AreEqual(48u, decoder.PixelHeight);
                });
        }

        [TestMethod]
        [DataRow("%7", "%7.png")]
        [DataRow("{%1}", "{Test}.png")]
        [DataRow("%%1", "%Test.png")]
        [DataRow("%10", "Test0.png")]
        [DataRow("%1_%1_%2", "Test_Test_Test.png")]
        public async Task ExecutePreservesFileNameFormatCompilerSemantics(string format, string expectedName)
        {
            await new ResizeOperation("Test.png", _directory, Settings(s => s.FileName = format)).ExecuteAsync();

            CollectionAssert.AreEqual(new[] { expectedName }, _directory.FileNames.ToArray());
            await AssertEx.ImageAsync(
                _directory.File(),
                decoder =>
                {
                    Assert.AreEqual(96u, decoder.PixelWidth);
                    Assert.AreEqual(48u, decoder.PixelHeight);
                });
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
            var output = _directory + @"\Directory\Test (Test).png";
            Assert.AreEqual(1, Directory.EnumerateFiles(_directory, "*", SearchOption.AllDirectories).Count());
            await ReadPixels(output, 96, 48);
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
            var output = _directory + @"\Directory\Test_______(Test__).png";
            Assert.AreEqual(1, Directory.EnumerateFiles(_directory, "*", SearchOption.AllDirectories).Count());
            await ReadPixels(output, 96, 48);
        }

        [TestMethod]
        [DataRow("nul", "nul_")]
        [DataRow("NUL", "NUL_")]
        [DataRow("CON", "CON_")]
        [DataRow("com1", "com1_")]
        [DataRow("LPT9", "LPT9_")]
        [DataRow("console", "console")]
        public async Task VerifyNotRecommendedNameIsChanged(string format, string expectedBasename)
        {
            var operation = new ResizeOperation(
                "Test.png",
                _directory,
                Settings(
                    s =>
                    {
                        s.FileName = format;
                    }));

            await operation.ExecuteAsync();

            if (format == "nul")
            {
                Assert.IsTrue(File.Exists(_directory + @"\nul_.png"));
            }

            var expectedName = expectedBasename + ".png";
            CollectionAssert.AreEqual(new[] { expectedName }, _directory.FileNames.ToArray());
            await ReadPixels(_directory.File(), 96, 48);
        }

        [TestMethod]
        [DataRow(ResizeFit.Fit, 400, 267)]
        [DataRow(ResizeFit.Stretch, 400, 400)]
        public async Task ExecuteFitAndStretchMatchFullSourceNativeReference(ResizeFit fit, int expectedWidth, int expectedHeight)
        {
            using var sourceDirectory = new TestDirectory();
            var source = Path.Combine(sourceDirectory, "coordinates.png");
            await CreateCoordinateFixtureAsync(source, 1200, 800);
            byte[] expected = null;
            await AssertEx.ImageAsync(source, async decoder =>
            {
                var transform = new BitmapTransform
                {
                    ScaledWidth = (uint)expectedWidth,
                    ScaledHeight = (uint)expectedHeight,
                    InterpolationMode = BitmapInterpolationMode.Fant,
                };
                var pixels = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    transform,
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage);
                expected = pixels.DetachPixelData();
                Assert.AreEqual(expectedWidth * expectedHeight * 4, expected.Length, "Full-source reference buffer length.");

                var croppedTransform = new BitmapTransform
                {
                    ScaledWidth = 600,
                    ScaledHeight = 400,
                    InterpolationMode = BitmapInterpolationMode.Fant,
                    Bounds = new BitmapBounds
                    {
                        X = 100,
                        Y = 0,
                        Width = 400,
                        Height = (uint)expectedHeight,
                    },
                };
                var croppedPixels = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    croppedTransform,
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage);
                var cropped = croppedPixels.DetachPixelData();
                Assert.AreEqual(expected.Length, cropped.Length, "Incorrect-crop control must have the same dimensions.");
                Assert.IsFalse(expected.SequenceEqual(cropped), "Fixture must distinguish a same-size cropped reference.");
                Console.WriteLine($"Full-source reference qualified: source=1200x800; fit={fit}; output={expectedWidth}x{expectedHeight}; bytes={expected.Length}; same-size crop differs.");
            });

            var sourceBytes = File.ReadAllBytes(source);
            var settings = CreateCoordinateSettings(fit, 400, 400);
            await new ResizeOperation(source, _directory, settings).ExecuteAsync();

            var actual = await ReadPixels(_directory.File(), expectedWidth, expectedHeight);
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(source), "Source must remain unchanged.");
            CollectionAssert.AreEqual(expected, actual, "Output must match every BGRA byte of the uncropped full-source native reference.");
        }

        [TestMethod]
        [DataRow(1200, 800, 600, 400, 100, 0)]
        [DataRow(800, 1200, 400, 600, 0, 100)]
        [DataRow(1202, 800, 601, 400, 100, 0)]
        public async Task ExecuteFillMatchesExactCenteredReference(
            int sourceWidth, int sourceHeight, int referenceWidth, int referenceHeight, int cropX, int cropY)
        {
            using var sourceDirectory = new TestDirectory();
            using var referenceDirectory = new TestDirectory();
            using var fillDirectory = new TestDirectory();
            var source = Path.Combine(sourceDirectory, "coordinates.png");
            await CreateCoordinateFixtureAsync(source, sourceWidth, sourceHeight);
            var referenceSettings = CreateCoordinateSettings(ResizeFit.Stretch, referenceWidth, referenceHeight);
            var fillSettings = CreateCoordinateSettings(ResizeFit.Fill, 400, 400);
            await new ResizeOperation(source, referenceDirectory, referenceSettings).ExecuteAsync();
            await new ResizeOperation(source, fillDirectory, fillSettings).ExecuteAsync();
            var reference = await ReadPixels(referenceDirectory.File(), referenceWidth, referenceHeight);
            var actual = await ReadPixels(fillDirectory.File(), 400, 400);
            var expected = Rectangle(cropX, cropY);
            foreach (var shift in new[] { -5, -1, 1, 5 })
            {
                var shifted = Rectangle(cropX + (cropX != 0 ? shift : 0), cropY + (cropY != 0 ? shift : 0));
                Assert.IsFalse(expected.SequenceEqual(shifted), "Fixture cannot distinguish crop shift " + shift);
            }

            var mismatches = 0;
            var channelCounts = new int[4];
            for (var offset = 0; offset < expected.Length; offset++)
            {
                if (expected[offset] != actual[offset])
                {
                    if (mismatches < 24)
                    {
                        Console.WriteLine(
                            $"Mismatch x={(offset / 4) % 400},y={offset / 1600},BGRA={offset % 4}: expected={expected[offset]},actual={actual[offset]}");
                    }

                    mismatches++;
                    channelCounts[offset % 4]++;
                }
            }

            Console.WriteLine($"Source={sourceWidth}x{sourceHeight}; reference={referenceWidth}x{referenceHeight}; rectangle={cropX},{cropY},400,400; shifted +/-1,+/-5 all differ; mismatched channels={mismatches}; BGRA counts={string.Join(",", channelCounts)}");
            Assert.AreEqual(0, mismatches, "Native Fill ROI differs from the exact full-surface reference slice.");

            byte[] Rectangle(int x, int y)
            {
                var result = new byte[400 * 400 * 4];
                for (var row = 0; row < 400; row++)
                {
                    Buffer.BlockCopy(reference, (((y + row) * referenceWidth) + x) * 4, result, row * 1600, 1600);
                }

                return result;
            }
        }

        [TestMethod]
        [DataRow(ResizeFit.Fill, 400, 200)]
        [DataRow(ResizeFit.Stretch, 400, 200)]
        [DataRow(ResizeFit.Fit, 267, 133)]
        public async Task ExecutePreservesLiteralBandContent(ResizeFit fit, int expectedHeight, int sampleY)
        {
            using var sourceDirectory = new TestDirectory();
            var source = Path.Combine(sourceDirectory, "bands.png");
            var sourcePixels = new byte[1200 * 800 * 4];
            for (var y = 0; y < 800; y++)
            {
                for (var x = 0; x < 1200; x++)
                {
                    var offset = ((y * 1200) + x) * 4;
                    sourcePixels[offset] = x >= 1000 ? (byte)255 : (byte)0;
                    sourcePixels[offset + 1] = x >= 200 && x < 1000 ? (byte)255 : (byte)0;
                    sourcePixels[offset + 2] = x < 200 ? (byte)255 : (byte)0;
                    sourcePixels[offset + 3] = 255;
                }
            }

            await CreateUnprofiledPngFixtureAsync(source, 1200, 800, sourcePixels);
            CollectionAssert.AreEqual(sourcePixels, await ReadPixels(source, 1200, 800), "Decoded source must preserve every opaque band pixel.");
            var sourceBytes = File.ReadAllBytes(source);
            var sourceTime = File.GetLastWriteTimeUtc(source);
            var settings = CreateCoordinateSettings(fit, 400, 400);

            await new ResizeOperation(source, _directory, settings).ExecuteAsync();

            Assert.AreEqual("bands (Test).png", _directory.FileNames.Single());
            var actual = await ReadPixels(_directory.File(), 400, expectedHeight);
            var fill = fit == ResizeFit.Fill;
            AssertColor(10, sampleY, fill ? 0 : 255, fill ? 255 : 0, 0);
            AssertColor(200, sampleY, 0, 255, 0);
            AssertColor(390, sampleY, 0, fill ? 255 : 0, fill ? 0 : 255);
            AssertColor(0, 0, fill ? 0 : 255, fill ? 255 : 0, 0);
            AssertColor(399, 0, 0, fill ? 255 : 0, fill ? 0 : 255);
            AssertColor(0, expectedHeight - 1, fill ? 0 : 255, fill ? 255 : 0, 0);
            AssertColor(399, expectedHeight - 1, 0, fill ? 255 : 0, fill ? 0 : 255);
            Assert.AreEqual("bands.png", sourceDirectory.FileNames.Single());
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(source), "Source bytes must remain unchanged.");
            Assert.AreEqual(sourceTime, File.GetLastWriteTimeUtc(source), "Source date must remain unchanged.");

            void AssertColor(int x, int y, int red, int green, int blue)
            {
                var offset = ((y * 400) + x) * 4;
                Assert.AreEqual((byte)blue, actual[offset], $"Blue at {x},{y}.");
                Assert.AreEqual((byte)green, actual[offset + 1], $"Green at {x},{y}.");
                Assert.AreEqual((byte)red, actual[offset + 2], $"Red at {x},{y}.");
                Assert.AreEqual((byte)255, actual[offset + 3], $"Opaque alpha at {x},{y}.");
            }
        }

        [TestMethod]
        [DataRow(1200, 800, 0, 200, 0, 300, 1200, 200, false)]
        [DataRow(192, 96, 96, 96, 48, 0, 96, 96, true)]
        public async Task ExecuteScaleOneFillMatchesLiteralSourceRectangle(
            int sourceWidth, int sourceHeight, int targetWidth, int targetHeight, int cropX, int cropY, int expectedWidth, int expectedHeight, bool shrinkOnly)
        {
            using var sourceDirectory = new TestDirectory();
            var source = Path.Combine(sourceDirectory, "coordinates.png");
            await CreateCoordinateFixtureAsync(source, sourceWidth, sourceHeight);
            var reference = await ReadPixels(source, sourceWidth, sourceHeight);
            var expected = Rectangle(cropX, cropY);
            var shifts = new[] { -5, -1, 1, 5 };
            foreach (var shift in shifts)
            {
                var shifted = Rectangle(cropX + (cropX != 0 ? shift : 0), cropY + (cropY != 0 ? shift : 0));
                Assert.IsFalse(expected.SequenceEqual(shifted), "Fixture cannot distinguish crop shift " + shift);
            }

            var sourceBytes = File.ReadAllBytes(source);
            var sourceTime = File.GetLastWriteTimeUtc(source);
            var settings = CreateCoordinateSettings(ResizeFit.Fill, targetWidth, targetHeight);
            settings.ShrinkOnly = shrinkOnly;

            await new ResizeOperation(source, _directory, settings).ExecuteAsync();

            Assert.AreEqual("coordinates (Test).png", _directory.FileNames.Single());
            var actual = await ReadPixels(_directory.File(), expectedWidth, expectedHeight);
            CollectionAssert.AreEqual(expected, actual, "Scale-one Fill must preserve every BGRA channel of the literal source rectangle.");
            Assert.AreEqual("coordinates.png", sourceDirectory.FileNames.Single());
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(source), "Source bytes must remain unchanged.");
            Assert.AreEqual(sourceTime, File.GetLastWriteTimeUtc(source), "Source date must remain unchanged.");

            byte[] Rectangle(int x, int y)
            {
                var result = new byte[expectedWidth * expectedHeight * 4];
                for (var row = 0; row < expectedHeight; row++)
                {
                    Buffer.BlockCopy(reference, (((y + row) * sourceWidth) + x) * 4, result, row * expectedWidth * 4, expectedWidth * 4);
                }

                return result;
            }
        }

        private static async Task CreateCoordinateFixtureAsync(string source, int sourceWidth, int sourceHeight)
        {
            var sourcePixels = new byte[sourceWidth * sourceHeight * 4];
            for (var y = 0; y < sourceHeight; y++)
            {
                for (var x = 0; x < sourceWidth; x++)
                {
                    var offset = ((y * sourceWidth) + x) * 4;
                    sourcePixels[offset] = (byte)(x % 256);
                    sourcePixels[offset + 1] = (byte)(y % 256);
                    sourcePixels[offset + 2] = (byte)((x / 256) + (16 * (y / 256)));
                    sourcePixels[offset + 3] = 255;
                }
            }

            await CreateUnprofiledPngFixtureAsync(source, sourceWidth, sourceHeight, sourcePixels);
            CollectionAssert.AreEqual(sourcePixels, await ReadPixels(source, sourceWidth, sourceHeight));
            await AssertEx.ImageAsync(source, decoder =>
            {
                Assert.AreEqual(96d, decoder.DpiX, 0.05);
                Assert.AreEqual(96d, decoder.DpiY, 0.05);
                Console.WriteLine($"Fixture: unprofiled eight-bit opaque PNG; DPI={decoder.DpiX},{decoder.DpiY}; all coordinate bytes verified.");
            });
        }

        private static async Task CreateUnprofiledPngFixtureAsync(string source, int sourceWidth, int sourceHeight, byte[] sourcePixels)
        {
            using (var stream = File.Create(source))
            {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream.AsRandomAccessStream());
                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    (uint)sourceWidth,
                    (uint)sourceHeight,
                    96,
                    96,
                    sourcePixels);
                await encoder.FlushAsync();
            }

            var png = File.ReadAllBytes(source);
            Assert.AreEqual((byte)8, png[24], "The coordinate fixture must use eight-bit samples.");
            using var unprofiled = new MemoryStream();
            unprofiled.Write(png, 0, 8);
            for (var offset = 8; offset < png.Length;)
            {
                var length = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset, 4));
                var chunk = System.Text.Encoding.ASCII.GetString(png, offset + 4, 4);
                if (chunk == "sRGB" || chunk == "gAMA" || chunk == "cHRM")
                {
                    // The native encoder supplies color metadata; the oracle fixture must not.
                    offset += length + 12;
                    continue;
                }

                Assert.IsTrue(
                    chunk == "IHDR" || chunk == "pHYs" || chunk == "IDAT" || chunk == "IEND",
                    "Unexpected fixture PNG chunk: " + chunk);
                unprofiled.Write(png, offset, length + 12);
                offset += length + 12;
            }

            File.WriteAllBytes(source, unprofiled.ToArray());
        }

        private static Settings CreateCoordinateSettings(ResizeFit fit, int width, int height)
        {
            return Settings(s =>
            {
                s.SelectedSize.Fit = fit;
                s.SelectedSize.Unit = ResizeUnit.Pixel;
                s.SelectedSize.Width = width;
                s.SelectedSize.Height = height;
                s.IgnoreOrientation = false;
                s.ShrinkOnly = false;
                s.Replace = false;
                s.KeepDateModified = false;
                s.RemoveMetadata = true;
                s.PngInterlaceOption = PngInterlaceOption.Off;
            });
        }

        private static async Task<byte[]> ReadPixels(string path, int width, int height)
        {
            byte[] result = null;
            await AssertEx.ImageAsync(path, async decoder =>
            {
                Assert.AreEqual(BitmapDecoder.PngDecoderId, decoder.DecoderInformation.CodecId);
                Assert.AreEqual(1u, decoder.FrameCount);
                Assert.AreEqual((uint)width, decoder.PixelWidth);
                Assert.AreEqual((uint)height, decoder.PixelHeight);
                var data = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    new BitmapTransform(),
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage);
                result = data.DetachPixelData();
                Assert.AreEqual(width * height * 4, result.Length);
            });
            return result;
        }

        private static async Task CreateJpegFixtureAsync(string path, int width, int height, double dpi = 96, double? verticalDpi = null)
        {
            using (var stream = File.Create(path))
            {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream.AsRandomAccessStream());
                var pixels = new byte[width * height * 4];
                Array.Fill(pixels, (byte)255);
                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, (uint)width, (uint)height, dpi, verticalDpi ?? dpi, pixels);
                await encoder.FlushAsync();
            }

            await AssertEx.ImageAsync(path, decoder =>
            {
                Assert.AreEqual((uint)width, decoder.PixelWidth, "Fixture qualification: source width.");
                Assert.AreEqual((uint)height, decoder.PixelHeight, "Fixture qualification: source height.");
                Assert.AreEqual(BitmapDecoder.JpegDecoderId, decoder.DecoderInformation.CodecId, "Fixture qualification: source codec.");
                Assert.AreEqual(1u, decoder.FrameCount, "Fixture qualification: source frame count.");
                Assert.AreEqual(dpi, decoder.DpiX, "Fixture qualification: source horizontal DPI.");
                Assert.AreEqual(verticalDpi ?? dpi, decoder.DpiY, "Fixture qualification: source vertical DPI.");
            });
        }

        private async Task AssertLiteralCopyAsync(
            int sourceWidth, int sourceHeight, Settings settings, string expectedName, int expectedWidth, int expectedHeight, double sourceDpi = 96, double? sourceVerticalDpi = null, string sourceFileName = "photo.jpg")
        {
            using var sourceDirectory = new TestDirectory();
            var source = Path.Combine(sourceDirectory, sourceFileName);
            await CreateJpegFixtureAsync(source, sourceWidth, sourceHeight, sourceDpi, sourceVerticalDpi);
            File.SetLastWriteTimeUtc(source, new DateTime(2020, 1, 15, 8, 30, 0, DateTimeKind.Utc));
            var sourceTime = File.GetLastWriteTimeUtc(source);
            var sourceBytes = File.ReadAllBytes(source);

            await new ResizeOperation(source, _directory, settings).ExecuteAsync();

            CollectionAssert.AreEqual(new[] { expectedName }, _directory.FileNames.ToArray(), "Product output name/count.");
            CollectionAssert.AreEqual(new[] { sourceFileName }, sourceDirectory.FileNames.ToArray(), "Product must retain only the original in the source folder.");
            CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(source), "Product must not change source bytes.");
            Assert.AreEqual(sourceTime, File.GetLastWriteTimeUtc(source), "Product must not change the source date.");
            await AssertEx.ImageAsync(Path.Combine(_directory, expectedName), decoder =>
            {
                Assert.AreEqual(BitmapDecoder.JpegDecoderId, decoder.DecoderInformation.CodecId, "Product output codec.");
                Assert.AreEqual(1u, decoder.FrameCount, "Product output frame count.");
                Assert.AreEqual((uint)expectedWidth, decoder.PixelWidth, "Product output width.");
                Assert.AreEqual((uint)expectedHeight, decoder.PixelHeight, "Product output height.");
            });
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
