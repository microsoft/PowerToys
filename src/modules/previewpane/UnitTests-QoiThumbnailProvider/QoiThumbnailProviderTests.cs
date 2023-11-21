// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using Microsoft.PowerToys.STATestExtension;
using Microsoft.PowerToys.ThumbnailHandler.Qoi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace QoiThumbnailProviderUnitTests
{
    [STATestClass]
    public class QoiThumbnailProviderTests
    {
        [TestMethod]
        public void GetThumbnailValidStreamQoi()
        {
            // Act
            var filePath = "HelperFiles/sample.qoi";

            QoiThumbnailProvider provider = new QoiThumbnailProvider(filePath);

            Bitmap bitmap = provider.GetThumbnail(256);

            Assert.IsTrue(bitmap != null);
        }

        [TestMethod]
        public void GetThumbnailInValidSizeQoi()
        {
            // Act
            var filePath = "HelperFiles/sample.qoi";

            QoiThumbnailProvider provider = new QoiThumbnailProvider(filePath);

            Bitmap bitmap = provider.GetThumbnail(0);

            Assert.IsTrue(bitmap == null);
        }

        [TestMethod]
        public void GetThumbnailToBigQoi()
        {
            // Act
            var filePath = "HelperFiles/sample.qoi";

            QoiThumbnailProvider provider = new QoiThumbnailProvider(filePath);

            Bitmap bitmap = provider.GetThumbnail(10001);

            Assert.IsTrue(bitmap == null);
        }

        [TestMethod]
        public void CheckNoQoiNullStringShouldReturnNullBitmap()
        {
            Bitmap thumbnail = QoiThumbnailProvider.GetThumbnail(null, 256);
            Assert.IsTrue(thumbnail == null);
        }
    }
}
