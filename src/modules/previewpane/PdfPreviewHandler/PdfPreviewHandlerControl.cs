// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Drawing;
using System.Windows.Forms;

using Common;
using Microsoft.PowerToys.PreviewHandler.Pdf.Properties;
using Microsoft.PowerToys.PreviewHandler.Pdf.Telemetry.Events;
using Microsoft.PowerToys.Telemetry;

namespace Microsoft.PowerToys.PreviewHandler.Pdf
{
    /// <summary>
    /// Win Form Implementation for Pdf Preview Handler.
    /// </summary>
    public class PdfPreviewHandlerControl : FormHandlerControl
    {
        /// <summary>
        /// RichTextBox control to display if external images are blocked.
        /// </summary>
        private RichTextBox _infoBar;

        /// <summary>
        /// Initializes a new instance of the <see cref="PdfPreviewHandlerControl"/> class.
        /// </summary>
        public PdfPreviewHandlerControl()
        {
        }

        /// <summary>
        /// Start the preview on the Control.
        /// </summary>
        /// <param name="dataSource">Path to the file.</param>
        public override void DoPreview<T>(T dataSource)
        {
            try
            {
                if (!(dataSource is string filePath))
                {
                    throw new ArgumentException($"{nameof(dataSource)} for {nameof(PdfPreviewHandler)} must be a string but was a '{typeof(T)}'");
                }

                InvokeOnControlThread(() =>
                {
                    var pdfViewer = new PdfiumViewer.PdfViewer
                    {
                        Dock = DockStyle.Fill,
                        ShowToolbar = false,
                        ShowBookmarks = false,
                        ZoomMode = PdfiumViewer.PdfViewerZoomMode.FitBest,
                    };

                    pdfViewer.Document?.Dispose();
                    pdfViewer.Document = PdfiumViewer.PdfDocument.Load(this, filePath);

                    Controls.Add(pdfViewer);
                });

                PowerToysTelemetry.Log.WriteEvent(new PdfFilePreviewed());
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                PowerToysTelemetry.Log.WriteEvent(new PdfFilePreviewError { Message = ex.Message });

                InvokeOnControlThread(() =>
                {
                    Controls.Clear();

                    _infoBar = GetTextBoxControl(Resources.PdfNotPreviewedError);

                    Controls.Add(_infoBar);
                });
            }
            finally
            {
                base.DoPreview(dataSource);
            }
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
            richTextBox.ContentsResized += RTBContentsResized;
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
    }
}
