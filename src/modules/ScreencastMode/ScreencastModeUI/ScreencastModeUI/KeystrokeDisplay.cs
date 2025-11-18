// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace ScreencastModeUI
{
    internal class KeystrokeDisplay
    {
        public string Text { get; set; }
        public DateTime Timestamp { get; set; }
        public int DisplayTimeMs { get; set; } = 5000;
        public SolidColorBrush BackgroundBrush { get; } = new SolidColorBrush(
            Color.FromArgb(230, 50, 50, 50)); 
        public SolidColorBrush ForegroundBrush { get; } = new SolidColorBrush(
            Colors.White); 
        public int FontSize { get; set; } = 32;
    }
}