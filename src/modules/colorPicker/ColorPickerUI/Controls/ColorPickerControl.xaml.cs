// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ColorPicker.Helpers;

namespace ColorPicker.Controls
{
    /// <summary>
    /// Interaction logic for ColorPickerControl.xaml
    /// </summary>
    public partial class ColorPickerControl : UserControl
    {
        private double currH = 360;
        private bool _changedByPicker;
        private bool _isCollapsed = true;
        private Color _originalColor;
        private Color _currentColor;

        public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register("SelectedColor", typeof(Color), typeof(ColorPickerControl), new PropertyMetadata(SelectedColorPropertyChanged));

        public static readonly DependencyProperty SelectedColorChangeCommandProperty = DependencyProperty.Register("SelectedColorChangedCommand", typeof(ICommand), typeof(ColorPickerControl));

        public ColorPickerControl()
        {
            InitializeComponent();

            var g6 = HSVColor.GradientSpectrum();

            LinearGradientBrush gradientBrush = new LinearGradientBrush();
            gradientBrush.StartPoint = new Point(0, 0);
            gradientBrush.EndPoint = new Point(1, 0);
            for (int i = 0; i < g6.Length; i++)
            {
                GradientStop stop = new GradientStop(g6[i], i * 0.16);
                gradientBrush.GradientStops.Add(stop);
            }

            SpectrumGrid.Opacity = 1;
            SpectrumGrid.Background = gradientBrush;

            currH = 360 * ((RgbGradientGrid.Width / 2) - (1 / RgbGradientGrid.Width));
            MiddleStop.Color = HSVColor.RGBFromHSV(currH, 1f, 1f);
            UpdateRgbGradient((RgbGradientGrid.Width / 2) - 1);
        }

        public Color SelectedColor
        {
            get { return (Color)GetValue(SelectedColorProperty); }
            set { SetValue(SelectedColorProperty, value); }
        }

        public ICommand SelectedColorChangedCommand
        {
            get { return (ICommand)GetValue(SelectedColorChangeCommandProperty); }
            set { SetValue(SelectedColorChangeCommandProperty, value); }
        }

        private static void SelectedColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var newColor = (Color)e.NewValue;
            ((ColorPickerControl)d)._originalColor = ((ColorPickerControl)d)._currentColor = newColor;
            var newColorBackground = new SolidColorBrush(newColor);
            ((ColorPickerControl)d).CurrentColorBorder.Background = newColorBackground;
            ((ColorPickerControl)d).HexCode.Text = ColorToHex(newColor);

            var hsv = ColorHelper.ConvertToHSVColor(System.Drawing.Color.FromArgb(newColor.R, newColor.G, newColor.B));

            var hueCoeficient = 0;
            var hueCoeficient2 = 0;
            if (1 - hsv.value < 0.15)
            {
                hueCoeficient = 1;
            }

            if (hsv.value - 0.3 < 0)
            {
                hueCoeficient2 = 1;
            }

            var s = hsv.saturation;

            ((ColorPickerControl)d).colorVariation1Border.Background = new SolidColorBrush(HSVColor.RGBFromHSV(Math.Min(hsv.hue + (hueCoeficient * 8), 360), s, Math.Min(hsv.value + 0.3, 1)));
            ((ColorPickerControl)d).colorVariation2Border.Background = new SolidColorBrush(HSVColor.RGBFromHSV(Math.Min(hsv.hue + (hueCoeficient * 4), 360), s, Math.Min(hsv.value + 0.15, 1)));

            ((ColorPickerControl)d).colorVariation3Border.Background = new SolidColorBrush(HSVColor.RGBFromHSV(Math.Max(hsv.hue - (hueCoeficient2 * 4), 0), s, Math.Max(hsv.value - 0.2, 0)));
            ((ColorPickerControl)d).colorVariation4Border.Background = new SolidColorBrush(HSVColor.RGBFromHSV(Math.Max(hsv.hue - (hueCoeficient2 * 8), 0), s, Math.Max(hsv.value - 0.3, 0)));
        }

        private void RgbGradient_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = System.Windows.Input.Mouse.GetPosition(sender as Grid);
                UpdateRgbGradient(pos.X);
            }
        }

        private void RgbGradientGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = System.Windows.Input.Mouse.GetPosition(sender as Grid);

            UpdateRgbGradient(pos.X);
        }

        private void UpdateRgbGradient(double posX, bool skipUpdatingText = false)
        {
            if (posX < 0)
            {
                posX = 0;
            }

            var width = RgbGradientGrid.Width;
            var x = width - posX;

            x = width - x;

            rgbGradientPointer.Margin = new Thickness(posX - 1, 0, 0, 0);
            Color c;
            if (x < width / 2)
            {
                c = HSVColor.RGBFromHSV(currH, x / (width / 2), 1f);
            }
            else
            {
                c = HSVColor.RGBFromHSV(currH, 1f, ((width / 2) - (x - (width / 2))) / (width / 2));
            }

            _currentColor = c;
            CurrentColorBorder.Background = new SolidColorBrush(c);

            if (!skipUpdatingText)
            {
                _changedByPicker = true;
                HexCode.Text = ColorToHex(c);
                _changedByPicker = false;
            }
        }

        private static string ColorToHex(Color color)
        {
            return "#" + BitConverter.ToString(new byte[] { color.R, color.G, color.B }).Replace("-", string.Empty);
        }

        private void UpdateColorAndHex()
        {
            var pointerPosition = rgbGradientPointer.Margin.Left + 1;
            UpdateRgbGradient(pointerPosition);
        }

        private void SpectrumGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var x = e.GetPosition(SpectrumGrid).X;
                OnSpectrumGridClick(x);
            }
        }

        private void SpectrumGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var x = e.GetPosition(SpectrumGrid).X;
            OnSpectrumGridClick(x);
        }

        private void OnSpectrumGridClick(double positionX)
        {
            if (positionX < 0)
            {
                positionX = 0;
            }

            if (positionX > RgbGradientGrid.Width)
            {
                positionX = RgbGradientGrid.Width;
            }

            spectrumGridPointer.Margin = new Thickness(positionX - 1, 0, 0, 0);
            currH = 360 * (positionX / RgbGradientGrid.Width);
            MiddleStop.Color = HSVColor.RGBFromHSV(currH, 1f, 1f);
            UpdateColorAndHex();
        }

        private void HexCode_TextChanged(object sender, TextChangedEventArgs e)
        {
            // TODO: add hex color validation
            if (!_changedByPicker)
            {
                var converter = new System.Drawing.ColorConverter();

                var color = (System.Drawing.Color)converter.ConvertFromString(HexCode.Text);

                var min = Math.Min(Math.Min(color.R, color.G), color.B) / 255d;
                var max = Math.Max(Math.Max(color.R, color.G), color.B) / 255d;

                var sat = max == 0d ? 0d : (max - min) / max;
                var v = max;

                var hue = Math.Round(color.GetHue());
                var saturation = Math.Round(sat * 100);
                var value = Math.Round(v * 100);

                spectrumGridPointer.Margin = new Thickness((hue / 360) * RgbGradientGrid.Width, 0, 0, 0);
                MiddleStop.Color = HSVColor.RGBFromHSV(hue, 1f, 1f);
                currH = hue;

                var width = RgbGradientGrid.Width;

                if (value == 100)
                {
                    rgbGradientPointer.Margin = new Thickness((sat * (width / 2)) - 1, 0, 0, 0);
                    UpdateRgbGradient((sat * (width / 2)) - 1, true);
                }
                else if (saturation == 100)
                {
                    rgbGradientPointer.Margin = new Thickness((width - (value / 100.0 * (width / 2))) - 1, 0, 0, 0);
                    UpdateRgbGradient((width - (value / 100.0 * (width / 2))) - 1, true);
                }
            }
        }

        private void CurrentColorBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                ShowDetails();
            }
        }

        private void ShowDetails()
        {
            if (_isCollapsed)
            {
                _isCollapsed = false;

                var opacityAppear = new DoubleAnimation(1.0, new Duration(TimeSpan.FromMilliseconds(400)));
                opacityAppear.EasingFunction = new QuadraticEase() { EasingMode = EasingMode.EaseOut };

                var resize = new DoubleAnimation(400, new Duration(TimeSpan.FromMilliseconds(400)));
                resize.EasingFunction = new ExponentialEase() { EasingMode = EasingMode.EaseOut };

                var resizeColor = new DoubleAnimation(300, new Duration(TimeSpan.FromMilliseconds(350)));

                var moveColor = new ThicknessAnimation(new Thickness(0), new Duration(TimeSpan.FromMilliseconds(350)));

                CurrentColorBorder.BeginAnimation(Border.WidthProperty, resizeColor);
                CurrentColorBorder.BeginAnimation(Border.MarginProperty, moveColor);
                detailsStackPanel.BeginAnimation(StackPanel.OpacityProperty, opacityAppear);
                detailsGrid.BeginAnimation(Grid.HeightProperty, resize);
            }
        }

        private void HideDetails()
        {
            if (!_isCollapsed)
            {
                _isCollapsed = true;

                var opacityAppear = new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(250)));
                opacityAppear.EasingFunction = new QuadraticEase() { EasingMode = EasingMode.EaseOut };

                var resize = new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(250)));
                resize.EasingFunction = new ExponentialEase() { EasingMode = EasingMode.EaseOut };

                var resizeColor = new DoubleAnimation(140, new Duration(TimeSpan.FromMilliseconds(250)));

                var moveColor = new ThicknessAnimation(new Thickness(80, 0, 0, 0), new Duration(TimeSpan.FromMilliseconds(250)));

                CurrentColorBorder.BeginAnimation(Border.WidthProperty, resizeColor);
                CurrentColorBorder.BeginAnimation(Border.MarginProperty, moveColor);
                detailsStackPanel.BeginAnimation(Window.OpacityProperty, opacityAppear);
                detailsGrid.BeginAnimation(Grid.HeightProperty, resize);
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            HideDetails();

            SelectedColorChangedCommand.Execute(_currentColor);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            HideDetails();

            // Revert to original color
            var originalColorBackground = new SolidColorBrush(_originalColor);
            CurrentColorBorder.Background = originalColorBackground;

            HexCode.Text = ColorToHex(_originalColor);
        }

        private void ColorVariationBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var selectedColor = ((SolidColorBrush)((Border)sender).Background).Color;
            SelectedColorChangedCommand.Execute(selectedColor);
        }
    }
}
