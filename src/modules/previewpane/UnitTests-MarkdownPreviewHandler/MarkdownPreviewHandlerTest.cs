// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Microsoft.PowerToys.PreviewHandler.Markdown;
using Microsoft.PowerToys.STATestExtension;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Web.WebView2.WinForms;

namespace MarkdownPreviewHandlerUnitTests
{
    [STATestClass]
    public class MarkdownPreviewHandlerTest
    {
        // A long timeout is needed. WebView2 can take a long time to load the first time in some CI systems.
        private static readonly int HardTimeoutInMilliseconds = 60000;
        private static readonly int SleepTimeInMilliseconds = 200;

        [TestMethod]
        public void MarkdownPreviewHandlerControlAddsBrowserToFormWhenDoPreviewIsCalled()
        {
            // Arrange
            using (var markdownPreviewHandlerControl = new MarkdownPreviewHandlerControl())
            {
                // Act
                markdownPreviewHandlerControl.DoPreview<string>("HelperFiles/MarkdownWithExternalImage.txt");

                int beforeTick = Environment.TickCount;

                while (markdownPreviewHandlerControl.Controls.Count == 0 && Environment.TickCount < beforeTick + HardTimeoutInMilliseconds)
                {
                    Application.DoEvents();
                    Thread.Sleep(SleepTimeInMilliseconds);
                }

                // Assert
                Assert.AreEqual(2, markdownPreviewHandlerControl.Controls.Count);
                Assert.IsInstanceOfType(markdownPreviewHandlerControl.Controls[0], typeof(WebView2));
            }
        }

        [TestMethod]
        public void MarkdownPreviewHandlerControlAddsInfoBarToFormIfExternalImageLinkPresentWhenDoPreviewIsCalled()
        {
            // Arrange
            using (var markdownPreviewHandlerControl = new MarkdownPreviewHandlerControl())
            {
                // Act
                markdownPreviewHandlerControl.DoPreview<string>("HelperFiles/MarkdownWithExternalImage.txt");

                int beforeTick = Environment.TickCount;

                while (markdownPreviewHandlerControl.Controls.Count == 0 && Environment.TickCount < beforeTick + HardTimeoutInMilliseconds)
                {
                    Application.DoEvents();
                    Thread.Sleep(SleepTimeInMilliseconds);
                }

                // Assert
                Assert.AreEqual(2, markdownPreviewHandlerControl.Controls.Count);
                Assert.IsInstanceOfType(markdownPreviewHandlerControl.Controls[1], typeof(RichTextBox));
            }
        }

        [TestMethod]
        public void MarkdownPreviewHandlerControlAddsInfoBarToFormIfHTMLImageTagIsPresentWhenDoPreviewIsCalled()
        {
            // Arrange
            using (var markdownPreviewHandlerControl = new MarkdownPreviewHandlerControl())
            {
                // Act
                markdownPreviewHandlerControl.DoPreview<string>("HelperFiles/MarkdownWithHTMLImageTag.txt");

                int beforeTick = Environment.TickCount;

                while (markdownPreviewHandlerControl.Controls.Count < 2 && Environment.TickCount < beforeTick + HardTimeoutInMilliseconds)
                {
                    Application.DoEvents();
                    Thread.Sleep(SleepTimeInMilliseconds);
                }

                // Assert
                Assert.AreEqual(2, markdownPreviewHandlerControl.Controls.Count);
                Assert.IsInstanceOfType(markdownPreviewHandlerControl.Controls[1], typeof(RichTextBox));
            }
        }

        [TestMethod]
        public void MarkdownPreviewHandlerControlDoesNotAddInfoBarToFormIfExternalImageLinkNotPresentWhenDoPreviewIsCalled()
        {
            // Arrange
            using (var markdownPreviewHandlerControl = new MarkdownPreviewHandlerControl())
            {
                // Act
                markdownPreviewHandlerControl.DoPreview<string>("HelperFiles/MarkdownWithScript.txt");

                int beforeTick = Environment.TickCount;

                while (markdownPreviewHandlerControl.Controls.Count == 0 && Environment.TickCount < beforeTick + HardTimeoutInMilliseconds)
                {
                    Application.DoEvents();
                    Thread.Sleep(SleepTimeInMilliseconds);
                }

                // Assert
                Assert.AreEqual(1, markdownPreviewHandlerControl.Controls.Count);
                Assert.IsInstanceOfType(markdownPreviewHandlerControl.Controls[0], typeof(WebView2));
            }
        }

        [TestMethod]
        public void MarkdownPreviewHandlerControlUpdatesWebBrowserSettingsWhenDoPreviewIsCalled()
        {
            // Arrange
            using (var markdownPreviewHandlerControl = new MarkdownPreviewHandlerControl())
            {
                // Act
                markdownPreviewHandlerControl.DoPreview<string>("HelperFiles/MarkdownWithExternalImage.txt");

                int beforeTick = Environment.TickCount;

                while (markdownPreviewHandlerControl.Controls.Count < 2 && Environment.TickCount < beforeTick + HardTimeoutInMilliseconds)
                {
                    Application.DoEvents();
                    Thread.Sleep(SleepTimeInMilliseconds);
                }

                // Assert
                Assert.IsInstanceOfType(markdownPreviewHandlerControl.Controls[0], typeof(WebView2));
                Assert.AreEqual(DockStyle.Fill, ((WebView2)markdownPreviewHandlerControl.Controls[0]).Dock);
            }
        }

        [TestMethod]
        public void MarkdownPreviewHandlerControlUpdateInfobarSettingsWhenDoPreviewIsCalled()
        {
            // Arrange
            using (var markdownPreviewHandlerControl = new MarkdownPreviewHandlerControl())
            {
                // Act
                markdownPreviewHandlerControl.DoPreview<string>("HelperFiles/MarkdownWithExternalImage.txt");

                int beforeTick = Environment.TickCount;

                while (markdownPreviewHandlerControl.Controls.Count == 0 && Environment.TickCount < beforeTick + HardTimeoutInMilliseconds)
                {
                    Application.DoEvents();
                    Thread.Sleep(SleepTimeInMilliseconds);
                }

                // Assert
                Assert.IsInstanceOfType(markdownPreviewHandlerControl.Controls[1], typeof(RichTextBox));
                Assert.IsNotNull(((RichTextBox)markdownPreviewHandlerControl.Controls[1]).Text);
                Assert.AreEqual(DockStyle.Top, ((RichTextBox)markdownPreviewHandlerControl.Controls[1]).Dock);
                Assert.AreEqual(BorderStyle.None, ((RichTextBox)markdownPreviewHandlerControl.Controls[1]).BorderStyle);
                Assert.AreEqual(Color.LightYellow, ((RichTextBox)markdownPreviewHandlerControl.Controls[1]).BackColor);
                Assert.AreEqual(true, ((RichTextBox)markdownPreviewHandlerControl.Controls[1]).Multiline);
            }
        }
    }
}
