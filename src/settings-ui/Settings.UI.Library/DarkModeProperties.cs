// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using Settings.UI.Library.Attributes;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class DarkModeProperties : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public DarkModeProperties()
        {
            ChangeSystem = false;
            ChangeApps = false;
            UseLocation = false;
            LightTime = 0;
            DarkTime = 1;
            Latitude = "0.0";
            Longitude = "0.0";
        }

        private bool _changeSystem;

        [JsonPropertyName("changeSystem")]
        public bool ChangeSystem
        {
            get => _changeSystem;
            set
            {
                if (_changeSystem != value)
                {
                    _changeSystem = value;
                    OnPropertyChanged(nameof(ChangeSystem));
                }
            }
        }

        private bool _changeApps;

        [JsonPropertyName("changeApps")]
        public bool ChangeApps
        {
            get => _changeApps;
            set
            {
                if (_changeApps != value)
                {
                    _changeApps = value;
                    OnPropertyChanged(nameof(ChangeApps));
                }
            }
        }

        private bool _useLocation;

        [JsonPropertyName("useLocation")]
        public bool UseLocation
        {
            get => _useLocation;
            set
            {
                if (_useLocation != value)
                {
                    _useLocation = value;
                    OnPropertyChanged(nameof(UseLocation));
                }
            }
        }

        private uint _lightTime;

        [JsonPropertyName("lightTime")]
        public uint LightTime
        {
            get => _lightTime;
            set
            {
                if (_lightTime != value)
                {
                    _lightTime = value;
                    OnPropertyChanged(nameof(LightTime));
                }
            }
        }

        private uint _darkTime;

        [JsonPropertyName("darkTime")]
        public uint DarkTime
        {
            get => _darkTime;
            set
            {
                if (_darkTime != value)
                {
                    _darkTime = value;
                    OnPropertyChanged(nameof(DarkTime));
                }
            }
        }

        private string _latitude;

        [JsonPropertyName("latitude")]
        public string Latitude
        {
            get => _latitude;
            set
            {
                if (_latitude != value)
                {
                    _latitude = value;
                    OnPropertyChanged(nameof(Latitude));
                }
            }
        }

        private string _longitude;

        [JsonPropertyName("longitude")]
        public string Longitude
        {
            get => _longitude;
            set
            {
                if (_longitude != value)
                {
                    _longitude = value;
                    OnPropertyChanged(nameof(Longitude));
                }
            }
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
