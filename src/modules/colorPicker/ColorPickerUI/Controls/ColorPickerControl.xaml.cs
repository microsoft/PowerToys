// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

using ColorPicker.Helpers;
using ManagedCommon;

using static System.Net.Mime.MediaTypeNames;

namespace ColorPicker.Controls
{
    /// <summary>
    /// Interaction logic for ColorPickerControl.xaml
    /// </summary>
    public partial class ColorPickerControl : UserControl
    {
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

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ColorPickerAutomationPeer(this);
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

            var hsv = ColorFormatHelper.ConvertToHSVColor(System.Drawing.Color.FromArgb(newColor.R, newColor.G, newColor.B));

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

            HueGradientSlider.Background = gradientBrush;
        }

        private static void SetColorVariationsForCurrentColor(DependencyObject d, (double Hue, double Saturation, double Value) hsv)
        {
            var hueCoefficient = 0;
            var hueCoefficient2 = 0;
            if (1 - hsv.Value < 0.15)
            {
                hueCoefficient = 1;
            }

            if (hsv.Value - 0.3 < 0)
            {
                hueCoefficient2 = 1;
            }

            var s = hsv.Saturation;
            var control = (ColorPickerControl)d;

            control.colorVariation1Button.Background = new SolidColorBrush(HSVColor.RGBFromHSV(Math.Min(hsv.Hue + (hueCoefficient * 8), 360), s, Math.Min(hsv.Value + 0.3, 1)));
            control.colorVariation2Button.Background = new SolidColorBrush(HSVColor.RGBFromHSV(Math.Min(hsv.Hue + (hueCoefficient * 4), 360), s, Math.Min(hsv.Value + 0.15, 1)));

            control.colorVariation3Button.Background = new SolidColorBrush(HSVColor.RGBFromHSV(Math.Max(hsv.Hue - (hueCoefficient2 * 4), 0), s, Math.Max(hsv.Value - 0.2, 0)));
            control.colorVariation4Button.Background = new SolidColorBrush(HSVColor.RGBFromHSV(Math.Max(hsv.Hue - (hueCoefficient2 * 8), 0), s, Math.Max(hsv.Value - 0.3, 0)));
        }

        private void UpdateValueColorGradient(double posX)
        {
            ValueGradientSlider.Value = posX;

            _currV = posX / ValueGradientSlider.Maximum;

            UpdateHueGradient(_currS, _currV);

            SaturationStartColor.Color = HSVColor.RGBFromHSV(_currH, 0f, _currV);
            SaturationStopColor.Color = HSVColor.RGBFromHSV(_currH, 1f, _currV);
        }

        private void UpdateSaturationColorGradient(double posX)
        {
            SaturationGradientSlider.Value = posX;

            _currS = posX / HueGradientSlider.Maximum;

            UpdateHueGradient(_currS, _currV);

            ValueStartColor.Color = HSVColor.RGBFromHSV(_currH, _currS, 0f);
            ValueStopColor.Color = HSVColor.RGBFromHSV(_currH, _currS, 1f);
        }

        private void UpdateHueColorGradient(double posX)
        {
            HueGradientSlider.Value = posX;
            _currH = posX / HueGradientSlider.Maximum * 360;

            SaturationStartColor.Color = HSVColor.RGBFromHSV(_currH, 0f, _currV);
            SaturationStopColor.Color = HSVColor.RGBFromHSV(_currH, 1f, _currV);

            ValueStartColor.Color = HSVColor.RGBFromHSV(_currH, _currS, 0f);
            ValueStopColor.Color = HSVColor.RGBFromHSV(_currH, _currS, 1f);
        }

        private void UpdateTextBoxesAndCurrentColor(Color currentColor)
        {
            if (!_ignoreHexChanges)
            {
                // Second parameter is set to keep the hashtag if typed by the user before
                HexCode.Text = ColorToHex(currentColor, HexCode.Text);
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
                _isCollapsed = false;

                var resizeColor = new DoubleAnimation(256, new Duration(TimeSpan.FromMilliseconds(250)));
                resizeColor.EasingFunction = new ExponentialEase() { EasingMode = EasingMode.EaseInOut };

                var moveColor = new ThicknessAnimation(new Thickness(0), new Duration(TimeSpan.FromMilliseconds(250)));
                moveColor.EasingFunction = new ExponentialEase() { EasingMode = EasingMode.EaseInOut };

                CurrentColorButton.BeginAnimation(System.Windows.Controls.Button.HeightProperty, resizeColor);
                CurrentColorButton.BeginAnimation(System.Windows.Controls.Button.MarginProperty, moveColor);
                CurrentColorButton.IsEnabled = false;
                SessionEventHelper.Event.EditorAdjustColorOpened = true;
                DetailsFlyout.IsOpen = true;
            }
        }

        private void HideDetails()
        {
            if (!_isCollapsed)
            {
                _isCollapsed = true;

                var resizeColor = new DoubleAnimation(165, new Duration(TimeSpan.FromMilliseconds(150)));
                resizeColor.EasingFunction = new ExponentialEase() { EasingMode = EasingMode.EaseInOut };

                var moveColor = new ThicknessAnimation(new Thickness(0, 72, 0, 72), new Duration(TimeSpan.FromMilliseconds(150)));
                moveColor.EasingFunction = new ExponentialEase() { EasingMode = EasingMode.EaseInOut };

                CurrentColorButton.BeginAnimation(System.Windows.Controls.Button.HeightProperty, resizeColor);
                CurrentColorButton.BeginAnimation(System.Windows.Controls.Button.MarginProperty, moveColor);
                CurrentColorButton.IsEnabled = true;
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedColorChangedCommand.Execute(_currentColor);
            SessionEventHelper.Event.EditorColorAdjusted = true;
            DetailsFlyout.IsOpen = false;
        }

        private void DetailsFlyout_Closed(object sender, object e)
        {
            HideDetails();
            AppStateHandler.BlockEscapeKeyClosingColorPickerEditor = false;

            // Revert to original color
            var originalColorBackground = new SolidColorBrush(_originalColor);
            CurrentColorButton.Background = originalColorBackground;

            HexCode.Text = ColorToHex(_originalColor);
        }

        private void DetailsFlyout_Opened(object sender, object e)
        {
            AppStateHandler.BlockEscapeKeyClosingColorPickerEditor = true;
        }

        private void ColorVariationButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedColor = ((SolidColorBrush)((System.Windows.Controls.Button)sender).Background).Color;
            SelectedColorChangedCommand.Execute(selectedColor);
            SessionEventHelper.Event.EditorSimilarColorPicked = true;
        }

        private void SaturationGradientSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateSaturationColorGradient((sender as Slider).Value);
            _ignoreGradientsChanges = true;
            UpdateTextBoxesAndCurrentColor(HSVColor.RGBFromHSV(_currH, _currS, _currV));
            _ignoreGradientsChanges = false;
        }

        private void HueGradientSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateHueColorGradient((sender as Slider).Value);
            _ignoreGradientsChanges = true;
            UpdateTextBoxesAndCurrentColor(HSVColor.RGBFromHSV(_currH, _currS, _currV));
            _ignoreGradientsChanges = false;
        }

        private void ValueGradientSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            UpdateValueColorGradient((sender as Slider).Value);
            _ignoreGradientsChanges = true;
            UpdateTextBoxesAndCurrentColor(HSVColor.RGBFromHSV(_currH, _currS, _currV));
            _ignoreGradientsChanges = false;
        }

        private void HexCode_TextChanged(object sender, TextChangedEventArgs e)
        {
            var newValue = (sender as System.Windows.Controls.TextBox).Text;

            // support hex with 3 and 6 characters and optional with hashtag
            var reg = new Regex("^#?([0-9A-Fa-f]{3}){1,2}$");

            if (!reg.IsMatch(newValue))
            {
                return;
            }

            if (!_ignoreHexChanges)
            {
                var converter = new System.Drawing.ColorConverter();

                // "FormatHexColorString()" is needed to add hashtag if missing and to convert the hex code from three to six characters. Without this we get format exceptions and incorrect color values.
                var color = (System.Drawing.Color)converter.ConvertFromString(FormatHexColorString(HexCode.Text));
                _ignoreHexChanges = true;
                SetColorFromTextBoxes(color);
                _ignoreHexChanges = false;
            }
        }

        private void SetColorFromTextBoxes(System.Drawing.Color color)
        {
            if (!_ignoreGradientsChanges)
            {
                var hsv = ColorFormatHelper.ConvertToHSVColor(color);

                var huePosition = (hsv.Hue / 360) * HueGradientSlider.Maximum;
                var saturationPosition = hsv.Saturation * SaturationGradientSlider.Maximum;
                var valuePosition = hsv.Value * ValueGradientSlider.Maximum;
                UpdateHueColorGradient(huePosition);
                UpdateSaturationColorGradient(saturationPosition);
                UpdateValueColorGradient(valuePosition);
            }

            UpdateTextBoxesAndCurrentColor(Color.FromRgb(color.R, color.G, color.B));
        }

        private static string ColorToHex(Color color, string oldValue = "")
        {
            string newHexString = Convert.ToHexString(new byte[] { color.R, color.G, color.B });
            newHexString = newHexString.ToLowerInvariant();

            // Return only with hashtag if user typed it before
            bool addHashtag = oldValue.StartsWith('#');
            return addHashtag ? "#" + newHexString : newHexString;
        }

        /// <summary>
        /// Formats the hex code string to be accepted by <see cref="ConvertFromString()"/> of <see cref="ColorConverter.ColorConverter"/>. We are adding hashtag at the beginning if needed and convert from three characters to six characters code.
        /// </summary>
        /// <param name="hexCodeText">The string we read from the hex text box.</param>
        /// <returns>Formatted string with hashtag and six characters of hex code.</returns>
        private static string FormatHexColorString(string hexCodeText)
        {
            if (hexCodeText.Length == 3 || hexCodeText.Length == 4)
            {
                // Hex with or without hashtag and three characters
                return Regex.Replace(hexCodeText, "^#?([0-9a-fA-F])([0-9a-fA-F])([0-9a-fA-F])$", "#$1$1$2$2$3$3");
            }
            else
            {
                // Hex with or without hashtag and six characters
                return hexCodeText.StartsWith('#') ? hexCodeText : "#" + hexCodeText;
            }
        }

        private void HexCode_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            (sender as System.Windows.Controls.TextBox).SelectAll();
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, "^[0-9]+$");
        }

        private void RGBNumberBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_ignoreRGBChanges)
            {
                var numberBox = sender as TextBox;
                byte r = numberBox.Name == "RNumberBox" ? GetValueFromNumberBox(numberBox, _currentColor.R) : _currentColor.R;
                byte g = numberBox.Name == "GNumberBox" ? GetValueFromNumberBox(numberBox, _currentColor.G) : _currentColor.G;
                byte b = numberBox.Name == "BNumberBox" ? GetValueFromNumberBox(numberBox, _currentColor.B) : _currentColor.B;

                _ignoreRGBChanges = true;
                SetColorFromTextBoxes(System.Drawing.Color.FromArgb(r, g, b));
                _ignoreRGBChanges = false;
            }
        }

        /// <summary>
        /// NumberBox provides value only after it has been validated - happens after pressing enter or leaving this control.
        /// However, we need to get value immediately after the underlying textbox value changes
        /// </summary>
        /// <param name="numberBox">numberBox control which value we want to get</param>
        /// <returns>Validated value as per numberbox conditions, if content is invalid it returns previous value</returns>
        private static byte GetValueFromNumberBox(TextBox numberBox, byte previousValue)
        {
            int minimum = 0;
            int maximum = 255;
            double? parsedValue = ParseDouble(numberBox.Text);

            if (parsedValue != null)
            {
                var parsedValueByte = (byte)parsedValue;

                if (parsedValueByte >= minimum && parsedValueByte <= maximum)
                {
                    return parsedValueByte;
                }
            }

            // not valid input, return previous value
            return previousValue;
        }

        public static double? ParseDouble(string text)
        {
            if (double.TryParse(text, out double result))
            {
                return result;
            }

            return null;
        }

        public static T GetChildOfType<T>(DependencyObject depObj)
            where T : DependencyObject
        {
            if (depObj == null)
            {
                return null;
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);

                var result = (child as T) ?? GetChildOfType<T>(child);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }
    }

#pragma warning disable SA1402 // File may only contain a single type
    public class ColorPickerAutomationPeer : UserControlAutomationPeer
#pragma warning restore SA1402 // File may only contain a single type
    {
        public ColorPickerAutomationPeer(ColorPickerControl owner)
            : base(owner)
        {
        }

        protected override string GetLocalizedControlTypeCore()
        {
            return ColorPicker.Properties.Resources.Color_Picker_Control;
        }
    }
}
