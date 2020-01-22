using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SvgPreviewHandler;

namespace UnitTests_SvgPreviewHandler
{
    [TestClass]
    public class SvgPreviewControlTests
    {
        [TestMethod]
        public void SvgPreviewControl_ShouldAddBrowserControl_WhenDoPreviewCalled()
        {
            // Arrange
            var svgPreviewControl = new SvgPreviewControl();
            var mockStreamObject = GetMockStream(string.Empty);

            // Act
            svgPreviewControl.DoPreview(mockStreamObject);

            // Assert
            Assert.AreEqual(svgPreviewControl.Controls.Count, 1);
            Assert.IsInstanceOfType(svgPreviewControl.Controls[0], typeof(WebBrowser));
        }

        [TestMethod]
        public void SvgPreviewControl_ShouldSetDocumentText_WhenDoPreviewCalled()
        {
            // Arrange
            var svgPreviewControl = new SvgPreviewControl();
            var mockStreamObject = GetMockStream(string.Empty);

            // Act
            svgPreviewControl.DoPreview(mockStreamObject);

            // Assert
            Assert.IsNotNull(((WebBrowser)svgPreviewControl.Controls[0]).DocumentText);
        }

        [TestMethod]
        public void SvgPreviewControl_ShouldDisableWebBrowserContextMenu_WhenDoPreviewCalled()
        {
            // Arrange
            var svgPreviewControl = new SvgPreviewControl();
            var mockStreamObject = GetMockStream(string.Empty);

            // Act
            svgPreviewControl.DoPreview(mockStreamObject);

            // Assert
            Assert.AreEqual(((WebBrowser)svgPreviewControl.Controls[0]).IsWebBrowserContextMenuEnabled, false);
        }

        [TestMethod]
        public void SvgPreviewControl_ShouldFillDockForWebBrowser_WhenDoPreviewCalled()
        {
            // Arrange
            var svgPreviewControl = new SvgPreviewControl();
            var mockStreamObject = GetMockStream(string.Empty);

            // Act
            svgPreviewControl.DoPreview(mockStreamObject);

            // Assert
            Assert.AreEqual(((WebBrowser)svgPreviewControl.Controls[0]).Dock, DockStyle.Fill);
        }

        private IStream GetMockStream(string streamData)
        {
            var byteStreamData = Encoding.Unicode.GetBytes(streamData);
            var mockStream = new Mock<IStream>();
            bool doesRead = false;
            mockStream.Setup(x => x.Read(It.IsAny<byte[]>(), It.IsAny<int>(), It.IsAny<IntPtr>()))
                .Callback<byte[], int, IntPtr>((pv, cb, pcbRead) =>
                {
                    if (!doesRead)
                    {
                        Array.Copy(byteStreamData, 0, pv, 0, byteStreamData.Length);
                        Marshal.WriteInt32(pcbRead, byteStreamData.Length);
                        doesRead = true;
                    }
                    else
                    {
                        Marshal.WriteInt32(pcbRead, 0);
                    }
                });

            return mockStream.Object;
        }
    }
}
