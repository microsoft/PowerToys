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
        private WebBrowserExt _browser;

        /// <summary>
        /// Text box to display the information about blocked elements from Svg.
        /// </summary>
        private RichTextBox _textBox;

        /// <summary>
        /// Represent if an text box info bar is added for showing message.
        /// </summary>
        private bool _infoBarAdded;

        /// <summary>
        /// Start the preview on the Control.
        /// </summary>
        /// <param name="dataSource">Stream reference to access source file.</param>
        public override void DoPreview<T>(T dataSource)
        {
            string svgData = null;
            bool blocked = false;

            try
            {
                using (var stream = new ReadonlyStream(dataSource as IStream))
                {
                    using (var reader = new StreamReader(stream))
                    {
                        svgData = reader.ReadToEnd();
                    }
                }

                blocked = SvgPreviewHandlerHelper.CheckBlockedElements(svgData);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                PreviewError(ex, dataSource);
                return;
            }

            InvokeOnControlThread(() =>
            {
                try
                {
                    _infoBarAdded = false;

                    // Add a info bar on top of the Preview if any blocked element is present.
                    if (blocked)
                    {
                        _infoBarAdded = true;
                        AddTextBoxControl(Resource.BlockedElementInfoText);
                    }

                    AddBrowserControl(svgData);
                    Resize += FormResized;
                    base.DoPreview(dataSource);
                    PowerToysTelemetry.Log.WriteEvent(new SvgFilePreviewed());
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    PreviewError(ex, dataSource);
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
            if (_infoBarAdded)
            {
                _textBox.Width = Width;
            }
        }

        /// <summary>
        /// Adds a Web Browser Control to Control Collection.
        /// </summary>
        /// <param name="svgData">Svg to display on Browser Control.</param>
        private void AddBrowserControl(string svgData)
        {
            _browser = new WebBrowserExt();
            _browser.DocumentText = svgData;
            _browser.Dock = DockStyle.Fill;
            _browser.IsWebBrowserContextMenuEnabled = false;
            _browser.ScriptErrorsSuppressed = true;
            _browser.ScrollBarsEnabled = true;
            _browser.AllowNavigation = false;
            Controls.Add(_browser);
        }

        /// <summary>
        /// Adds a Text Box in Controls for showing information about blocked elements.
        /// </summary>
        /// <param name="message">Message to be displayed in textbox.</param>
        private void AddTextBoxControl(string message)
        {
            _textBox = new RichTextBox();
            _textBox.Text = message;
            _textBox.BackColor = Color.LightYellow;
            _textBox.Multiline = true;
            _textBox.Dock = DockStyle.Top;
            _textBox.ReadOnly = true;
            _textBox.ContentsResized += RTBContentsResized;
            _textBox.ScrollBars = RichTextBoxScrollBars.None;
            _textBox.BorderStyle = BorderStyle.None;
            Controls.Add(_textBox);
        }

        /// <summary>
        /// Called when an error occurs during preview.
        /// </summary>
        /// <param name="exception">The exception which occurred.</param>
        /// <param name="dataSource">Stream reference to access source file.</param>
        private void PreviewError<T>(Exception exception, T dataSource)
        {
            PowerToysTelemetry.Log.WriteEvent(new SvgFilePreviewError { Message = exception.Message });
            Controls.Clear();
            _infoBarAdded = true;
            AddTextBoxControl(Resource.SvgNotPreviewedError);
            base.DoPreview(dataSource);
        }
    }
}
