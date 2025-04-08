// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.IO;

using Microsoft.PowerToys.ThumbnailHandler.Bgcode;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BgcodeThumbnailProviderUnitTests
{
    [STATestClass]
    public class BgcodeThumbnailProviderTests
    {
        [DataTestMethod]
        [DataRow("HelperFiles/sample.bgcode")]
        public void GetThumbnailValidStreamBgcode(string filePath)
        {
            // Act
            BgcodeThumbnailProvider provider = new BgcodeThumbnailProvider(filePath);

            Bitmap bitmap = provider.GetThumbnail(256);

            Assert.IsTrue(bitmap != null);
        }

        [TestMethod]
        public void GetThumbnailInValidSizeBgcode()
        {
            // Act
            var filePath = "HelperFiles/sample.bgcode";

            BgcodeThumbnailProvider provider = new BgcodeThumbnailProvider(filePath);

            Bitmap bitmap = provider.GetThumbnail(0);

            Assert.IsTrue(bitmap == null);
        }

        [TestMethod]
        public void GetThumbnailToBigBgcode()
        {
            // Act
            var filePath = "HelperFiles/sample.bgcode";

            BgcodeThumbnailProvider provider = new BgcodeThumbnailProvider(filePath);

            Bitmap bitmap = provider.GetThumbnail(10001);

            Assert.IsTrue(bitmap == null);
        }

        [TestMethod]
        public void CheckNoBgcodeEmptyDataShouldReturnNullBitmap()
        {
            using (var reader = new BinaryReader(new MemoryStream()))
            {
                Bitmap thumbnail = BgcodeThumbnailProvider.GetThumbnail(reader, 256);
                Assert.IsTrue(thumbnail == null);
            }
        }

        [TestMethod]
        public void CheckNoBgcodeNullStringShouldReturnNullBitmap()
        {
            Bitmap thumbnail = BgcodeThumbnailProvider.GetThumbnail(null, 256);
            Assert.IsTrue(thumbnail == null);
        }
    }
}
