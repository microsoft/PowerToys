// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;

using Microsoft.UI.Xaml.Media;

namespace SoundManager
{
    // One segment of the level meter. The segments are lit one after another, the way a receiver shows how loud a signal is.
    public sealed class MeterSegment : INotifyPropertyChanged
    {
        private double _level;

        public event PropertyChangedEventHandler PropertyChanged;

        public Brush Color { get; set; }

        // Zero when the segment is dark, one when it is lit.
        public double Level
        {
            get => _level;
            set
            {
                if (_level == value)
                {
                    return;
                }

                _level = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Level)));
            }
        }
    }
}
