// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel.Composition;
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
using Microsoft.PowerToys.Settings.UI.Library;
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
        /// Backing field for <see cref="OtherColor"/>
        /// </summary>
        private string _colorText;

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
        public string ColorText
        {
            get => _colorText;
            private set
            {
                _colorText = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Tell the color picker that the color on the position of the mouse cursor have changed
        /// </summary>
        /// <param name="sender">The sender of this event</param>
        /// <param name="color">The new <see cref="Color"/> under the mouse cursor</param>
        private void Mouse_ColorChanged(object sender, System.Drawing.Color color)
        {
            ColorBrush = new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
            ColorText = ColorRepresentationHelper.GetStringRepresentation(color, _userSettings.CopiedColorRepresentation.Value);
        }

        /// <summary>
        /// Tell the color picker that the user have press a mouse button (after release the button)
        /// </summary>
        /// <param name="sender">The sender of this event</param>
        /// <param name="p">The current <see cref="System.Drawing.Point"/> of the mouse cursor</param>
        private void MouseInfoProvider_OnMouseDown(object sender, System.Drawing.Point p)
        {
            CopyToClipboard(ColorText);

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

        /// <summary>
        /// Tell the color picker that the user have used the mouse wheel
        /// </summary>
        /// <param name="sender">The sender of this event</param>
        /// <param name="e">The new values for the zoom</param>
        private void MouseInfoProvider_OnMouseWheel(object sender, Tuple<Point, bool> e)
            => _zoomWindowHelper.Zoom(e.Item1, e.Item2);
    }
}
