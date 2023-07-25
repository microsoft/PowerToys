// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class AlphaColorPickerButton : UserControl
    {
        private Color _selectedColor;

        public Color SelectedColor
        {
            get
            {
                return _selectedColor;
            }

            set
            {
                if (_selectedColor != value)
                {
                    _selectedColor = value;
                    SetValue(SelectedColorProperty, value);
                }
            }
        }

        public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register("SelectedColor", typeof(Color), typeof(AlphaColorPickerButton), new PropertyMetadata(null));

        public AlphaColorPickerButton()
        {
            this.InitializeComponent();
            IsEnabledChanged -= AlphaColorPickerButton_IsEnabledChanged;
            SetEnabledState();
            IsEnabledChanged += AlphaColorPickerButton_IsEnabledChanged;
        }

        private void AlphaColorPickerButton_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            SetEnabledState();
        }

        private void SetEnabledState()
        {
            if (this.IsEnabled)
            {
                ColorPreviewBorder.Opacity = 1;
            }
            else
            {
                ColorPreviewBorder.Opacity = 0.2;
            }
        }
    }
}
