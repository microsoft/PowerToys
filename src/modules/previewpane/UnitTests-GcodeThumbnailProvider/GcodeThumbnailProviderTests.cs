// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Common.ComInterlop;
using Microsoft.PowerToys.STATestExtension;
using Microsoft.PowerToys.ThumbnailHandler.Gcode;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace GcodeThumbnailProviderUnitTests
{
    [STATestClass]
    public class GcodeThumbnailProviderTests
    {
        [TestMethod]
        public void GetThumbnailValidStreamGcode()
        {
            // Act
            var filePath = "HelperFiles/sample.gcode";

            GcodeThumbnailProvider provider = new GcodeThumbnailProvider(filePath);

            Bitmap bitmap = provider.GetThumbnail(256);

            Assert.IsTrue(bitmap != null);
        }

        [TestMethod]
        public void GetThumbnailInValidSizeGcode()
        {
            // Act
            var filePath = "HelperFiles/sample.gcode";

            GcodeThumbnailProvider provider = new GcodeThumbnailProvider(filePath);

            Bitmap bitmap = provider.GetThumbnail(0);

            Assert.IsTrue(bitmap == null);
        }

        [TestMethod]
        public void GetThumbnailToBigGcode()
        {
            // Act
            var filePath = "HelperFiles/sample.gcode";

            GcodeThumbnailProvider provider = new GcodeThumbnailProvider(filePath);

            Bitmap bitmap = provider.GetThumbnail(10001);

            Assert.IsTrue(bitmap == null);
        }

        [TestMethod]
        public void CheckNoGcodeEmptyStringShouldReturnNullBitmap()
        {
            using (var reader = new StringReader(string.Empty))
            {
                Bitmap thumbnail = GcodeThumbnailProvider.GetThumbnail(reader, 256);
                Assert.IsTrue(thumbnail == null);
            }
        }

        [TestMethod]
        public void CheckNoGcodeNullStringShouldReturnNullBitmap()
        {
            Bitmap thumbnail = GcodeThumbnailProvider.GetThumbnail(null, 256);
            Assert.IsTrue(thumbnail == null);
        }
    }
}
