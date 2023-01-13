// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.PowerToys.PreviewHandler.Svg;
using Microsoft.PowerToys.STATestExtension;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Web.WebView2.WinForms;
using Moq;

namespace SvgPreviewHandlerUnitTests
{
    [STATestClass]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2201:Do not raise reserved exception types", Justification = "new Exception() is fine in test projects.")]
    public class SvgPreviewControlTests
    {
        // A long timeout is needed. WebView2 can take a long time to load the first time in some CI systems.
        private static readonly int HardTimeoutInMilliseconds = 30000;
        private static readonly int SleepTimeInMilliseconds = 200;

        [TestMethod]
        public void SvgPreviewControlShouldAddExtendedBrowserControlWhenDoPreviewCalled()
        {
            // Arrange
            using (var svgPreviewControl = new SvgPreviewControl())
            {
                // Act
                svgPreviewControl.DoPreview("HelperFiles/file1.svg");

                int beforeTick = Environment.TickCount;

                while (svgPreviewControl.Controls.Count == 0 && Environment.TickCount < beforeTick + HardTimeoutInMilliseconds)
                {
                    Application.DoEvents();
                    Thread.Sleep(SleepTimeInMilliseconds);
                }

                // Assert
                Assert.AreEqual(1, svgPreviewControl.Controls.Count);
                Assert.IsInstanceOfType(svgPreviewControl.Controls[0], typeof(WebView2));
            }
        }

        [TestMethod]
        public void SvgPreviewControlShouldFillDockForWebView2WhenDoPreviewCalled()
        {
            // Arrange
            using (var svgPreviewControl = new SvgPreviewControl())
            {
                // Act
                svgPreviewControl.DoPreview("HelperFiles/file1.svg");

                int beforeTick = Environment.TickCount;

                while (svgPreviewControl.Controls.Count == 0 && Environment.TickCount < beforeTick + HardTimeoutInMilliseconds)
                {
                    Application.DoEvents();
                    Thread.Sleep(SleepTimeInMilliseconds);
                }

                // Assert
                Assert.AreEqual(DockStyle.Fill, ((WebView2)svgPreviewControl.Controls[0]).Dock);
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

                int beforeTick = Environment.TickCount;

                while (svgPreviewControl.Controls.Count == 0 && Environment.TickCount < beforeTick + HardTimeoutInMilliseconds)
                {
                    Application.DoEvents();
                    Thread.Sleep(SleepTimeInMilliseconds);
                }

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

                int beforeTick = Environment.TickCount;

                while (svgPreviewControl.Controls.Count == 0 && Environment.TickCount < beforeTick + HardTimeoutInMilliseconds)
                {
                    Application.DoEvents();
                    Thread.Sleep(SleepTimeInMilliseconds);
                }

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
                // Act
                svgPreviewControl.DoPreview("HelperFiles/file2.svg");

                int beforeTick = Environment.TickCount;

                while (svgPreviewControl.Controls.Count < 2 && Environment.TickCount < beforeTick + HardTimeoutInMilliseconds)
                {
                    Application.DoEvents();
                    Thread.Sleep(SleepTimeInMilliseconds);
                }

                // Assert
                Assert.IsInstanceOfType(svgPreviewControl.Controls[0], typeof(RichTextBox));
                Assert.IsInstanceOfType(svgPreviewControl.Controls[1], typeof(WebView2));
                Assert.AreEqual(2, svgPreviewControl.Controls.Count);
            }
        }

        [TestMethod]
        public void SvgPreviewControlShouldNotAddTextBoxIfNoBlockedElementsArePresent()
        {
            // Arrange
            using (var svgPreviewControl = new SvgPreviewControl())
            {
                svgPreviewControl.DoPreview("HelperFiles/file1.svg");

                int beforeTick = Environment.TickCount;

                while (svgPreviewControl.Controls.Count == 0 && Environment.TickCount < beforeTick + HardTimeoutInMilliseconds)
                {
                    Application.DoEvents();
                    Thread.Sleep(SleepTimeInMilliseconds);
                }

                // Assert
                Assert.IsInstanceOfType(svgPreviewControl.Controls[0], typeof(WebView2));
                Assert.AreEqual(1, svgPreviewControl.Controls.Count);
            }
        }

        [TestMethod]
        public void SvgPreviewControlInfoBarWidthShouldAdjustWithParentControlWidthChangesIfBlockedElementsArePresent()
        {
            // Arrange
            using (var svgPreviewControl = new SvgPreviewControl())
            {
                svgPreviewControl.DoPreview("HelperFiles/file2.svg");

                int beforeTick = Environment.TickCount;

                while (svgPreviewControl.Controls.Count == 0 && Environment.TickCount < beforeTick + HardTimeoutInMilliseconds)
                {
                    Application.DoEvents();
                    Thread.Sleep(SleepTimeInMilliseconds);
                }

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
    }
}
