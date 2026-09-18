// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Media.Media3D;

using Microsoft.PowerToys.ThumbnailHandler.ThreeMf;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MediaColor = System.Windows.Media.Color;

namespace ThreeMfThumbnailProviderUnitTests
{
    [STATestClass]
    public class ThreeMfThumbnailProviderTests
    {
        private const string RelationshipXml =
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rel0" Type="http://schemas.microsoft.com/3dmanufacturing/2013/01/thumbnail" Target="/Auxiliary/preview.png" />
            </Relationships>
            """;

        private const string CrossPartRootModelXml =
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <model unit="millimeter"
                   xmlns="http://schemas.microsoft.com/3dmanufacturing/core/2015/02"
                   xmlns:p="http://schemas.microsoft.com/3dmanufacturing/production/2015/06">
              <resources />
              <build>
                <item objectid="2" p:path="Components/part.model" transform="1 0 0 0 1 0 0 0 1 10 0 0" />
              </build>
            </model>
            """;

        [TestMethod]
        public void GetThumbnailValidStreamThreeMf()
        {
            // Act
            var filePath = "HelperFiles/sample.3mf";

            ThreeMfThumbnailProvider provider = new ThreeMfThumbnailProvider(filePath);

            Bitmap bitmap = provider.GetThumbnail(256);

            Assert.IsTrue(bitmap != null);
        }

        [TestMethod]
        public void GetThumbnailInValidSizeThreeMf()
        {
            // Act
            var filePath = "HelperFiles/sample.3mf";

            ThreeMfThumbnailProvider provider = new ThreeMfThumbnailProvider(filePath);

            Bitmap bitmap = provider.GetThumbnail(0);

            Assert.IsTrue(bitmap == null);
        }

        [TestMethod]
        public void GetThumbnailToBigThreeMf()
        {
            // Act
            var filePath = "HelperFiles/sample.3mf";

            ThreeMfThumbnailProvider provider = new ThreeMfThumbnailProvider(filePath);

            Bitmap bitmap = provider.GetThumbnail(10001);

            Assert.IsTrue(bitmap == null);
        }

        [TestMethod]
        public void CheckNoThreeMfEmptyStreamShouldReturnNullBitmap()
        {
            using (var stream = new MemoryStream())
            {
                Bitmap thumbnail = ThreeMfThumbnailProvider.GetThumbnail(stream, 256);
                Assert.IsTrue(thumbnail == null);
            }
        }

        [TestMethod]
        public void CheckNoThreeMfNullStreamShouldReturnNullBitmap()
        {
            Bitmap thumbnail = ThreeMfThumbnailProvider.GetThumbnail(null, 256);
            Assert.IsTrue(thumbnail == null);
        }

        [TestMethod]
        public void GetThumbnailRelationshipTargetReturnsEmbeddedImageDimensions()
        {
            using var package = CreateRelationshipThumbnailPackage(13, 7);

            using Bitmap thumbnail = ThreeMfThumbnailProvider.GetThumbnail(package, 256);

            Assert.IsNotNull(thumbnail);
            Assert.AreEqual(13, thumbnail.Width);
            Assert.AreEqual(7, thumbnail.Height);
        }

        [TestMethod]
        public void LoadModelMeshOnlyPreservesConfiguredMaterialColor()
        {
            var expectedColor = MediaColor.FromRgb(0x12, 0x78, 0xE0);
            using var package = CreateMeshOnlyPackage();

            Model3DGroup model = ThreeMfModelLoader.LoadModel(package, expectedColor);

            Assert.IsNotNull(model);
            var geometryModel = model.Children.OfType<GeometryModel3D>().Single();
            Assert.IsInstanceOfType(geometryModel.Material, typeof(DiffuseMaterial));
            var material = (DiffuseMaterial)geometryModel.Material;
            Assert.IsInstanceOfType(material.Brush, typeof(System.Windows.Media.SolidColorBrush));
            Assert.AreEqual(expectedColor, ((System.Windows.Media.SolidColorBrush)material.Brush).Color);
        }

        [TestMethod]
        public void LoadModelProductionPathResolvesExternalModelPart()
        {
            using var package = CreateCrossPartPackage();

            Model3DGroup model = ThreeMfModelLoader.LoadModel(package, System.Windows.Media.Colors.Gold);

            Assert.IsNotNull(model);
            Assert.IsTrue(
                model.Children.OfType<GeometryModel3D>().Any(child => child.Bounds.X >= 10),
                "Expected the external model geometry to have the build item's translation.");

            package.Position = 0;
            using Bitmap thumbnail = ThreeMfThumbnailProvider.GetThumbnail(package, 128);
            Assert.IsNotNull(thumbnail);
        }

        [TestMethod]
        public void GetThumbnailNonSeekableReadableStreamDoesNotReadLength()
        {
            using var package = CreateRelationshipThumbnailPackage(5, 3);
            using var nonSeekableStream = new NonSeekableReadStream(package);

            using Bitmap thumbnail = ThreeMfThumbnailProvider.GetThumbnail(nonSeekableStream, 256);

            Assert.IsNotNull(thumbnail);
            Assert.AreEqual(5, thumbnail.Width);
            Assert.AreEqual(3, thumbnail.Height);
        }

        private static MemoryStream CreateRelationshipThumbnailPackage(int width, int height)
        {
            var package = new MemoryStream();
            using (var archive = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
            {
                AddTextEntry(archive, "_rels/.rels", RelationshipXml);

                var thumbnailEntry = archive.CreateEntry("Auxiliary/preview.png");
                using var thumbnailStream = thumbnailEntry.Open();
                using var image = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using (var graphics = Graphics.FromImage(image))
                {
                    graphics.Clear(System.Drawing.Color.CornflowerBlue);
                }

                image.Save(thumbnailStream, ImageFormat.Png);
            }

            package.Position = 0;
            return package;
        }

        private static MemoryStream CreateMeshOnlyPackage()
        {
            var package = new MemoryStream();
            using (var archive = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
            {
                AddTextEntry(archive, "3D/3dmodel.model", CreateModelXml(1, includeBuildItem: true));
            }

            package.Position = 0;
            return package;
        }

        private static MemoryStream CreateCrossPartPackage()
        {
            var package = new MemoryStream();
            using (var archive = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
            {
                AddTextEntry(archive, "3D/3dmodel.model", CrossPartRootModelXml);
                AddTextEntry(archive, "3D/Components/part.model", CreateModelXml(2, includeBuildItem: false));
            }

            package.Position = 0;
            return package;
        }

        private static string CreateModelXml(int objectId, bool includeBuildItem)
        {
            var build = includeBuildItem ? $"<build><item objectid=\"{objectId}\" /></build>" : string.Empty;
            return $"""
                    <?xml version="1.0" encoding="UTF-8"?>
                    <model unit="millimeter" xmlns="http://schemas.microsoft.com/3dmanufacturing/core/2015/02">
                      <resources>
                        <object id="{objectId}" type="model">
                          <mesh>
                            <vertices>
                              <vertex x="0" y="0" z="0" />
                              <vertex x="1" y="0" z="0" />
                              <vertex x="0" y="1" z="0" />
                              <vertex x="0" y="0" z="1" />
                            </vertices>
                            <triangles>
                              <triangle v1="0" v2="1" v3="2" />
                              <triangle v1="0" v2="1" v3="3" />
                            </triangles>
                          </mesh>
                        </object>
                      </resources>
                      {build}
                    </model>
                    """;
        }

        private static void AddTextEntry(ZipArchive archive, string path, string content)
        {
            var entry = archive.CreateEntry(path);
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            writer.Write(content);
        }

        private sealed class NonSeekableReadStream : Stream
        {
            private readonly Stream innerStream;

            public NonSeekableReadStream(Stream innerStream)
            {
                this.innerStream = innerStream;
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override void Flush()
            {
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return innerStream.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }
        }
    }
}
