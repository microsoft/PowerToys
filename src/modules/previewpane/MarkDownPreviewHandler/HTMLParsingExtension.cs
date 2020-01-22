// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Markdig;
using Markdig.Extensions.Figures;
using Markdig.Extensions.Tables;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace MarkDownPreviewHandler
{
    /// <summary>
    /// Markdig Extension to process html nodes in markdown AST.
    /// </summary>
    internal class HTMLParsingExtension : IMarkdownExtension
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HTMLParsingExtension"/> class.
        /// </summary>
        public HTMLParsingExtension(string baseUrl = "")
        {
            this.BaseUrl = baseUrl;
        }

        /// <summary>
        /// Gets or sets path to directory containing markdown file.
        /// </summary>
        public string BaseUrl { get; set; }

        /// <inheritdoc/>
        public void Setup(MarkdownPipelineBuilder pipeline)
        {
            // Make sure we don't have a delegate twice
            pipeline.DocumentProcessed -= this.PipelineOnDocumentProcessed;
            pipeline.DocumentProcessed += this.PipelineOnDocumentProcessed;
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
                    if (node is LinkInline link && link.IsImage)
                    {
                        if (!Uri.TryCreate(link.Url, UriKind.Absolute, out Uri uriLink))
                        {
                            link.Url = link.Url.TrimStart('/', '\\');
                            this.BaseUrl = this.BaseUrl.TrimEnd('/', '\\');
                            uriLink = new Uri(Path.Combine(this.BaseUrl, link.Url));
                            link.Url = uriLink.ToString();
                        }

                        link.GetAttributes().AddClass("img-fluid");
                    }
                }
            }
        }
    }
}
