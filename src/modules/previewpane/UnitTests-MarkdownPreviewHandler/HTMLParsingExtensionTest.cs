// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

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

        // Resolving checks each path component for reparse points, so these cases operate on real
        // files rather than notional paths.
        [DataTestMethod]
        [DataRow("images/test.png", "https://localmdimages/images/test.png")]
        [DataRow("sub/dir/images/test.png", "https://localmdimages/sub/dir/images/test.png")]
        [DataRow("my image.png", "https://localmdimages/my%20image.png")]
        public void TryResolveVirtualUrlAllowsContainedRequests(string relativePath, string requestUri)
        {
            string root = Path.Combine(Path.GetTempPath(), "ptmd-" + Guid.NewGuid().ToString("N"));
            string expectedPath = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(expectedPath));
            File.WriteAllText(expectedPath, "not really an image");

            try
            {
                bool result = Microsoft.PowerToys.FilePreviewCommon.HTMLParsingExtension.TryResolveVirtualUrl(requestUri, root, out string resolvedPath);

                Assert.IsTrue(result);
                Assert.AreEqual(expectedPath, resolvedPath);
            }
            finally
            {
                try
                {
                    Directory.Delete(root, true);
                }
                catch (IOException)
                {
                }
            }
        }

        [TestMethod]
        public void TryResolveVirtualUrlRejectsMissingFile()
        {
            string root = Path.Combine(Path.GetTempPath(), "ptmd-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            try
            {
                bool result = Microsoft.PowerToys.FilePreviewCommon.HTMLParsingExtension.TryResolveVirtualUrl(
                    "https://localmdimages/does-not-exist.png", root, out string resolvedPath);

                Assert.IsFalse(result, "a path that cannot be inspected must fail closed");
                Assert.IsNull(resolvedPath);
            }
            finally
            {
                try
                {
                    Directory.Delete(root, true);
                }
                catch (IOException)
                {
                }
            }
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

        [DataTestMethod]
        [DataRow("<img src=\"images/test.png\" />")]
        [DataRow("<img src='images/test.png' />")]
        [DataRow("<img src=images/test.png />")]
        [DataRow("<img alt=\"x\" SRC = \"images/test.png\" />")]
        public void RawHtmlImageIsRewrittenForEveryQuoteStyle(string mdString)
        {
            string html = Microsoft.PowerToys.FilePreviewCommon.MarkdownHelper.MarkdownHtml(
                mdString, "light", @"C:\docs\doc.md", () => { }, true, @"C:\docs");

            StringAssert.Contains(html, "https://localmdimages/images/test.png");
        }

        [DataTestMethod]
        [DataRow("<img src=\"https://example.com/track.png\" />")]
        [DataRow("<img src='https://example.com/track.png' />")]
        [DataRow("<img src=https://example.com/track.png />")]
        [DataRow("<img src='../secret.png' />")]
        [DataRow("<img src='data:image/png;base64,iVBORw0KGgo=' />")]
        public void RawHtmlImageIsBlockedForEveryQuoteStyle(string mdString)
        {
            int blockedCount = 0;
            string html = Microsoft.PowerToys.FilePreviewCommon.MarkdownHelper.MarkdownHtml(
                mdString, "light", @"C:\docs\doc.md", () => { blockedCount++; }, true, @"C:\docs");

            Assert.AreNotEqual(0, blockedCount, "the blocked-images callback should fire so the info bar is shown");
            StringAssert.Contains(html, "src=");
            Assert.IsFalse(html.Contains("example.com"), "remote source must not survive the rewrite");
            Assert.IsFalse(html.Contains("secret.png"), "traversal source must not survive the rewrite");
            Assert.IsFalse(html.Contains("base64"), "data URI must not survive the rewrite");
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void RawHtmlSrcsetIsRemovedInBothSettingStates(bool allowLocalImages)
        {
            int blockedCount = 0;
            string mdString = "<img src=\"images/test.png\" srcset=\"data:image/png;base64,iVBORw0KGgo= 2x\" />";

            string html = Microsoft.PowerToys.FilePreviewCommon.MarkdownHelper.MarkdownHtml(
                mdString, "light", @"C:\docs\doc.md", () => { blockedCount++; }, allowLocalImages, @"C:\docs");

            Assert.IsFalse(html.Contains("srcset"), "srcset must be removed so its candidates cannot bypass the src sanitizer");
            Assert.IsFalse(html.Contains("base64"), "the srcset data URI must not survive");
            Assert.AreNotEqual(0, blockedCount);
        }

        [DataTestMethod]
        [DataRow("<img src=\"data:image/png;base64,iVBORw0KGgo=\" />", "base64")]
        [DataRow("<img src=\"https://example.com/track.png\" />", "example.com")]
        [DataRow("<img src=\"images/test.png\" />", "images/test.png")]
        public void RawHtmlSrcIsSanitizedWhenLocalImagesDisabled(string mdString, string forbidden)
        {
            int blockedCount = 0;

            string html = Microsoft.PowerToys.FilePreviewCommon.MarkdownHelper.MarkdownHtml(
                mdString, "light", @"C:\docs\doc.md", () => { blockedCount++; }, false, @"C:\docs");

            Assert.IsFalse(html.Contains(forbidden), "raw HTML img sources must be blocked while the setting is off");
            StringAssert.Contains(html, "src=\"#\"");
            Assert.AreNotEqual(0, blockedCount);
        }

        [TestMethod]
        public void TryResolveVirtualUrlRejectsPathBehindDirectoryLink()
        {
            string root = Path.Combine(Path.GetTempPath(), "ptmd-" + Guid.NewGuid().ToString("N"));
            string allowed = Path.Combine(root, "allowed");
            string outside = Path.Combine(root, "outside");
            Directory.CreateDirectory(allowed);
            Directory.CreateDirectory(outside);
            File.WriteAllText(Path.Combine(outside, "secret.png"), "not really an image");

            try
            {
                string link = Path.Combine(allowed, "link");
                try
                {
                    Directory.CreateSymbolicLink(link, outside);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    Assert.Inconclusive("Creating a directory link requires privilege on this machine.");
                    return;
                }

                // Lexically "link/secret.png" sits inside the allowed directory, but the read would
                // follow the link outside it.
                bool result = Microsoft.PowerToys.FilePreviewCommon.HTMLParsingExtension.TryResolveVirtualUrl(
                    "https://localmdimages/link/secret.png", allowed, out string resolvedPath);

                Assert.IsFalse(result, "a path traversing a reparse point must be rejected");
                Assert.IsNull(resolvedPath);
            }
            finally
            {
                try
                {
                    Directory.Delete(root, true);
                }
                catch (IOException)
                {
                }
            }
        }

        [DataTestMethod]
        [DataRow(true, "img-src https://localmdimages")]
        [DataRow(false, "img-src 'none'")]
        public void ContentSecurityPolicyMatchesTheSettingState(bool allowLocalImages, string expectedImgSrc)
        {
            string html = Microsoft.PowerToys.FilePreviewCommon.MarkdownHelper.MarkdownHtml(
                "# heading", "light", @"C:\docs\doc.md", () => { }, allowLocalImages, @"C:\docs");

            StringAssert.Contains(html, "http-equiv=\"Content-Security-Policy\"");
            StringAssert.Contains(html, expectedImgSrc);
            StringAssert.Contains(html, "default-src 'none'");
            StringAssert.Contains(html, "object-src 'none'");
            StringAssert.Contains(html, "frame-src 'none'");

            // The policy has to precede the content it governs.
            Assert.IsTrue(
                html.IndexOf("Content-Security-Policy", StringComparison.Ordinal) < html.IndexOf("<body", StringComparison.Ordinal),
                "the policy must appear before the body so the parser places it in the head");
        }

        [TestMethod]
        public void FourArgumentOverloadStillBlocksImages()
        {
            int blockedCount = 0;

            // Callers compiled against the original signature (Peek) must keep working.
            string html = Microsoft.PowerToys.FilePreviewCommon.MarkdownHelper.MarkdownHtml(
                "![text](images/test.png)", "light", @"C:\docs\doc.md", () => { blockedCount++; });

            StringAssert.Contains(html, "src=\"#\"");
            StringAssert.Contains(html, "img-src 'none'");
            Assert.AreNotEqual(0, blockedCount);
        }

        [DataTestMethod]
        [DataRow("images/a#b.png")]
        [DataRow("images/a%20b.png")]
        [DataRow("images/a b.png")]
        [DataRow("images/a&b.png")]
        public void VirtualUrlRoundTripsFilenamesWithReservedCharacters(string relativePath)
        {
            string root = Path.Combine(Path.GetTempPath(), "ptmd-" + Guid.NewGuid().ToString("N"));
            string onDisk = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(onDisk));
            File.WriteAllText(onDisk, "not really an image");

            try
            {
                bool built = Microsoft.PowerToys.FilePreviewCommon.HTMLParsingExtension.TryGetLocalImageVirtualUrl(
                    relativePath, root, root, out string virtualUrl);
                Assert.IsTrue(built, "the URL should be produced");

                // A URL carrying a raw '#' or '%' would be truncated or misparsed here.
                bool resolved = Microsoft.PowerToys.FilePreviewCommon.HTMLParsingExtension.TryResolveVirtualUrl(
                    virtualUrl, root, out string resolvedPath);

                Assert.IsTrue(resolved, $"the escaped URL '{virtualUrl}' should resolve back");
                Assert.AreEqual(onDisk, resolvedPath);
            }
            finally
            {
                try
                {
                    Directory.Delete(root, true);
                }
                catch (IOException)
                {
                }
            }
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
