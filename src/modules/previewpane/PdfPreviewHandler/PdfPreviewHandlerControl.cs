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
        /// RichTextBox control to display if external images are blocked.
        /// </summary>
        private RichTextBox _infoBar;

        /// <summary>
        /// FlowLayoutPanel control to display the image of the pdf.
        /// </summary>
        private FlowLayoutPanel _flowLayoutPanel;

        /// <summary>
        /// Initializes a new instance of the <see cref="PdfPreviewHandlerControl"/> class.
        /// </summary>
        public PdfPreviewHandlerControl()
        {
            BackColor = GetBackgroundColor();
        }

        /// <summary>
        /// Start the preview on the Control.
        /// </summary>
        /// <param name="dataSource">Stream reference to access source file.</param>
        public override void DoPreview<T>(T dataSource)
        {
            this.SuspendLayout();

            try
            {
                using (var stream = new ReadonlyStream(dataSource as IStream))
                {
                    var memStream = new MemoryStream();
                    stream.CopyTo(memStream);
                    memStream.Position = 0;

                    try
                    {
                        // AsRandomAccessStream() extension method from System.Runtime.WindowsRuntime
                        var pdf = PdfDocument.LoadFromStreamAsync(memStream.AsRandomAccessStream()).GetAwaiter().GetResult();

                        if (pdf.PageCount > 0)
                        {
                            InvokeOnControlThread(() =>
                            {
                                _flowLayoutPanel = new FlowLayoutPanel();
                                _flowLayoutPanel.AutoScroll = true;
                                _flowLayoutPanel.AutoSize = true;
                                _flowLayoutPanel.Dock = DockStyle.Fill;
                                _flowLayoutPanel.FlowDirection = FlowDirection.TopDown;
                                _flowLayoutPanel.Resize += FlowLayoutPanel_Resize;
                                _flowLayoutPanel.WrapContents = false;

                                // Only show first 10 pages.
                                for (uint i = 0; i < pdf.PageCount && i < 10; i++)
                                {
                                    using (var page = pdf.GetPage(i))
                                    {
                                        var image = PageToImage(page);

                                        Panel panel = new Panel()
                                        {
                                            Margin = new Padding(6, 6, 6, 0),
                                            Size = CalculateSize(image),
                                            BorderStyle = BorderStyle.FixedSingle,
                                        };

                                        PictureBox pictureBox = new PictureBox
                                        {
                                            Dock = DockStyle.Fill,
                                            Image = image,
                                            SizeMode = PictureBoxSizeMode.Zoom,
                                        };

                                        panel.Controls.Add(pictureBox);
                                        _flowLayoutPanel.Controls.Add(panel);
                                    }
                                }

                                Controls.Add(_flowLayoutPanel);
                            });
                        }
                    }
#pragma warning disable CA1031 // Password protected files throws an generic Exception
                    catch (Exception ex)
#pragma warning restore CA1031
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

            foreach (Panel panel in _flowLayoutPanel.Controls)
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
        /// <param name="page">The page to transform.</param>
        /// <returns>An object of type <see cref="Image"/></returns>
        private static Image PageToImage(PdfPage page)
        {
            Image image;

            using (var stream = new InMemoryRandomAccessStream())
            {
                page.RenderToStreamAsync(stream).GetAwaiter().GetResult();

                image = Image.FromStream(stream.AsStream());
            }

            return image;
        }

        /// <summary>
        /// Calculate the size of the control based on the size of the image/pdf page.
        /// </summary>
        /// <param name="image">Image of pdf page.</param>
        /// <returns>New size off the panel.</returns>
        private Size CalculateSize(Image image)
        {
            var hasScrollBar = _flowLayoutPanel.VerticalScroll.Visible;
            int width = this.ClientSize.Width - 12 - (hasScrollBar ? 16 : 0);

            int originalWidth = image.Width;
            int originalHeight = image.Height;
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
            var uiSettings = new UISettings();
            Windows.UI.Color systemBackgroundColor = uiSettings.GetColorValue(UIColorType.Background);

            return Color.FromArgb(systemBackgroundColor.A, systemBackgroundColor.R, systemBackgroundColor.G, systemBackgroundColor.B);
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
