// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Markdig;
using Microsoft.PowerToys.PreviewHandler.Markdown;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PreviewPaneUnitTests
{
    [TestClass]
    public class HTMLParsingExtensionTest
    {
        private static MarkdownPipeline BuidPipeline(IMarkdownExtension extension)
        {
            MarkdownPipelineBuilder pipelineBuilder = new MarkdownPipelineBuilder().UseAdvancedExtensions();
            pipelineBuilder.Extensions.Add(extension);

            return pipelineBuilder.Build();
        }

        [TestMethod]
        public void ExtensionUpdatesTablesClassWhenUsed()
        {
            // Arrange
            string mdString = "| A | B |\n| -- | -- | ";
            HTMLParsingExtension htmlParsingExtension = new HTMLParsingExtension(() => { });
            MarkdownPipeline markdownPipeline = BuidPipeline(htmlParsingExtension);

            // Act
            string html = Markdown.ToHtml(mdString, markdownPipeline);

            // Assert
            const string expected = "<table class=\"table table-striped table-bordered\">\n<thead>\n<tr>\n<th>A</th>\n<th>B</th>\n</tr>\n</thead>\n</table>\n";
            Assert.AreEqual(expected, html);
        }

        [TestMethod]
        public void ExtensionUpdatesBlockQuotesClassWhenUsed()
        {
            // Arrange
            string mdString = "> Blockquotes.";
            HTMLParsingExtension htmlParsingExtension = new HTMLParsingExtension(() => { });
            MarkdownPipeline markdownPipeline = BuidPipeline(htmlParsingExtension);

            // Act
            string html = Markdown.ToHtml(mdString, markdownPipeline);

            // Assert
            const string expected = "<blockquote class=\"blockquote\">\n<p>Blockquotes.</p>\n</blockquote>\n";
            Assert.AreEqual(expected, html);
        }

        [TestMethod]
        public void ExtensionUpdatesFigureClassAndBlocksRelativeUrlWhenUsed()
        {
            // arrange
            string mdString = "![text](a.jpg \"Figure\")";
            HTMLParsingExtension htmlParsingExtension = new HTMLParsingExtension(() => { }, "C:\\Users\\");
            MarkdownPipeline markdownPipeline = BuidPipeline(htmlParsingExtension);

            // Act
            string html = Markdown.ToHtml(mdString, markdownPipeline);

            // Assert
            const string expected = "<p><img src=\"#\" class=\"img-fluid\" alt=\"text\" title=\"Figure\" /></p>\n";
            Assert.AreEqual(expected, html);
        }

        [TestMethod]
        public void ExtensionAddsClassToFigureCaptionWhenUsed()
        {
            // arrange
            string mdString = "^^^ This is a caption";
            HTMLParsingExtension htmlParsingExtension = new HTMLParsingExtension(() => { }, "C:/Users/");
            MarkdownPipeline markdownPipeline = BuidPipeline(htmlParsingExtension);

            // Act
            string html = Markdown.ToHtml(mdString, markdownPipeline);

            // Assert
            const string expected = "<figure class=\"figure\">\n<figcaption class=\"figure-caption\">This is a caption</figcaption>\n</figure>\n";
            Assert.AreEqual(expected, html);
        }

        [TestMethod]
        public void ExtensionRemovesExternalImageUrlAndMakeCallbackWhenUsed()
        {
            // arrange
            int count = 0;
            string mdString = "![text](http://dev.nodeca.com \"Figure\")";
            HTMLParsingExtension htmlParsingExtension = new HTMLParsingExtension(() => { count++; });
            MarkdownPipeline markdownPipeline = BuidPipeline(htmlParsingExtension);

            // Act
            string html = Markdown.ToHtml(mdString, markdownPipeline);

            // Assert
            Assert.AreEqual(1, count);
            const string expected = "<p><img src=\"#\" class=\"img-fluid\" alt=\"text\" title=\"Figure\" /></p>\n";
            Assert.AreEqual(expected, html);
        }
    }
}
