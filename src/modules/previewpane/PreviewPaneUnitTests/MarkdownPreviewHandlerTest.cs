// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Windows.Forms;
using Microsoft.PowerToys.PreviewHandler.Markdown;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PreviewHandlerCommon;

namespace PreviewPaneUnitTests
{
    [TestClass]
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
                Assert.AreEqual(markdownPreviewHandlerControl.Controls.Count, 2);
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
                Assert.AreEqual(markdownPreviewHandlerControl.Controls.Count, 2);
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
                Assert.AreEqual(markdownPreviewHandlerControl.Controls.Count, 2);
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
                Assert.AreEqual(markdownPreviewHandlerControl.Controls.Count, 1);
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
                Assert.AreEqual(((WebBrowser)markdownPreviewHandlerControl.Controls[0]).Dock, DockStyle.Fill);
                Assert.AreEqual(((WebBrowser)markdownPreviewHandlerControl.Controls[0]).IsWebBrowserContextMenuEnabled, false);
                Assert.AreEqual(((WebBrowser)markdownPreviewHandlerControl.Controls[0]).ScriptErrorsSuppressed, true);
                Assert.AreEqual(((WebBrowser)markdownPreviewHandlerControl.Controls[0]).ScrollBarsEnabled, true);
                Assert.AreEqual(((WebBrowser)markdownPreviewHandlerControl.Controls[0]).AllowNavigation, false);
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
                Assert.AreEqual(((RichTextBox)markdownPreviewHandlerControl.Controls[1]).Dock, DockStyle.Top);
                Assert.AreEqual(((RichTextBox)markdownPreviewHandlerControl.Controls[1]).BorderStyle, BorderStyle.None);
                Assert.AreEqual(((RichTextBox)markdownPreviewHandlerControl.Controls[1]).BackColor, Color.LightYellow);
                Assert.AreEqual(((RichTextBox)markdownPreviewHandlerControl.Controls[1]).Multiline, true);
            }
        }
    }
}
