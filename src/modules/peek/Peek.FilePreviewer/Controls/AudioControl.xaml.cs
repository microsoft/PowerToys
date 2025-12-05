// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Peek.FilePreviewer.Previewers.MediaPreviewer.Models;

namespace Peek.FilePreviewer.Controls
{
    public sealed partial class AudioControl : UserControl
    {
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

        public AudioControl()
        {
            this.InitializeComponent();

            // Re-enable CommandManager when a new audio source is set via SourceChanged event
            // This ensures CommandManager is enabled only after MediaPlayer source is actually set
            PlayerElement.MediaPlayer.SourceChanged += AudioMediaPlayer_SourceChanged;
        }

        private void AudioMediaPlayer_SourceChanged(Windows.Media.Playback.MediaPlayer sender, object args)
        {
            // Re-enable CommandManager when a new source is set so transport controls work
            if (sender.Source != null)
            {
                sender.CommandManager.IsEnabled = true;
            }
        }

        private void SourcePropertyChanged()
        {
            var mediaPlayer = PlayerElement.MediaPlayer;

            if (Source == null)
            {
                mediaPlayer.Pause();
                mediaPlayer.Source = null;

                // Disable CommandManager to remove the app from System Media Transport Controls panel
                mediaPlayer.CommandManager.IsEnabled = false;
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
