// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ColorPicker.Helpers;
using ModernWpf.Controls.Primitives;

namespace ColorPicker.Controls
{
    /// <summary>
    /// Interaction logic for ColorPickerControl.xaml
    /// </summary>
    public partial class ColorPickerControl : UserControl
    {
        private const int GradientPointerHalfWidth = 3;
        private double _currH = 360;
        private double _currS = 1;
        private double _currV = 1;
        private bool _ignoreHexChanges;
        private bool _ignoreRGBChanges;
        private bool _ignoreGradientsChanges;
        private bool _isCollapsed = true;
        private Color _originalColor;
        private Color _currentColor;

        public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register("SelectedColor", typeof(Color), typeof(ColorPickerControl), new PropertyMetadata(SelectedColorPropertyChanged));

        public static readonly DependencyProperty SelectedColorChangeCommandProperty = DependencyProperty.Register("SelectedColorChangedCommand", typeof(ICommand), typeof(ColorPickerControl));

        public ColorPickerControl()
        {
            InitializeComponent();

            UpdateHueGradient(1, 1);
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
            var control = (ColorPickerControl)d;
            var newColor = (Color)e.NewValue;

            control._originalColor = control._currentColor = newColor;
            var newColorBackground = new SolidColorBrush(newColor);
            control.CurrentColorButton.Background = newColorBackground;

            control._ignoreHexChanges = true;
            control._ignoreRGBChanges = true;

            control.HexCode.Text = ColorToHex(newColor);
            control.RNumberBox.Text = newColor.R.ToString(CultureInfo.InvariantCulture);
            control.GNumberBox.Text = newColor.G.ToString(CultureInfo.InvariantCulture);
            control.BNumberBox.Text = newColor.B.ToString(CultureInfo.InvariantCulture);
            control.SetColorFromTextBoxes(System.Drawing.Color.FromArgb(newColor.R, newColor.G, newColor.B));

            control._ignoreRGBChanges = false;
            control._ignoreHexChanges = false;

            var hsv = ColorHelper.ConvertToHSVColor(System.Drawing.Color.FromArgb(newColor.R, newColor.G, newColor.B));

            SetColorVariationsForCurrentColor(d, hsv);
        }

        private void UpdateHueGradient(double saturation, double value)
        {
            var g6 = HSVColor.HueSpectrum(saturation, value);

            var gradientBrush = new LinearGradientBrush();
            gradientBrush.StartPoint = new Point(0, 0);
            gradientBrush.EndPoint = new Point(1, 0);
            for (int i = 0; i < g6.Length; i++)
            {
                var stop = new GradientStop(g6[i], i * 0.16);
                gradientBrush.GradientStops.Add(stop);
            }

            HueGradientGrid.Background = gradientBrush;
        }

        private static void SetColorVariationsForCurrentColor(DependencyObject d, (double hue, double saturation, double value) hsv)
        {
            var hueCoefficient = 0;
            var hueCoefficient2 = 0;
            if (1 - hsv.value < 0.15)
            {
                hueCoefficient = 1;
            }

            if (hsv.value - 0.3 < 0)
            {
                hueCoefficient2 = 1;
            }

            var s = hsv.saturation;
            var control = (ColorPickerControl)d;

            control.colorVariation1Button.Background = new SolidColorBrush(HSVColor.RGBFromHSV(Math.Min(hsv.hue + (hueCoefficient * 8), 360), s, Math.Min(hsv.value + 0.3, 1)));
            control.colorVariation2Button.Background = new SolidColorBrush(HSVColor.RGBFromHSV(Math.Min(hsv.hue + (hueCoefficient * 4), 360), s, Math.Min(hsv.value + 0.15, 1)));

            control.colorVariation3Button.Background = new SolidColorBrush(HSVColor.RGBFromHSV(Math.Max(hsv.hue - (hueCoefficient2 * 4), 0), s, Math.Max(hsv.value - 0.2, 0)));
            control.colorVariation4Button.Background = new SolidColorBrush(HSVColor.RGBFromHSV(Math.Max(hsv.hue - (hueCoefficient2 * 8), 0), s, Math.Max(hsv.value - 0.3, 0)));
        }

        private void UpdateValueColorGradient(double posX)
        {
            valueGradientPointer.Margin = new Thickness(posX - GradientPointerHalfWidth, 0, 0, 0);

            _currV = posX / ValueGradientGrid.Width;

            UpdateHueGradient(_currS, _currV);

            SaturationStartColor.Color = HSVColor.RGBFromHSV(_currH, 0f, _currV);
            SaturationStopColor.Color = HSVColor.RGBFromHSV(_currH, 1f, _currV);
        }

        private void UpdateSaturationColorGradient(double posX)
        {
            saturationGradientPointer.Margin = new Thickness(posX - GradientPointerHalfWidth, 0, 0, 0);

            _currS = posX / SaturationGradientGrid.Width;

            UpdateHueGradient(_currS, _currV);

            ValueStartColor.Color = HSVColor.RGBFromHSV(_currH, _currS, 0f);
            ValueStopColor.Color = HSVColor.RGBFromHSV(_currH, _currS, 1f);
        }

        private void UpdateHueColorGradient(double posX)
        {
            hueGradientPointer.Margin = new Thickness(posX - GradientPointerHalfWidth, 0, 0, 0);

            _currH = posX / HueGradientGrid.Width * 360;

            SaturationStartColor.Color = HSVColor.RGBFromHSV(_currH, 0f, _currV);
            SaturationStopColor.Color = HSVColor.RGBFromHSV(_currH, 1f, _currV);

            ValueStartColor.Color = HSVColor.RGBFromHSV(_currH, _currS, 0f);
            ValueStopColor.Color = HSVColor.RGBFromHSV(_currH, _currS, 1f);
        }

        private void UpdateTextBoxesAndCurrentColor(Color currentColor)
        {
            if (!_ignoreHexChanges)
            {
                HexCode.Text = ColorToHex(currentColor);
            }

            if (!_ignoreRGBChanges)
            {
                RNumberBox.Text = currentColor.R.ToString(CultureInfo.InvariantCulture);
                GNumberBox.Text = currentColor.G.ToString(CultureInfo.InvariantCulture);
                BNumberBox.Text = currentColor.B.ToString(CultureInfo.InvariantCulture);
            }

            _currentColor = currentColor;
            CurrentColorButton.Background = new SolidColorBrush(currentColor);
        }

        private void CurrentColorButton_Click(object sender, RoutedEventArgs e)
        {
            ShowDetails();
        }

        private void ShowDetails()
        {
            if (_isCollapsed)
            {
                detailsGrid.Visibility = Visibility.Visible;
                _isCollapsed = false;

                var opacityAppear = new DoubleAnimation(1.0, new Duration(TimeSpan.FromMilliseconds(300)));
                opacityAppear.EasingFunction = new QuadraticEase() { EasingMode = EasingMode.EaseInOut };

                var resize = new DoubleAnimation(400, new Duration(TimeSpan.FromMilliseconds(300)));
                resize.EasingFunction = new ExponentialEase() { EasingMode = EasingMode.EaseInOut };

                var resizeColor = new DoubleAnimation(309, new Duration(TimeSpan.FromMilliseconds(250)));
                resizeColor.EasingFunction = new ExponentialEase() { EasingMode = EasingMode.EaseInOut };

                var moveColor = new ThicknessAnimation(new Thickness(0), new Duration(TimeSpan.FromMilliseconds(250)));
                moveColor.EasingFunction = new ExponentialEase() { EasingMode = EasingMode.EaseInOut };

                ControlHelper.SetCornerRadius(CurrentColorButton, new CornerRadius(2));
                CurrentColorButton.BeginAnimation(Button.WidthProperty, resizeColor);
                CurrentColorButton.BeginAnimation(Button.MarginProperty, moveColor);
                detailsStackPanel.BeginAnimation(StackPanel.OpacityProperty, opacityAppear);
                detailsGrid.BeginAnimation(Grid.HeightProperty, resize);
                CurrentColorButton.IsEnabled = false;
                SessionEventHelper.Event.EditorAdjustColorOpened = true;
            }
        }

        private void HideDetails()
        {
            if (!_isCollapsed)
            {
                _isCollapsed = true;

                var opacityAppear = new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(150)));
                opacityAppear.EasingFunction = new QuadraticEase() { EasingMode = EasingMode.EaseInOut };

                var resize = new DoubleAnimation(0, new Duration(TimeSpan.FromMilliseconds(150)));
                resize.EasingFunction = new ExponentialEase() { EasingMode = EasingMode.EaseInOut };

                var resizeColor = new DoubleAnimation(165, new Duration(TimeSpan.FromMilliseconds(150)));
                resizeColor.EasingFunction = new ExponentialEase() { EasingMode = EasingMode.EaseInOut };

                var moveColor = new ThicknessAnimation(new Thickness(72, 0, 0, 0), new Duration(TimeSpan.FromMilliseconds(150)));
                moveColor.EasingFunction = new ExponentialEase() { EasingMode = EasingMode.EaseInOut };

                ControlHelper.SetCornerRadius(CurrentColorButton, new CornerRadius(0));
                CurrentColorButton.BeginAnimation(Button.WidthProperty, resizeColor);
                CurrentColorButton.BeginAnimation(Button.MarginProperty, moveColor);
                detailsStackPanel.BeginAnimation(Window.OpacityProperty, opacityAppear);
                detailsGrid.BeginAnimation(Grid.HeightProperty, resize);
                detailsGrid.Visibility = Visibility.Collapsed;
                CurrentColorButton.IsEnabled = true;
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            HideDetails();
            SelectedColorChangedCommand.Execute(_currentColor);
            SessionEventHelper.Event.EditorColorAdjusted = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            HideDetails();

            // Revert to original color
            var originalColorBackground = new SolidColorBrush(_originalColor);
            CurrentColorButton.Background = originalColorBackground;

            HexCode.Text = ColorToHex(_originalColor);
        }

        private void ColorVariationButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedColor = ((SolidColorBrush)((Button)sender).Background).Color;
            SelectedColorChangedCommand.Execute(selectedColor);
            SessionEventHelper.Event.EditorSimilarColorPicked = true;
        }

        private void ValueGradientGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = GetMousePositionWithinGrid(sender as Border);
                UpdateValueColorGradient(pos.X);
                _ignoreGradientsChanges = true;
                UpdateTextBoxesAndCurrentColor(HSVColor.RGBFromHSV(_currH, _currS, _currV));
                _ignoreGradientsChanges = false;
            }
        }

        private void ValueGradientGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = GetMousePositionWithinGrid(sender as Border);
            UpdateValueColorGradient(pos.X);
            _ignoreGradientsChanges = true;
            UpdateTextBoxesAndCurrentColor(HSVColor.RGBFromHSV(_currH, _currS, _currV));
            _ignoreGradientsChanges = false;
        }

        private void SaturationGradientGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = GetMousePositionWithinGrid(sender as Border);
            UpdateSaturationColorGradient(pos.X);
            _ignoreGradientsChanges = true;
            UpdateTextBoxesAndCurrentColor(HSVColor.RGBFromHSV(_currH, _currS, _currV));
            _ignoreGradientsChanges = false;
        }

        private void SaturationGradientGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = GetMousePositionWithinGrid(sender as Border);
                UpdateSaturationColorGradient(pos.X);
                _ignoreGradientsChanges = true;
                UpdateTextBoxesAndCurrentColor(HSVColor.RGBFromHSV(_currH, _currS, _currV));
                _ignoreGradientsChanges = false;
            }
        }

        private void HueGradientGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var pos = GetMousePositionWithinGrid(sender as Border);
            UpdateHueColorGradient(pos.X);
            _ignoreGradientsChanges = true;
            UpdateTextBoxesAndCurrentColor(HSVColor.RGBFromHSV(_currH, _currS, _currV));
            _ignoreGradientsChanges = false;
        }

        private void HueGradientGrid_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var pos = GetMousePositionWithinGrid(sender as Border);
                UpdateHueColorGradient(pos.X);
                _ignoreGradientsChanges = true;
                UpdateTextBoxesAndCurrentColor(HSVColor.RGBFromHSV(_currH, _currS, _currV));
                _ignoreGradientsChanges = false;
            }
        }

        private static Point GetMousePositionWithinGrid(Border border)
        {
            var pos = System.Windows.Input.Mouse.GetPosition(border);
            if (pos.X < 0)
            {
                pos.X = 0;
            }

            if (pos.X > border.Width)
            {
                pos.X = border.Width;
            }

            return pos;
        }

        private void HexCode_TextChanged(object sender, TextChangedEventArgs e)
        {
            var newValue = (sender as TextBox).Text;

            // support hex with 3 and 6 characters
            var reg = new Regex("^#([0-9A-F]{3}){1,2}$");

            if (!reg.IsMatch(newValue))
            {
                return;
            }

            if (!_ignoreHexChanges)
            {
                var converter = new System.Drawing.ColorConverter();

                var color = (System.Drawing.Color)converter.ConvertFromString(HexCode.Text);
                _ignoreHexChanges = true;
                SetColorFromTextBoxes(color);
                _ignoreHexChanges = false;
            }
        }

#pragma warning disable CA1801 // Review unused parameters
        private void RGBNumberBox_ValueChanged(ModernWpf.Controls.NumberBox sender, ModernWpf.Controls.NumberBoxValueChangedEventArgs args)
#pragma warning restore CA1801 // Review unused parameters
        {
            if (!_ignoreRGBChanges)
            {
                var r = byte.Parse(RNumberBox.Text, CultureInfo.InvariantCulture);
                var g = byte.Parse(GNumberBox.Text, CultureInfo.InvariantCulture);
                var b = byte.Parse(BNumberBox.Text, CultureInfo.InvariantCulture);
                _ignoreRGBChanges = true;
                SetColorFromTextBoxes(System.Drawing.Color.FromArgb(r, g, b));
                _ignoreRGBChanges = false;
            }
        }

        private void SetColorFromTextBoxes(System.Drawing.Color color)
        {
            if (!_ignoreGradientsChanges)
            {
                var hsv = ColorHelper.ConvertToHSVColor(color);

                var huePosition = (hsv.hue / 360) * HueGradientGrid.Width;
                var saturationPosition = hsv.saturation * SaturationGradientGrid.Width;
                var valuePosition = hsv.value * ValueGradientGrid.Width;
                UpdateHueColorGradient(huePosition);
                UpdateSaturationColorGradient(saturationPosition);
                UpdateValueColorGradient(valuePosition);
            }

            UpdateTextBoxesAndCurrentColor(Color.FromRgb(color.R, color.G, color.B));
        }

        private static string ColorToHex(Color color)
        {
            return "#" + BitConverter.ToString(new byte[] { color.R, color.G, color.B }).Replace("-", string.Empty, StringComparison.InvariantCulture);
        }

        private void HexCode_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            (sender as TextBox).SelectAll();
        }
    }
}
