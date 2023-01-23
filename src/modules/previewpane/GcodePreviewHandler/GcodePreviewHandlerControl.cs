// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Forms;
using Common;
using Common.Utilities;
using Microsoft.PowerToys.PreviewHandler.Gcode.Telemetry.Events;
using Microsoft.PowerToys.Telemetry;

namespace Microsoft.PowerToys.PreviewHandler.Gcode
{
    /// <summary>
    /// Implementation of Control for Gcode Preview Handler.
    /// </summary>
    public class GcodePreviewHandlerControl : FormHandlerControl
    {
        /// <summary>
        /// Picture box control to display the G-code thumbnail.
        /// </summary>
        private PictureBox _pictureBox;

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
            if (global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredGcodePreviewEnabledValue() == global::PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                // GPO is disabling this utility. Show an error message instead.
                _infoBarAdded = true;
                AddTextBoxControl(Properties.Resource.GpoDisabledErrorText);
                Resize += FormResized;
                base.DoPreview(dataSource);

                return;
            }

            try
            {
                Bitmap thumbnail = null;

                if (!(dataSource is string filePath))
                {
                    throw new ArgumentException($"{nameof(dataSource)} for {nameof(GcodePreviewHandlerControl)} must be a string but was a '{typeof(T)}'");
                }

                FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);

                using (var reader = new StreamReader(fs))
                {
                    thumbnail = GetThumbnail(reader);
                }

                _infoBarAdded = false;

                if (thumbnail == null)
                {
                    _infoBarAdded = true;
                    AddTextBoxControl(Properties.Resource.GcodeWithoutEmbeddedThumbnails);
                }
                else
                {
                    AddPictureBoxControl(thumbnail);
                }

                Resize += FormResized;
                base.DoPreview(fs);
                PowerToysTelemetry.Log.WriteEvent(new GcodeFilePreviewed());
            }
            catch (Exception ex)
            {
                PreviewError(ex, dataSource);
            }
        }

        /// <summary>
        /// Reads the G-code content searching for thumbnails and returns the largest.
        /// </summary>
        /// <param name="reader">The TextReader instance for the G-code content.</param>
        /// <returns>A thumbnail extracted from the G-code content.</returns>
        public static Bitmap GetThumbnail(TextReader reader)
        {
            if (reader == null)
            {
                return null;
            }

            Bitmap thumbnail = null;

            var bitmapBase64 = GetBase64Thumbnails(reader)
                .OrderByDescending(x => x.Length)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(bitmapBase64))
            {
                var bitmapBytes = Convert.FromBase64String(bitmapBase64);

                thumbnail = new Bitmap(new MemoryStream(bitmapBytes));
            }

            return thumbnail;
        }

        /// <summary>
        /// Gets all thumbnails in base64 format found on the G-code data.
        /// </summary>
        /// <param name="reader">The TextReader instance for the G-code content.</param>
        /// <returns>An enumeration of thumbnails in base64 format found on the G-code.</returns>
        private static IEnumerable<string> GetBase64Thumbnails(TextReader reader)
        {
            string line;
            StringBuilder capturedText = null;

            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("; thumbnail begin", StringComparison.InvariantCulture))
                {
                    capturedText = new StringBuilder();
                }
                else if (line == "; thumbnail end")
                {
                    if (capturedText != null)
                    {
                        yield return capturedText.ToString();

                        capturedText = null;
                    }
                }
                else if (capturedText != null)
                {
                    capturedText.Append(line[2..]);
                }
            }
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
        /// Adds a PictureBox Control to Control Collection.
        /// </summary>
        /// <param name="image">Image to display on PictureBox Control.</param>
        private void AddPictureBoxControl(Image image)
        {
            _pictureBox = new PictureBox();
            _pictureBox.BackgroundImage = image;
            _pictureBox.BackgroundImageLayout = ImageLayout.Center;
            _pictureBox.Dock = DockStyle.Fill;
            Controls.Add(_pictureBox);
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
            PowerToysTelemetry.Log.WriteEvent(new GcodeFilePreviewError { Message = exception.Message });
            Controls.Clear();
            _infoBarAdded = true;
            AddTextBoxControl(Properties.Resource.GcodeNotPreviewedError);
            base.DoPreview(dataSource);
        }
    }
}
