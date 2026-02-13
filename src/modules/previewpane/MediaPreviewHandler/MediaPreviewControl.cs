// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.IO;
using System.Windows.Forms.Integration;

using Common;

using Wpf = System.Windows;
using WpfControls = System.Windows.Controls;
using WpfInput = System.Windows.Input;
using WpfMedia = System.Windows.Media;

namespace Microsoft.PowerToys.PreviewHandler.Media
{
    /// <summary>
    /// Implementation of Control for Media Preview Handler.
    /// Uses native media playback (no WebView2).
    /// </summary>
    public class MediaPreviewControl : FormHandlerControl
    {
        /// <summary>
        /// Supported video file extensions.
        /// </summary>
        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".avi", ".mkv", ".mov", ".webm", ".wmv", ".m4v", ".3gp", ".3g2",
        };

        /// <summary>
        /// Supported audio file extensions.
        /// </summary>
        private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg", ".wma",
        };

        private ElementHost _mediaHost;
        private WpfControls.MediaElement _mediaElement;
        private WpfControls.Button _playPauseButton;
        private WpfControls.Slider _positionSlider;
        private WpfControls.TextBlock _timeText;
        private WpfControls.TextBlock _messageText;
        private System.Windows.Forms.Timer _positionTimer;

        private bool _isDragging;
        private bool _isPlaying;
        private bool _mediaLoaded;
        private bool _isUpdatingSlider;
        private TimeSpan _duration = TimeSpan.Zero;

        /// <summary>
        /// Initializes a new instance of the <see cref="MediaPreviewControl"/> class.
        /// </summary>
        public MediaPreviewControl()
        {
            SetBackgroundColor(Color.Black);
        }

        /// <summary>
        /// Start the preview on the Control.
        /// </summary>
        /// <param name="filePath">Path to the media file.</param>
        public void DoPreview(string filePath)
        {
            try
            {
                base.DoPreview(filePath);
                InitializePlayer(filePath);
            }
            catch (Exception ex)
            {
                PreviewError(ex);
            }
        }

        /// <inheritdoc />
        public override void Unload()
        {
            DisposePlayer();
            base.Unload();
        }

        private void InitializePlayer(string filePath)
        {
            DisposePlayer();
            Controls.Clear();

            var extension = Path.GetExtension(filePath);
            var isVideo = VideoExtensions.Contains(extension);
            var isAudio = AudioExtensions.Contains(extension);

            if (!isVideo && !isAudio)
            {
                throw new NotSupportedException($"Unsupported media format: {extension}");
            }

            _mediaHost = new ElementHost
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
            };

            _mediaHost.Child = CreatePlayerRoot(filePath);
            Controls.Add(_mediaHost);

            _positionTimer = new System.Windows.Forms.Timer
            {
                Interval = 250,
            };
            _positionTimer.Tick += PositionTimer_Tick;
            _positionTimer.Start();

            LoadMedia(filePath);
        }

        private Wpf.UIElement CreatePlayerRoot(string filePath)
        {
            var root = new WpfControls.Grid
            {
                Background = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(24, 24, 24)),
            };

            root.RowDefinitions.Add(new WpfControls.RowDefinition { Height = Wpf.GridLength.Auto });
            root.RowDefinitions.Add(new WpfControls.RowDefinition { Height = new Wpf.GridLength(1, Wpf.GridUnitType.Star) });
            root.RowDefinitions.Add(new WpfControls.RowDefinition { Height = Wpf.GridLength.Auto });

            var fileNameText = new WpfControls.TextBlock
            {
                Text = Path.GetFileName(filePath),
                Foreground = new WpfMedia.SolidColorBrush(WpfMedia.Colors.White),
                Margin = new Wpf.Thickness(10, 8, 10, 6),
                TextTrimming = Wpf.TextTrimming.CharacterEllipsis,
            };
            WpfControls.Grid.SetRow(fileNameText, 0);
            root.Children.Add(fileNameText);

            var mediaArea = new WpfControls.Grid
            {
                Background = new WpfMedia.SolidColorBrush(WpfMedia.Colors.Black),
            };

            _mediaElement = new WpfControls.MediaElement
            {
                LoadedBehavior = WpfControls.MediaState.Manual,
                UnloadedBehavior = WpfControls.MediaState.Manual,
                Stretch = WpfMedia.Stretch.Uniform,
                ScrubbingEnabled = true,
            };
            _mediaElement.MediaOpened += MediaElement_MediaOpened;
            _mediaElement.MediaEnded += MediaElement_MediaEnded;
            _mediaElement.MediaFailed += MediaElement_MediaFailed;
            mediaArea.Children.Add(_mediaElement);

            _messageText = new WpfControls.TextBlock
            {
                Visibility = Wpf.Visibility.Collapsed,
                Foreground = new WpfMedia.SolidColorBrush(WpfMedia.Colors.White),
                Background = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromArgb(180, 20, 20, 20)),
                TextAlignment = Wpf.TextAlignment.Center,
                TextWrapping = Wpf.TextWrapping.Wrap,
                VerticalAlignment = Wpf.VerticalAlignment.Center,
                HorizontalAlignment = Wpf.HorizontalAlignment.Center,
                Margin = new Wpf.Thickness(20),
                Padding = new Wpf.Thickness(12),
                MaxWidth = 520,
            };
            mediaArea.Children.Add(_messageText);

            WpfControls.Grid.SetRow(mediaArea, 1);
            root.Children.Add(mediaArea);

            var controlsGrid = new WpfControls.Grid
            {
                Margin = new Wpf.Thickness(10, 6, 10, 10),
            };
            controlsGrid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = Wpf.GridLength.Auto });
            controlsGrid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = new Wpf.GridLength(1, Wpf.GridUnitType.Star) });
            controlsGrid.ColumnDefinitions.Add(new WpfControls.ColumnDefinition { Width = Wpf.GridLength.Auto });

            _playPauseButton = new WpfControls.Button
            {
                Content = "Play",
                Width = 70,
                Height = 30,
                IsEnabled = false,
                Margin = new Wpf.Thickness(0, 0, 8, 0),
            };
            _playPauseButton.Click += PlayPauseButton_Click;
            WpfControls.Grid.SetColumn(_playPauseButton, 0);
            controlsGrid.Children.Add(_playPauseButton);

            _positionSlider = new WpfControls.Slider
            {
                Minimum = 0,
                Maximum = 1,
                IsEnabled = false,
                VerticalAlignment = Wpf.VerticalAlignment.Center,
            };
            _positionSlider.PreviewMouseLeftButtonDown += PositionSlider_MouseDown;
            _positionSlider.PreviewMouseLeftButtonUp += PositionSlider_MouseUp;
            _positionSlider.ValueChanged += PositionSlider_ValueChanged;
            WpfControls.Grid.SetColumn(_positionSlider, 1);
            controlsGrid.Children.Add(_positionSlider);

            _timeText = new WpfControls.TextBlock
            {
                Text = "00:00 / 00:00",
                Foreground = new WpfMedia.SolidColorBrush(WpfMedia.Colors.White),
                VerticalAlignment = Wpf.VerticalAlignment.Center,
                Margin = new Wpf.Thickness(8, 0, 0, 0),
            };
            WpfControls.Grid.SetColumn(_timeText, 2);
            controlsGrid.Children.Add(_timeText);

            WpfControls.Grid.SetRow(controlsGrid, 2);
            root.Children.Add(controlsGrid);

            return root;
        }

        private void LoadMedia(string filePath)
        {
            _mediaElement.Source = new Uri(filePath, UriKind.Absolute);
            _playPauseButton.IsEnabled = true;

            if (_isPlaying)
            {
                _mediaElement.Play();
            }
        }

        private void MediaElement_MediaOpened(object sender, Wpf.RoutedEventArgs e)
        {
            _duration = _mediaElement.NaturalDuration.HasTimeSpan ? _mediaElement.NaturalDuration.TimeSpan : TimeSpan.Zero;
            _mediaLoaded = true;

            _playPauseButton.IsEnabled = true;

            if (_duration > TimeSpan.Zero)
            {
                _positionSlider.Maximum = Math.Max(1, Math.Ceiling(_duration.TotalSeconds));
                _positionSlider.IsEnabled = true;
            }

            UpdateTimeLabel(TimeSpan.Zero);
        }

        private void MediaElement_MediaEnded(object sender, Wpf.RoutedEventArgs e)
        {
            _isPlaying = false;
            _playPauseButton.Content = "Play";
            _isUpdatingSlider = true;
            _positionSlider.Value = _positionSlider.Maximum;
            _isUpdatingSlider = false;
            UpdateTimeLabel(_duration);
        }

        private void MediaElement_MediaFailed(object sender, Wpf.ExceptionRoutedEventArgs e)
        {
            var message = e.ErrorException == null
                ? "Unable to preview this media file. The codec may be unsupported on this machine."
                : e.ErrorException.Message;
            ShowMessage(message);
            _isPlaying = false;
            _playPauseButton.Content = "Play";
            _playPauseButton.IsEnabled = false;
            _positionSlider.IsEnabled = false;
        }

        private void PlayPauseButton_Click(object sender, Wpf.RoutedEventArgs e)
        {
            if (_isPlaying)
            {
                _mediaElement.Pause();
                _isPlaying = false;
                _playPauseButton.Content = "Play";
                return;
            }

            if (_duration > TimeSpan.Zero && _mediaElement.Position >= _duration)
            {
                _mediaElement.Position = TimeSpan.Zero;
                _isUpdatingSlider = true;
                _positionSlider.Value = 0;
                _isUpdatingSlider = false;
            }

            _mediaElement.Play();
            _isPlaying = true;
            _playPauseButton.Content = "Pause";
        }

        private void PositionTimer_Tick(object sender, EventArgs e)
        {
            if (!_mediaLoaded || _isDragging || _duration <= TimeSpan.Zero)
            {
                return;
            }

            var current = _mediaElement.Position;
            var clampedSeconds = Math.Clamp(current.TotalSeconds, 0, _positionSlider.Maximum);

            _isUpdatingSlider = true;
            _positionSlider.Value = clampedSeconds;
            _isUpdatingSlider = false;

            UpdateTimeLabel(current);
        }

        private void PositionSlider_MouseDown(object sender, WpfInput.MouseButtonEventArgs e)
        {
            _isDragging = true;
        }

        private void PositionSlider_MouseUp(object sender, WpfInput.MouseButtonEventArgs e)
        {
            _isDragging = false;
            SeekFromSlider();
        }

        private void PositionSlider_ValueChanged(object sender, Wpf.RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isUpdatingSlider)
            {
                return;
            }

            if (_isDragging)
            {
                UpdateTimeLabel(TimeSpan.FromSeconds(_positionSlider.Value));
            }
        }

        private void SeekFromSlider()
        {
            if (!_mediaLoaded || _duration <= TimeSpan.Zero)
            {
                return;
            }

            var targetSeconds = Math.Clamp(_positionSlider.Value, 0, _positionSlider.Maximum);
            _mediaElement.Position = TimeSpan.FromSeconds(targetSeconds);
            UpdateTimeLabel(_mediaElement.Position);
        }

        private void UpdateTimeLabel(TimeSpan current)
        {
            var safeCurrent = current < TimeSpan.Zero ? TimeSpan.Zero : current;
            var safeDuration = _duration < TimeSpan.Zero ? TimeSpan.Zero : _duration;
            _timeText.Text = string.Format(
                CultureInfo.InvariantCulture,
                "{0} / {1}",
                FormatTime(safeCurrent),
                FormatTime(safeDuration));
        }

        private static string FormatTime(TimeSpan value)
        {
            return value.Hours > 0
                ? value.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture)
                : value.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
        }

        private void ShowMessage(string message)
        {
            if (_messageText == null)
            {
                return;
            }

            _messageText.Dispatcher.Invoke(() =>
            {
                _messageText.Text = message;
                _messageText.Visibility = Wpf.Visibility.Visible;
            });
        }

        private void DisposePlayer()
        {
            if (_positionTimer != null)
            {
                _positionTimer.Stop();
                _positionTimer.Tick -= PositionTimer_Tick;
                _positionTimer.Dispose();
                _positionTimer = null;
            }

            if (_playPauseButton != null)
            {
                _playPauseButton.Click -= PlayPauseButton_Click;
                _playPauseButton = null;
            }

            if (_positionSlider != null)
            {
                _positionSlider.PreviewMouseLeftButtonDown -= PositionSlider_MouseDown;
                _positionSlider.PreviewMouseLeftButtonUp -= PositionSlider_MouseUp;
                _positionSlider.ValueChanged -= PositionSlider_ValueChanged;
                _positionSlider = null;
            }

            if (_mediaElement != null)
            {
                _mediaElement.MediaOpened -= MediaElement_MediaOpened;
                _mediaElement.MediaEnded -= MediaElement_MediaEnded;
                _mediaElement.MediaFailed -= MediaElement_MediaFailed;
                _mediaElement.Stop();
                _mediaElement.Source = null;
                _mediaElement = null;
            }

            _timeText = null;
            _messageText = null;

            if (_mediaHost != null)
            {
                _mediaHost.Child = null;
                _mediaHost.Dispose();
                _mediaHost = null;
            }

            _isDragging = false;
            _isPlaying = false;
            _mediaLoaded = false;
            _isUpdatingSlider = false;
            _duration = TimeSpan.Zero;
        }

        /// <summary>
        /// Called when an error occurs during preview.
        /// </summary>
        /// <param name="exception">The exception which occurred.</param>
        private void PreviewError(Exception exception)
        {
            DisposePlayer();
            Controls.Clear();

            var errorLabel = new Label
            {
                Text = $"Error loading media preview:{Environment.NewLine}{exception.Message}",
                Dock = DockStyle.Fill,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(30, 30, 30),
                TextAlign = ContentAlignment.MiddleCenter,
            };

            Controls.Add(errorLabel);
        }
    }
}
