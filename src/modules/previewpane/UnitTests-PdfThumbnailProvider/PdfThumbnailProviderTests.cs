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
using Microsoft.PowerToys.ThumbnailHandler.Pdf;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace PdfThumbnailProviderUnitTests
{
    [STATestClass]
    public class PdfThumbnailProviderTests
    {
        [TestMethod]
        public void GetThumbnailValidStreamPDF()
        {
            // Act
            var filePath = System.IO.Path.GetFullPath("HelperFiles/sample.pdf");

            PdfThumbnailProvider provider = new PdfThumbnailProvider(filePath);

            Bitmap bitmap = provider.GetThumbnail(256);

            Assert.IsTrue(bitmap != null);
        }

        [TestMethod]
        public void GetThumbnailInValidSizePDF()
        {
            // Act
            var filePath = System.IO.Path.GetFullPath("HelperFiles/sample.pdf");

            PdfThumbnailProvider provider = new PdfThumbnailProvider(filePath);

            Bitmap bitmap = provider.GetThumbnail(0);

            Assert.IsTrue(bitmap == null);
        }

        [TestMethod]
        public void GetThumbnailToBigPDF()
        {
            // Act
            var filePath = System.IO.Path.GetFullPath("HelperFiles/sample.pdf");

            PdfThumbnailProvider provider = new PdfThumbnailProvider(filePath);

            Bitmap bitmap = provider.GetThumbnail(10001);

            Assert.IsTrue(bitmap == null);
        }
    }
}
