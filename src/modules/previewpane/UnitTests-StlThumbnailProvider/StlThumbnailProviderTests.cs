// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;

using Microsoft.PowerToys.ThumbnailHandler.Stl;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace StlThumbnailProviderUnitTests
{
    [STATestClass]
    public class StlThumbnailProviderTests
    {
        [TestMethod]
        public void GetThumbnailValidStreamStl()
        {
            // Act
            var filePath = "HelperFiles/sample.stl";

            StlThumbnailProvider provider = new StlThumbnailProvider(filePath);

            Bitmap bitmap = provider.GetThumbnail(256);

            Assert.IsTrue(bitmap != null);
        }

        [TestMethod]
        public void GetThumbnailInValidSizeStl()
        {
            // Act
            var filePath = "HelperFiles/sample.stl";

            StlThumbnailProvider provider = new StlThumbnailProvider(filePath);

            Bitmap bitmap = provider.GetThumbnail(0);

            Assert.IsTrue(bitmap == null);
        }

        [TestMethod]
        public void GetThumbnailToBigStl()
        {
            // Act
            var filePath = "HelperFiles/sample.stl";

            StlThumbnailProvider provider = new StlThumbnailProvider(filePath);

            Bitmap bitmap = provider.GetThumbnail(10001);

            Assert.IsTrue(bitmap == null);
        }

        [TestMethod]
        public void CheckNoStlEmptyStreamShouldReturnNullBitmap()
        {
            using (var stream = new MemoryStream())
            {
                Bitmap thumbnail = StlThumbnailProvider.GetThumbnail(stream, 256);
                Assert.IsTrue(thumbnail == null);
            }
        }

        [TestMethod]
        public void CheckNoStlNullStreamShouldReturnNullBitmap()
        {
            Bitmap thumbnail = StlThumbnailProvider.GetThumbnail(null, 256);
            Assert.IsTrue(thumbnail == null);
        }

        [TestMethod]
        public void BinaryStlDeclaredFacetPastEofShouldFailPreflight()
        {
            using var stream = CreateBinaryStl(triangleCount: 1, facetBytes: 0);

            Assert.IsFalse(StlThumbnailProvider.IsSafeToParse(stream));
            Assert.IsNull(StlThumbnailProvider.GetThumbnail(stream, 256));
        }

        [TestMethod]
        public void BinaryStlWrappedFacetCountShouldFailPreflight()
        {
            using var stream = CreateBinaryStl(0x80000000, facetBytes: 0, header: "solid wrapped binary");

            Assert.IsFalse(StlThumbnailProvider.IsSafeToParse(stream));
            Assert.IsNull(StlThumbnailProvider.GetThumbnail(stream, 256));
        }

        [TestMethod]
        public void ValidBinaryStlShouldPassPreflight()
        {
            using var stream = File.OpenRead("HelperFiles/sample.stl");

            Assert.IsTrue(StlThumbnailProvider.IsSafeToParse(stream));
        }

        [DataTestMethod]
        [DataRow(true, false, false, DisplayName = "Leading CRLF")]
        [DataRow(false, true, false, DisplayName = "UTF-8 BOM")]
        [DataRow(false, false, true, DisplayName = "Uppercase SOLID")]
        public void ValidAsciiStlVariantsShouldPassPreflightAndRender(bool leadingCrLf, bool bom, bool uppercase)
        {
            using var stream = CreateAsciiStl(leadingCrLf, bom, uppercase);

            Assert.IsTrue(StlThumbnailProvider.IsSafeToParse(stream));
            using var thumbnail = StlThumbnailProvider.GetThumbnail(stream, 256);
            Assert.IsNotNull(thumbnail);
        }

        [TestMethod]
        public void ValidCrOnlyAsciiStlShouldPassPreflightAndRender()
        {
            using var stream = CreateAsciiStl(leadingCrLf: false, bom: false, uppercase: false, lineEnding: "\r");

            Assert.IsTrue(StlThumbnailProvider.IsSafeToParse(stream));
            using var thumbnail = StlThumbnailProvider.GetThumbnail(stream, 256);
            Assert.IsNotNull(thumbnail);
        }

        [TestMethod]
        public void ValidAsciiStlWithMultipleFacetsAndBlankLinesShouldRender()
        {
            const string content =
                "\r\n" +
                "solid unusual\r\n" +
                "facet normal 0 0 1\r\n" +
                "outer loop\r\n" +
                "vertex 0 0 0\r\n" +
                "vertex 1 0 0\r\n" +
                "vertex 0 1 0\r\n" +
                "endloop\r\n" +
                "endfacet\r\n" +
                "\r\n" +
                "facet normal 0 0 1\r\n" +
                "outer loop\r\n" +
                "vertex 1 0 0\r\n" +
                "vertex 1 1 0\r\n" +
                "vertex 0 1 0\r\n" +
                "endloop\r\n" +
                "endfacet\r\n" +
                "\r\n" +
                "endsolid unusual\r\n";
            using var stream = new MemoryStream(Encoding.ASCII.GetBytes(content));

            Assert.IsTrue(StlThumbnailProvider.IsSafeToParse(stream));
            using var thumbnail = StlThumbnailProvider.GetThumbnail(stream, 256);
            Assert.IsNotNull(thumbnail);
        }

        [TestMethod]
        [Timeout(5000)]
        public void TruncatedAsciiStlWithUnterminatedFacetShouldFailBoundedly()
        {
            const string lineEnding = "\r\n";
            var content =
                $"solid truncated{lineEnding}" +
                $"facet normal 0 0 1{lineEnding}" +
                $"outer loop{lineEnding}" +
                $"vertex 0 0 0{lineEnding}" +
                $"vertex 1 0 0{lineEnding}" +
                $"vertex 0 1 0{lineEnding}" +
                $"endloop{lineEnding}";
            using var stream = new MemoryStream(Encoding.ASCII.GetBytes(content));
            var stopwatch = Stopwatch.StartNew();

            Assert.IsFalse(StlThumbnailProvider.IsSafeToParse(stream));
            Assert.IsNull(StlThumbnailProvider.GetThumbnail(stream, 256));

            stopwatch.Stop();
            Assert.IsTrue(
                stopwatch.Elapsed < TimeSpan.FromSeconds(1),
                $"Truncated ASCII facet rejection took {stopwatch.Elapsed}.");
        }

        [TestMethod]
        public void ValidBinaryStlWithSolidHeaderShouldUseBinaryLengthValidation()
        {
            using var stream = CreateValidBinaryStl("solid binary header");

            Assert.IsTrue(StlThumbnailProvider.IsSafeToParse(stream));
            using var thumbnail = StlThumbnailProvider.GetThumbnail(stream, 256);
            Assert.IsNotNull(thumbnail);
        }

        private static MemoryStream CreateBinaryStl(uint triangleCount, int facetBytes, string header = null)
        {
            var stream = new MemoryStream(new byte[84 + facetBytes]);
            if (header != null)
            {
                var headerBytes = Encoding.ASCII.GetBytes(header);
                stream.Write(headerBytes, 0, Math.Min(headerBytes.Length, 80));
            }

            stream.Position = 80;
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(triangleCount);
            }

            stream.Position = 0;
            return stream;
        }

        private static MemoryStream CreateValidBinaryStl(string header)
        {
            var stream = CreateBinaryStl(triangleCount: 1, facetBytes: 50, header);
            stream.Position = 84;
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(0f);
                writer.Write(0f);
                writer.Write(1f);
                writer.Write(0f);
                writer.Write(0f);
                writer.Write(0f);
                writer.Write(1f);
                writer.Write(0f);
                writer.Write(0f);
                writer.Write(0f);
                writer.Write(1f);
                writer.Write(0f);
                writer.Write((ushort)0);
            }

            stream.Position = 0;
            return stream;
        }

        private static MemoryStream CreateAsciiStl(bool leadingCrLf, bool bom, bool uppercase, string lineEnding = "\r\n")
        {
            var content =
                $"solid sample{lineEnding}" +
                $"facet normal 0 0 1{lineEnding}" +
                $"outer loop{lineEnding}" +
                $"vertex 0 0 0{lineEnding}" +
                $"vertex 1 0 0{lineEnding}" +
                $"vertex 0 1 0{lineEnding}" +
                $"endloop{lineEnding}" +
                $"endfacet{lineEnding}" +
                $"endsolid sample{lineEnding}";

            if (uppercase)
            {
                content = content.ToUpperInvariant();
            }

            if (leadingCrLf)
            {
                content = "\r\n" + content;
            }

            var data = Encoding.UTF8.GetBytes(content);
            if (!bom)
            {
                return new MemoryStream(data);
            }

            var preamble = Encoding.UTF8.GetPreamble();
            var withPreamble = new byte[preamble.Length + data.Length];
            preamble.CopyTo(withPreamble, 0);
            data.CopyTo(withPreamble, preamble.Length);
            return new MemoryStream(withPreamble);
        }
    }
}
