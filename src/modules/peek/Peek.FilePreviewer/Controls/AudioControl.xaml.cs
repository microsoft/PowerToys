// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Peek.FilePreviewer.Previewers.MediaPreviewer.Models;

namespace Peek.FilePreviewer.Controls
{
    public sealed partial class AudioControl : UserControl
    {
        public event EventHandler<double>? VolumeChanged;

        private bool _isSettingVolume;

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
            nameof(Source),
            typeof(AudioPreviewData),
            typeof(AudioControl),
            new PropertyMetadata(null, new PropertyChangedCallback((d, e) => ((AudioControl)d).SourcePropertyChanged())));

        public static readonly DependencyProperty ToolTipTextProperty = DependencyProperty.Register(
            nameof(ToolTipText),
            typeof(string),
            typeof(AudioControl),
            new PropertyMetadata(null));

        public static readonly DependencyProperty MediaVolumeProperty = DependencyProperty.Register(
            nameof(MediaVolume),
            typeof(double),
            typeof(AudioControl),
            new PropertyMetadata(1.0, new PropertyChangedCallback((d, e) => ((AudioControl)d).MediaVolumePropertyChanged())));

        public AudioPreviewData? Source
        {
            get { return (AudioPreviewData)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public string ToolTipText
        {
            get { return (string)GetValue(ToolTipTextProperty); }
            set { SetValue(ToolTipTextProperty, value); }
        }

        public double MediaVolume
        {
            get { return (double)GetValue(MediaVolumeProperty); }
            set { SetValue(MediaVolumeProperty, value); }
        }

        public AudioControl()
        {
            this.InitializeComponent();
            PlayerElement.MediaPlayer.VolumeChanged += MediaPlayer_VolumeChanged;
        }

        private void MediaPlayer_VolumeChanged(Windows.Media.Playback.MediaPlayer sender, object args)
        {
            if (!_isSettingVolume)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    VolumeChanged?.Invoke(this, sender.Volume);
                });
            }
        }

        private void SourcePropertyChanged()
        {
            if (Source == null)
            {
                PlayerElement.MediaPlayer.Pause();
                PlayerElement.MediaPlayer.Source = null;
            }
            else
            {
                // Apply saved volume when source changes
                ApplyVolume();
            }
        }

        private void MediaVolumePropertyChanged()
        {
            ApplyVolume();
        }

        private void ApplyVolume()
        {
            if (PlayerElement.MediaPlayer != null)
            {
                _isSettingVolume = true;
                try
                {
                    PlayerElement.MediaPlayer.Volume = MediaVolume;
                }
                finally
                {
                    _isSettingVolume = false;
                }
            }
        }

        private void KeyboardAccelerator_Space_Invoked(Microsoft.UI.Xaml.Input.KeyboardAccelerator sender, Microsoft.UI.Xaml.Input.KeyboardAcceleratorInvokedEventArgs args)
        {
            var mediaPlayer = PlayerElement.MediaPlayer;

            if (mediaPlayer.Source == null || !mediaPlayer.CanPause)
            {
                return;
            }

            if (mediaPlayer.CurrentState == Windows.Media.Playback.MediaPlayerState.Playing)
            {
                mediaPlayer.Pause();
            }
            else
            {
                mediaPlayer.Play();
            }

            // Prevent the keyboard accelerator to be called twice
            args.Handled = true;
        }
    }
}
