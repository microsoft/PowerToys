// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace MouseWithoutBorders
{
    public partial class ColorBorderField : UserControl
    {
        [Category("Property Changed")]
        [Description("The text property of the field has changed")]
        public event EventHandler FieldTextChanged;

        private int _borderSize;

        [Category("Appearance")]
        [Description("The thickness of the border around the field")]
        public int BorderSize
        {
            get => _borderSize;
            set
            {
                _borderSize = value;
                UpdateFieldSize();
            }
        }

        private Color _borderColor;

        [Category("Appearance")]
        [Description("The color of the border around the field")]
        public Color BorderColor
        {
            get => _borderColor;
            set
            {
                _borderColor = value;
                UpdateBorderColor();
            }
        }

        private Color _focusColor;

        [Category("Appearance")]
        [Description("The color of the border around the field when it has focus")]
        public Color FocusColor
        {
            get => _focusColor;
            set
            {
                _focusColor = value;
                UpdateBorderColor();
            }
        }

        [Category("Behavior")]
        [Description("The maximum number of characters that can be typed in the field")]
        public int MaximumLength
        {
            get => InnerField.MaxLength;
            set => InnerField.MaxLength = value;
        }

        public override string Text
        {
            get => InnerField.Text;
            set => InnerField.Text = value;
        }

        public ColorBorderField()
        {
            InitializeComponent();
            InnerField.GotFocus += InnerFieldGotFocus;
            InnerField.LostFocus += InnerFieldLostFocus;
            InnerField.TextChanged += InnerFieldTextChanged;
            UpdateBorderColor();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateFieldSize();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            _ = InnerField.Focus();
        }

        private void InnerFieldGotFocus(object sender, EventArgs e)
        {
            BackColor = FocusColor;
        }

        private void InnerFieldLostFocus(object sender, EventArgs e)
        {
            BackColor = BorderColor;
        }

        private void InnerFieldTextChanged(object sender, EventArgs e)
        {
            FieldTextChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateFieldSize()
        {
            InnerField.Left = BorderSize;
            InnerField.Top = BorderSize;
            InnerField.Width = Width - (BorderSize * 2);
            Height = InnerField.Height + (BorderSize * 2);
        }

        private void UpdateBorderColor()
        {
            BackColor = Focused ? FocusColor : BorderColor;
        }
    }
}
