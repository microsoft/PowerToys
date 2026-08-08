// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.IO;

using Microsoft.PowerToys.ThumbnailHandler.ThreeMf;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ThreeMfThumbnailProviderUnitTests
{
    [STATestClass]
    public class ThreeMfThumbnailProviderTests
    {
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
    }
}
