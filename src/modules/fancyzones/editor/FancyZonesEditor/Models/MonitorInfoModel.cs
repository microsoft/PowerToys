// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Globalization;
using System.Text;

using FancyZonesEditor.Properties;
using FancyZonesEditor.ViewModels;

namespace FancyZonesEditor.Utils
{
    public class MonitorInfoModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private static readonly CompositeFormat MonitorIndexFormat = CompositeFormat.Parse(Resources.Monitor_Index);

        public MonitorInfoModel(int index, int height, int width, int dpi, bool selected = false)
        {
            Index = index;
            ScreenBoundsHeight = height;
            ScreenBoundsWidth = width;
            DPI = dpi;
            Scaling = string.Format(CultureInfo.InvariantCulture, format: "{0}%", arg0: (int)Math.Round(dpi * 100 / 96.0));
            Selected = selected;
        }

        public string AccessibleName => string.Format(CultureInfo.CurrentCulture, MonitorIndexFormat, Index);

        public string AccessibleHelpText => $"{Resources.Dimensions} {Dimensions}, {Resources.Scaling} {Scaling}";

        public int Index { get; set; }

        public int ScreenBoundsHeight { get; set; }

        public double DisplayHeight
        {
            get
            {
                return ScreenBoundsHeight * MonitorViewModel.DesktopPreviewMultiplier;
            }
        }

        public int ScreenBoundsWidth { get; set; }

        public double DisplayWidth
        {
            get
            {
                return ScreenBoundsWidth * MonitorViewModel.DesktopPreviewMultiplier;
            }
        }

        public int DPI { get; set; }

        public string Scaling { get; set; }

        public string Dimensions
        {
            get
            {
                if (App.DebugMode)
                {
                    var rect = App.Overlay.Monitors[Index - 1].Device.WorkAreaRect;
                    return "Screen: (" + rect.X + ", " + rect.Y + "); (" + rect.Width + ", " + rect.Height + ")";
                }
                else
                {
                    return ScreenBoundsWidth + " × " + ScreenBoundsHeight;
                }
            }
        }

        public bool Selected
        {
            get
            {
                return _selected;
            }

            set
            {
                if (_selected == value)
                {
                    return;
                }

                _selected = value;
                OnPropertyChanged(nameof(Selected));
            }
        }

        private bool _selected;

        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
