// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;
using Microsoft.PowerToys.PreviewHandler.Pdf;
using Microsoft.PowerToys.STATestExtension;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace PdfPreviewHandlerUnitTests
{
    [STATestClass]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2201:Do not raise reserved exception types", Justification = "new Exception() is fine in test projects.")]
    public class PdfPreviewHandlerTest
    {
        [TestMethod]
        public void PdfPreviewHandlerControlAddsControlsToFormWhenDoPreviewIsCalled()
        {
            // Arrange
            using (var pdfPreviewHandlerControl = new PdfPreviewHandlerControl())
            {
                // Act
                var file = File.ReadAllBytes("HelperFiles/sample.pdf");

                pdfPreviewHandlerControl.DoPreview<IStream>(GetMockStream(file));

                var flowLayoutPanel = pdfPreviewHandlerControl.Controls[0] as FlowLayoutPanel;

                // Assert
                Assert.AreEqual(1, pdfPreviewHandlerControl.Controls.Count);
            }
        }

        [TestMethod]
        public void PdfPreviewHandlerControlShouldAddValidInfoBarIfPdfPreviewThrows()
        {
            // Arrange
            using (var pdfPreviewHandlerControl = new PdfPreviewHandlerControl())
            {
                var mockStream = new Mock<IStream>();
                mockStream
                    .Setup(x => x.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<IntPtr>()))
                    .Throws(new Exception());

                // Act
                pdfPreviewHandlerControl.DoPreview(mockStream.Object);
                var textBox = pdfPreviewHandlerControl.Controls[0] as RichTextBox;

                // Assert
                Assert.IsFalse(string.IsNullOrWhiteSpace(textBox.Text));
                Assert.AreEqual(1, pdfPreviewHandlerControl.Controls.Count);
                Assert.AreEqual(DockStyle.Top, textBox.Dock);
                Assert.AreEqual(Color.LightYellow, textBox.BackColor);
                Assert.IsTrue(textBox.Multiline);
                Assert.IsTrue(textBox.ReadOnly);
                Assert.AreEqual(RichTextBoxScrollBars.None, textBox.ScrollBars);
                Assert.AreEqual(BorderStyle.None, textBox.BorderStyle);
            }
        }

        private static IStream GetMockStream(byte[] sourceArray)
        {
            var streamMock = new Mock<IStream>();
            int bytesRead = 0;

            streamMock
                .Setup(x => x.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<IntPtr>()))
                .Callback<byte[], int, IntPtr>((buffer, countToRead, bytesReadPtr) =>
                {
                    int actualCountToRead = Math.Min(sourceArray.Length - bytesRead, countToRead);
                    if (actualCountToRead > 0)
                    {
                        Array.Copy(sourceArray, bytesRead, buffer, 0, actualCountToRead);
                        Marshal.WriteInt32(bytesReadPtr, actualCountToRead);
                        bytesRead += actualCountToRead;
                    }
                    else
                    {
                        Marshal.WriteInt32(bytesReadPtr, 0);
                    }
                });

            return streamMock.Object;
        }
    }
}
