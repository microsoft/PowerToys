// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using SoundManager.Helpers;
using Windows.UI;

namespace SoundManager
{
    // Observable wrapper around an application entry, so the window can bind to it.
    public sealed class AppViewModel : AudioRowViewModel
    {
        // The icon of a speaker.
        private const string SpeakerGlyph = "\uE767";

        // The icon of a muted speaker.
        private const string MutedGlyph = "\uE74F";

        // The icon of a microphone.
        private const string MicrophoneGlyph = "\uE720";

        private const int SegmentCount = 20;

        // The last segments of the meter show the levels that are close to the top.
        private const int HighSegmentCount = 3;

        private const int MidSegmentCount = 6;

        // The level of the meter when nothing is playing.
        private const double MinimumLevel = -60;

        // The level of every segment in decibels. The scale gets finer towards the top, the way a hardware meter does it.
        private static readonly double[] SegmentLevels =
        {
            MinimumLevel, -52, -45, -40, -35, -31, -28, -25, -22, -20,
            -18, -16, -14.5, -13, -12, -11, -10, -8.5, -7, -5,
        };

        // How fast the meter falls back when the sound gets quieter.
        private const double ReleaseFactor = 0.15;

        // How fast the highest segment falls back.
        private const double HoldDecay = 0.2;

        // The level below which there is no sound at all.
        private const double SilenceLevel = 0.00001;

        private const double UnlitLevel = 0.15;

        private const double HoldLevel = 0.5;

        private static readonly Brush LowColor = new SolidColorBrush(Color.FromArgb(0xFF, 0x3F, 0xB9, 0x50));
        private static readonly Brush MidColor = new SolidColorBrush(Color.FromArgb(0xFF, 0xD8, 0xA4, 0x25));
        private static readonly Brush HighColor = new SolidColorBrush(Color.FromArgb(0xFF, 0xE0, 0x52, 0x52));

        private readonly List<MeterSegment> _segments = new();

        private double _volume;
        private bool _muted;
        private bool _isCapture;
        private double _peakLevel;
        private double _peakDisplay = MinimumLevel;
        private double _hold = MinimumLevel;
        private string _selectedDeviceId = string.Empty;
        private string _selectedDeviceName;
        private ImageSource _icon;

        public AppViewModel()
        {
            _selectedDeviceName = ResourceLoaderInstance.ResourceLoader.GetString("SystemDefaultDevice");

            for (var i = 0; i < SegmentCount; i++)
            {
                Brush color = LowColor;
                if (i >= SegmentCount - HighSegmentCount)
                {
                    color = HighColor;
                }
                else if (i >= SegmentCount - MidSegmentCount)
                {
                    color = MidColor;
                }

                _segments.Add(new MeterSegment { Color = color });
            }
        }

        public string SessionId { get; set; } = string.Empty;

        public string ProcessPath { get; set; } = string.Empty;

        public string AppName { get; set; } = string.Empty;

        public uint ProcessId { get; set; }

        public bool IsActive { get; set; }

        // A program that plays and a program that records are two rows, because they are two different things to change.
        public bool IsCapture
        {
            get => _isCapture;
            set
            {
                if (Set(ref _isCapture, value))
                {
                    OnPropertyChanged(nameof(MuteGlyph));
                    OnPropertyChanged(nameof(DeviceButtonVisibility));
                    OnPropertyChanged(nameof(RecordingVisibility));
                }
            }
        }

        public IReadOnlyList<AudioDeviceInfo> AvailableDevices { get; set; } = Array.Empty<AudioDeviceInfo>();

        public IReadOnlyList<MeterSegment> Segments => _segments;

        public ImageSource Icon
        {
            get => _icon;
            set => Set(ref _icon, value);
        }

        public double Volume
        {
            get => _volume;
            set
            {
                if (Set(ref _volume, value))
                {
                    OnPropertyChanged(nameof(VolumePercent));
                    OnPropertyChanged(nameof(VolumeText));
                }
            }
        }

        // The volume from 0 to 100, which is what the slider is sized with.
        public double VolumePercent => Volume * 100.0;

        public string VolumeText => FormatVolume(VolumePercent);

        public bool Muted
        {
            get => _muted;
            set
            {
                if (Set(ref _muted, value))
                {
                    OnPropertyChanged(nameof(MuteGlyph));
                }
            }
        }

        // A speaker with a cross while the application is muted. A program that records gets a microphone instead.
        public string MuteGlyph
        {
            get
            {
                if (Muted)
                {
                    return MutedGlyph;
                }

                return IsCapture ? MicrophoneGlyph : SpeakerGlyph;
            }
        }

        // A program that records has no device of its own.
        public Visibility DeviceButtonVisibility => IsCapture ? Visibility.Collapsed : Visibility.Visible;

        public Visibility RecordingVisibility => IsCapture ? Visibility.Visible : Visibility.Collapsed;

        public double PeakLevel
        {
            get => _peakLevel;
            set => Set(ref _peakLevel, value);
        }

        public string SelectedDeviceId
        {
            get => _selectedDeviceId;
            set => Set(ref _selectedDeviceId, value);
        }

        // The device this application plays through. An application that is not pinned to a device follows the system default.
        public string SelectedDeviceName
        {
            get => _selectedDeviceName;
            set => Set(ref _selectedDeviceName, value);
        }

        public void AdvanceMeter()
        {
            var level = ToDecibels(_peakLevel);

            if (level > _peakDisplay)
            {
                _peakDisplay = level;
            }
            else
            {
                _peakDisplay += (level - _peakDisplay) * ReleaseFactor;
            }

            if (_peakDisplay > _hold)
            {
                _hold = _peakDisplay;
            }
            else
            {
                _hold = Math.Max(_hold, _hold - HoldDecay);
            }

            var lit = CountSegments(_peakDisplay);
            var hold = CountSegments(_hold);

            for (var i = 0; i < SegmentCount; i++)
            {
                double opacity;
                if (i < lit)
                {
                    opacity = 1.0;
                }
                else if (i == hold && hold > lit)
                {
                    opacity = HoldLevel;
                }
                else
                {
                    opacity = UnlitLevel;
                }

                _segments[i].Level = opacity;
            }
        }

        // The percentage is put together with the format of the language of Windows.
        private static string FormatVolume(double volume)
        {
            var format = ResourceLoaderInstance.ResourceLoader.GetString("VolumeFormat");
            return string.Format(CultureInfo.CurrentCulture, format, Math.Round(volume));
        }

        private static double ToDecibels(double peak)
        {
            if (peak <= SilenceLevel)
            {
                return SegmentLevels[0];
            }

            return Math.Max(20.0 * Math.Log10(peak), SegmentLevels[0]);
        }

        private static int CountSegments(double decibels)
        {
            var count = 0;
            for (var i = 0; i < SegmentCount; i++)
            {
                if (decibels > SegmentLevels[i])
                {
                    count++;
                }
            }

            return count;
        }
    }
}
