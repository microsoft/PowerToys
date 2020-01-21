// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Windows.Forms;
using Common;
using Markdig;

namespace MarkdownPreviewHandler
{
    /// <summary>
    /// Win Form Implementation for Markdown Preview Handler.
    /// </summary>
    public class MarkdownPreviewHandlerControl : FormHandlerControl
    {
        /// <summary>
        /// Extension to modify markdown AST.
        /// </summary>
        private readonly HTMLParsingExtension extension;

        /// <summary>
        /// Markdig Pipeline builder.
        /// </summary>
        private readonly MarkdownPipelineBuilder pipelineBuilder;

        /// <summary>
        /// Markdown HTML header.
        /// </summary>
        private readonly string htmlHeader = "<!doctype html><html lang=\"en\"><head></head><body><div class=\"container\">";

        /// <summary>
        /// Markdown HTML footer.
        /// </summary>
        private readonly string htmlFooter = "</div></body></html>";

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkdownPreviewHandlerControl"/> class.
        /// </summary>
        public MarkdownPreviewHandlerControl()
        {
            this.extension = new HTMLParsingExtension();
            this.pipelineBuilder = new MarkdownPipelineBuilder().UseAdvancedExtensions().UseEmojiAndSmiley();
            this.pipelineBuilder.Extensions.Add(this.extension);
        }

        /// <summary>
        /// Start the preview on the Control.
        /// </summary>
        /// <param name="dataSource">Path to the file.</param>
        public override void DoPreview<T>(T dataSource)
        {
            this.InvokeOnControlThread(() =>
            {
                string filePath = dataSource as string;
                string fileText = File.ReadAllText(filePath);
                this.extension.BaseUrl = Path.GetDirectoryName(filePath);

                MarkdownPipeline pipeline = this.pipelineBuilder.Build();
                string parsedMarkdown = Markdown.ToHtml(fileText, pipeline);
                string html = this.htmlHeader + parsedMarkdown + this.htmlFooter;

                File.WriteAllText("C:\\Users\\divyan\\Desktop\\output.txt", html);
                WebBrowser browser = new WebBrowser();
                browser.DocumentText = html;
                browser.Dock = DockStyle.Fill;
                browser.IsWebBrowserContextMenuEnabled = false;
                browser.ScriptErrorsSuppressed = true;
                browser.ScrollBarsEnabled = true;
                this.Controls.Add(browser);
                base.DoPreview(dataSource);
            });
        }
    }
}
