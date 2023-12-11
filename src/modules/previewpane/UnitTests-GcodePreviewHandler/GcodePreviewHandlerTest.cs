// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;
using Microsoft.PowerToys.PreviewHandler.Gcode;
using Microsoft.PowerToys.STATestExtension;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace GcodePreviewHandlerUnitTests
{
    [STATestClass]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2201:Do not raise reserved exception types", Justification = "new Exception() is fine in test projects.")]
    public class GcodePreviewHandlerTest
    {
        [DataTestMethod]
        [DataRow("HelperFiles/sample.gcode")]
        [DataRow("HelperFiles/sample_JPG.gcode")]
        [DataRow("HelperFiles/sample_QOI.gcode")]
        public void GcodePreviewHandlerControlAddsControlsToFormWhenDoPreviewIsCalled(string filePath)
        {
            // Arrange
            using (var gcodePreviewHandlerControl = new GcodePreviewHandlerControl())
            {
                // Act
                var file = File.ReadAllBytes(filePath);

                gcodePreviewHandlerControl.DoPreview<IStream>(GetMockStream(file));

                var flowLayoutPanel = gcodePreviewHandlerControl.Controls[0] as FlowLayoutPanel;

                // Assert
                Assert.AreEqual(1, gcodePreviewHandlerControl.Controls.Count);
            }
        }

        [TestMethod]
        public void GcodePreviewHandlerControlShouldAddValidInfoBarIfGcodePreviewThrows()
        {
            // Arrange
            using (var gcodePreviewHandlerControl = new GcodePreviewHandlerControl())
            {
                var mockStream = new Mock<IStream>();
                mockStream
                    .Setup(x => x.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<IntPtr>()))
                    .Throws(new Exception());

                // Act
                gcodePreviewHandlerControl.DoPreview(mockStream.Object);
                var textBox = gcodePreviewHandlerControl.Controls[0] as RichTextBox;

                // Assert
                Assert.IsFalse(string.IsNullOrWhiteSpace(textBox.Text));
                Assert.AreEqual(1, gcodePreviewHandlerControl.Controls.Count);
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
