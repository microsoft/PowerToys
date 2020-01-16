// Copyright (c) Alexandre Mutel. All rights reserved.
// This file is licensed under the BSD-Clause 2 license. 
// See the license.txt file in the project root for more information.

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
    /// Extension for tagging some HTML elements with bootstrap classes.
    /// </summary>
    /// <seealso cref="Markdig.IMarkdownExtension" />
    public class BootstrapExtension : IMarkdownExtension
    {
        public static string BaseUrl;

        public void Setup(MarkdownPipelineBuilder pipeline)
        {
            // Make sure we don't have a delegate twice
            pipeline.DocumentProcessed -= PipelineOnDocumentProcessed;
            pipeline.DocumentProcessed += PipelineOnDocumentProcessed;
        }

        public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
        {
        }

        private static void PipelineOnDocumentProcessed(MarkdownDocument document)
        {
            foreach (var node in document.Descendants())
            {
                if (node is HtmlBlock)
                {
                    var content = node as HtmlBlock;
                    Console.WriteLine(content.Type);

                }
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
                    var link = node as LinkInline;

                    if (link != null && link.IsImage)
                    {
                        if (!Uri.TryCreate(link.Url, UriKind.Absolute, out Uri uriLink))
                        {
                            link.Url = link.Url.TrimStart('/');
                            BaseUrl = BaseUrl.TrimEnd('/');
                            uriLink = new Uri(Path.Combine(BaseUrl, link.Url));
                            //link.GetAttributes().AddClass("img-fluid");
                            link.Url = uriLink.ToString();
                        }
                        link.GetAttributes().AddClass("img-fluid");
                    }
                }
            }
        }
    }
}