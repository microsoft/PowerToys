// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Common;
using Microsoft.PowerToys.FilePreviewCommon;
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
        /// Initializes a new instance of the <see cref="GcodePreviewHandlerControl"/> class.
        /// </summary>
        public GcodePreviewHandlerControl()
        {
            SetBackgroundColor(Settings.BackgroundColor);
        }

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
                    var gcodeThumbnail = GcodeHelper.GetBestThumbnail(reader);

                    thumbnail = gcodeThumbnail?.GetBitmap();
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
                try
                {
                    PowerToysTelemetry.Log.WriteEvent(new GcodeFilePreviewed());
                }
                catch
                { // Should not crash if sending telemetry is failing. Ignore the exception.
                }
            }
            catch (Exception ex)
            {
                PreviewError(ex, dataSource);
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
            _pictureBox.BackgroundImageLayout = Width >= image.Width && Height >= image.Height ? ImageLayout.Center : ImageLayout.Zoom;
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
            try
            {
                PowerToysTelemetry.Log.WriteEvent(new GcodeFilePreviewError { Message = exception.Message });
            }
            catch
            { // Should not crash if sending telemetry is failing. Ignore the exception.
            }

            Controls.Clear();
            _infoBarAdded = true;
            AddTextBoxControl(Properties.Resource.GcodeNotPreviewedError);
            base.DoPreview(dataSource);
        }
    }
}
