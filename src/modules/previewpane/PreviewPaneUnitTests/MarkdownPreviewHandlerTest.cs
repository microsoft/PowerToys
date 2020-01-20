using System;
using System.Windows.Forms;
using Markdig;
using MarkdownPreviewHandler;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PreviewPaneUnitTests
{
    [TestClass]
    public class MarkdownPreviewHandlerTest
    {
        [TestMethod]
        public void MarkdownPreviewHandlerControl__AddsBrowserToForm_WhenDoPreviewIsCalled()
        {
            // Arrange 
            MarkdownPreviewHandlerControl markdownPreviewHandlerControl = new MarkdownPreviewHandlerControl();

            // Act
            markdownPreviewHandlerControl.DoPreview<string>("HelperFiles/SampleMarkDown.txt");

            // Assert
            Assert.AreEqual(markdownPreviewHandlerControl.Controls.Count, 1);
            Assert.IsInstanceOfType(markdownPreviewHandlerControl.Controls[0], typeof(WebBrowser));
        }

        [TestMethod]
        public void MarkdownPreviewHandlerControl__UpdatesWebBrowserDocumentText_WhenDoPreviewIsCalled()
        {
            // Arrange 
            MarkdownPreviewHandlerControl markdownPreviewHandlerControl = new MarkdownPreviewHandlerControl();

            // Act
            markdownPreviewHandlerControl.DoPreview<string>("HelperFiles/SampleMarkDown.txt");

            // Assert
            Assert.IsInstanceOfType(markdownPreviewHandlerControl.Controls[0], typeof(WebBrowser));
            Assert.IsNotNull(((WebBrowser)markdownPreviewHandlerControl.Controls[0]).DocumentText);      
        }
    }
}