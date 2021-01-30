using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.ViewManagement;

namespace PdfPreviewHandlerUI
{
    public partial class Form1 : Form
    {
        private RichTextBox _infoBar;
        private FlowLayoutPanel _flowLayoutPanel;

        public Form1()
        {
            InitializeComponent();
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                this.SuspendLayout();

                this.Padding = new Padding(0);

                var toolStripStatusLabel = new System.Windows.Forms.ToolStripStatusLabel();
                toolStripStatusLabel.Text = "1/2";

                var statusStrip = new System.Windows.Forms.StatusStrip();
                statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { toolStripStatusLabel });

                this.Controls.Add(statusStrip);

                _flowLayoutPanel = new FlowLayoutPanel();
                _flowLayoutPanel.SuspendLayout();

                _flowLayoutPanel.Dock = DockStyle.Fill;
                _flowLayoutPanel.AutoScroll = true;
                _flowLayoutPanel.AutoSize = true;
                _flowLayoutPanel.FlowDirection = FlowDirection.TopDown;
                _flowLayoutPanel.WrapContents = false;
                _flowLayoutPanel.Resize += _flowLayoutPanel_Resize;
                _flowLayoutPanel.BackColor = Color.Green;

                // using (var stream = new FileStream("HelperFiles/dummy.pdf", FileMode.Open))
                {
                    using (var reader = await FileRandomAccessStream.OpenAsync(@"C:\Source\Repos\PowerToys\src\modules\previewpane\PdfPreviewHandlerUI\bin\Debug\netcoreapp3.1\HelperFiles\text.pdf",
                        FileAccessMode.Read))
                    {
                        //await stream.CopyToAsync(reader.AsStream()).ConfigureAwait(true);

                        var pdf = await PdfDocument.LoadFromStreamAsync(reader);

                        for (uint i = 0; i < pdf.PageCount; i++)
                        {
                            using (var page = pdf.GetPage(i))
                            {
                                var image = await PageToImageAsync(page).ConfigureAwait(true);

                                var hasScrollbar = _flowLayoutPanel.VerticalScroll.Visible;
                                int width = this.ClientSize.Width - 12 - (hasScrollbar ? 16 : 0);

                                int originalWidth = image.Width;
                                int originalHeight = image.Height;
                                float percentWidth = (float)width / (float)originalWidth;
                                var newHeight = (int)(originalHeight * percentWidth);

                                Panel panel = new Panel()
                                {
                                    Width = width,
                                    Height = newHeight,
                                    // Dock = DockStyle.Fill,
                                    BorderStyle = BorderStyle.FixedSingle,
                                    BackColor = Color.Red, 
                            };
                                PictureBox _pictureBox = new PictureBox
                                {
                                    Image = image,
                                    SizeMode = PictureBoxSizeMode.Zoom,
                                    Dock = DockStyle.Fill,
                                };

                                panel.Controls.Add(_pictureBox);

                                ((System.ComponentModel.ISupportInitialize)(_pictureBox)).BeginInit();
                                _flowLayoutPanel.Controls.Add(panel);
                                ((System.ComponentModel.ISupportInitialize)(_pictureBox)).EndInit();
                            }
                        }
                    }
                }

                Controls.Add(_flowLayoutPanel);
                _flowLayoutPanel.ResumeLayout(false);

                this.ResumeLayout(false);
                this.PerformLayout();
            }
            catch (Exception ex)
            {
                Controls.Clear();
                _infoBar = GetTextBoxControl(ex.Message);
                Controls.Add(_infoBar);
            }
        }

        private void _flowLayoutPanel_Resize(object sender, EventArgs e)
        {
            this.SuspendLayout();
            _flowLayoutPanel.SuspendLayout();

            foreach (Panel panel in _flowLayoutPanel.Controls)
            {
                var pictureBox = panel.Controls[0] as PictureBox;
                var image = pictureBox.Image;

                var hasScrollbar = _flowLayoutPanel.VerticalScroll.Visible;
                int width = this.ClientSize.Width - 12 - (hasScrollbar ? 16 : 0);

                int originalWidth = image.Width;
                int originalHeight = image.Height;
                float percentWidth = (float)width / (float)originalWidth;
                var newWidth = (int)(originalWidth * percentWidth);
                var newHeight = (int)(originalHeight * percentWidth);

                panel.Width = width;
                panel.Height = newHeight;
            }

            _flowLayoutPanel.ResumeLayout(false);

            this.ResumeLayout(false);
        }

        /// <summary>
        /// Transform the PdfPage to an Image.
        /// </summary>
        /// <param name="page">The page to transform.</param>
        /// <returns>An object of type <see cref="Image"/></returns>
        private static async Task<Image> PageToImageAsync(PdfPage page)
        {
            Image image;

            using (var stream = new InMemoryRandomAccessStream())
            {
                await page.RenderToStreamAsync(stream);

                image = Image.FromStream(stream.AsStream());
            }

            return image;
        }

        /// <summary>
        /// Get the <see cref="PictureBox"/> with the <see cref="Image"/> as source.
        /// </summary>
        /// <param name="image"><see cref="Image"/> to show in the <see cref="PictureBox"/></param>
        /// <returns>An object of type <see cref="PictureBox"/></returns>
        private static PictureBox GetPictureBoxControl(Image image)
        {
            PictureBox pictureBox = new PictureBox
            {
                Dock = DockStyle.Fill,
                Image = image,
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = GetBackgroundColor(),
            };

            return pictureBox;
        }

        /// <summary>
        /// Get the system background color, based on the selected theme.
        /// </summary>
        /// <returns>An object of type <see cref="Color"/>.</returns>
        private static Color GetBackgroundColor()
        {
            return Color.Black;
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
