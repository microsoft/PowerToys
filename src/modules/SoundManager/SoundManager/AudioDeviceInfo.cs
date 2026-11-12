// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using SoundManager.Helpers;

namespace SoundManager
{
    // An audio endpoint the sound can be sent to or recorded from.
    public sealed class AudioDeviceInfo : AudioRowViewModel
    {
        private bool _isSelected;
        private bool _isDefaultCommunications;
        private bool _listening;
        private bool _isTesting;

        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public bool IsDefault { get; set; }

        // Windows keeps a second default device for calls and chats.
        public bool IsDefaultCommunications
        {
            get => _isDefaultCommunications;
            set
            {
                if (Set(ref _isDefaultCommunications, value))
                {
                    OnPropertyChanged(nameof(CallsBadgeVisibility));
                    OnPropertyChanged(nameof(CallsIconIdleVisibility));
                    OnPropertyChanged(nameof(CallsButtonTooltip));
                }
            }
        }

        public double Volume { get; set; }

        public bool Muted { get; set; }

        // The old Sound dialog calls this "Listen to this device".
        public bool Listening
        {
            get => _listening;
            set
            {
                if (Set(ref _listening, value))
                {
                    OnPropertyChanged(nameof(ListenIconIdleVisibility));
                    OnPropertyChanged(nameof(ListenIconOnVisibility));
                    OnPropertyChanged(nameof(ListenButtonTooltip));
                }
            }
        }

        public Visibility ListenIconIdleVisibility => Listening ? Visibility.Collapsed : Visibility.Visible;

        public Visibility ListenIconOnVisibility => Listening ? Visibility.Visible : Visibility.Collapsed;

        public string ListenButtonTooltip => Listening
            ? ResourceLoaderInstance.ResourceLoader.GetString("BtnListenTooltipStop")
            : ResourceLoaderInstance.ResourceLoader.GetString("BtnListenTooltipPlay");

        public bool IsSelected
        {
            get => _isSelected;
            set => Set(ref _isSelected, value);
        }

        public bool IsTesting
        {
            get => _isTesting;
            set
            {
                if (Set(ref _isTesting, value))
                {
                    OnPropertyChanged(nameof(CanTest));
                    OnPropertyChanged(nameof(TestIconIdleVisibility));
                    OnPropertyChanged(nameof(TestSpinnerVisibility));
                    OnPropertyChanged(nameof(TestButtonTooltip));
                }
            }
        }

        public bool CanTest => !IsTesting;

        public Visibility TestIconIdleVisibility => IsTesting ? Visibility.Collapsed : Visibility.Visible;

        public Visibility TestSpinnerVisibility => IsTesting ? Visibility.Visible : Visibility.Collapsed;

        public string TestButtonTooltip => IsTesting
            ? ResourceLoaderInstance.ResourceLoader.GetString("BtnTestTooltipPlaying")
            : ResourceLoaderInstance.ResourceLoader.GetString("BtnTestTooltipPlay");

        public Visibility CallsBadgeVisibility => IsDefaultCommunications ? Visibility.Visible : Visibility.Collapsed;

        public Visibility CallsIconIdleVisibility => IsDefaultCommunications ? Visibility.Collapsed : Visibility.Visible;

        public string CallsButtonTooltip => IsDefaultCommunications
            ? ResourceLoaderInstance.ResourceLoader.GetString("BtnCallsTooltipActive")
            : ResourceLoaderInstance.ResourceLoader.GetString("BtnCallsTooltipSet");
    }
}
