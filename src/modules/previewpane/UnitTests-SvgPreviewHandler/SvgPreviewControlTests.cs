// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Forms;
using Microsoft.PowerToys.PreviewHandler.Svg;
using Microsoft.PowerToys.STATestExtension;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PreviewHandlerCommon;

namespace SvgPreviewHandlerUnitTests
{
    [STATestClass]
    public class SvgPreviewControlTests
    {
        [TestMethod]
        public void SvgPreviewControlShouldAddExtendedBrowserControlWhenDoPreviewCalled()
        {
            // Arrange
            using (var svgPreviewControl = new SvgPreviewControl())
            {
                // Act
                svgPreviewControl.DoPreview(GetMockStream("<svg></svg>"));

                // Assert
                Assert.AreEqual(1, svgPreviewControl.Controls.Count);
                Assert.IsInstanceOfType(svgPreviewControl.Controls[0], typeof(WebBrowserExt));
            }
        }

        [TestMethod]
        public void SvgPreviewControlShouldSetDocumentStreamWhenDoPreviewCalled()
        {
            // Arrange
            using (var svgPreviewControl = new SvgPreviewControl())
            {
                // Act
                svgPreviewControl.DoPreview(GetMockStream("<svg></svg>"));

                // Assert
                Assert.IsNotNull(((WebBrowser)svgPreviewControl.Controls[0]).DocumentStream);
            }
        }

        [TestMethod]
        public void SvgPreviewControlShouldDisableWebBrowserContextMenuWhenDoPreviewCalled()
        {
            // Arrange
            using (var svgPreviewControl = new SvgPreviewControl())
            {
                // Act
                svgPreviewControl.DoPreview(GetMockStream("<svg></svg>"));

                // Assert
                Assert.AreEqual(false, ((WebBrowser)svgPreviewControl.Controls[0]).IsWebBrowserContextMenuEnabled);
            }
        }

        [TestMethod]
        public void SvgPreviewControlShouldFillDockForWebBrowserWhenDoPreviewCalled()
        {
            // Arrange
            using (var svgPreviewControl = new SvgPreviewControl())
            {
                // Act
                svgPreviewControl.DoPreview(GetMockStream("<svg></svg>"));

                // Assert
                Assert.AreEqual(DockStyle.Fill, ((WebBrowser)svgPreviewControl.Controls[0]).Dock);
            }
        }

        [TestMethod]
        public void SvgPreviewControlShouldSetScriptErrorsSuppressedPropertyWhenDoPreviewCalled()
        {
            // Arrange
            using (var svgPreviewControl = new SvgPreviewControl())
            {
                // Act
                svgPreviewControl.DoPreview(GetMockStream("<svg></svg>"));

                // Assert
                Assert.AreEqual(true, ((WebBrowser)svgPreviewControl.Controls[0]).ScriptErrorsSuppressed);
            }
        }

        // ToDo: fix unit test
        [Ignore]
        [TestMethod]
        public void SvgPreviewControlShouldSetScrollBarsEnabledPropertyWhenDoPreviewCalled()
        {
            // Arrange
            using (var svgPreviewControl = new SvgPreviewControl())
            {
                // Act
                svgPreviewControl.DoPreview(GetMockStream("<svg></svg>"));

                // Assert
                Assert.AreEqual(true, ((WebBrowser)svgPreviewControl.Controls[0]).ScrollBarsEnabled);
            }
        }

        [TestMethod]
        public void SvgPreviewControlShouldDisableAllowNavigationWhenDoPreviewCalled()
        {
            // Arrange
            using (var svgPreviewControl = new SvgPreviewControl())
            {
                // Act
                svgPreviewControl.DoPreview(GetMockStream("<svg></svg>"));

                // Assert
                Assert.AreEqual(false, ((WebBrowser)svgPreviewControl.Controls[0]).AllowNavigation);
            }
        }

        [TestMethod]
        public void SvgPreviewControlShouldAddValidInfoBarIfSvgPreviewThrows()
        {
            // Arrange
            using (var svgPreviewControl = new SvgPreviewControl())
            {
                var mockStream = new Mock<IStream>();
                mockStream
                    .Setup(x => x.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<IntPtr>()))
                    .Throws(new Exception());

                // Act
                svgPreviewControl.DoPreview(mockStream.Object);
                var textBox = svgPreviewControl.Controls[0] as RichTextBox;

                // Assert
                Assert.IsFalse(string.IsNullOrWhiteSpace(textBox.Text));
                Assert.AreEqual(1, svgPreviewControl.Controls.Count);
                Assert.AreEqual(DockStyle.Top, textBox.Dock);
                Assert.AreEqual(Color.LightYellow, textBox.BackColor);
                Assert.IsTrue(textBox.Multiline);
                Assert.IsTrue(textBox.ReadOnly);
                Assert.AreEqual(RichTextBoxScrollBars.None, textBox.ScrollBars);
                Assert.AreEqual(BorderStyle.None, textBox.BorderStyle);
            }
        }

        [TestMethod]
        public void SvgPreviewControlInfoBarWidthShouldAdjustWithParentControlWidthChangesIfSvgPreviewThrows()
        {
            // Arrange
            using (var svgPreviewControl = new SvgPreviewControl())
            {
                var mockStream = new Mock<IStream>();
                mockStream
                    .Setup(x => x.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<IntPtr>()))
                    .Throws(new Exception());
                svgPreviewControl.DoPreview(mockStream.Object);
                var textBox = svgPreviewControl.Controls[0] as RichTextBox;
                var incrementParentControlWidth = 5;
                var initialParentWidth = svgPreviewControl.Width;
                var initialTextBoxWidth = textBox.Width;
                var finalParentWidth = initialParentWidth + incrementParentControlWidth;

                // Act
                svgPreviewControl.Width += incrementParentControlWidth;

                // Assert
                Assert.AreEqual(initialTextBoxWidth, initialParentWidth);
                Assert.AreEqual(textBox.Width, finalParentWidth);
            }
        }

        [TestMethod]
        public void SvgPreviewControlShouldAddTextBoxIfBlockedElementsArePresent()
        {
            // Arrange
            using (var svgPreviewControl = new SvgPreviewControl())
            {
                var svgBuilder = new StringBuilder();
                svgBuilder.AppendLine("<svg width =\"200\" height=\"200\" xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\">");
                svgBuilder.AppendLine("\t<script>alert(\"hello\")</script>");
                svgBuilder.AppendLine("</svg>");

                // Act
                svgPreviewControl.DoPreview(GetMockStream(svgBuilder.ToString()));

                // Assert
                Assert.IsInstanceOfType(svgPreviewControl.Controls[0], typeof(RichTextBox));
                Assert.IsInstanceOfType(svgPreviewControl.Controls[1], typeof(WebBrowserExt));
                Assert.AreEqual(2, svgPreviewControl.Controls.Count);
            }
        }

        [TestMethod]
        public void SvgPreviewControlShouldNotAddTextBoxIfNoBlockedElementsArePresent()
        {
            // Arrange
            using (var svgPreviewControl = new SvgPreviewControl())
            {
                var svgBuilder = new StringBuilder();
                svgBuilder.AppendLine("<svg viewBox=\"0 0 100 100\" xmlns=\"http://www.w3.org/2000/svg\">");
                svgBuilder.AppendLine("\t<circle cx=\"50\" cy=\"50\" r=\"50\">");
                svgBuilder.AppendLine("\t</circle>");
                svgBuilder.AppendLine("</svg>");

                // Act
                svgPreviewControl.DoPreview(GetMockStream(svgBuilder.ToString()));

                // Assert
                Assert.IsInstanceOfType(svgPreviewControl.Controls[0], typeof(WebBrowserExt));
                Assert.AreEqual(1, svgPreviewControl.Controls.Count);
            }
        }

        [TestMethod]
        public void SvgPreviewControlInfoBarWidthShouldAdjustWithParentControlWidthChangesIfBlockedElementsArePresent()
        {
            // Arrange
            using (var svgPreviewControl = new SvgPreviewControl())
            {
                var svgBuilder = new StringBuilder();
                svgBuilder.AppendLine("<svg width =\"200\" height=\"200\" xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\">");
                svgBuilder.AppendLine("\t<script>alert(\"hello\")</script>");
                svgBuilder.AppendLine("</svg>");
                svgPreviewControl.DoPreview(GetMockStream(svgBuilder.ToString()));
                var textBox = svgPreviewControl.Controls[0] as RichTextBox;
                var incrementParentControlWidth = 5;
                var initialParentWidth = svgPreviewControl.Width;
                var initialTextBoxWidth = textBox.Width;
                var finalParentWidth = initialParentWidth + incrementParentControlWidth;

                // Act
                svgPreviewControl.Width += incrementParentControlWidth;

                // Assert
                Assert.AreEqual(initialTextBoxWidth, initialParentWidth);
                Assert.AreEqual(textBox.Width, finalParentWidth);
            }
        }

        private static IStream GetMockStream(string streamData)
        {
            var mockStream = new Mock<IStream>();
            var streamBytes = Encoding.UTF8.GetBytes(streamData);

            var streamMock = new Mock<IStream>();
            var firstCall = true;
            streamMock
                .Setup(x => x.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<IntPtr>()))
                .Callback<byte[], int, IntPtr>((buffer, countToRead, bytesReadPtr) =>
                {
                    if (firstCall)
                    {
                        Array.Copy(streamBytes, 0, buffer, 0, streamBytes.Length);
                        Marshal.WriteInt32(bytesReadPtr, streamBytes.Length);
                        firstCall = false;
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
