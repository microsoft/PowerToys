// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.IO;

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

            Assert.IsNotNull(bitmap);
        }

        [TestMethod]
        public void GetThumbnailInValidSizeStl()
        {
            // Act
            var filePath = "HelperFiles/sample.stl";

            StlThumbnailProvider provider = new StlThumbnailProvider(filePath);

            Bitmap bitmap = provider.GetThumbnail(0);

            Assert.IsNull(bitmap);
        }

        [TestMethod]
        public void GetThumbnailToBigStl()
        {
            // Act
            var filePath = "HelperFiles/sample.stl";

            StlThumbnailProvider provider = new StlThumbnailProvider(filePath);

            Bitmap bitmap = provider.GetThumbnail(10001);

            Assert.IsNull(bitmap);
        }

        [TestMethod]
        public void CheckNoStlEmptyStreamShouldReturnNullBitmap()
        {
            using (var stream = new MemoryStream())
            {
                Bitmap thumbnail = StlThumbnailProvider.GetThumbnail(stream, 256);
                Assert.IsNull(thumbnail);
            }
        }

        [TestMethod]
        public void CheckNoStlNullStreamShouldReturnNullBitmap()
        {
            Bitmap thumbnail = StlThumbnailProvider.GetThumbnail(null, 256);
            Assert.IsNull(thumbnail);
        }
    }
}
