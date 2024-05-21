// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ProjectsEditor.Models
{
    public class MonitorSetup : Monitor, INotifyPropertyChanged
    {
        private BitmapImage _previewImage;

        public BitmapImage PreviewImage
        {
            get
            {
                return _previewImage;
            }

            set
            {
                _previewImage = value;
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(PreviewImage)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        public string MonitorInfo { get => MonitorName; }

        public string MonitorInfoWithResolution { get => $"{MonitorName}    {MonitorDpiAwareBounds.Width}x{MonitorDpiAwareBounds.Height}"; }

        public MonitorSetup(string monitorName, string monitorInstanceId, int number, int dpi, Rect dpiAwareBounds, Rect dpiUnawareBounds)
            : base(monitorName, monitorInstanceId, number, dpi, dpiAwareBounds, dpiUnawareBounds)
        {
        }
    }
}
