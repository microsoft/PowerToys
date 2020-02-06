// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using Common;
using Common.Utilities;
using SvgPreviewHandler.Utilities;

namespace SvgPreviewHandler
{
    /// <summary>
    /// Implementation of Control for Svg Preview Handler.
    /// </summary>
    public class SvgPreviewControl : FormHandlerControl
    {
        /// <summary>
        /// Browser Control to display Svg.
        /// </summary>
        private WebBrowser browser;

        /// <summary>
        /// Text box to display the information about blocked elements from Svg.
        /// </summary>
        private RichTextBox textBox;

        /// <summary>
        /// Represent if any blocked element is found in the Svg.
        /// </summary>
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
                string svgData = null;
                using (var stream = new StreamWrapper(dataSource as IStream))
                {
                    using (var reader = new StreamReader(stream))
                    {
                        svgData = reader.ReadToEnd();
                    }
                }

                svgData = SvgPreviewHandlerHelper.RemoveElements(svgData, out this.foundFilteredElements);

                if (this.foundFilteredElements)
                {
                    this.AddTextBoxControl();
                }

                this.AddBrowserControl(svgData);
                this.Resize += this.FormResized;
                base.DoPreview(dataSource);
            });
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

        /// <summary>
        /// Adds a Web Browser Control to Control Collection.
        /// </summary>
        /// <param name="svgData">Svg to display on Browser Control.</param>
        private void AddBrowserControl(string svgData)
        {
            this.browser = new WebBrowser();
            this.browser.DocumentText = svgData;
            this.browser.Dock = DockStyle.Fill;
            this.browser.IsWebBrowserContextMenuEnabled = false;
            this.browser.ScriptErrorsSuppressed = true;
            this.browser.ScrollBarsEnabled = true;
            this.Controls.Add(this.browser);
        }

        /// <summary>
        /// Adds a Text Box in Controls for showing information about blocked elements.
        /// </summary>
        private void AddTextBoxControl()
        {
            this.textBox = new RichTextBox();
            this.textBox.Text = "Some elements have been blocked to help prevent the sender from identifying your computer. Open this item to view all elements.";
            this.textBox.BackColor = Color.LightYellow;
            this.textBox.Multiline = true;
            this.textBox.Dock = DockStyle.Top;
            this.textBox.ReadOnly = true;
            this.textBox.ContentsResized += this.RTBContentsResized;
            this.textBox.ScrollBars = RichTextBoxScrollBars.None;
            this.textBox.BorderStyle = BorderStyle.None;
            this.Controls.Add(this.textBox);
        }
    }
}
