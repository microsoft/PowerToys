// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.UI;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class ColorPickerButton : UserControl
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

        public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(nameof(SelectedColor), typeof(Color), typeof(ColorPickerButton), new PropertyMetadata(null));

        public static readonly DependencyProperty IsAlphaEnabledProperty = DependencyProperty.Register(nameof(IsAlphaEnabled), typeof(bool), typeof(ColorPickerButton), new PropertyMetadata(defaultValue: false));

        public bool IsAlphaEnabled
        {
            get => (bool)GetValue(IsAlphaEnabledProperty);
            set => SetValue(IsAlphaEnabledProperty, value);
        }

        public ColorPickerButton()
        {
            this.InitializeComponent();
            IsEnabledChanged -= ColorPickerButton_IsEnabledChanged;
            SetEnabledState();
            IsEnabledChanged += ColorPickerButton_IsEnabledChanged;
        }

        private void ColorPickerButton_IsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
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
