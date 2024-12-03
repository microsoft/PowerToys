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
using Microsoft.PowerToys.PreviewHandler.Pdf.Properties;
using Microsoft.PowerToys.PreviewHandler.Pdf.Telemetry.Events;
using Microsoft.PowerToys.Telemetry;
using Windows.Data.Pdf;
using Windows.Storage.Streams;
using Windows.UI.ViewManagement;

namespace Microsoft.PowerToys.PreviewHandler.Pdf
{
    /// <summary>
    /// Win Form Implementation for Pdf Preview Handler.
    /// </summary>
    public class PdfPreviewHandlerControl : FormHandlerControl
    {
        /// <summary>
        /// RichTextBox control to display error message.
        /// </summary>
        private RichTextBox _infoBar;

        /// <summary>
        /// FlowLayoutPanel control to display the image of the pdf.
        /// </summary>
        private FlowLayoutPanel _flowLayoutPanel;

        /// <summary>
        /// Use UISettings to get system colors and scroll bar size.
        /// </summary>
        private static readonly UISettings _uISettings = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="PdfPreviewHandlerControl"/> class.
        /// </summary>
        public PdfPreviewHandlerControl()
        {
            SetBackgroundColor(GetBackgroundColor());
        }

        /// <summary>
        /// Start the preview on the Control.
        /// </summary>
        /// <param name="dataSource">Stream reference to access source file.</param>
        public override void DoPreview<T>(T dataSource)
        {
            if (global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredPdfPreviewEnabledValue() == global::PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                // GPO is disabling this utility. Show an error message instead.
                _infoBar = GetTextBoxControl(Resources.GpoDisabledErrorText);
                Controls.Add(_infoBar);
                base.DoPreview(dataSource);

                return;
            }

            this.SuspendLayout();

            try
            {
                if (dataSource is not string filePath)
                {
                    throw new ArgumentException($"{nameof(dataSource)} for {nameof(PdfPreviewHandlerControl)} must be a string but was a '{typeof(T)}'");
                }

                using (var dataStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                {
                    var memStream = new MemoryStream();
                    dataStream.CopyTo(memStream);
                    memStream.Position = 0;

                    try
                    {
                        // AsRandomAccessStream() extension method from System.Runtime.WindowsRuntime
                        var pdf = PdfDocument.LoadFromStreamAsync(memStream.AsRandomAccessStream()).GetAwaiter().GetResult();

                        if (pdf.PageCount > 0)
                        {
                            _flowLayoutPanel = new FlowLayoutPanel
                            {
                                AutoScroll = true,
                                AutoSize = true,
                                Dock = DockStyle.Fill,
                                FlowDirection = FlowDirection.TopDown,
                                WrapContents = false,
                            };
                            _flowLayoutPanel.Resize += FlowLayoutPanel_Resize;

                            // Only show first 10 pages.
                            for (uint i = 0; i < pdf.PageCount && i < 10; i++)
                            {
                                using var page = pdf.GetPage(i);
                                var image = PageToImage(page);

                                var picturePanel = new Panel()
                                {
                                    Name = "picturePanel",
                                    Margin = new Padding(6, 6, 6, 0),
                                    Size = CalculateSize(image),
                                    BorderStyle = BorderStyle.FixedSingle,
                                };

                                var picture = new PictureBox
                                {
                                    Dock = DockStyle.Fill,
                                    Image = image,
                                    SizeMode = PictureBoxSizeMode.Zoom,
                                };

                                picturePanel.Controls.Add(picture);
                                _flowLayoutPanel.Controls.Add(picturePanel);
                            }

                            if (pdf.PageCount > 10)
                            {
                                var messageBox = new RichTextBox
                                {
                                    Name = "messageBox",
                                    Text = Resources.PdfMorePagesMessage,
                                    BackColor = Color.LightYellow,
                                    Dock = DockStyle.Fill,
                                    Multiline = true,
                                    ReadOnly = true,
                                    ScrollBars = RichTextBoxScrollBars.None,
                                    BorderStyle = BorderStyle.None,
                                };
                                messageBox.ContentsResized += RTBContentsResized;

                                _flowLayoutPanel.Controls.Add(messageBox);
                            }

                            Controls.Add(_flowLayoutPanel);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message.Contains("Unable to update the password. The value provided as the current password is incorrect.", StringComparison.Ordinal))
                        {
                            Controls.Clear();
                            _infoBar = GetTextBoxControl(Resources.PdfPasswordProtectedError);
                            Controls.Add(_infoBar);
                        }
                        else
                        {
                            throw;
                        }
                    }
                    finally
                    {
                        memStream.Dispose();
                    }
                }

                try
                {
                    PowerToysTelemetry.Log.WriteEvent(new PdfFilePreviewed());
                }
                catch
                { // Should not crash if sending telemetry is failing. Ignore the exception.
                }
            }
            catch (Exception ex)
            {
                try
                {
                    PowerToysTelemetry.Log.WriteEvent(new PdfFilePreviewError { Message = ex.Message });
                }
                catch
                { // Should not crash if sending telemetry is failing. Ignore the exception.
                }

                Controls.Clear();
                _infoBar = GetTextBoxControl(Resources.PdfNotPreviewedError);
                Controls.Add(_infoBar);
            }
            finally
            {
                base.DoPreview(dataSource);
            }

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        /// <summary>
        /// Resize the Panels on FlowLayoutPanel resize based on the size of the image.
        /// </summary>
        /// <param name="sender">sender (not used)</param>
        /// <param name="e">args (not used)</param>
        private void FlowLayoutPanel_Resize(object sender, EventArgs e)
        {
            this.SuspendLayout();
            _flowLayoutPanel.SuspendLayout();

            foreach (Panel panel in _flowLayoutPanel.Controls.Find("picturePanel", false))
            {
                var pictureBox = panel.Controls[0] as PictureBox;
                var image = pictureBox.Image;

                panel.Size = CalculateSize(image);
            }

            _flowLayoutPanel.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        /// <summary>
        /// Transform the PdfPage to an Image.
        /// </summary>
        /// <param name="page">The page to transform to an Image.</param>
        /// <returns>An object of type <see cref="Image"/></returns>
        private Image PageToImage(PdfPage page)
        {
            Image imageOfPage = null;

            using (var stream = new InMemoryRandomAccessStream())
            {
                page.RenderToStreamAsync(stream, new PdfPageRenderOptions()
                {
                    DestinationWidth = (uint)this.ClientSize.Width,
                }).GetAwaiter().GetResult();

                imageOfPage = Image.FromStream(stream.AsStream());
            }

            return imageOfPage;
        }

        /// <summary>
        /// Calculate the size of the control based on the size of the image/pdf page.
        /// </summary>
        /// <param name="pdfImage">Image of pdf page.</param>
        /// <returns>New size off the panel.</returns>
        private Size CalculateSize(Image pdfImage)
        {
            var hasScrollBar = _flowLayoutPanel.VerticalScroll.Visible;

            // Add 12px margin to the image by making it 12px smaller.
            int width = this.ClientSize.Width - 12;

            // If the vertical scroll bar is visible, make the image smaller.
            var scrollBarSizeWidth = (int)_uISettings.ScrollBarSize.Width;
            if (hasScrollBar && width > scrollBarSizeWidth)
            {
                width -= scrollBarSizeWidth;
            }

            int originalWidth = pdfImage.Width;
            int originalHeight = pdfImage.Height;
            float percentWidth = (float)width / originalWidth;

            int newHeight = (int)(originalHeight * percentWidth);

            return new Size(width, newHeight);
        }

        /// <summary>
        /// Get the system background color, based on the selected theme.
        /// </summary>
        /// <returns>An object of type <see cref="Color"/>.</returns>
        private static Color GetBackgroundColor()
        {
            var systemBackgroundColor = _uISettings.GetColorValue(UIColorType.Background);

            return Color.FromArgb(systemBackgroundColor.A, systemBackgroundColor.R, systemBackgroundColor.G, systemBackgroundColor.B);
        }

        /// <summary>
        /// Gets a textbox control.
        /// </summary>
        /// <param name="message">Message to be displayed in textbox.</param>
        /// <returns>An object of type <see cref="RichTextBox"/>.</returns>
        private RichTextBox GetTextBoxControl(string message)
        {
            var textBox = new RichTextBox
            {
                Text = message,
                BackColor = Color.LightYellow,
                Multiline = true,
                Dock = DockStyle.Top,
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.None,
                BorderStyle = BorderStyle.None,
            };
            textBox.ContentsResized += RTBContentsResized;

            return textBox;
        }

        /// <summary>
        /// Callback when RichTextBox is resized.
        /// </summary>
        /// <param name="sender">Reference to resized control.</param>
        /// <param name="e">Provides data for the resize event.</param>
        private void RTBContentsResized(object sender, ContentsResizedEventArgs e)
        {
            var richTextBox = (RichTextBox)sender;

            // Add 5px extra height to the textbox.
            richTextBox.Height = e.NewRectangle.Height + 5;
        }
    }
}
