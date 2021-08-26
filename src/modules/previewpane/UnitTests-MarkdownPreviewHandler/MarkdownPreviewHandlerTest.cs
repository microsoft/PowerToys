// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Windows.Forms;
using Microsoft.PowerToys.PreviewHandler.Markdown;
using Microsoft.PowerToys.STATestExtension;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PreviewHandlerCommon;

namespace MarkdownPreviewHandlerUnitTests
{
    [STATestClass]
    public class MarkdownPreviewHandlerTest
    {
        [TestMethod]
        public void MarkdownPreviewHandlerControlAddsBrowserToFormWhenDoPreviewIsCalled()
        {
            // Arrange
            using (var markdownPreviewHandlerControl = new MarkdownPreviewHandlerControl())
            {
                // Act
                markdownPreviewHandlerControl.DoPreview<string>("HelperFiles/MarkdownWithExternalImage.txt");

                // Assert
                Assert.AreEqual(2, markdownPreviewHandlerControl.Controls.Count);
                Assert.IsInstanceOfType(markdownPreviewHandlerControl.Controls[0], typeof(WebBrowserExt));
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

                // Assert
                Assert.AreEqual(1, markdownPreviewHandlerControl.Controls.Count);
                Assert.IsInstanceOfType(markdownPreviewHandlerControl.Controls[0], typeof(WebBrowserExt));
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

                // Assert
                Assert.IsInstanceOfType(markdownPreviewHandlerControl.Controls[0], typeof(WebBrowserExt));
                Assert.IsNotNull(((WebBrowser)markdownPreviewHandlerControl.Controls[0]).DocumentText);
                Assert.AreEqual(DockStyle.Fill, ((WebBrowser)markdownPreviewHandlerControl.Controls[0]).Dock);
                Assert.AreEqual(false, ((WebBrowser)markdownPreviewHandlerControl.Controls[0]).IsWebBrowserContextMenuEnabled);
                Assert.AreEqual(true, ((WebBrowser)markdownPreviewHandlerControl.Controls[0]).ScriptErrorsSuppressed);
                Assert.AreEqual(true, ((WebBrowser)markdownPreviewHandlerControl.Controls[0]).ScrollBarsEnabled);
                Assert.AreEqual(false, ((WebBrowser)markdownPreviewHandlerControl.Controls[0]).AllowNavigation);
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
