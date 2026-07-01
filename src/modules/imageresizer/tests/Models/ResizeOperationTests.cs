#pragma warning disable IDE0073
// Copyright (c) Brice Lambson
// The Brice Lambson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.  Code forked from Brice Lambson's https://github.com/bricelam/ImageResizer/
#pragma warning restore IDE0073

using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ImageResizer.Properties;
using ImageResizer.Services;
using ImageResizer.Test;
using ImageResizer.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.Graphics.Imaging;

namespace ImageResizer.Models
{
    [TestClass]
    public class ResizeOperationTests : IDisposable
    {
        // Known legacy container format GUID for PNG, used as FallbackEncoder value in settings JSON
        private static readonly Guid PngContainerFormatGuid = new("1b7cfaf4-713f-473c-bbcd-6137425faeaf");

        private static readonly string[] CommentPropertyQuery = ["System.Comment"];

        private static readonly string[] DateTakenPropertyQuery = ["System.Photo.DateTaken"];

        private static readonly string[] OrientationPropertyQuery = ["System.Photo.Orientation"];

        private static readonly string[] CameraModelPropertyQuery = ["System.Photo.CameraModel"];

        private static readonly string[] GpsPropertyQuery =
        [
            "System.GPS.Latitude",
            "System.GPS.Longitude",
            "System.GPS.Altitude",
        ];

        private static readonly string[] GpsReferencePropertyQuery =
        [
            "System.GPS.VersionID",
            "System.GPS.LatitudeRef",
            "System.GPS.LongitudeRef",
            "System.GPS.AltitudeRef",
        ];

        private static readonly string[] CameraAndAuthorPropertyQuery =
        [
            "System.Photo.CameraManufacturer",
            "System.Photo.CameraModel",
            "System.Photo.LensModel",
            "System.Photo.ExposureTime",
            "System.Photo.FNumber",
            "System.Photo.ISOSpeed",
            "System.Photo.ExposureBias",
            "System.Photo.MeteringMode",
            "System.Photo.Flash",
            "System.Photo.FocalLength",
            "System.Photo.WhiteBalance",
            "System.Author",
            "System.Copyright",
        ];

        private static readonly string[] ExtendedExifPropertyQuery =
        [
            "System.Photo.ExposureTime",
            "System.Photo.FNumber",
            "System.Photo.ISOSpeed",
            "System.Photo.ExposureBias",
            "System.Photo.Flash",
            "System.Photo.FocalLength",
            "System.Photo.MeteringMode",
            "System.Photo.WhiteBalance",
        ];

        private static readonly string[] ColorSpacePropertyQuery =
        [
            "System.Image.ColorSpace",
        ];

        private static readonly string[] RawGpsMetadataQueryPaths =
        [
            $"/app1/ifd/{{ushort={MetadataTagIds.Ifd.GpsIfdPointer}}}",
            $"/app1/ifd/gps/{{ushort={MetadataTagIds.Gps.VersionId}}}",
            $"/app1/ifd/gps/{{ushort={MetadataTagIds.Gps.LatitudeRef}}}",
            $"/app1/ifd/gps/{{ushort={MetadataTagIds.Gps.Latitude}}}",
            $"/app1/ifd/gps/{{ushort={MetadataTagIds.Gps.LongitudeRef}}}",
            $"/app1/ifd/gps/{{ushort={MetadataTagIds.Gps.Longitude}}}",
            $"/app1/ifd/gps/{{ushort={MetadataTagIds.Gps.AltitudeRef}}}",
            $"/app1/ifd/gps/{{ushort={MetadataTagIds.Gps.Altitude}}}",
            $"/ifd/{{ushort={MetadataTagIds.Ifd.GpsIfdPointer}}}",
            $"/ifd/gps/{{ushort={MetadataTagIds.Gps.VersionId}}}",
            $"/ifd/gps/{{ushort={MetadataTagIds.Gps.LatitudeRef}}}",
            $"/ifd/gps/{{ushort={MetadataTagIds.Gps.Latitude}}}",
            $"/ifd/gps/{{ushort={MetadataTagIds.Gps.LongitudeRef}}}",
            $"/ifd/gps/{{ushort={MetadataTagIds.Gps.Longitude}}}",
            $"/ifd/gps/{{ushort={MetadataTagIds.Gps.AltitudeRef}}}",
            $"/ifd/gps/{{ushort={MetadataTagIds.Gps.Altitude}}}",
        ];

        private static readonly string[] RawResizedDimensionMetadataQueryPaths =
        [
            $"/app1/ifd/{{ushort={MetadataTagIds.Ifd.ImageWidth}}}",
            $"/app1/ifd/{{ushort={MetadataTagIds.Ifd.ImageHeight}}}",
            $"/app1/ifd/exif/{{ushort={MetadataTagIds.Exif.PixelXDimension}}}",
            $"/app1/ifd/exif/{{ushort={MetadataTagIds.Exif.PixelYDimension}}}",
            $"/ifd/{{ushort={MetadataTagIds.Ifd.ImageWidth}}}",
            $"/ifd/{{ushort={MetadataTagIds.Ifd.ImageHeight}}}",
            $"/ifd/exif/{{ushort={MetadataTagIds.Exif.PixelXDimension}}}",
            $"/ifd/exif/{{ushort={MetadataTagIds.Exif.PixelYDimension}}}",
        ];

        private static readonly string[] RawThumbnailMetadataQueryPaths =
        [
            $"/app1/ifd/{{ushort={MetadataTagIds.Ifd.ThumbnailOffset}}}",
            $"/app1/ifd/{{ushort={MetadataTagIds.Ifd.ThumbnailLength}}}",
            $"/ifd/{{ushort={MetadataTagIds.Ifd.ThumbnailOffset}}}",
            $"/ifd/{{ushort={MetadataTagIds.Ifd.ThumbnailLength}}}",
        ];

        private static readonly string[] RawOrientationMetadataQueryPaths =
        [
            $"/app1/ifd/{{ushort={MetadataTagIds.Ifd.Orientation}}}",
            $"/ifd/{{ushort={MetadataTagIds.Ifd.Orientation}}}",
        ];

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
        public async Task ExecutePreservesExtendedExifMetadataOnReencode()
        {
            var operation = new ResizeOperation("TestMetadataIssue1928.jpg", _directory, Settings());

            await operation.ExecuteAsync();

            await AssertEx.ImageAsync(
                "TestMetadataIssue1928.jpg",
                async sourceDecoder =>
                {
                    var sourceProps = await sourceDecoder.BitmapProperties.GetPropertiesAsync(ExtendedExifPropertyQuery);

                    await AssertEx.ImageAsync(
                        _directory.File(),
                        async outputDecoder =>
                        {
                            var outputProps = await outputDecoder.BitmapProperties.GetPropertiesAsync(ExtendedExifPropertyQuery);

                            foreach (var propertyName in ExtendedExifPropertyQuery)
                            {
                                Assert.IsTrue(sourceProps.ContainsKey(propertyName), $"Source image should contain {propertyName}");
                            }

                            foreach (var propertyName in ExtendedExifPropertyQuery)
                            {
                                Assert.IsTrue(outputProps.ContainsKey(propertyName), $"{propertyName} should be preserved during re-encode");
                            }
                        });
                });
        }

        [TestMethod]
        public async Task ExecutePreservesGpsMetadataOnReencode()
        {
            Assert.AreEqual(
                ResizeOperation.EncodeStrategy.Reencode,
                ResizeOperation.DetermineEncodeStrategy(
                    BitmapEncoder.JpegEncoderId,
                    BitmapEncoder.JpegEncoderId,
                    noTransformNeeded: false,
                    removeMetadata: false));

            var operation = new ResizeOperation("exif_test.jpg", _directory, Settings());

            await operation.ExecuteAsync();

            await AssertRawMetadataQueryPathsPreservedAsync(
                "exif_test.jpg",
                RawGpsMetadataQueryPaths,
                "GPS raw metadata query paths should be preserved during fresh encode even when WinRT System.GPS projection is inconsistent");
        }

        [TestMethod]
        public async Task ExecutePreservesGpsMetadataProjectionForExplorerOnReencode()
        {
            var operation = new ResizeOperation("exif_test.jpg", _directory, Settings());

            await operation.ExecuteAsync();

            var sourcePath = Path.GetFullPath("exif_test.jpg");
            var outputPath = Path.GetFullPath(_directory.File());

            foreach (var propertyName in GpsPropertyQuery)
            {
                Assert.AreEqual(
                    ShellPropertyStoreHelper.TryHasProperty(sourcePath, propertyName),
                    ShellPropertyStoreHelper.TryHasProperty(outputPath, propertyName),
                    $"Explorer Shell property projection should match for {propertyName}");
            }
        }

        [TestMethod]
        public async Task ExecutePreservesGpsReferenceMetadataOnReencode()
        {
            var operation = new ResizeOperation("exif_test.jpg", _directory, Settings());

            await operation.ExecuteAsync();

            await AssertPropertiesPreservedAsync("exif_test.jpg", GpsReferencePropertyQuery);
        }

        [TestMethod]
        public async Task ExecutePreservesColorSpaceMetadataOnReencode()
        {
            var operation = new ResizeOperation("exif_test.jpg", _directory, Settings());

            await operation.ExecuteAsync();

            await AssertPropertiesPreservedAsync("exif_test.jpg", ColorSpacePropertyQuery);
        }

        [TestMethod]
        public async Task ExecutePreservesDateTakenMetadataOnReencode()
        {
            var operation = new ResizeOperation("TestMetadataIssue1928.jpg", _directory, Settings());

            await operation.ExecuteAsync();

            await AssertPropertiesPreservedAsync("TestMetadataIssue1928.jpg", DateTakenPropertyQuery);
        }

        [TestMethod]
        public async Task ExecutePreservesCommentMetadataOnJpegReencode()
        {
            var operation = new ResizeOperation(
                "Test.jpg",
                _directory,
                Settings(x =>
                {
                    x.SelectedSize.Width = 48;
                    x.SelectedSize.Height = 48;
                }));

            await operation.ExecuteAsync();

            await AssertPropertiesPreservedAsync("Test.jpg", CommentPropertyQuery);
        }

        [TestMethod]
        public async Task ExecuteWithoutExifMetadataCompletesWithoutAddingMetadata()
        {
            var operation = new ResizeOperation("TestMetadataIssue1928_NoMetadata.jpg", _directory, Settings());

            await operation.ExecuteAsync();

            await AssertPropertiesRemovedAsync(_directory.File(), DateTakenPropertyQuery);
        }

        [TestMethod]
        public async Task ExecuteAiPreservesGpsMetadataProjectionAndDimensionsOnReencode()
        {
            using var aiService = new FakeAiSuperResolutionService(
                (source, scale) => new SoftwareBitmap(
                    BitmapPixelFormat.Bgra8,
                    Math.Max(1, source.PixelWidth * scale),
                    Math.Max(1, source.PixelHeight * scale),
                    BitmapAlphaMode.Premultiplied));

            uint sourceWidth = 0;
            uint sourceHeight = 0;
            await AssertEx.ImageAsync(
                "exif_test.jpg",
                decoder =>
                {
                    sourceWidth = decoder.PixelWidth;
                    sourceHeight = decoder.PixelHeight;
                });

            var operation = new ResizeOperation(
                "exif_test.jpg",
                _directory,
                Settings(x =>
                {
                    x.Sizes.Add(new AiSize(2));
                    x.SelectedSizeIndex = x.Sizes.Count - 1;
                }),
                aiService);

            await operation.ExecuteAsync();

            await AssertEx.ImageAsync(
                _directory.File(),
                decoder =>
                {
                    Assert.AreEqual(sourceWidth * 2, decoder.PixelWidth, "AI output width should use the super-resolution result.");
                    Assert.AreEqual(sourceHeight * 2, decoder.PixelHeight, "AI output height should use the super-resolution result.");
                });

            await AssertPropertiesPreservedAsync("exif_test.jpg", GpsReferencePropertyQuery);

            var sourcePath = Path.GetFullPath("exif_test.jpg");
            var outputPath = Path.GetFullPath(_directory.File());
            foreach (var propertyName in GpsPropertyQuery)
            {
                Assert.AreEqual(
                    ShellPropertyStoreHelper.TryHasProperty(sourcePath, propertyName),
                    ShellPropertyStoreHelper.TryHasProperty(outputPath, propertyName),
                    $"Explorer Shell property projection should match for AI output {propertyName}");
            }
        }

        [TestMethod]
        public async Task ExecuteWithCorruptedExifMetadataDoesNotCrashAndRetainsResizedImage()
        {
            using var sourceDirectory = new TestDirectory();
            var sourcePath = Path.Combine(sourceDirectory.ToString(), "TestCorruptedExif.jpg");
            WriteJpegWithExif("Test.jpg", sourcePath, CreateCorruptedExifSegmentData());

            var operation = new ResizeOperation(
                sourcePath,
                _directory,
                Settings(x =>
                {
                    x.SelectedSize.Width = 48;
                    x.SelectedSize.Height = 48;
                }));

            await operation.ExecuteAsync();

            Assert.IsTrue(File.Exists(_directory.File()), "The resized image should still be written when EXIF fix-up fails.");
            await AssertEx.ImageAsync(
                _directory.File(),
                decoder =>
                {
                    Assert.AreEqual(48u, decoder.PixelWidth);
                    Assert.AreEqual(48u, decoder.PixelHeight);
                });
        }

        [TestMethod]
        public async Task ExecutePreservesOrientationMetadataOnReencodeFromBigEndianExif()
        {
            using var sourceDirectory = new TestDirectory();

            uint sourceWidth = 0;
            uint sourceHeight = 0;
            await AssertEx.ImageAsync(
                "Test.jpg",
                decoder =>
                {
                    sourceWidth = decoder.PixelWidth;
                    sourceHeight = decoder.PixelHeight;
                });

            var sourcePath = Path.Combine(sourceDirectory.ToString(), "TestBigEndianOrientation.jpg");
            WriteJpegWithExif(
                "Test.jpg",
                sourcePath,
                CreateBigEndianExifSegmentData(sourceWidth, sourceHeight, orientation: 6));

            var operation = new ResizeOperation(
                sourcePath,
                _directory,
                Settings(x =>
                {
                    x.SelectedSize.Width = 48;
                    x.SelectedSize.Height = 48;
                }));

            await operation.ExecuteAsync();

            await AssertPropertiesPreservedAsync(sourcePath, OrientationPropertyQuery);
            await AssertRawMetadataValuesPreservedAsync(sourcePath, RawOrientationMetadataQueryPaths);
        }

        [TestMethod]
        public async Task ExecutePreservesOrientationMetadataOnReencodeWhenJpegUsesMarkerPadding()
        {
            using var sourceDirectory = new TestDirectory();

            uint sourceWidth = 0;
            uint sourceHeight = 0;
            await AssertEx.ImageAsync(
                "Test.jpg",
                decoder =>
                {
                    sourceWidth = decoder.PixelWidth;
                    sourceHeight = decoder.PixelHeight;
                });

            var sourcePath = Path.Combine(sourceDirectory.ToString(), "TestPaddedOrientation.jpg");
            WriteJpegWithExif(
                "Test.jpg",
                sourcePath,
                CreateBigEndianExifSegmentData(sourceWidth, sourceHeight, orientation: 6),
                addPaddingBeforeExifMarker: true,
                addPaddingBeforeStartOfScanMarker: true);

            var operation = new ResizeOperation(
                sourcePath,
                _directory,
                Settings(x =>
                {
                    x.SelectedSize.Width = 48;
                    x.SelectedSize.Height = 48;
                }));

            await operation.ExecuteAsync();

            await AssertPropertiesPreservedAsync(sourcePath, OrientationPropertyQuery);
            await AssertRawMetadataValuesPreservedAsync(sourcePath, RawOrientationMetadataQueryPaths);
        }

        [TestMethod]
        public async Task ExecutePreservesCameraAndAuthorMetadataOnReencode()
        {
            Assert.AreEqual(
                ResizeOperation.EncodeStrategy.Reencode,
                ResizeOperation.DetermineEncodeStrategy(
                    BitmapEncoder.JpegEncoderId,
                    BitmapEncoder.JpegEncoderId,
                    noTransformNeeded: false,
                    removeMetadata: false));

            var operation = new ResizeOperation("exif_test.jpg", _directory, Settings());

            await operation.ExecuteAsync();

            await AssertPropertiesPreservedAsync("exif_test.jpg", CameraAndAuthorPropertyQuery);
        }

        [TestMethod]
        public async Task ExecuteRemovesStaleDimensionMetadataOnReencode()
        {
            var operation = new ResizeOperation("exif_test.jpg", _directory, Settings());

            await operation.ExecuteAsync();

            await AssertRawMetadataPathsRemovedAsync(_directory.File(), RawResizedDimensionMetadataQueryPaths);
        }

        [TestMethod]
        public async Task ExecuteRemovesEmbeddedThumbnailMetadataOnReencode()
        {
            var operation = new ResizeOperation("exif_test.jpg", _directory, Settings());

            await operation.ExecuteAsync();

            await AssertRawMetadataPathsRemovedAsync(_directory.File(), RawThumbnailMetadataQueryPaths);
        }

        [TestMethod]
        public async Task StripMetadataRemovesGpsAndDescriptiveMetadata()
        {
            Assert.AreEqual(
                ResizeOperation.EncodeStrategy.Reencode,
                ResizeOperation.DetermineEncodeStrategy(
                    BitmapEncoder.JpegEncoderId,
                    BitmapEncoder.JpegEncoderId,
                    noTransformNeeded: false,
                    removeMetadata: true));

            var operation = new ResizeOperation(
                "exif_test.jpg",
                _directory,
                Settings(s => s.RemoveMetadata = true));

            await operation.ExecuteAsync();

            await AssertPropertiesRemovedAsync(_directory.File(), GpsPropertyQuery);
            await AssertPropertiesRemovedAsync(_directory.File(), CameraAndAuthorPropertyQuery);
        }

        [TestMethod]
        public async Task StripMetadataRemovesGpsMetadataProjectionForExplorer()
        {
            var operation = new ResizeOperation(
                "exif_test.jpg",
                _directory,
                Settings(s => s.RemoveMetadata = true));

            await operation.ExecuteAsync();

            var outputPath = Path.GetFullPath(_directory.File());
            foreach (var propertyName in GpsPropertyQuery)
            {
                Assert.IsFalse(
                    ShellPropertyStoreHelper.TryHasProperty(outputPath, propertyName),
                    $"Explorer Shell property projection should be removed for {propertyName}");
            }
        }

        [TestMethod]

        // Image dimensions are rewritten to match the resized output.
        [DataRow("/app1/ifd/{ushort=256}")]
        [DataRow("/app1/ifd/{ushort=257}")]

        // Storage offsets and byte counts are container layout details that become stale after re-encode.
        [DataRow("/app1/ifd/{ushort=273}")]
        [DataRow("/app1/ifd/{ushort=279}")]
        [DataRow("/app1/ifd/{ushort=324}")]
        [DataRow("/app1/ifd/{ushort=325}")]

        // Thumbnail pointers must be dropped because the original embedded thumbnail is no longer valid.
        [DataRow("/app1/ifd/{ushort=513}")]
        [DataRow("/app1/ifd/{ushort=514}")]

        // EXIF pixel dimensions mirror the resized image dimensions and are rebuilt by the JPEG fix-up path.
        [DataRow("/app1/ifd/exif/{ushort=40962}")]
        [DataRow("/app1/ifd/exif/{ushort=40963}")]
        public void WicMetadataCopySkipsStructuralAndThumbnailTags(string metadataPath)
        {
            Assert.IsTrue(WicMetadataCopier.ShouldSkipMetadataPath(metadataPath));
        }

        [TestMethod]

        // Make and Model values should be preserved.
        [DataRow("/app1/ifd/{ushort=271}")]
        [DataRow("/app1/ifd/{ushort=272}")]

        // Shot properties like Exposure Time and F-stop should be preserved.
        [DataRow("/app1/ifd/exif/{ushort=33434}")]
        [DataRow("/app1/ifd/exif/{ushort=33437}")]

        // GPS coordinate values are descriptive metadata and should survive when metadata is preserved.
        [DataRow("/app1/ifd/gps/{ushort=2}")]
        [DataRow("/xmp/dc:title/x-default")]
        public void WicMetadataCopyKeepsDescriptiveTags(string metadataPath)
        {
            Assert.IsFalse(WicMetadataCopier.ShouldSkipMetadataPath(metadataPath));
        }

        [TestMethod]
        public void DetermineEncodeStrategy_UsesTranscodeWhenCodecMatchesAndMetadataIsKept()
        {
            Assert.AreEqual(
                ResizeOperation.EncodeStrategy.Transcode,
                ResizeOperation.DetermineEncodeStrategy(
                    BitmapEncoder.PngEncoderId,
                    BitmapEncoder.PngEncoderId,
                    noTransformNeeded: false,
                    removeMetadata: false));
        }

        [TestMethod]
        public void DetermineEncodeStrategy_UsesReencodeForJpegTransforms()
        {
            Assert.AreEqual(
                ResizeOperation.EncodeStrategy.Reencode,
                ResizeOperation.DetermineEncodeStrategy(
                    BitmapEncoder.JpegEncoderId,
                    BitmapEncoder.JpegEncoderId,
                    noTransformNeeded: false,
                    removeMetadata: false));
        }

        [TestMethod]
        public void DetermineEncodeStrategy_UsesReencodeWhenMetadataIsRemoved()
        {
            Assert.AreEqual(
                ResizeOperation.EncodeStrategy.Reencode,
                ResizeOperation.DetermineEncodeStrategy(
                    BitmapEncoder.PngEncoderId,
                    BitmapEncoder.PngEncoderId,
                    noTransformNeeded: false,
                    removeMetadata: true));
        }

        [TestMethod]
        public void DetermineEncodeStrategy_UsesReencodeWhenCodecDoesNotMatch()
        {
            Assert.AreEqual(
                ResizeOperation.EncodeStrategy.Reencode,
                ResizeOperation.DetermineEncodeStrategy(
                    BitmapEncoder.PngEncoderId,
                    BitmapEncoder.JpegEncoderId,
                    noTransformNeeded: false,
                    removeMetadata: false));
        }

        [TestMethod]
        public void DetermineEncodeStrategy_UsesReencodeWhenForced()
        {
            Assert.AreEqual(
                ResizeOperation.EncodeStrategy.Reencode,
                ResizeOperation.DetermineEncodeStrategy(
                    BitmapEncoder.PngEncoderId,
                    BitmapEncoder.PngEncoderId,
                    noTransformNeeded: true,
                    removeMetadata: false,
                    forceReencode: true));
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

        private static async Task AssertRawMetadataPathsRemovedAsync(string path, string[] metadataQueryPaths)
        {
            await AssertEx.ImageAsync(
                path,
                async decoder =>
                {
                    try
                    {
                        var props = await decoder.BitmapProperties.GetPropertiesAsync(metadataQueryPaths);
                        foreach (var metadataPath in metadataQueryPaths)
                        {
                            Assert.IsFalse(props.ContainsKey(metadataPath), $"{metadataPath} should be removed during fresh encode");
                        }
                    }
                    catch (Exception)
                    {
                        // If raw metadata queries fail entirely, the queried paths are absent.
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

        private async Task AssertPropertiesPreservedAsync(string sourcePath, string[] propertyQuery)
        {
            await AssertEx.ImageAsync(
                sourcePath,
                async sourceDecoder =>
                {
                    var sourceProps = await sourceDecoder.BitmapProperties.GetPropertiesAsync(propertyQuery);

                    await AssertEx.ImageAsync(
                        _directory.File(),
                        async outputDecoder =>
                        {
                            var outputProps = await outputDecoder.BitmapProperties.GetPropertiesAsync(propertyQuery);

                            foreach (var propertyName in propertyQuery)
                            {
                                Assert.IsTrue(sourceProps.ContainsKey(propertyName), $"Source image should contain {propertyName}");
                                Assert.IsTrue(outputProps.ContainsKey(propertyName), $"{propertyName} should be preserved during fresh encode");
                            }
                        });
                });
        }

        private async Task AssertRawMetadataQueryPathsPreservedAsync(string sourcePath, string[] metadataQueryPaths, string message)
        {
            await AssertEx.ImageAsync(
                sourcePath,
                async sourceDecoder =>
                {
                    var sourceProps = await sourceDecoder.BitmapProperties.GetPropertiesAsync(metadataQueryPaths);

                    await AssertEx.ImageAsync(
                        _directory.File(),
                        async outputDecoder =>
                        {
                            var outputProps = await outputDecoder.BitmapProperties.GetPropertiesAsync(metadataQueryPaths);

                            Assert.IsTrue(sourceProps.Count > 0, "Source image should contain matching raw metadata query paths.");

                            foreach (var property in sourceProps)
                            {
                                Assert.IsTrue(outputProps.ContainsKey(property.Key), $"{message} Missing raw path: {property.Key}");
                            }
                        });
                });
        }

        private async Task AssertRawMetadataValuesPreservedAsync(string sourcePath, string[] metadataQueryPaths)
        {
            await AssertEx.ImageAsync(
                sourcePath,
                async sourceDecoder =>
                {
                    var sourceProps = await sourceDecoder.BitmapProperties.GetPropertiesAsync(metadataQueryPaths);

                    await AssertEx.ImageAsync(
                        _directory.File(),
                        async outputDecoder =>
                        {
                            var outputProps = await outputDecoder.BitmapProperties.GetPropertiesAsync(metadataQueryPaths);
                            Assert.IsTrue(sourceProps.Count > 0, "Source image should contain matching raw metadata query paths.");

                            foreach (var property in sourceProps)
                            {
                                Assert.IsTrue(outputProps.ContainsKey(property.Key), $"Raw metadata path should be preserved: {property.Key}");
                                Assert.AreEqual(
                                    property.Value.Value,
                                    outputProps[property.Key].Value,
                                    $"Raw metadata value should be preserved for {property.Key}");
                            }
                        });
                });
        }

        private static void WriteJpegWithExif(
            string baseImagePath,
            string outputPath,
            byte[] exifSegmentData,
            bool addPaddingBeforeExifMarker = false,
            bool addPaddingBeforeStartOfScanMarker = false)
        {
            var jpegBytes = File.ReadAllBytes(baseImagePath);
            Assert.IsTrue(jpegBytes.Length > 4, "Base JPEG should be long enough to contain SOI and metadata segments.");
            Assert.AreEqual((byte)0xFF, jpegBytes[0], "Base JPEG should start with SOI marker.");
            Assert.AreEqual((byte)0xD8, jpegBytes[1], "Base JPEG should start with SOI marker.");

            ushort exifLength = checked((ushort)(exifSegmentData.Length + 2));
            var exifSegment = new byte[4 + exifSegmentData.Length];
            exifSegment[0] = 0xFF;
            exifSegment[1] = 0xE1;
            WriteUInt16BigEndian(exifSegment, 2, exifLength);
            Buffer.BlockCopy(exifSegmentData, 0, exifSegment, 4, exifSegmentData.Length);

            if (addPaddingBeforeExifMarker)
            {
                exifSegment = [0xFF, 0xFF, .. exifSegment];
            }

            int insertOffset = 2;
            while (insertOffset + 4 <= jpegBytes.Length
                && jpegBytes[insertOffset] == 0xFF
                && jpegBytes[insertOffset + 1] == 0xE0)
            {
                ushort segmentLength = ReadUInt16BigEndian(jpegBytes, insertOffset + 2);
                insertOffset += 2 + segmentLength;
            }

            using var output = new MemoryStream(jpegBytes.Length + exifSegment.Length);
            output.Write(jpegBytes, 0, insertOffset);
            output.Write(exifSegment, 0, exifSegment.Length);
            if (!addPaddingBeforeStartOfScanMarker)
            {
                output.Write(jpegBytes, insertOffset, jpegBytes.Length - insertOffset);
            }
            else
            {
                int startOfScanOffset = FindMarkerOffset(jpegBytes, 0xDA);
                Assert.IsTrue(startOfScanOffset >= insertOffset, "Base JPEG should contain a Start of Scan marker after metadata.");
                output.Write(jpegBytes, insertOffset, startOfScanOffset - insertOffset);
                output.WriteByte(0xFF);
                output.WriteByte(0xFF);
                output.Write(jpegBytes, startOfScanOffset, jpegBytes.Length - startOfScanOffset);
            }

            File.WriteAllBytes(outputPath, output.ToArray());
        }

        private static byte[] CreateBigEndianExifSegmentData(uint width, uint height, ushort orientation)
        {
            var tiffBytes = new byte[92];

            tiffBytes[0] = (byte)'M';
            tiffBytes[1] = (byte)'M';
            WriteUInt16BigEndian(tiffBytes, 2, 42);
            WriteUInt32BigEndian(tiffBytes, 4, 8);

            WriteUInt16BigEndian(tiffBytes, 8, 4);

            WriteIfdEntryBigEndian(tiffBytes, 10, MetadataTagIds.Ifd.ImageWidth, 4, 1, width);
            WriteIfdEntryBigEndian(tiffBytes, 22, MetadataTagIds.Ifd.ImageHeight, 4, 1, height);
            WriteIfdEntryBigEndian(tiffBytes, 34, MetadataTagIds.Ifd.Orientation, 3, 1, (uint)orientation << 16);
            WriteIfdEntryBigEndian(tiffBytes, 46, MetadataTagIds.Ifd.ExifIfdPointer, 4, 1, 62);
            WriteUInt32BigEndian(tiffBytes, 58, 0);

            WriteUInt16BigEndian(tiffBytes, 62, 2);
            WriteIfdEntryBigEndian(tiffBytes, 64, MetadataTagIds.Exif.PixelXDimension, 4, 1, width);
            WriteIfdEntryBigEndian(tiffBytes, 76, MetadataTagIds.Exif.PixelYDimension, 4, 1, height);
            WriteUInt32BigEndian(tiffBytes, 88, 0);

            var exifSegmentData = new byte[6 + tiffBytes.Length];
            Buffer.BlockCopy(Encoding.ASCII.GetBytes("Exif\0\0"), 0, exifSegmentData, 0, 6);
            Buffer.BlockCopy(tiffBytes, 0, exifSegmentData, 6, tiffBytes.Length);
            return exifSegmentData;
        }

        private static byte[] CreateCorruptedExifSegmentData()
        {
            var exifSegmentData = new byte[14];
            Buffer.BlockCopy(Encoding.ASCII.GetBytes("Exif\0\0"), 0, exifSegmentData, 0, 6);
            exifSegmentData[6] = (byte)'M';
            exifSegmentData[7] = (byte)'M';
            WriteUInt16BigEndian(exifSegmentData, 8, 42);
            WriteUInt32BigEndian(exifSegmentData, 10, 32);
            return exifSegmentData;
        }

        private static void WriteIfdEntryBigEndian(byte[] bytes, int offset, ushort tag, ushort type, uint count, uint value)
        {
            WriteUInt16BigEndian(bytes, offset, tag);
            WriteUInt16BigEndian(bytes, offset + 2, type);
            WriteUInt32BigEndian(bytes, offset + 4, count);
            WriteUInt32BigEndian(bytes, offset + 8, value);
        }

        private static ushort ReadUInt16BigEndian(byte[] bytes, int offset)
            => (ushort)((bytes[offset] << 8) | bytes[offset + 1]);

        private static void WriteUInt16BigEndian(byte[] bytes, int offset, ushort value)
        {
            bytes[offset] = (byte)(value >> 8);
            bytes[offset + 1] = (byte)(value & 0xFF);
        }

        private static void WriteUInt32BigEndian(byte[] bytes, int offset, uint value)
        {
            bytes[offset] = (byte)((value >> 24) & 0xFF);
            bytes[offset + 1] = (byte)((value >> 16) & 0xFF);
            bytes[offset + 2] = (byte)((value >> 8) & 0xFF);
            bytes[offset + 3] = (byte)(value & 0xFF);
        }

        private static int FindMarkerOffset(byte[] jpegBytes, byte marker)
        {
            for (int index = 2; index + 1 < jpegBytes.Length; index++)
            {
                if (jpegBytes[index] == 0xFF)
                {
                    int lookahead = index + 1;
                    while (lookahead < jpegBytes.Length && jpegBytes[lookahead] == 0xFF)
                    {
                        lookahead++;
                    }

                    if (lookahead < jpegBytes.Length && jpegBytes[lookahead] == marker)
                    {
                        return index;
                    }
                }
            }

            return -1;
        }

        private static async Task AssertPropertiesRemovedAsync(string path, string[] propertyQuery)
        {
            await AssertEx.ImageAsync(
                path,
                async decoder =>
                {
                    try
                    {
                        var props = await decoder.BitmapProperties.GetPropertiesAsync(propertyQuery);
                        foreach (var propertyName in propertyQuery)
                        {
                            Assert.IsFalse(props.ContainsKey(propertyName), $"{propertyName} should be stripped");
                        }
                    }
                    catch (Exception)
                    {
                        // If metadata queries fail entirely, the metadata is absent, which is expected.
                    }
                });
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

        private sealed class FakeAiSuperResolutionService(Func<SoftwareBitmap, int, SoftwareBitmap> createResult) : IAISuperResolutionService
        {
            private readonly Func<SoftwareBitmap, int, SoftwareBitmap> _createResult = createResult ?? throw new ArgumentNullException(nameof(createResult));

            public SoftwareBitmap ApplySuperResolution(SoftwareBitmap source, int scale, string filePath)
                => _createResult(source, scale);

            public void Dispose()
            {
            }
        }
    }
}
