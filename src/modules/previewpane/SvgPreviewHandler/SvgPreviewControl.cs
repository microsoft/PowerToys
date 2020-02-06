// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using Common;
using Common.Utilities;

namespace SvgPreviewHandler
{
    /// <summary>
    /// Implementation of Control for Svg Preview Handler.
    /// </summary>
    public class SvgPreviewControl : FormHandlerControl
    {
        private Stream dataSourceStream;

        private RichTextBox textBox;

        private bool foundFilteredElements = false;

        /// <summary>
        /// Start the preview on the Control.
        /// </summary>
        /// <param name="dataSource">Stream reference to access source file.</param>
        public override void DoPreview<T>(T dataSource)
        {
            this.InvokeOnControlThread(() =>
            {
                this.foundFilteredElements = false;
                WebBrowser browser = new WebBrowser();
                string svgData = null;
                using (var stream = new StreamWrapper(dataSource as IStream))
                {
                    using (var reader = new StreamReader(stream))
                    {
                        svgData = reader.ReadToEnd();
                    }
                }

                browser.DocumentText = this.RemoveFilteredElements(svgData);
                browser.Dock = DockStyle.Fill;
                browser.IsWebBrowserContextMenuEnabled = false;
                browser.ScriptErrorsSuppressed = true;
                browser.ScrollBarsEnabled = true;

                if (this.foundFilteredElements)
                {
                    this.textBox = new RichTextBox();
                    this.textBox.Text = "Blocked scripts and image to load external resources components Blocked scripts and image to load external resources components Blocked scripts and image to load external resources components";
                    this.textBox.BackColor = Color.LightYellow;
                    this.textBox.Multiline = true;
                    this.textBox.Dock = DockStyle.Top;
                    this.textBox.ReadOnly = true;
                    this.textBox.ContentsResized += this.RTBContentsResized;
                    this.textBox.ScrollBars = RichTextBoxScrollBars.None;
                    this.textBox.BorderStyle = BorderStyle.None;
                    this.Controls.Add(this.textBox);
                }

                this.Controls.Add(browser);

                this.Resize += this.FormResized;
                base.DoPreview(dataSource);
            });
        }

        /// <summary>
        /// Free resources on the unload of Preview.
        /// </summary>
        public override void Unload()
        {
            base.Unload();
            if (this.dataSourceStream != null)
            {
                this.dataSourceStream.Dispose();
                this.dataSourceStream = null;
            }
        }

        private void RTBContentsResized(object sender, ContentsResizedEventArgs e)
        {
            RichTextBox richTextBox = (RichTextBox)sender;
            richTextBox.Height = e.NewRectangle.Height + 5;
        }

        private void FormResized(object sender, EventArgs e)
        {
            if (this.foundFilteredElements)
            {
                this.textBox.Width = this.Width;
            }
        }

        private string RemoveFilteredElements(string svgData)
        {
            var doc = XDocument.Parse(svgData);
            var elements = doc.Descendants().ToList();
            foreach (XElement node in elements)
            {
                if (string.Equals(node.Name.LocalName, "script", StringComparison.OrdinalIgnoreCase) || string.Equals(node.Name.LocalName, "image", StringComparison.OrdinalIgnoreCase))
                {
                    node.RemoveAll();
                    this.foundFilteredElements = true;
                }
            }

            return doc.ToString();
        }
    }
}
