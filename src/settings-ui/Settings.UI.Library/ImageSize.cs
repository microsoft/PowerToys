// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class ImageSize : INotifyPropertyChanged
    {
        public ImageSize(int id)
        {
            Id = id;
            Name = string.Empty;
            Fit = (int)ResizeFit.Fit;
            Width = 0;
            Height = 0;
            Unit = (int)ResizeUnit.Pixel;
        }

        public ImageSize()
        {
            Id = 0;
            Name = string.Empty;
            Fit = (int)ResizeFit.Fit;
            Width = 0;
            Height = 0;
            Unit = (int)ResizeUnit.Pixel;
        }

        public ImageSize(int id, string name, ResizeFit fit, double width, double height, ResizeUnit unit)
        {
            Id = id;
            Name = name;
            Fit = (int)fit;
            Width = width;
            Height = height;
            Unit = (int)unit;
        }

        private int _id;
        private string _name;
        private int _fit;
        private double _height;
        private double _width;
        private int _unit;

        public int Id
        {
            get
            {
                return _id;
            }

            set
            {
                if (_id != value)
                {
                    _id = value;
                    OnPropertyChanged();
                }
            }
        }

        public int ExtraBoxOpacity
        {
            get
            {
                if (Unit == 2 && Fit != 2)
                {
                    return 0;
                }
                else
                {
                    return 100;
                }
            }
        }

        public bool EnableEtraBoxes
        {
            get
            {
                if (Unit == 2 && Fit != 2)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
        }

        [JsonPropertyName("name")]
        public string Name
        {
            get
            {
                return _name;
            }

            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        [JsonPropertyName("fit")]
        public int Fit
        {
            get
            {
                return _fit;
            }

            set
            {
                if (_fit != value)
                {
                    _fit = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ExtraBoxOpacity));
                    OnPropertyChanged(nameof(EnableEtraBoxes));
                }
            }
        }

        [JsonPropertyName("width")]
        public double Width
        {
            get
            {
                return _width;
            }

            set
            {
                double newWidth = -1;

                if (value < 0 || double.IsNaN(value))
                {
                    newWidth = 0;
                }
                else
                {
                    newWidth = value;
                }

                if (_width != newWidth)
                {
                    _width = newWidth;
                    OnPropertyChanged();
                }
            }
        }

        [JsonPropertyName("height")]
        public double Height
        {
            get
            {
                return _height;
            }

            set
            {
                double newHeight = -1;

                if (value < 0 || double.IsNaN(value))
                {
                    newHeight = 0;
                }
                else
                {
                    newHeight = value;
                }

                if (_height != newHeight)
                {
                    _height = newHeight;
                    OnPropertyChanged();
                }
            }
        }

        [JsonPropertyName("unit")]
        public int Unit
        {
            get
            {
                return _unit;
            }

            set
            {
                if (_unit != value)
                {
                    _unit = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ExtraBoxOpacity));
                    OnPropertyChanged(nameof(EnableEtraBoxes));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public void Update(ImageSize modifiedSize)
        {
            ArgumentNullException.ThrowIfNull(modifiedSize);

            Id = modifiedSize.Id;
            Name = modifiedSize.Name;
            Fit = modifiedSize.Fit;
            Width = modifiedSize.Width;
            Height = modifiedSize.Height;
            Unit = modifiedSize.Unit;
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
