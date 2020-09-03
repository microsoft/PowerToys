// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Markdig;
using MarkdownPreviewHandler;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PreviewPaneUnitTests
{
    [TestClass]
    public class HTMLParsingExtensionTest
    {
        private MarkdownPipeline BuidPipeline(IMarkdownExtension extension)
        {
            MarkdownPipelineBuilder pipelineBuilder = new MarkdownPipelineBuilder().UseAdvancedExtensions();
            pipelineBuilder.Extensions.Add(extension);
            return pipelineBuilder.Build();
        }

        [TestMethod]
        public void Extension_UpdatesTablesClass_WhenUsed()
        {
            // Arrange
            string mdString = "| A | B |\n| -- | -- | ";
            HTMLParsingExtension htmlParsingExtension = new HTMLParsingExtension(() => { });
            MarkdownPipeline markdownPipeline = BuidPipeline(htmlParsingExtension);

            // Act
            string html = Markdown.ToHtml(mdString, markdownPipeline);

            // Assert
            Assert.AreEqual(html, "<table class=\"table table-striped table-bordered\">\n<thead>\n<tr>\n<th>A</th>\n<th>B</th>\n</tr>\n</thead>\n</table>\n");
        }

        [TestMethod]
        public void Extension_UpdatesBlockQuotesClass_WhenUsed()
        {
            // Arrange
            string mdString = "> Blockquotes.";
            HTMLParsingExtension htmlParsingExtension = new HTMLParsingExtension(() => { });
            MarkdownPipeline markdownPipeline = BuidPipeline(htmlParsingExtension);

            // Act
            string html = Markdown.ToHtml(mdString, markdownPipeline);

            // Assert
            Assert.AreEqual(html, "<blockquote class=\"blockquote\">\n<p>Blockquotes.</p>\n</blockquote>\n");
        }

        [TestMethod]
        public void Extension_UpdatesFigureClassAndBlocksRelativeUrl_WhenUsed()
        {
            // arrange
            string mdString = "![text](a.jpg \"Figure\")";
            HTMLParsingExtension htmlParsingExtension = new HTMLParsingExtension(() => { }, "C:\\Users\\");
            MarkdownPipeline markdownPipeline = BuidPipeline(htmlParsingExtension);

            // Act
            string html = Markdown.ToHtml(mdString, markdownPipeline);

            // Assert
            Assert.AreEqual(html, "<p><img src=\"#\" class=\"img-fluid\" alt=\"text\" title=\"Figure\" /></p>\n");
        }

        [TestMethod]
        public void Extension_AddsClassToFigureCaption_WhenUsed()
        {
            // arrange
            string mdString = "^^^ This is a caption";
            HTMLParsingExtension htmlParsingExtension = new HTMLParsingExtension(() => { }, "C:/Users/");
            MarkdownPipeline markdownPipeline = BuidPipeline(htmlParsingExtension);

            // Act
            string html = Markdown.ToHtml(mdString, markdownPipeline);

            // Assert
            Assert.AreEqual(html, "<figure class=\"figure\">\n<figcaption class=\"figure-caption\">This is a caption</figcaption>\n</figure>\n");
        }

        [TestMethod]
        public void Extension_RemovesExternalImageUrlAndMakeCallback_WhenUsed()
        {
            // arrange
            int count = 0;
            string mdString = "![text](http://dev.nodeca.com \"Figure\")";
            HTMLParsingExtension htmlParsingExtension = new HTMLParsingExtension(() => { count++; });
            MarkdownPipeline markdownPipeline = BuidPipeline(htmlParsingExtension);

            // Act
            string html = Markdown.ToHtml(mdString, markdownPipeline);

            // Assert
            Assert.AreEqual(count, 1);
            Assert.AreEqual(html, "<p><img src=\"#\" class=\"img-fluid\" alt=\"text\" title=\"Figure\" /></p>\n");
        }
    }
}
