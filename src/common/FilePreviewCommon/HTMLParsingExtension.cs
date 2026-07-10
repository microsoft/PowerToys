// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

using Markdig;
using Markdig.Extensions.Figures;
using Markdig.Extensions.Tables;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace Microsoft.PowerToys.FilePreviewCommon
{
    /// <summary>
    /// Callback if extension blocks external images.
    /// </summary>
    public delegate void ImagesBlockedCallBack();

    /// <summary>
    /// Markdig Extension to process html nodes in markdown AST.
    /// </summary>
    public class HTMLParsingExtension : IMarkdownExtension
    {
        /// <summary>
        /// Callback if extension blocks external images.
        /// </summary>
        private readonly ImagesBlockedCallBack imagesBlockedCallBack;

        /// <summary>
        /// Initializes a new instance of the <see cref="HTMLParsingExtension"/> class.
        /// </summary>
        /// <param name="imagesBlockedCallBack">Callback function if image is blocked by extension.</param>
        /// <param name="filePath">Absolute path of markdown file.</param>
        public HTMLParsingExtension(ImagesBlockedCallBack imagesBlockedCallBack, string filePath = "")
        {
            this.imagesBlockedCallBack = imagesBlockedCallBack;
            FilePath = filePath;
        }

        /// <summary>
        /// Gets or sets path to directory containing markdown file.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Gets or sets the base path used for path validation and relative URL computation.
        /// For local files this equals FilePath. For UNC paths this is the share root.
        /// </summary>
        public string? AllowedBasePath { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether local images should be rendered.
        /// </summary>
        public bool AllowLocalImages { get; set; }

        private static bool IsLocalImage(string? url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return false;
            }

            // Reject any URI-like scheme (http:, https:, data:, javascript:, file:, ...).
            // A colon is only permitted as part of a drive path like "C:\" or "C:/".
            int colonIndex = url.IndexOf(':');
            if (colonIndex >= 0)
            {
                bool isDrivePath = colonIndex == 1 && char.IsLetter(url[0]) && url.Length > 2 && (url[2] == '\\' || url[2] == '/');
                if (!isDrivePath)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Validates that a local image URL resolves to a path inside the allowed base path and
        /// computes the corresponding virtual host URL. Returns false for remote URLs, URI schemes
        /// (data:, javascript:, file:, ...), path traversal outside the base path and malformed paths.
        /// </summary>
        /// <param name="url">Image URL from the markdown document.</param>
        /// <param name="markdownDirectory">Directory containing the markdown file; relative URLs resolve against it.</param>
        /// <param name="allowedBasePath">Base path the resolved path must be contained in. Falls back to <paramref name="markdownDirectory"/> if empty.</param>
        /// <param name="virtualUrl">The rewritten virtual host URL on success.</param>
        /// <returns>True if the URL is a contained local image and <paramref name="virtualUrl"/> was set.</returns>
        public static bool TryGetLocalImageVirtualUrl(string? url, string markdownDirectory, string? allowedBasePath, [NotNullWhen(true)] out string? virtualUrl)
        {
            virtualUrl = null;

            if (!IsLocalImage(url))
            {
                return false;
            }

            try
            {
                string basePath = Path.GetFullPath(string.IsNullOrEmpty(allowedBasePath) ? markdownDirectory : allowedBasePath);
                string resolvedPath = Path.GetFullPath(Path.Combine(markdownDirectory, url));
                string relativePath = Path.GetRelativePath(basePath, resolvedPath);

                if (relativePath == "." || relativePath == ".." ||
                    relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                    relativePath.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal) ||
                    Path.IsPathRooted(relativePath))
                {
                    return false;
                }

                virtualUrl = "https://localmdimages/" + relativePath.Replace('\\', '/');
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (PathTooLongException)
            {
                return false;
            }
            catch (NotSupportedException)
            {
                return false;
            }
        }

        /// <inheritdoc/>
        public void Setup(MarkdownPipelineBuilder pipeline)
        {
            if (pipeline != null)
            {
                // Make sure we don't have a delegate twice
                pipeline.DocumentProcessed -= PipelineOnDocumentProcessed;
                pipeline.DocumentProcessed += PipelineOnDocumentProcessed;
            }
        }

        /// <inheritdoc/>
        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
        }

        /// <summary>
        /// Process nodes in markdown AST.
        /// </summary>
        /// <param name="document">Markdown Document.</param>
        public void PipelineOnDocumentProcessed(MarkdownDocument document)
        {
            foreach (var node in document.Descendants())
            {
                if (node is Block)
                {
                    if (node is Table)
                    {
                        node.GetAttributes().AddClass("table table-striped table-bordered");
                    }
                    else if (node is QuoteBlock)
                    {
                        node.GetAttributes().AddClass("blockquote");
                    }
                    else if (node is Figure)
                    {
                        node.GetAttributes().AddClass("figure");
                    }
                    else if (node is FigureCaption)
                    {
                        node.GetAttributes().AddClass("figure-caption");
                    }
                }
                else if (node is Inline)
                {
                    if (node is LinkInline link)
                    {
                        if (link.IsImage)
                        {
                            if (AllowLocalImages && TryGetLocalImageVirtualUrl(link.Url, FilePath, AllowedBasePath, out string? virtualUrl))
                            {
                                link.Url = virtualUrl;
                                link.GetAttributes().AddClass("img-fluid");
                            }
                            else
                            {
                                link.Url = "#";
                                link.GetAttributes().AddClass("img-fluid");
                                imagesBlockedCallBack();
                            }
                        }
                    }
                }
            }
        }
    }
}
