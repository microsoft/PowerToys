// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using ColorPicker.Common;
using ColorPicker.Helpers;
using ColorPicker.Keyboard;
using ColorPicker.Mouse;
using ColorPicker.Settings;
using ColorPicker.Telemetry;
using ColorPicker.ViewModelContracts;
using Microsoft.PowerToys.Settings.UI.Lib;
using Microsoft.PowerToys.Telemetry;

namespace ColorPicker.ViewModels
{
    [Export(typeof(IMainViewModel))]
    public class MainViewModel : ViewModelBase, IMainViewModel
    {
        /// <summary>
        /// Defined error code for "clipboard can't open"
        /// </summary>
        private const uint ErrorCodeClipboardCantOpen = 0x800401D0;

        private readonly ZoomWindowHelper _zoomWindowHelper;
        private readonly AppStateHandler _appStateHandler;
        private readonly IUserSettings _userSettings;

        /// <summary>
        /// Backing field for <see cref="HexColor"/>
        /// </summary>
        private string _hexColor;

        /// <summary>
        /// Backing field for <see cref="RgbColor"/>
        /// </summary>
        private string _rgbColor;

        /// <summary>
        /// Backing field for <see cref="OtherColor"/>
        /// </summary>
        private string _otherColorText;

        /// <summary>
        /// Backing field for <see cref="ColorBrush"/>
        /// </summary>
        private Brush _colorBrush;

        [ImportingConstructor]
        public MainViewModel(
            IMouseInfoProvider mouseInfoProvider,
            ZoomWindowHelper zoomWindowHelper,
            AppStateHandler appStateHandler,
            KeyboardMonitor keyboardMonitor,
            IUserSettings userSettings)
        {
            _zoomWindowHelper = zoomWindowHelper;
            _appStateHandler = appStateHandler;
            _userSettings = userSettings;

            if (mouseInfoProvider != null)
            {
                mouseInfoProvider.MouseColorChanged += Mouse_ColorChanged;
                mouseInfoProvider.OnMouseDown += MouseInfoProvider_OnMouseDown;
                mouseInfoProvider.OnMouseWheel += MouseInfoProvider_OnMouseWheel;
            }

            keyboardMonitor?.Start();
        }

        /// <summary>
        /// Gets the current selected color in a hexadecimal presentation
        /// </summary>
        public string HexColor
        {
            get => _hexColor;
            private set
            {
                _hexColor = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets the current selected color in a RGB presentation
        /// </summary>
        public string RgbColor
        {
            get => _rgbColor;
            private set
            {
                _rgbColor = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets the current selected color as a <see cref="Brush"/>
        /// </summary>
        public Brush ColorBrush
        {
            get => _colorBrush;
            private set
            {
                _colorBrush = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets the text representation of the selected color value
        /// </summary>
        public string OtherColorText
        {
            get => _otherColorText;
            private set
            {
                _otherColorText = value;
                OnPropertyChanged();
            }
        }

        private void Mouse_ColorChanged(object sender, System.Drawing.Color color)
        {
            ColorBrush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
            HexColor = ColorToHex(color);
            RgbColor = ColorToRGB(color);

            OtherColorText = _userSettings.CopiedColorRepresentation.Value switch
            {
                ColorRepresentationType.HSL => ColorToHSL(color),
                ColorRepresentationType.HSV => ColorToHSV(color),
                _ => ColorToRGB(color),
            };
        }

        private void MouseInfoProvider_OnMouseDown(object sender, System.Drawing.Point p)
        {
            CopyToClipboard(OtherColorText);

            _appStateHandler.HideColorPicker();
            PowerToysTelemetry.Log.WriteEvent(new ColorPickerShowEvent());
        }

        /// <summary>
        /// Copy the given text to the Windows clipboard
        /// </summary>
        /// <param name="text">The text to copy to the Windows clipboard</param>
        private static void CopyToClipboard(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            // nasty hack - sometimes clipboard can be in use and it will raise and exception
            for (var i = 0; i < 10; i++)
            {
                try
                {
                    Clipboard.SetText(text);
                    break;
                }
                catch (COMException ex)
                {
                    if ((uint)ex.ErrorCode != ErrorCodeClipboardCantOpen)
                    {
                        Logger.LogError("Failed to set text into clipboard", ex);
                    }
                }

                Thread.Sleep(10);
            }
        }

        private void MouseInfoProvider_OnMouseWheel(object sender, Tuple<Point, bool> e)
            => _zoomWindowHelper.Zoom(e.Item1, e.Item2);

        /// <summary>
        /// Return a hexadecimal representation of a RGB color
        /// </summary>
        /// <param name="color">The color for the hexadecimal presentation</param>
        /// <returns>A hexadecimal representation of a RGB color</returns>
        private static string ColorToHex(System.Drawing.Color color)
            => $"#{color.R.ToString("X2", CultureInfo.InvariantCulture)}"
             + $"{color.G.ToString("X2", CultureInfo.InvariantCulture)}"
             + $"{color.B.ToString("X2", CultureInfo.InvariantCulture)}";

        /// <summary>
        /// Return a <see cref="string"/> representation of a RGB color
        /// </summary>
        /// <param name="color">The color for the RGB color presentation</param>
        /// <returns>A <see cref="string"/> representation of a <see cref="System.Drawing.Color"/></returns>
        private static string ColorToRGB(System.Drawing.Color color)
            => $"rgb({color.R.ToString(CultureInfo.InvariantCulture)}"
             + $", {color.G.ToString(CultureInfo.InvariantCulture)}"
             + $", {color.B.ToString(CultureInfo.InvariantCulture)})";

        /// <summary>
        /// Return a <see cref="string"/>  representation of a HSL color
        /// </summary>
        /// <param name="color">The color for the HSL color presentation</param>
        /// <returns>A <see cref="string"/> representation of a HSL color</returns>
        private static string ColorToHSL(System.Drawing.Color color)
        {
            var (hue, saturation, lightness) = ColorHelper.ConvertToHSLColor(color);

            hue = Math.Round(hue);
            saturation = Math.Round(saturation * 100);
            lightness = Math.Round(lightness * 100);

            return $"hsl({hue.ToString(CultureInfo.InvariantCulture)}"
                 + $", {saturation.ToString(CultureInfo.InvariantCulture)}%"
                 + $", {lightness.ToString(CultureInfo.InvariantCulture)}%)";
        }

        /// <summary>
        /// Return a <see cref="string"/> representation of a HSV color
        /// </summary>
        /// <param name="color">The color for the HSV color presentation</param>
        /// <returns>A <see cref="string"/> representation of a HSV color</returns>
        private static string ColorToHSV(System.Drawing.Color color)
        {
            var (hue, saturation, value) = ColorHelper.ConvertToHSVColor(color);

            hue = Math.Round(hue);
            saturation = Math.Round(saturation * 100);
            value = Math.Round(value * 100);

            return $"hsv({hue.ToString(CultureInfo.InvariantCulture)}"
                 + $", {saturation.ToString(CultureInfo.InvariantCulture)}%"
                 + $", {value.ToString(CultureInfo.InvariantCulture)}%)";
        }
    }
}
