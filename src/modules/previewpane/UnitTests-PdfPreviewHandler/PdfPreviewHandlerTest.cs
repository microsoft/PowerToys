// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Windows.Forms;
using Microsoft.PowerToys.PreviewHandler.Pdf;
using Microsoft.PowerToys.STATestExtension;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PdfPreviewHandlerUnitTests
{
    [STATestClass]
    public class PdfPreviewHandlerTest
    {
        [TestMethod]
        public void PdfPreviewHandlerControlAddsPdfViewerToFormWhenDoPreviewIsCalled()
        {
            // Arrange
            using (var pdfPreviewHandlerControl = new PdfPreviewHandlerControl())
            {
                // Act
                pdfPreviewHandlerControl.DoPreview<string>("HelperFiles/dummy.pdf");

                // Assert
                Assert.AreEqual(1, pdfPreviewHandlerControl.Controls.Count);
                Assert.IsInstanceOfType(pdfPreviewHandlerControl.Controls[0], typeof(PdfiumViewer.PdfViewer));
            }
        }
    }
}
