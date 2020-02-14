// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Common;
using Markdig;
using MarkdownPreviewHandler.Properties;

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
        private readonly string htmlHeader = "<!doctype html><style>body{width:100%;margin:0;font-family:-apple-system,BlinkMacSystemFont,\"Segoe UI\",Roboto,\"Helvetica Neue\",Arial,\"Noto Sans\",sans-serif,\"Apple Color Emoji\",\"Segoe UI Emoji\",\"Segoe UI Symbol\",\"Noto Color Emoji\";font-size:1rem;font-weight:400;line-height:1.5;color:#212529;text-align:left;background-color:#fff}.container{padding:5%}body img{max-width:100%;height:auto}body h1,body h2,body h3,body h4,body h5,body h6{margin-top:24px;margin-bottom:16px;font-weight:600;line-height:1.25}body h1,body h2{padding-bottom:.3em;border-bottom:1px solid #eaecef}body{font-family:-apple-system,BlinkMacSystemFont,Segoe UI,Helvetica,Arial,sans-serif,Apple Color Emoji,Segoe UI Emoji}body h3{font-size:1.25em}body h4{font-size:1em}body h5{font-size:.875em}body h6{font-size:.85em;color:#6a737d}pre{font-family:SFMono-Regular,Consolas,Liberation Mono,Menlo,monospace;background-color:#f6f8fa;border-radius:3px;padding:16px;font-size:85%}a{color:#0366d6}strong{font-weight:600}em{font-style:italic}code{padding:.2em .4em;margin:0;font-size:85%;background-color:#f6f8fa;border-radius:3px}hr{border-color:#EEE -moz-use-text-color #FFF;border-style:solid none;border-width:.5px 0;margin:18px 0}table{display:block;width:100%;overflow:auto;border-spacing:0;border-collapse:collapse}tbody{display:table-row-group;vertical-align:middle;border-color:inherit;display:table-row;vertical-align:inherit;border-color:inherit}table tr{background-color:#fff;border-top:1px solid #c6cbd1}tr{display:table-row;vertical-align:inherit;border-color:inherit}table td,table th{padding:6px 13px;border:1px solid #dfe2e5}th{font-weight:600;display:table-cell;vertical-align:inherit;font-weight:bold;text-align:-internal-center}thead{display:table-header-group;vertical-align:middle;border-color:inherit}td{display:table-cell;vertical-align:inherit}code,pre,tt{font-family:SFMono-Regular,Menlo,Monaco,Consolas,\"Liberation Mono\",\"Courier New\",monospace;color:#24292e;overflow-x:auto}pre code{font-size:inherit;color:inherit;word-break:normal}blockquote{background-color:#fff;border-radius:3px;padding:15px;font-size:14px;display:block;margin-block-start:1em;margin-block-end:1em;margin-inline-start:40px;margin-inline-end:40px;padding:0 1em;color:#6a737d;border-left:.25em solid #dfe2e5}</style><body><div class=\"container\">";

        /// <summary>
        /// Markdown HTML footer.
        /// </summary>
        private readonly string htmlFooter = "</div></body></html>";

        /// <summary>
        /// RichTextBox control to display if external images are blocked.
        /// </summary>
        private RichTextBox infoBar;

        /// <summary>
        /// WebBrowser control to display markdown html.
        /// </summary>
        private WebBrowser browser;

        /// <summary>
        /// True if external image is blocked, false otherwise.
        /// </summary>
        private bool infoBarDisplayed = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkdownPreviewHandlerControl"/> class.
        /// </summary>
        public MarkdownPreviewHandlerControl()
        {
            this.extension = new HTMLParsingExtension(this.ImagesBlockedCallBack);
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
                try
                {
                    this.infoBarDisplayed = false;

                    StringBuilder sb = new StringBuilder();
                    string filePath = dataSource as string;
                    string fileText = File.ReadAllText(filePath);
                    this.extension.BaseUrl = Path.GetDirectoryName(filePath);

                    MarkdownPipeline pipeline = this.pipelineBuilder.Build();
                    string parsedMarkdown = Markdown.ToHtml(fileText, pipeline);
                    sb.AppendFormat("{0}{1}{2}", this.htmlHeader, parsedMarkdown, this.htmlFooter);
                    string markdownHTML = this.RemoveScriptFromHTML(sb.ToString());

                    this.browser = new WebBrowser
                    {
                        DocumentText = markdownHTML,
                        Dock = DockStyle.Fill,
                        IsWebBrowserContextMenuEnabled = false,
                        ScriptErrorsSuppressed = true,
                        ScrollBarsEnabled = true,
                    };
                    this.browser.Navigating += this.WebBrowserNavigating;
                    this.Controls.Add(this.browser);

                    if (this.infoBarDisplayed)
                    {
                        this.infoBar = this.GetTextBoxControl(Resources.BlockedImageInfoText);
                        this.Controls.Add(this.infoBar);
                    }

                    this.Resize += this.FormResized;
                    base.DoPreview(dataSource);
                    MarkdownTelemetry.Log.MarkdownFilePreviewed();
                }
                catch (Exception e)
                {
                    MarkdownTelemetry.Log.MarkdownFilePreviewError(e.Message);
                    this.infoBarDisplayed = true;
                    this.infoBar = this.GetTextBoxControl(Resources.MarkdownNotPreviewedError);
                    this.Resize += this.FormResized;
                    this.Controls.Clear();
                    this.Controls.Add(this.infoBar);
                    base.DoPreview(dataSource);
                }
            });
        }

        /// <summary>
        /// Removes script tag from html string.
        /// </summary>
        /// <param name="html">html string.</param>
        /// <returns>HTML string without script tag.</returns>
        public string RemoveScriptFromHTML(string html)
        {
            HtmlAgilityPack.HtmlDocument doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            doc.DocumentNode.Descendants()
                            .Where(n => n.Name == "script")
                            .ToList()
                            .ForEach(n => n.Remove());
            return doc.DocumentNode.InnerHtml;
        }

        /// <summary>
        /// Gets a textbox control.
        /// </summary>
        /// <param name="message">Message to be displayed in textbox.</param>
        /// <returns>An object of type <see cref="RichTextBox"/>.</returns>
        private RichTextBox GetTextBoxControl(string message)
        {
            RichTextBox richTextBox = new RichTextBox
            {
                Text = message,
                BackColor = Color.LightYellow,
                Multiline = true,
                Dock = DockStyle.Top,
                ReadOnly = true,
            };
            richTextBox.ContentsResized += this.RTBContentsResized;
            richTextBox.ScrollBars = RichTextBoxScrollBars.None;
            richTextBox.BorderStyle = BorderStyle.None;

            return richTextBox;
        }

        /// <summary>
        /// Gets a textbox control.
        /// </summary>
        /// <param name="message">Message to be displayed in textbox.</param>
        /// <returns>An object of type <see cref="RichTextBox"/>.</returns>
        private RichTextBox GetTextBoxControl(string message)
        {
            RichTextBox richTextBox = new RichTextBox
            {
                Text = message,
                BackColor = Color.LightYellow,
                Multiline = true,
                Dock = DockStyle.Top,
                ReadOnly = true,
            };
            richTextBox.ContentsResized += this.RTBContentsResized;
            richTextBox.ScrollBars = RichTextBoxScrollBars.None;
            richTextBox.BorderStyle = BorderStyle.None;

            return richTextBox;
        }

        /// <summary>
        /// Callback when RichTextBox is resized.
        /// </summary>
        /// <param name="sender">Reference to resized control.</param>
        /// <param name="e">Provides data for the resize event.</param>
        private void RTBContentsResized(object sender, ContentsResizedEventArgs e)
        {
            RichTextBox richTextBox = (RichTextBox)sender;
            richTextBox.Height = e.NewRectangle.Height + 5;
        }

        /// <summary>
        /// Callback when form is resized.
        /// </summary>
        /// <param name="sender">Reference to resized control.</param>
        /// <param name="e">Provides data for the event.</param>
        private void FormResized(object sender, EventArgs e)
        {
            if (this.infoBarDisplayed)
            {
                this.infoBar.Width = this.Width;
            }
        }

        /// <summary>
        /// Callback when image is blocked by extension.
        /// </summary>
        private void ImagesBlockedCallBack()
        {
            this.infoBarDisplayed = true;
        }

        /// <summary>
        /// Callback when link tag is clicked in html.
        /// </summary>
        /// <param name="sender">Reference to resized control.</param>
        /// <param name="e">Provides data for the WebBrowserNavigatingEventArgs event.</param>
        private void WebBrowserNavigating(object sender, WebBrowserNavigatingEventArgs e)
        {
            e.Cancel = true;
            Process.Start(e.Url.ToString());
        }
    }
}
