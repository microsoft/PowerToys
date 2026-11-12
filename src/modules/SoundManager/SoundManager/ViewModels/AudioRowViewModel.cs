// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using Microsoft.UI.Xaml;

namespace SoundManager
{
    // What a row of the mixer has in common: the balance, the switch that puts the sound in mono, and the arrow that opens the row.
    public abstract class AudioRowViewModel : INotifyPropertyChanged
    {
        // The chevron that points down, shown while the row is closed.
        private const string ExpandGlyphDown = "\uE70D";

        // The chevron that points up, shown while the row is open.
        private const string ExpandGlyphUp = "\uE70E";

        // The balance runs from -100, which is all in the left ear, to 100, which is all in the right one.
        private const double BalanceScale = 100.0;

        // The sound has to be this far from the middle before the marker shows.
        private const double BalanceOffsetTolerance = 0.5;

        // The bar is 52 wide and the dot is 10 wide, so the dot has 42 pixels to travel.
        private const double DotTravel = 42.0;

        private double _balanceLeft = 1.0;
        private double _balanceRight = 1.0;
        private bool _isExpanded;
        private bool _isMono;
        private double _monoLeft = 1.0;
        private double _monoRight = 1.0;

        public event PropertyChangedEventHandler PropertyChanged;

        public double BalanceLeft
        {
            get => _balanceLeft;
            set
            {
                if (Set(ref _balanceLeft, value))
                {
                    OnPropertyChanged(nameof(BalancePosition));
                    OnPropertyChanged(nameof(BalanceValue));
                    OnPropertyChanged(nameof(BalanceDotMargin));
                    OnPropertyChanged(nameof(BalanceIndicatorVisibility));
                }
            }
        }

        public double BalanceRight
        {
            get => _balanceRight;
            set
            {
                if (Set(ref _balanceRight, value))
                {
                    OnPropertyChanged(nameof(BalancePosition));
                    OnPropertyChanged(nameof(BalanceValue));
                    OnPropertyChanged(nameof(BalanceDotMargin));
                    OnPropertyChanged(nameof(BalanceIndicatorVisibility));
                }
            }
        }

        // Windows cannot fold the sound into one channel, so this only makes the two ears the same. The pan is kept, so that it can come back.
        public bool IsMono
        {
            get => _isMono;
            set
            {
                if (Set(ref _isMono, value))
                {
                    OnPropertyChanged(nameof(MonoBadgeVisibility));
                    OnPropertyChanged(nameof(BalanceIndicatorVisibility));
                    OnPropertyChanged(nameof(BalanceSliderEnabled));
                }
            }
        }

        // The pan of the sound before it was put in mono.
        public double MonoLeft
        {
            get => _monoLeft;
            set => Set(ref _monoLeft, value);
        }

        public double MonoRight
        {
            get => _monoRight;
            set => Set(ref _monoRight, value);
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (Set(ref _isExpanded, value))
                {
                    OnPropertyChanged(nameof(DetailsVisibility));
                    OnPropertyChanged(nameof(ExpandGlyph));
                }
            }
        }

        public Visibility DetailsVisibility => IsExpanded ? Visibility.Visible : Visibility.Collapsed;

        public string ExpandGlyph => IsExpanded ? ExpandGlyphUp : ExpandGlyphDown;

        // Where the sound sits between the two ears, from -100, which is all in the left ear, to 100, which is all in the right one.
        public double BalancePosition
        {
            get
            {
                var louder = Math.Max(_balanceLeft, _balanceRight);
                if (louder <= 0)
                {
                    return 0;
                }

                return (_balanceRight - _balanceLeft) / louder * 100.0;
            }
        }

        // The value of the slider, from 0, all in the left ear, to 100, all in the right one, so that the control never shows a negative number.
        public double BalanceValue => (BalancePosition + BalanceScale) / 2.0;

        public Visibility MonoBadgeVisibility => IsMono ? Visibility.Visible : Visibility.Collapsed;

        // The little bar only shows when the sound is not in the middle.
        public Visibility BalanceIndicatorVisibility => !IsMono && HasBalanceOffset ? Visibility.Visible : Visibility.Collapsed;

        public bool HasBalanceOffset => Math.Abs(BalancePosition) > BalanceOffsetTolerance;

        // The balance cannot be moved while the sound is in mono.
        public bool BalanceSliderEnabled => !IsMono;

        // It uses the scale of the slider, so the two agree.
        public Thickness BalanceDotMargin
        {
            get
            {
                var travel = BalanceValue / BalanceScale * DotTravel;
                return new Thickness(travel, 0, 0, 0);
            }
        }

        // The louder ear keeps the level it has and the other one follows.
        public (double Left, double Right) MoveBalance(double position)
        {
            var louder = Math.Max(_balanceLeft, _balanceRight);
            var left = louder;
            var right = louder;

            if (position < 0)
            {
                right = louder * (1 + (position / BalanceScale));
            }
            else if (position > 0)
            {
                left = louder * (1 - (position / BalanceScale));
            }

            BalanceLeft = left;
            BalanceRight = right;
            return (left, right);
        }

        // Windows cannot fold the sound into one channel, so both ears get the same level.
        public (double Left, double Right) ApplyMono(bool mono)
        {
            double left;
            double right;

            if (mono)
            {
                MonoLeft = BalanceLeft;
                MonoRight = BalanceRight;

                left = (BalanceLeft + BalanceRight) / 2;
                right = left;
            }
            else
            {
                left = MonoLeft;
                right = MonoRight;
            }

            IsMono = mono;
            BalanceLeft = left;
            BalanceRight = right;
            return (left, right);
        }

        public (double Left, double Right) ResetBalance()
        {
            IsMono = false;
            MonoLeft = 1.0;
            MonoRight = 1.0;
            BalanceLeft = 1.0;
            BalanceRight = 1.0;
            return (BalanceLeft, BalanceRight);
        }

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
