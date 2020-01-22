using System;
using System.IO;
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
            var mockStream = new Mock<IStream>();

            // Act
            svgPreviewControl.DoPreview(mockStream.Object);

            // Assert
            Assert.AreEqual(svgPreviewControl.Controls.Count, 1);
            Assert.IsInstanceOfType(svgPreviewControl.Controls[0], typeof(WebBrowser));
        }

        [TestMethod]
        public void SvgPreviewControl_ShouldSetDocumentStream_WhenDoPreviewCalled()
        {
            // Arrange
            var svgPreviewControl = new SvgPreviewControl();
            var mockStream = new Mock<IStream>();

            // Act
            svgPreviewControl.DoPreview(mockStream.Object);
            
            // Assert
            Assert.IsNotNull(((WebBrowser)svgPreviewControl.Controls[0]).DocumentStream);
        }

        [TestMethod]
        public void SvgPreviewControl_ShouldDisableWebBrowserContextMenu_WhenDoPreviewCalled()
        {
            // Arrange
            var svgPreviewControl = new SvgPreviewControl();
            var mockStream = new Mock<IStream>();

            // Act
            svgPreviewControl.DoPreview(mockStream.Object);

            // Assert
            Assert.AreEqual(((WebBrowser)svgPreviewControl.Controls[0]).IsWebBrowserContextMenuEnabled, false);
        }

        [TestMethod]
        public void SvgPreviewControl_ShouldFillDockForWebBrowser_WhenDoPreviewCalled()
        {
            // Arrange
            var svgPreviewControl = new SvgPreviewControl();
            var mockStream = new Mock<IStream>();

            // Act
            svgPreviewControl.DoPreview(mockStream.Object);

            // Assert
            Assert.AreEqual(((WebBrowser)svgPreviewControl.Controls[0]).Dock, DockStyle.Fill);
        }

        [TestMethod]
        public void SvgPreviewControl_ShouldSetScriptErrorsSuppressedProperty_WhenDoPreviewCalled()
        {
            // Arrange
            var svgPreviewControl = new SvgPreviewControl();
            var mockStream = new Mock<IStream>();

            // Act
            svgPreviewControl.DoPreview(mockStream.Object);

            // Assert
            Assert.AreEqual(((WebBrowser)svgPreviewControl.Controls[0]).ScriptErrorsSuppressed, true);
        }

        [TestMethod]
        public void SvgPreviewControl_ShouldSetScrollBarsEnabledProperty_WhenDoPreviewCalled()
        {
            // Arrange
            var svgPreviewControl = new SvgPreviewControl();
            var mockStream = new Mock<IStream>();

            // Act
            svgPreviewControl.DoPreview(mockStream.Object);

            // Assert
            Assert.AreEqual(((WebBrowser)svgPreviewControl.Controls[0]).ScrollBarsEnabled, true);
        }
    }
}
