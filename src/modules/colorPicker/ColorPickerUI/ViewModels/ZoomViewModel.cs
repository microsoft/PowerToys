// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using ColorPicker.ViewModelContracts;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace ColorPicker.ViewModels
{
    public class ZoomViewModel : ObservableObject, IZoomViewModel
    {
        private ImageSource _zoomArea;
        private double _zoomFactor = 1;
        private double _desiredWidth;
        private double _desiredHeight;
        private double _width;
        private double _height;

        public ImageSource ZoomArea
        {
            get => _zoomArea;
            set
            {
                _zoomArea = value;
                OnPropertyChanged();
            }
        }

        public double ZoomFactor
        {
            get => _zoomFactor;
            set
            {
                _zoomFactor = value;
                OnPropertyChanged();
            }
        }

        public double DesiredWidth
        {
            get => _desiredWidth;
            set
            {
                _desiredWidth = value;
                OnPropertyChanged();
            }
        }

        public double DesiredHeight
        {
            get => _desiredHeight;
            set
            {
                _desiredHeight = value;
                OnPropertyChanged();
            }
        }

        public double Width
        {
            get => _width;
            set
            {
                _width = value;
                OnPropertyChanged();
            }
        }

        public double Height
        {
            get => _height;
            set
            {
                _height = value;
                OnPropertyChanged();
            }
        }
    }
}
