// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Windows;

namespace ColorPicker
{
    /// <summary>
    /// Interaction logic for ZoomWindow.xaml
    /// </summary>
    public partial class ZoomWindow : Window, INotifyPropertyChanged
    {
        private double _left;
        private double _top;

        public ZoomWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        public double DesiredLeft
        {
            get
            {
                return _left;
            }

            set
            {
                _left = value;
                NotifyPropertyChanged(nameof(DesiredLeft));
            }
        }

        public double DesiredTop
        {
            get
            {
                return _top;
            }

            set
            {
                _top = value;
                NotifyPropertyChanged(nameof(DesiredTop));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
