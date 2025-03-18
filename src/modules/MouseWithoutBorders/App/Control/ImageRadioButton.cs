// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace MouseWithoutBorders
{
    public partial class ImageRadioButton : RadioButton
    {
        private Image CheckImage => Checked ? CheckedImage : UncheckedImage;

        private Point _imageLocation;

        [Category("Appearance")]
        [Description("The bounding rectangle of the check image in local co-ordinates")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Point ImageLocation
        {
            get => _imageLocation;
            set
            {
                _imageLocation = value;
                Refresh();
            }
        }

        private Point _textLocation;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Point TextLocation
        {
            get => _textLocation;
            set
            {
                _textLocation = value;
                Refresh();
            }
        }

        public ImageRadioButton()
        {
            InitializeComponent();
        }

        private Image _checkedImage;

        [Category("Appearance")]
        [Description("Image to show when Mouse is pressed on button")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Image CheckedImage
        {
            get => _checkedImage;
            set
            {
                _checkedImage = value;
                Refresh();
            }
        }

        private Image _uncheckedImage;

        [Category("Appearance")]
        [Description("Image to show when button is in normal state")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public Image UncheckedImage
        {
            get => _uncheckedImage;
            set
            {
                _uncheckedImage = value;
                Refresh();
            }
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            if (CheckImage == null)
            {
                base.OnPaint(pevent);
            }
            else
            {
                OnPaintBackground(pevent);
                pevent.Graphics.DrawImage(CheckImage, ImageLocation.X, ImageLocation.Y, CheckImage.Width, CheckImage.Height);
                if (!string.IsNullOrEmpty(Text))
                {
                    pevent.Graphics.DrawString(Text, Font, Brushes.White, TextLocation);
                }
            }
        }
    }
}
