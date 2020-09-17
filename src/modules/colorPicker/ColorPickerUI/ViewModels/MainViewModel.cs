// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Runtime.InteropServices;
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
        private readonly ZoomWindowHelper _zoomWindowHelper;
        private readonly AppStateHandler _appStateHandler;
        private readonly IUserSettings _userSettings;
        private string _hexColor;
        private string _rgbColor;
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

        public string HexColor
        {
            get
            {
                return _hexColor;
            }

            private set
            {
                _hexColor = value;
                OnPropertyChanged();
            }
        }

        public string RgbColor
        {
            get
            {
                return _rgbColor;
            }

            private set
            {
                _rgbColor = value;
                OnPropertyChanged();
            }
        }

        public Brush ColorBrush
        {
            get
            {
                return _colorBrush;
            }

            private set
            {
                _colorBrush = value;
                OnPropertyChanged();
            }
        }

        private void Mouse_ColorChanged(object sender, System.Drawing.Color color)
        {
            HexColor = ColorToHex(color);
            RgbColor = ColorToRGB(color);
            ColorBrush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
        }

        private void MouseInfoProvider_OnMouseDown(object sender, System.Drawing.Point p)
        {
            string colorRepresentationToCopy = string.Empty;

            switch (_userSettings.CopiedColorRepresentation.Value)
            {
                case ColorRepresentationType.HEX:
                    colorRepresentationToCopy = HexColor;
                    break;
                case ColorRepresentationType.RGB:
                    colorRepresentationToCopy = RgbColor;
                    break;
                default:
                    break;
            }

            CopyToClipboard(colorRepresentationToCopy);

            _appStateHandler.HideColorPicker();
            PowerToysTelemetry.Log.WriteEvent(new ColorPickerShowEvent());
        }

        private static void CopyToClipboard(string colorRepresentationToCopy)
        {
            if (!string.IsNullOrEmpty(colorRepresentationToCopy))
            {
                // nasty hack - sometimes clipboard can be in use and it will raise and exception
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        Clipboard.SetText(colorRepresentationToCopy);
                        break;
                    }
                    catch (COMException ex)
                    {
                        const uint CLIPBRD_E_CANT_OPEN = 0x800401D0;
                        if ((uint)ex.ErrorCode != CLIPBRD_E_CANT_OPEN)
                        {
                            Logger.LogError("Failed to set text into clipboard", ex);
                        }
                    }

                    System.Threading.Thread.Sleep(10);
                }
            }
        }

        private void MouseInfoProvider_OnMouseWheel(object sender, Tuple<Point, bool> e)
        {
            _zoomWindowHelper.Zoom(e.Item1, e.Item2);
        }

        private static string ColorToHex(System.Drawing.Color c)
        {
            return "#" + c.R.ToString("X2", CultureInfo.InvariantCulture) + c.G.ToString("X2", CultureInfo.InvariantCulture) + c.B.ToString("X2", CultureInfo.InvariantCulture);
        }

        private static string ColorToRGB(System.Drawing.Color c)
        {
            return "RGB(" + c.R.ToString(CultureInfo.InvariantCulture) + "," + c.G.ToString(CultureInfo.InvariantCulture) + "," + c.B.ToString(CultureInfo.InvariantCulture) + ")";
        }
    }
}
