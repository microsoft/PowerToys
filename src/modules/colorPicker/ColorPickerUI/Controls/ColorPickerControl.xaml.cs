// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Windows.Input;

using ColorPicker.Helpers;
using ManagedCommon;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI;

namespace ColorPicker.Controls
{
    /// <summary>
    /// Interaction logic for ColorPickerControl.xaml
    /// </summary>
    public sealed partial class ColorPickerControl : UserControl
    {
        private static readonly TimeSpan ExpandAnimationDuration = TimeSpan.FromMilliseconds(250);
        private static readonly TimeSpan CollapseAnimationDuration = TimeSpan.FromMilliseconds(150);

        private double _currH = 360;
        private double _currS = 1;
        private double _currV = 1;
        private Visual _currentColorButtonVisual;
        private bool _currentColorButtonCenterPinned;
        private bool _ignoreHexChanges;
        private bool _ignoreRGBChanges;
        private bool _ignoreGradientsChanges;
        private bool _isCollapsed = true;
        private Color _originalColor;
        private Color _currentColor;

        public static readonly DependencyProperty SelectedColorProperty = DependencyProperty.Register(nameof(SelectedColor), typeof(Color), typeof(ColorPickerControl), new PropertyMetadata(default(Color), SelectedColorPropertyChanged));

        public static readonly DependencyProperty SelectedColorChangeCommandProperty = DependencyProperty.Register(nameof(SelectedColorChangedCommand), typeof(ICommand), typeof(ColorPickerControl), new PropertyMetadata(null));

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
            get => (Color)GetValue(SelectedColorProperty);
            set => SetValue(SelectedColorProperty, value);
        }

        public ICommand SelectedColorChangedCommand
        {
            get => (ICommand)GetValue(SelectedColorChangeCommandProperty);
            set => SetValue(SelectedColorChangeCommandProperty, value);
        }

        private static void SelectedColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ColorPickerControl)d;
            var newColor = (Color)e.NewValue;

            control._originalColor = control._currentColor = newColor;
            control.CurrentColorButton.Background = new SolidColorBrush(newColor);

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

            var gradientBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(1, 0),
            };
            for (int i = 0; i < g6.Length; i++)
            {
                gradientBrush.GradientStops.Add(new GradientStop { Color = g6[i], Offset = i * 0.16 });
            }

            HueGradientBorder.Background = gradientBrush;
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

                // The flyout opens automatically via Button.Flyout; animate the swatch to its
                // expanded pose alongside it.
                AnimateCurrentColorButton(expand: true, ExpandAnimationDuration);
                CurrentColorButton.IsEnabled = false;
                SessionEventHelper.Event.EditorAdjustColorOpened = true;
            }
        }

        private void HideDetails()
        {
            if (!_isCollapsed)
            {
                _isCollapsed = true;

                AnimateCurrentColorButton(expand: false, CollapseAnimationDuration);
                CurrentColorButton.IsEnabled = true;
            }
        }

        private void AnimateCurrentColorButton(bool expand, TimeSpan duration)
        {
            UpdateLayout();

            _currentColorButtonVisual ??= ElementCompositionPreview.GetElementVisual(CurrentColorButton);
            var compositor = _currentColorButtonVisual.Compositor;

            if (!_currentColorButtonCenterPinned)
            {
                var center = compositor.CreateExpressionAnimation("Vector3(this.Target.Size.X * 0.5, this.Target.Size.Y * 0.5, 0)");
                _currentColorButtonVisual.StartAnimation(nameof(Visual.CenterPoint), center);
                _currentColorButtonCenterPinned = true;
            }

            float expandedScale = CurrentColorButton.ActualHeight > 0
                ? (float)(PickerPanel.ActualHeight / CurrentColorButton.ActualHeight)
                : 1f;
            var targetScale = expand ? new Vector3(1f, expandedScale, 1f) : Vector3.One;
            var easing = compositor.CreateCubicBezierEasingFunction(
                new Vector2(0.45f, 0f),
                new Vector2(0.55f, 1f));
            var animation = compositor.CreateVector3KeyFrameAnimation();
            animation.InsertKeyFrame(1f, targetScale, easing);
            animation.Duration = duration;

            _currentColorButtonVisual.StartAnimation(nameof(Visual.Scale), animation);
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedColorChangedCommand.Execute(_currentColor);
            SessionEventHelper.Event.EditorColorAdjusted = true;
            DetailsFlyout.Hide();
        }

        private void DetailsFlyout_Closed(object sender, object e)
        {
            HideDetails();
            EditorState.BlockEscapeKeyClosingColorPickerEditor = false;

            // Revert to original color
            CurrentColorButton.Background = new SolidColorBrush(_originalColor);
            HexCode.Text = ColorToHex(_originalColor);
        }

        private void DetailsFlyout_Opened(object sender, object e)
        {
            EditorState.BlockEscapeKeyClosingColorPickerEditor = true;
        }

        private void ColorVariationButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedColor = ((SolidColorBrush)((Button)sender).Background).Color;
            SelectedColorChangedCommand.Execute(selectedColor);
            SessionEventHelper.Event.EditorSimilarColorPicked = true;
        }

        private void SaturationGradientSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            UpdateSaturationColorGradient((sender as Slider).Value);
            _ignoreGradientsChanges = true;
            UpdateTextBoxesAndCurrentColor(HSVColor.RGBFromHSV(_currH, _currS, _currV));
            _ignoreGradientsChanges = false;
        }

        private void HueGradientSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            UpdateHueColorGradient((sender as Slider).Value);
            _ignoreGradientsChanges = true;
            UpdateTextBoxesAndCurrentColor(HSVColor.RGBFromHSV(_currH, _currS, _currV));
            _ignoreGradientsChanges = false;
        }

        private void ValueGradientSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            UpdateValueColorGradient((sender as Slider).Value);
            _ignoreGradientsChanges = true;
            UpdateTextBoxesAndCurrentColor(HSVColor.RGBFromHSV(_currH, _currS, _currV));
            _ignoreGradientsChanges = false;
        }

        private void HexCode_TextChanged(object sender, TextChangedEventArgs e)
        {
            var newValue = (sender as TextBox).Text;

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

            UpdateTextBoxesAndCurrentColor(new Color { A = 255, R = color.R, G = color.G, B = color.B });
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
        /// Formats the hex code string to be accepted by the <see cref="System.Drawing.ColorConverter"/>.
        /// We add a hashtag at the beginning if needed and convert from three to six characters.
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

        private void HexCode_GotFocus(object sender, RoutedEventArgs e)
        {
            (sender as TextBox).SelectAll();
        }

        private void NumberBox_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            // WinUI has no PreviewTextInput; reject any non-digit edit before it is applied.
            args.Cancel = !Regex.IsMatch(args.NewText, "^[0-9]*$");
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
    }

#pragma warning disable SA1402 // File may only contain a single type
    public sealed partial class ColorPickerAutomationPeer : FrameworkElementAutomationPeer
#pragma warning restore SA1402 // File may only contain a single type
    {
        public ColorPickerAutomationPeer(ColorPickerControl owner)
            : base(owner)
        {
        }
    }
}
