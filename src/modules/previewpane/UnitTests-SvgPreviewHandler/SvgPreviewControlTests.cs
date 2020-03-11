using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using PreviewHandlerCommon;
using SvgPreviewHandler;

namespace UnitTests_SvgPreviewHandler
{
    [TestClass]
    public class SvgPreviewControlTests
    {
        [TestMethod]
        public void SvgPreviewControl_ShouldAddExtendedBrowserControl_WhenDoPreviewCalled()
        {
            // Arrange
            var svgPreviewControl = new SvgPreviewControl();

            // Act
            svgPreviewControl.DoPreview(GetMockStream("<svg></svg>"));

            // Assert
            Assert.AreEqual(svgPreviewControl.Controls.Count, 1);
            Assert.IsInstanceOfType(svgPreviewControl.Controls[0], typeof(WebBrowserExt));
        }

        [TestMethod]
        public void SvgPreviewControl_ShouldSetDocumentStream_WhenDoPreviewCalled()
        {
            // Arrange
            var svgPreviewControl = new SvgPreviewControl();

            // Act
            svgPreviewControl.DoPreview(GetMockStream("<svg></svg>"));
            
            // Assert
            Assert.IsNotNull(((WebBrowser)svgPreviewControl.Controls[0]).DocumentStream);
        }

        [TestMethod]
        public void SvgPreviewControl_ShouldDisableWebBrowserContextMenu_WhenDoPreviewCalled()
        {
            // Arrange
            var svgPreviewControl = new SvgPreviewControl();

            // Act
            svgPreviewControl.DoPreview(GetMockStream("<svg></svg>"));

            // Assert
            Assert.AreEqual(((WebBrowser)svgPreviewControl.Controls[0]).IsWebBrowserContextMenuEnabled, false);
        }

        [TestMethod]
        public void SvgPreviewControl_ShouldFillDockForWebBrowser_WhenDoPreviewCalled()
        {
            // Arrange
            var svgPreviewControl = new SvgPreviewControl();

            // Act
            svgPreviewControl.DoPreview(GetMockStream("<svg></svg>"));

            // Assert
            Assert.AreEqual(((WebBrowser)svgPreviewControl.Controls[0]).Dock, DockStyle.Fill);
        }

        [TestMethod]
        public void SvgPreviewControl_ShouldSetScriptErrorsSuppressedProperty_WhenDoPreviewCalled()
        {
            // Arrange
            var svgPreviewControl = new SvgPreviewControl();

            // Act
            svgPreviewControl.DoPreview(GetMockStream("<svg></svg>"));

            // Assert
            Assert.AreEqual(((WebBrowser)svgPreviewControl.Controls[0]).ScriptErrorsSuppressed, true);
        }

        [TestMethod]
        public void SvgPreviewControl_ShouldSetScrollBarsEnabledProperty_WhenDoPreviewCalled()
        {
            // Arrange
            var svgPreviewControl = new SvgPreviewControl();

            // Act
            svgPreviewControl.DoPreview(GetMockStream("<svg></svg>"));

            // Assert
            Assert.AreEqual(((WebBrowser)svgPreviewControl.Controls[0]).ScrollBarsEnabled, true);
        }

        [TestMethod]
        public void SvgPreviewControl_ShouldDisableAllowNavigation_WhenDoPreviewCalled()
        {
            // Arrange
            var svgPreviewControl = new SvgPreviewControl();

            // Act
            svgPreviewControl.DoPreview(GetMockStream("<svg></svg>"));

            // Assert
            Assert.AreEqual(((WebBrowser)svgPreviewControl.Controls[0]).AllowNavigation, false);
        }

        [TestMethod]
        public void SvgPreviewControl_ShouldAddValidInfoBar_IfSvgPreviewThrows()
        {
            // Arrange
            var svgPreviewControl = new SvgPreviewControl();
            var mockStream = new Mock<IStream>();
            mockStream
                .Setup(x => x.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<IntPtr>()))
                .Throws(new Exception());

            // Act
            svgPreviewControl.DoPreview(mockStream.Object);
            var textBox = svgPreviewControl.Controls[0] as RichTextBox;

            // Assert
            Assert.IsFalse(string.IsNullOrWhiteSpace(textBox.Text));
            Assert.AreEqual(svgPreviewControl.Controls.Count, 1);
            Assert.AreEqual(textBox.Dock, DockStyle.Top);
            Assert.AreEqual(textBox.BackColor, Color.LightYellow);
            Assert.IsTrue(textBox.Multiline);
            Assert.IsTrue(textBox.ReadOnly);
            Assert.AreEqual(textBox.ScrollBars, RichTextBoxScrollBars.None);
            Assert.AreEqual(textBox.BorderStyle, BorderStyle.None);
        }


        [TestMethod]
        public void SvgPreviewControl_InfoBarWidthShouldAdjustWithParentControlWidthChanges_IfSvgPreviewThrows()
        {
            // Arrange
            var svgPreviewControl = new SvgPreviewControl();
            var mockStream = new Mock<IStream>();
            mockStream
                .Setup(x => x.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<IntPtr>()))
                .Throws(new Exception());
            svgPreviewControl.DoPreview(mockStream.Object);
            var textBox = svgPreviewControl.Controls[0] as RichTextBox;
            var incrementParentControlWidth = 5;
            var intialParentWidth = svgPreviewControl.Width;
            var intitialTextBoxWidth = textBox.Width;
            var finalParentWidth = intialParentWidth + incrementParentControlWidth;

            // Act
            svgPreviewControl.Width += incrementParentControlWidth;

            // Assert
            Assert.AreEqual(intialParentWidth, intitialTextBoxWidth);
            Assert.AreEqual(finalParentWidth, textBox.Width);
        }

        private IStream GetMockStream(string streamData) 
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
