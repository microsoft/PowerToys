// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;
using Common;
using Common.Utilities;
using PreviewHandlerCommon;

namespace SvgPreviewHandler
{
    /// <summary>
    /// Implementation of Control for Svg Preview Handler.
    /// </summary>
    public class SvgPreviewControl : FormHandlerControl
    {
        /// <summary>
        /// Extended Browser Control to display Svg.
        /// </summary>
        private WebBrowserExt browser;

        /// <summary>
        /// Text box to display the information about blocked elements from Svg.
        /// </summary>
        private RichTextBox textBox;

        /// <summary>
        /// Represent if an text box info bar is added for showing message.
        /// </summary>
        private bool infoBarAdded = false;

        /// <summary>
        /// Start the preview on the Control.
        /// </summary>
        /// <param name="dataSource">Stream reference to access source file.</param>
        public override void DoPreview<T>(T dataSource)
        {
            this.InvokeOnControlThread(() =>
            {
                try
                {
                    this.infoBarAdded = false;
                    string svgData = null;
                    using (var stream = new StreamWrapper(dataSource as IStream))
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            svgData = reader.ReadToEnd();
                        }
                    }

                    this.AddBrowserControl(svgData);
                    this.Resize += this.FormResized;
                    base.DoPreview(dataSource);
                    SvgTelemetry.Log.SvgFilePreviewed();
                }
                catch (Exception ex)
                {
                    SvgTelemetry.Log.SvgFilePreviewError(ex.Message);
                    this.Controls.Clear();
                    this.infoBarAdded = true;
                    this.AddTextBoxControl(Resource.SvgNotPreviewedError);
                    base.DoPreview(dataSource);
                }
            });
        }

        /// <summary>
        /// Occurs when RichtextBox is resized.
        /// </summary>
        /// <param name="sender">Reference to resized control.</param>
        /// <param name="e">Provides data for the ContentsResized event.</param>
        private void RTBContentsResized(object sender, ContentsResizedEventArgs e)
        {
            var richTextBox = sender as RichTextBox;
            richTextBox.Height = e.NewRectangle.Height + 5;
        }

        /// <summary>
        /// Occurs when form is resized.
        /// </summary>
        /// <param name="sender">Reference to resized control.</param>
        /// <param name="e">Provides data for the resize event.</param>
        private void FormResized(object sender, EventArgs e)
        {
            if (this.infoBarAdded)
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
            this.browser = new WebBrowserExt();
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
        /// <param name="message">Message to be displayed in textbox.</param>
        private void AddTextBoxControl(string message)
        {
            this.textBox = new RichTextBox();
            this.textBox.Text = message;
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
