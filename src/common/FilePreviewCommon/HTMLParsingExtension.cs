// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
