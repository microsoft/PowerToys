// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;
using Common;
using Common.Utilities;
using Microsoft.PowerToys.PreviewHandler.Svg.Telemetry.Events;
using Microsoft.PowerToys.PreviewHandler.Svg.Utilities;
using Microsoft.PowerToys.Telemetry;
using PreviewHandlerCommon;

namespace Microsoft.PowerToys.PreviewHandler.Svg
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
            InvokeOnControlThread(() =>
            {
                try
                {
                    infoBarAdded = false;
                    string svgData = null;
                    using (var stream = new StreamWrapper(dataSource as IStream))
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            svgData = reader.ReadToEnd();
                        }
                    }

                    // Add a info bar on top of the Preview if any blocked element is present.
                    if (SvgPreviewHandlerHelper.CheckBlockedElements(svgData))
                    {
                        infoBarAdded = true;
                        AddTextBoxControl(Resource.BlockedElementInfoText);
                    }

                    AddBrowserControl(svgData);
                    Resize += FormResized;
                    base.DoPreview(dataSource);
                    PowerToysTelemetry.Log.WriteEvent(new SvgFilePreviewed());
                }
                catch (Exception ex)
                {
                    PowerToysTelemetry.Log.WriteEvent(new SvgFilePreviewError { Message = ex.Message });
                    Controls.Clear();
                    infoBarAdded = true;
                    AddTextBoxControl(Resource.SvgNotPreviewedError);
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
            if (infoBarAdded)
            {
                textBox.Width = Width;
            }
        }

        /// <summary>
        /// Adds a Web Browser Control to Control Collection.
        /// </summary>
        /// <param name="svgData">Svg to display on Browser Control.</param>
        private void AddBrowserControl(string svgData)
        {
            browser = new WebBrowserExt();
            browser.DocumentText = svgData;
            browser.Dock = DockStyle.Fill;
            browser.IsWebBrowserContextMenuEnabled = false;
            browser.ScriptErrorsSuppressed = true;
            browser.ScrollBarsEnabled = true;
            browser.AllowNavigation = false;
            Controls.Add(browser);
        }

        /// <summary>
        /// Adds a Text Box in Controls for showing information about blocked elements.
        /// </summary>
        /// <param name="message">Message to be displayed in textbox.</param>
        private void AddTextBoxControl(string message)
        {
            textBox = new RichTextBox();
            textBox.Text = message;
            textBox.BackColor = Color.LightYellow;
            textBox.Multiline = true;
            textBox.Dock = DockStyle.Top;
            textBox.ReadOnly = true;
            textBox.ContentsResized += RTBContentsResized;
            textBox.ScrollBars = RichTextBoxScrollBars.None;
            textBox.BorderStyle = BorderStyle.None;
            Controls.Add(textBox);
        }
    }
}
