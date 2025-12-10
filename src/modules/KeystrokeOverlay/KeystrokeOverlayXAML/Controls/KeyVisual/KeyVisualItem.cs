// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using KeystrokeOverlayUI.Models;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace KeystrokeOverlayUI.Controls
{
    public partial class KeyVisualItem : ObservableObject
    {
        [ObservableProperty]
        private string _text;

        [ObservableProperty]
        private int _textSize;

        [ObservableProperty]
        private double _opacity = 1.0;

        public bool IsExiting { get; set; }
    }
}
