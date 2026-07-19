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
        private static MarkdownPipeline BuildPipeline(IMarkdownExtension extension)
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
            Microsoft.PowerToys.FilePreviewCommon.HTMLParsingExtension htmlParsingExtension = new Microsoft.PowerToys.FilePreviewCommon.HTMLParsingExtension(() => { });
            MarkdownPipeline markdownPipeline = BuildPipeline(htmlParsingExtension);

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
            Microsoft.PowerToys.FilePreviewCommon.HTMLParsingExtension htmlParsingExtension = new Microsoft.PowerToys.FilePreviewCommon.HTMLParsingExtension(() => { });
            MarkdownPipeline markdownPipeline = BuildPipeline(htmlParsingExtension);

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
            Microsoft.PowerToys.FilePreviewCommon.HTMLParsingExtension htmlParsingExtension = new Microsoft.PowerToys.FilePreviewCommon.HTMLParsingExtension(() => { }, "C:\\Users\\");
            MarkdownPipeline markdownPipeline = BuildPipeline(htmlParsingExtension);

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
            Microsoft.PowerToys.FilePreviewCommon.HTMLParsingExtension htmlParsingExtension = new Microsoft.PowerToys.FilePreviewCommon.HTMLParsingExtension(() => { }, "C:/Users/");
            MarkdownPipeline markdownPipeline = BuildPipeline(htmlParsingExtension);

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
            Microsoft.PowerToys.FilePreviewCommon.HTMLParsingExtension htmlParsingExtension = new Microsoft.PowerToys.FilePreviewCommon.HTMLParsingExtension(() => { count++; });
            MarkdownPipeline markdownPipeline = BuildPipeline(htmlParsingExtension);

            // Act
            string html = Markdown.ToHtml(mdString, markdownPipeline);

            // Assert
            Assert.AreEqual(1, count);
            const string expected = "<p><img src=\"#\" class=\"img-fluid\" alt=\"text\" title=\"Figure\" /></p>\n";
            Assert.AreEqual(expected, html);
        }

        [DataTestMethod]
        [DataRow("images/test.png", @"C:\docs", @"C:\docs", "https://localmdimages/images/test.png")]
        [DataRow(@"C:\docs\images\test.png", @"C:\docs", @"C:\docs", "https://localmdimages/images/test.png")]
        [DataRow("images/test.png", @"\\server\share\sub\dir", @"\\server\share", "https://localmdimages/sub/dir/images/test.png")]
        [DataRow("../test.png", @"\\server\share\sub", @"\\server\share", "https://localmdimages/test.png")]
        public void TryGetLocalImageVirtualUrlAllowsContainedPaths(string url, string markdownDirectory, string basePath, string expectedVirtualUrl)
        {
            bool result = Microsoft.PowerToys.FilePreviewCommon.HTMLParsingExtension.TryGetLocalImageVirtualUrl(url, markdownDirectory, basePath, out string virtualUrl);

            Assert.IsTrue(result);
            Assert.AreEqual(expectedVirtualUrl, virtualUrl);
        }

        [DataTestMethod]
        [DataRow("http://example.com/a.png", @"C:\docs", @"C:\docs")]
        [DataRow("https://example.com/a.png", @"C:\docs", @"C:\docs")]
        [DataRow("data:image/png;base64,iVBORw0KGgo=", @"C:\docs", @"C:\docs")]
        [DataRow("javascript:alert(1)", @"C:\docs", @"C:\docs")]
        [DataRow("file:///C:/secret.png", @"C:\docs", @"C:\docs")]
        [DataRow("../secret.png", @"C:\docs", @"C:\docs")]
        [DataRow(@"..\..\secret.png", @"C:\docs\sub", @"C:\docs\sub")]
        [DataRow(@"C:\other\secret.png", @"C:\docs", @"C:\docs")]
        [DataRow(@"C:\docsBackup\secret.png", @"C:\docs", @"C:\docs")]
        [DataRow(@"\\server\share2\secret.png", @"\\server\share\sub", @"\\server\share")]
        [DataRow("", @"C:\docs", @"C:\docs")]
        public void TryGetLocalImageVirtualUrlBlocksUnsafeUrls(string url, string markdownDirectory, string basePath)
        {
            bool result = Microsoft.PowerToys.FilePreviewCommon.HTMLParsingExtension.TryGetLocalImageVirtualUrl(url, markdownDirectory, basePath, out string virtualUrl);

            Assert.IsFalse(result);
            Assert.IsNull(virtualUrl);
        }

        [DataTestMethod]
        [DataRow("https://localmdimages/images/test.png", @"C:\docs", @"C:\docs\images\test.png")]
        [DataRow("https://localmdimages/sub/dir/images/test.png", @"\\server\share", @"\\server\share\sub\dir\images\test.png")]
        [DataRow("https://localmdimages/my%20image.png", @"C:\docs", @"C:\docs\my image.png")]
        public void TryResolveVirtualUrlAllowsContainedRequests(string requestUri, string basePath, string expectedPath)
        {
            bool result = Microsoft.PowerToys.FilePreviewCommon.HTMLParsingExtension.TryResolveVirtualUrl(requestUri, basePath, out string resolvedPath);

            Assert.IsTrue(result);
            Assert.AreEqual(expectedPath, resolvedPath);
        }

        [DataTestMethod]
        [DataRow("https://localmdimages/.." + "%2F" + "secret.png", @"C:\docs")]
        [DataRow("https://localmdimages/.." + "%5C" + "secret.png", @"C:\docs")]
        [DataRow("https://localmdimages/C" + "%3A%5C" + "other" + "%5C" + "secret.png", @"C:\docs")]
        [DataRow("https://example.com/images/test.png", @"C:\docs")]
        [DataRow("https://localmdimages/", @"C:\docs")]
        [DataRow("not a url", @"C:\docs")]
        [DataRow("", @"C:\docs")]
        public void TryResolveVirtualUrlBlocksUnsafeRequests(string requestUri, string basePath)
        {
            bool result = Microsoft.PowerToys.FilePreviewCommon.HTMLParsingExtension.TryResolveVirtualUrl(requestUri, basePath, out string resolvedPath);

            Assert.IsFalse(result);
            Assert.IsNull(resolvedPath);
        }

        [TestMethod]
        public void ExtensionRewritesLocalImageToVirtualHostWhenLocalImagesAllowed()
        {
            // arrange
            string mdString = "![text](images/test.png)";
            Microsoft.PowerToys.FilePreviewCommon.HTMLParsingExtension htmlParsingExtension = new Microsoft.PowerToys.FilePreviewCommon.HTMLParsingExtension(() => { }, @"C:\docs");
            htmlParsingExtension.AllowLocalImages = true;
            MarkdownPipeline markdownPipeline = BuildPipeline(htmlParsingExtension);

            // Act
            string html = Markdown.ToHtml(mdString, markdownPipeline);

            // Assert
            const string expected = "<p><img src=\"https://localmdimages/images/test.png\" class=\"img-fluid\" alt=\"text\" /></p>\n";
            Assert.AreEqual(expected, html);
        }

        [TestMethod]
        public void ExtensionBlocksPathTraversalAndMakesCallbackWhenLocalImagesAllowed()
        {
            // arrange
            int count = 0;
            string mdString = "![text](../secret.png)";
            Microsoft.PowerToys.FilePreviewCommon.HTMLParsingExtension htmlParsingExtension = new Microsoft.PowerToys.FilePreviewCommon.HTMLParsingExtension(() => { count++; }, @"C:\docs");
            htmlParsingExtension.AllowLocalImages = true;
            MarkdownPipeline markdownPipeline = BuildPipeline(htmlParsingExtension);

            // Act
            string html = Markdown.ToHtml(mdString, markdownPipeline);

            // Assert
            Assert.AreEqual(1, count);
            const string expected = "<p><img src=\"#\" class=\"img-fluid\" alt=\"text\" /></p>\n";
            Assert.AreEqual(expected, html);
        }
    }
}
